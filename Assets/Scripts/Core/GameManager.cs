using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 전체 진행을 관리하는 최상위 매니저.
/// 한 씬 안에서 상태만 전환하며 [스토리 → 전투 → 승리/보상 → 다음 스토리]를 반복한다.
///
/// 덱 성장 시스템:
/// - 전투에서 이기면 쓰러뜨린 적의 태엽판 한 장을 골라 내 덱으로 '흡수'한다 (◈ 표시, 전투마다 확정).
/// - 흡수 카드는 강력하지만, 사용할 때 확률적으로 '침식'이 발동해 사용자가 피해를 입는다.
/// - 같은 태엽판을 중복 흡수하면 침식 수치가 누적되어 발동 확률과 피해가 커진다.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState
    {
        Story,      // 대사 진행 중
        Battle,     // 전투 진행 중
        Route,      // 전투 뒤 갈림길 (휴식 · 이벤트 · 태엽판 삭제 ...)
        Clear,      // 모든 챕터 클리어
        GameOver    // 패배
    }

    [Header("스토리 (넣으면 아래 Chapters / Starting Deck 대신 이 에셋의 것을 사용)")]
    public StoryData story;

    [Header("챕터 목록 (진행 순서대로 등록)")]
    public List<ChapterData> chapters = new List<ChapterData>();

    [Header("씬 배치 참조")]
    public Unit playerUnit;
    public DialogueManager dialogueManager;
    public BattleManager battleManager;

    [Header("플레이어가 처음부터 가지는 덱(태엽판)")]
    public List<CardData> startingDeck = new List<CardData>();

    [Header("기력")]
    [Tooltip("시작할 때의 최대 기력 (라운드마다 이만큼 찬다. 부품으로 늘어난다)")]
    public int startingMaxKi = 3;

    [Header("갈림길 지도 (전투에서 이긴 뒤, 다음 챕터 전투까지)")]
    [Tooltip("지도의 층 수 = 다음 챕터 전투까지 거치는 칸 수 (0 = 지도 없음)")]
    public int routeRows = 6;
    [Tooltip("챕터 전투(보스)를 이기면 최대 체력의 이만큼 회복 (0 = 회복 없음). 갈래길 위의 전투에는 적용되지 않는다")]
    [Range(0f, 1f)]
    public float chapterClearHealRatio = 0.35f;

    [Header("시작 화면 · 로딩 화면")]
    [Tooltip("체크: 게임을 켜면 시작 화면부터 (패배 뒤 '다시 시작'은 건너뛴다)")]
    public bool showTitleScreen = true;
    [Tooltip("체크: 게임 시작과 전투 진입 때 로딩 화면으로 장면 전환을 덮는다")]
    public bool showLoadingScreen = true;
    [Tooltip("시작 화면의 큰 제목")]
    public string gameTitle = "CLOCKWORK ROLL";
    [Tooltip("시작 화면 제목 아래 한 줄 소개")]
    public string titleTagline = "멈춘 종탑 때문에 물에 잠긴 시계 마을 — 떠다니는 기억을 삼키며 종탑으로";

    [Header("침식(흡수 카드 부작용) 설정")]
    [Tooltip("침식 1인 흡수 카드를 사용할 때 발동 확률")]
    public float corruptionBaseChance = 0.20f;
    [Tooltip("침식 1pt당 추가되는 발동 확률")]
    public float corruptionChancePerPoint = 0.05f;

    /// <summary>현재 보유 덱 (적 태엽판 흡수 + 챕터 보상으로 계속 불어난다)</summary>
    public List<DeckEntry> OwnedDeck { get; private set; }

    /// <summary>이번 판의 최대 기력 (부품으로 늘어난다)</summary>
    public int MaxKi { get; private set; }

    /// <summary>이번 판에 얻은 부품</summary>
    public List<RunItem> OwnedItems { get; } = new List<RunItem>();

    public GameState State { get; private set; }
    public int ChapterIndex { get; private set; } = -1;

    /// <summary>상태가 바뀔 때마다 호출 (UI 갱신용으로 구독)</summary>
    public event Action<GameState> OnStateChanged;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        // 스토리 에셋 하나로 챕터 순서 + 시작 덱을 한 번에 지정
        if (story != null)
        {
            chapters = new List<ChapterData>(story.chapters);
            startingDeck = new List<CardData>(story.startingDeck);
            Debug.Log($"── 스토리: {story.storyTitle} ──");
        }

        MaxKi = startingMaxKi;
        OwnedDeck = new List<DeckEntry>();
        if (startingDeck != null)
        {
            foreach (CardData card in startingDeck)
            {
                if (card != null) OwnedDeck.Add(new DeckEntry(card, false, 0));
            }
        }

        BattleManager.DressUnit(playerUnit);   // 그림이 비어 있으면 자동 일러스트 (스토리 장면부터 보이게)

        // 시작 화면 → 로딩 → 첫 챕터. '다시 시작'으로 씬을 다시 불렀으면 시작 화면 없이 바로 덮고 들어간다
        bool restarted = skipTitleOnce;
        skipTitleOnce = false;
        if (showTitleScreen && !restarted)
            TitleScreen.Show(gameTitle, story != null ? story.storyTitle : "", titleTagline, () => BeginGame(false));
        else
            BeginGame(restarted);
    }

    // ─────────────────────────── 시작 화면 · 로딩 ───────────────────────────

    private static bool skipTitleOnce;   // RestartScene이 켠다 (씬을 다시 불러도 남는다)

    private void BeginGame(bool coverInstantly)
    {
        if (!showLoadingScreen) { NextChapter(); return; }
        LoadingScreen.Play(story != null ? story.storyTitle : gameTitle, "태엽을 감는 중…", Prewarm(), NextChapter, 1.6f, coverInstantly);
    }

    /// <summary>
    /// 로딩 화면 뒤에서 미리 해 둘 일: 지도 문양과, 그림이 없는 적의 자동 일러스트를 그려 둔다
    /// (전투가 시작되는 순간 그리면 화면이 잠깐 멈춘다). 한 걸음(yield)마다 하나씩.
    /// </summary>
    private IEnumerator Prewarm()
    {
        RouteIcons.Warm();
        yield return null;

        HashSet<EnemyData> seen = new HashSet<EnemyData>();
        foreach (ChapterData chapter in chapters)
        {
            if (chapter == null) continue;
            List<BattleStageData> stages = new List<BattleStageData> { chapter.battle };
            if (chapter.routeFights != null) stages.AddRange(chapter.routeFights);
            if (chapter.routeElites != null) stages.AddRange(chapter.routeElites);

            foreach (BattleStageData stage in stages)
            {
                if (stage == null || stage.enemies == null) continue;
                foreach (EnemySpawnInfo info in stage.enemies)
                {
                    EnemyData enemy = info != null ? info.enemy : null;
                    if (enemy == null || !seen.Add(enemy)) continue;
                    if (enemy.sprite != null || enemy.modelPrefab != null) continue;   // 그림이 이미 있다
                    Illustrator.Character(enemy.enemyName, enemy.tintColor);
                    yield return null;
                }
            }
        }
    }

    /// <summary>전투로 들어간다 — 로딩 화면이 다 덮은 뒤에 배경과 적을 바꾼다 (끄면 바로)</summary>
    private void EnterBattle(BattleStageData stage, string caption)
    {
        Action begin = () =>
        {
            SetState(GameState.Battle);
            battleManager.StartBattle(stage, playerUnit);
        };
        if (showLoadingScreen) LoadingScreen.Play(stage.stageName, caption, null, begin, 0.9f);
        else begin();
    }

    // ─────────────────────────── 챕터 진행 ───────────────────────────

    /// <summary>다음 챕터로 진행. 마지막 챕터면 클리어 상태로 전환.</summary>
    public void NextChapter()
    {
        ChapterIndex++;

        if (ChapterIndex >= chapters.Count)
        {
            SetState(GameState.Clear);
            Debug.Log("── 모든 챕터 클리어! 게임 클리어 ──");
            return;
        }

        Debug.Log($"── [{ChapterIndex + 1}] {chapters[ChapterIndex].chapterTitle} ──");
        PlayChapterIntro();
    }

    private void PlayChapterIntro()
    {
        SetState(GameState.Story);
        ChapterData chapter = chapters[ChapterIndex];

        if (dialogueManager == null)
        {
            Debug.LogError("GameManager에 DialogueManager가 연결되지 않았습니다.");
            return;
        }

        // 인트로 대사가 없으면 바로 전투로
        if (chapter.introLines == null || chapter.introLines.Count == 0)
        {
            StartChapterBattle();
            return;
        }

        dialogueManager.PlayLines(chapter.introLines, StartChapterBattle);
    }

    /// <summary>인트로 대사가 끝나면 호출됨.</summary>
    private void StartChapterBattle()
    {
        ChapterData chapter = chapters[ChapterIndex];

        // 전투가 없는 순수 스토리 챕터 → 승리 처리로 아웃트로/다음 챕터 진행
        if (chapter.battle == null)
        {
            OnBattleWon();
            return;
        }

        if (battleManager == null)
        {
            Debug.LogError("GameManager에 BattleManager가 연결되지 않았습니다.");
            return;
        }

        if (playerUnit == null)
        {
            Debug.LogError("GameManager에 Player Unit이 연결되지 않았습니다.");
            return;
        }

        EnterBattle(chapter.battle, chapter.chapterTitle);
    }

    // ─────────────────────────── 전투 결과 수신 ───────────────────────────

    /// <summary>
    /// BattleManager가 승리 시 호출 (전투 없는 챕터는 바로 호출).
    /// absorbed = 승리 화면에서 고른 적의 태엽판. 없으면 null.
    /// </summary>
    public void OnBattleWon(List<CardData> absorbed = null)
    {
        ChapterData chapter = chapters[ChapterIndex];

        // 1) 고른 적의 태엽판을 흡수한다 (덱 오염 = 이 게임의 핵심 트레이드오프)
        if (absorbed != null && absorbed.Count > 0) AbsorbCards(absorbed);

        // 지도 위 전투였으면 챕터 보상/대사 없이 지도로 돌아간다
        if (routeBattleWon != null)
        {
            Action back = routeBattleWon;
            routeBattleWon = null;
            SetState(GameState.Route);
            back();
            return;
        }

        // 1-2) 챕터의 큰 싸움을 넘겼으면 한숨 돌린다 — 다섯 장을 이어 가려면 장마다 숨 쉴 틈이 있어야 한다
        if (chapter.battle != null && playerUnit != null && chapterClearHealRatio > 0f)
        {
            int healed = playerUnit.Heal(Mathf.CeilToInt(playerUnit.MaxHp * chapterClearHealRatio));
            if (healed > 0) Debug.Log($"챕터 클리어 — 체력 +{healed}");
        }

        // 2) 기존 보상 카드 지급 (침식 없는 순수 획득)
        if (chapter.rewardCards != null)
        {
            foreach (CardData reward in chapter.rewardCards)
            {
                if (reward == null) continue;
                OwnedDeck.Add(new DeckEntry(reward, false, 0));
                Debug.Log($"보상 태엽판 획득: [{reward.cardName}]");
            }
        }

        // 3) 아웃트로 대사 → (전투를 이겼고 앞에 전투가 남았으면 갈림길) → 다음 챕터
        if (chapter.outroLines != null && chapter.outroLines.Count > 0)
        {
            SetState(GameState.Story);
            dialogueManager.PlayLines(chapter.outroLines, AfterChapter);
        }
        else
        {
            AfterChapter();
        }
    }

    /// <summary>챕터가 끝난 뒤: 전투를 이긴 챕터이고 앞에 전투가 남아 있으면 갈림길을 거쳐 다음 챕터로</summary>
    private void AfterChapter()
    {
        bool foughtHere = chapters[ChapterIndex].battle != null;
        ChapterData next = NextBattleChapter();
        if (foughtHere && routeRows > 0 && next != null)
        {
            SetState(GameState.Route);
            ChapterData here = chapters[ChapterIndex];
            RouteScreen.Play(routeRows, here.battle, here.routeFights, here.routeElites, next.battle.stageName, NextChapter);
        }
        else
        {
            NextChapter();
        }
    }

    /// <summary>앞으로 남은 첫 전투 챕터 (없으면 null) — 지도 꼭대기의 목적지</summary>
    private ChapterData NextBattleChapter()
    {
        for (int i = ChapterIndex + 1; i < chapters.Count; i++)
        {
            if (chapters[i] != null && chapters[i].battle != null) return chapters[i];
        }
        return null;
    }

    // ─────────────────────────── 갈림길용 (RouteScreen이 호출) ───────────────────────────

    private Action routeBattleWon;   // 지도 위 전투 중이면 이긴 뒤 지도로 돌아갈 콜백

    /// <summary>지도의 전투 칸: 챕터 진행과 별개로 전투를 하고, 이기면 onWon (지도로 복귀). 지면 평소처럼 패배.</summary>
    public void StartRouteBattle(BattleStageData stage, Action onWon)
    {
        routeBattleWon = onWon;
        EnterBattle(stage, "갈래길");
    }

    /// <summary>침식 없는 태엽판 1장을 덱에 넣는다</summary>
    public void AddCard(CardData card)
    {
        if (card == null) return;
        OwnedDeck.Add(new DeckEntry(card, false, 0));
        Debug.Log($"태엽판 획득: [{card.cardName}]");
    }

    public void AddMaxKi(int amount)
    {
        MaxKi = Mathf.Max(1, MaxKi + amount);
    }

    /// <summary>부품을 얻는다 (효과는 바로 적용되고 이번 판 내내 유지)</summary>
    public void GainItem(RunItem item)
    {
        if (item == null || OwnedItems.Contains(item)) return;
        OwnedItems.Add(item);
        item.onGain(this);
        Debug.Log($"부품 획득: [{item.name}]");
    }

    /// <summary>
    /// 갈래길 보상으로 나올 수 있는 태엽판 = 스토리의 보상 전용 태엽판 + 챕터 보상에 등장하는 태엽판 (중복 제외).
    /// 보상 전용 목록이 비어 있으면 시작 덱의 태엽판도 넣는다.
    /// </summary>
    public List<CardData> RouteCardPool()
    {
        List<CardData> pool = new List<CardData>();
        if (story != null && story.rewardPool != null)
        {
            foreach (CardData c in story.rewardPool)
            {
                if (c != null && !pool.Contains(c)) pool.Add(c);
            }
        }
        if (pool.Count == 0)
        {
            foreach (CardData c in startingDeck)
            {
                if (c != null && !pool.Contains(c)) pool.Add(c);
            }
        }
        foreach (ChapterData ch in chapters)
        {
            if (ch == null || ch.rewardCards == null) continue;
            foreach (CardData c in ch.rewardCards)
            {
                if (c != null && !pool.Contains(c)) pool.Add(c);
            }
        }
        return pool;
    }

    /// <summary>BattleManager가 패배 시 호출.</summary>
    public void OnBattleLost()
    {
        SetState(GameState.GameOver);
        Debug.Log("패배했습니다. 재시작 버튼 등으로 RestartScene()을 호출하세요.");
    }

    // ─────────────────────────── 흡수 시스템 ───────────────────────────

    /// <summary>
    /// 적의 태엽판을 내 덱으로 흡수한다.
    /// 원본 에셋은 절대 건드리지 않고 런타임 클론에 ◈ 표식을 남긴다.
    /// 중복 흡수 시 침식 수치만 +1 (같은 태엽판을 또 삼키면 원한이 깊어진다).
    /// </summary>
    public void AbsorbCards(List<CardData> cards)
    {
        if (cards == null) return;

        foreach (CardData card in cards)
        {
            if (card == null) continue;

            DeckEntry existing = OwnedDeck.Find(
                e => e.absorbed && e.sourceName == card.cardName);

            if (existing != null)
            {
                existing.corruption++;
                Debug.Log($"태엽판 [{card.cardName}] 재흡수 — 침식이 깊어진다 ({existing.corruption})");
            }
            else
            {
                CardData clone = Instantiate(card);
                clone.name = card.cardName + " ◈";
                // ◈ 표식은 CardView가 붙이므로 cardName은 원본 그대로 둔다 (◈◈ 중복 방지)

                OwnedDeck.Add(new DeckEntry(clone, true, 1, card.cardName));
                Debug.Log($"적의 태엽판 흡수: [{card.cardName}] ◈ (침식 1)");
            }
        }
    }

    /// <summary>이미 흡수한 태엽판의 원본 이름들 (승리 화면에서 "침식 +1" 경고용)</summary>
    public HashSet<string> AbsorbedSourceNames()
    {
        HashSet<string> names = new HashSet<string>();
        foreach (DeckEntry e in OwnedDeck)
        {
            if (e.absorbed && e.sourceName != null) names.Add(e.sourceName);
        }
        return names;
    }

    /// <summary>침식 수치에 대응하는 '침식 발동' 확률 (0~0.8로 제한).</summary>
    public float CorruptionChance(int corruption)
    {
        return Mathf.Min(0.8f, corruptionBaseChance + corruptionChancePerPoint * Mathf.Max(0, corruption - 1));
    }

    // ─────────────────────────── 기타 ───────────────────────────

    /// <summary>패배 UI의 버튼에 연결해서 사용. 씬을 다시 불러 덱·챕터까지 처음부터 시작한다.</summary>
    public void RestartScene()
    {
        Time.timeScale = 1f;   // 히트스톱 중에 끝났어도 멈춘 시간이 다음 판으로 넘어가지 않게
        skipTitleOnce = true;  // 다시 시작은 시작 화면 없이 바로
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    private void SetState(GameState state)
    {
        State = state;
        if (OnStateChanged != null) OnStateChanged(state);
    }
}
