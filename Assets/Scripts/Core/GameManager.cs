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
    public string titleTagline = "종탑이 멈추자 마을이 물에 잠겼다. 떠다니는 기억을 삼키며 종탑으로 올라가자";

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

    // ─────────────────────────── 모드 (스토리 / 무한) ───────────────────────────

    public enum RunMode { Story, Endless }

    /// <summary>지금 판의 모드. 씬을 다시 불러도(다시 시작) 남는다 — 시작 화면에서 고른다</summary>
    public static RunMode Mode { get; private set; } = RunMode.Story;
    public bool IsEndless { get { return Mode == RunMode.Endless; } }

    [Header("무한 모드 (스토리 없이 지도 → ??? 보스를 끝없이 되풀이)")]
    [Tooltip("회차가 하나 오를 때마다 적 체력 배율 +")]
    public float endlessHpPerLoop = 0.25f;
    [Tooltip("회차가 이만큼 오를 때마다 적의 모든 칸 위력 +1")]
    [Min(1)] public int endlessPowerEvery = 2;
    [Tooltip("보스는 같은 회차의 잡몹보다 체력 배율을 이만큼 더")]
    public float endlessBossHpBonus = 0.2f;
    [Tooltip("보스를 쓰러뜨린 뒤 회복하는 체력 비율")]
    public float endlessLoopHealRatio = 0.3f;

    [Tooltip("무한 모드 랭킹 서버 주소 (Server/ranking-server.js). 비우면 랭킹을 쓰지 않는다")]
    public string rankingServerUrl = "https://clockworkroll-ranking.clockworkroll-ranking-worker.workers.dev";

    /// <summary>무한 모드 회차 (1부터)</summary>
    public int EndlessLoop { get; private set; }

    private const string EndlessBestKey = "endless.best";
    /// <summary>무한 모드 최고 기록 (보스까지 쓰러뜨린 회차)</summary>
    public static int EndlessBest { get { try { return PlayerPrefs.GetInt(EndlessBestKey, 0); } catch { return 0; } } }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 시작 화면으로 나갔다 돌아오면(씬을 다시 부르면) 화면들이 '열려 있다'고 기억한 값이 남는다 — 새 씬에 맞게 지운다
        RouteScreen.ForgetScene();
        VictoryScreen.ForgetScene();
        TitleScreen.ForgetScene();
        LoadingScreen.ForgetScene();
    }

    void Start()
    {
        // 스토리 에셋 하나로 챕터 순서 + 시작 덱을 한 번에 지정
        if (story != null)
        {
            chapters = new List<ChapterData>(story.chapters);
            startingDeck = new List<CardData>(story.startingDeck);
            Debug.Log($"―― 스토리: {story.storyTitle} ――");
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
        PauseMenu.Ensure();                    // 오른쪽 위 [메뉴] — 저장하고 나가기
        Ranking.ServerUrl = rankingServerUrl;
        GameStatePanel.ExtraLine = null;      // 지난 판(무한 모드)의 랭킹 줄이 남지 않게
        UIScale.Ensure();                      // 화면 배율 (멈춤 메뉴 · Ctrl + '+' / '-')
        TutorialCoach.Ensure();                // 처음 하는 사람에게 규칙을 장면마다 하나씩

        // 시작 화면 → 로딩 → 첫 챕터. '다시 시작'으로 씬을 다시 불렀으면 시작 화면 없이 바로 덮고 들어간다
        bool restarted = skipTitleOnce;
        skipTitleOnce = false;
        if (showTitleScreen && !restarted)
        {
            // 저장이 있으면 시작 화면에 '이어하기'가 생긴다
            SaveData save = SaveSystem.Load(StoryKey);
            int best = EndlessBest;
            TitleScreen.Show(gameTitle, story != null ? story.storyTitle : "", titleTagline, () => { Mode = RunMode.Story; BeginGame(false); },
                             save != null ? DescribeSave(save) : null,
                             save != null ? (Action)(() => { Mode = RunMode.Story; resume = save; BeginGame(false); }) : null,
                             () => { Mode = RunMode.Endless; BeginGame(false); },
                             best > 0 ? $"최고 기록 {best}회차" : "점점 강해지는 적 · 정체를 알 수 없는 보스");
        }
        else
            BeginGame(restarted);
    }

    // ─────────────────────────── 시작 화면 · 로딩 ───────────────────────────

    private static bool skipTitleOnce;   // RestartScene이 켠다 (씬을 다시 불러도 남는다)

    private void BeginGame(bool coverInstantly)
    {
        Action start = StartRun;
        if (!showLoadingScreen) { start(); return; }
        LoadingScreen.Play(IsEndless ? "무한 모드" : story != null ? story.storyTitle : gameTitle, IsEndless ? "끝나지 않는 태엽을 감는 중…" : resume != null ? "멈춰 둔 태엽을 다시 감는 중…" : "태엽을 감는 중…",
                           Prewarm(), start, 1.6f, coverInstantly);
    }

    /// <summary>새 판이면 첫 챕터부터, 이어하기면 저장해 둔 지점부터</summary>
    private void StartRun()
    {
        SaveData save = resume;
        resume = null;
        if (IsEndless) { StartEndless(); return; }
        if (save != null && Restore(save)) return;
        NextChapter();
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
        if (IsEndless) ApplyEndlessBoost(endlessBossBattle);
        Action begin = () =>
        {
            SetState(GameState.Battle);
            battleManager.StartBattle(stage, playerUnit);
        };
        string title = IsEndless && endlessBossBattle ? BattleManager.HiddenName : stage.stageName;   // 무한 모드 보스는 정체를 숨긴다
        if (showLoadingScreen) LoadingScreen.Play(title, caption, null, begin, 0.9f);
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
            SaveSystem.Delete();   // 끝까지 갔다 — 이어할 것이 없다
            Debug.Log("―― 모든 챕터 클리어! 게임 클리어 ――");
            return;
        }

        Debug.Log($"―― [{ChapterIndex + 1}] {chapters[ChapterIndex].chapterTitle} ――");
        SaveCheckpoint(false, 0, null);   // 챕터의 첫 장면 = 이어하기 지점
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
        if (IsEndless) { OnEndlessWon(absorbed); return; }
        ChapterData chapter = chapters[ChapterIndex];

        // 1) 고른 적의 태엽판을 흡수한다 (덱 오염 = 이 게임의 핵심 트레이드오프)
        if (absorbed != null && absorbed.Count > 0) AbsorbCards(absorbed);

        // 1-1) 부품: 전투에서 이길 때마다 발동하는 효과 (전투 없는 챕터는 건너뛴다)
        if (chapter.battle != null || routeBattleWon != null)
        {
            foreach (RunItem item in OwnedItems)
            {
                if (item != null && item.onBattleWon != null) item.onBattleWon(this);
            }
        }

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
            if (healed > 0) Debug.Log($"챕터 클리어! 체력 +{healed}");
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
            RouteScreen.Play(routeRows, here.battle, here.routeFights, here.routeElites, next.battle.stageName, NextChapter, backdrops: here.routeBackdrops);
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
    /// <summary>다음 지도 전투를 이 무대로 바꿔 친다 (홍보영상 촬영 — 한 번 쓰고 비운다)</summary>
    public static BattleStageData StageOverride;

    public void StartRouteBattle(BattleStageData stage, Action onWon)
    {
        if (StageOverride != null) { stage = StageOverride; StageOverride = null; }
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
        if (IsEndless)
        {
            Debug.Log($"무한 모드 {EndlessLoop}회차에서 쓰러졌다 (최고 기록 {EndlessBest}회차)");
            EndEndlessRun();
            return;   // 무한 모드는 저장을 쓰지 않는다 (스토리 저장을 지우지 않게)
        }
        SaveSystem.Delete();   // 쓰러진 판은 이어할 수 없다 (처음부터 다시)
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
                Debug.Log($"태엽판 [{card.cardName}] 재흡수. 침식이 깊어진다 ({existing.corruption})");
            }
            else
            {
                CardData clone = Instantiate(card);
                clone.name = card.cardName + " ❖";
                // ◈ 표식은 CardView가 붙이므로 cardName은 원본 그대로 둔다 (◈◈ 중복 방지)

                OwnedDeck.Add(new DeckEntry(clone, true, 1, card.cardName));
                Debug.Log($"적의 태엽판 흡수: [{card.cardName}] ❖ (침식 1)");
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

    // ─────────────────────────── 저장 · 이어하기 ───────────────────────────

    private SaveData resume;   // 시작 화면에서 '이어하기'를 고르면 채워진다

    /// <summary>어느 이야기의 저장인지 가리는 이름</summary>
    private string StoryKey { get { return story != null ? story.storyTitle : gameTitle; } }

    /// <summary>시작 화면의 '이어하기' 아래에 보여 줄 한 줄</summary>
    private static string DescribeSave(SaveData s)
    {
        string where = string.IsNullOrEmpty(s.chapterTitle) ? $"{s.chapterIndex + 1}번째 장" : s.chapterTitle;
        if (s.onRoute) where += " 뒤 갈래길";
        return $"{where} · 체력 {s.hp}/{s.maxHp} · 태엽판 {s.deck.Count}장";
    }

    /// <summary>갈래길 지도에서 칸 하나를 마칠 때마다 RouteScreen이 부른다 (seed = 지도의 씨앗, path = 지나온 칸)</summary>
    public void SaveRouteCheckpoint(int seed, List<int> path)
    {
        SaveCheckpoint(true, seed, path);
    }

    private void SaveCheckpoint(bool onRoute, int routeSeed, List<int> routePath)
    {
        if (IsEndless) return;   // 무한 모드는 한 번에 끝까지 (스토리 저장을 덮어쓰지 않는다)
        if (playerUnit == null || OwnedDeck == null || ChapterIndex < 0 || ChapterIndex >= chapters.Count) return;

        SaveData s = new SaveData
        {
            story = StoryKey,
            chapterIndex = ChapterIndex,
            chapterTitle = chapters[ChapterIndex] != null ? chapters[ChapterIndex].chapterTitle : "",
            onRoute = onRoute,
            routeSeed = routeSeed,
            hp = playerUnit.Hp,
            maxHp = playerUnit.MaxHp,
            maxKi = MaxKi,
        };
        if (routePath != null) s.routePath.AddRange(routePath);
        foreach (RunItem item in OwnedItems) if (item != null) s.items.Add(item.name);
        foreach (DeckEntry e in OwnedDeck)
        {
            if (e == null || e.data == null) continue;
            // 흡수한 태엽판은 실행 중에 만든 복제본이라 원본의 카드 이름으로 적어 둔다
            s.deck.Add(new SavedCard { card = e.absorbed ? e.sourceName : e.data.name, absorbed = e.absorbed, corruption = e.corruption, sourceName = e.sourceName });
        }
        SaveSystem.Write(s);
    }

    /// <summary>저장해 둔 지점으로 돌아간다. 저장 내용이 지금 게임과 맞지 않으면 false (새 판으로 시작)</summary>
    private bool Restore(SaveData s)
    {
        if (s.chapterIndex < 0 || s.chapterIndex >= chapters.Count || playerUnit == null) return false;

        // 덱: 이름으로 태엽판 에셋을 다시 찾는다
        Dictionary<string, CardData> byAsset = new Dictionary<string, CardData>(), byName = new Dictionary<string, CardData>();
        foreach (CardData card in AllCards())
        {
            if (!byAsset.ContainsKey(card.name)) byAsset[card.name] = card;
            if (!string.IsNullOrEmpty(card.cardName) && !byName.ContainsKey(card.cardName)) byName[card.cardName] = card;
        }
        List<DeckEntry> deck = new List<DeckEntry>();
        foreach (SavedCard sc in s.deck)
        {
            if (sc == null || string.IsNullOrEmpty(sc.card)) continue;
            CardData card;
            if (sc.absorbed ? !byName.TryGetValue(sc.card, out card) && !byAsset.TryGetValue(sc.card, out card)
                            : !byAsset.TryGetValue(sc.card, out card) && !byName.TryGetValue(sc.card, out card))
            {
                Debug.LogWarning($"이어하기: 태엽판 [{sc.card}]을(를) 찾지 못해 덱에서 뺐습니다.");
                continue;
            }
            if (sc.absorbed)
            {
                CardData clone = Instantiate(card);
                clone.name = card.cardName + " ❖";
                deck.Add(new DeckEntry(clone, true, Mathf.Max(1, sc.corruption), sc.sourceName));
            }
            else deck.Add(new DeckEntry(card, false, 0));
        }
        if (deck.Count == 0) return false;

        OwnedDeck = deck;
        MaxKi = Mathf.Max(1, s.maxKi);
        playerUnit.RestoreVitals(s.maxHp, s.hp);

        // 부품: 얻을 때의 효과(최대 체력 · 기력)는 위의 숫자에 이미 들어 있으니 목록에만 되돌린다
        OwnedItems.Clear();
        foreach (string itemName in s.items)
        {
            RunItem item = RunItems.All.Find(i => i.name == itemName);
            if (item != null && !OwnedItems.Contains(item)) OwnedItems.Add(item);
        }

        Debug.Log($"―― 이어하기: {DescribeSave(s)} ――");

        if (s.onRoute)
        {
            ChapterIndex = s.chapterIndex;
            ChapterData here = chapters[ChapterIndex], next = NextBattleChapter();
            if (here != null && here.battle != null && next != null && routeRows > 0)
            {
                SetState(GameState.Route);
                RouteScreen.Play(routeRows, here.battle, here.routeFights, here.routeElites, next.battle.stageName, NextChapter, s.routeSeed, s.routePath, here.routeBackdrops);
                return true;
            }
        }

        ChapterIndex = s.chapterIndex - 1;   // NextChapter가 하나 올린다
        NextChapter();
        return true;
    }

    /// <summary>이 이야기에 나오는 모든 태엽판 (시작 덱 · 보상 · 적의 덱)</summary>
    private List<CardData> AllCards()
    {
        List<CardData> all = new List<CardData>();
        Action<IEnumerable<CardData>> add = cards =>
        {
            if (cards == null) return;
            foreach (CardData c in cards) if (c != null && !all.Contains(c)) all.Add(c);
        };

        add(startingDeck);
        add(RouteCardPool());
        foreach (ChapterData chapter in chapters)
        {
            if (chapter == null) continue;
            add(chapter.rewardCards);
            List<BattleStageData> stages = new List<BattleStageData> { chapter.battle };
            if (chapter.routeFights != null) stages.AddRange(chapter.routeFights);
            if (chapter.routeElites != null) stages.AddRange(chapter.routeElites);
            foreach (BattleStageData stage in stages)
            {
                if (stage == null) continue;
                add(stage.deckOverride);
                if (stage.enemies == null) continue;
                foreach (EnemySpawnInfo info in stage.enemies) if (info != null && info.enemy != null) add(info.enemy.deck);
            }
        }
        return all;
    }

    /// <summary>[저장하고 나가기] — 시작 화면으로 돌아간다. 진행은 이미 저장돼 있다 (챕터 시작 · 갈래길 칸마다)</summary>
    public void QuitToTitle()
    {
        Time.timeScale = 1f;
        skipTitleOnce = false;
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    // ─────────────────────────── 무한 모드 ───────────────────────────
    // 스토리 대사 없이: 지도(모든 챕터의 적 무리 · 모든 배경) → 꼭대기의 ??? 보스 → 다음 회차 … 를 끝없이.
    // 회차마다 적의 체력 배율 · 칸 위력이 오른다. 보스는 챕터 보스 중 하나가 무작위로 나오고, 이름은 ???로 감춘다.

    private bool endlessBossBattle;          // 지금 들어가는 전투가 회차의 보스인가
    private BattleStageData lastEndlessBoss;

    private void StartEndless()
    {
        EndlessLoop = 0;
        lastEndlessBoss = null;
        if (!string.IsNullOrEmpty(rankingServerUrl)) Ranking.BeginRun();   // 서버에 판을 연다 (기록은 이 판의 회차 통과로만 오른다)
        ChapterIndex = Mathf.Max(0, chapters.FindIndex(c => c != null && c.battle != null));
        NextEndlessLoop();
    }

    private void NextEndlessLoop()
    {
        EndlessLoop++;
        Debug.Log($"―― 무한 모드 {EndlessLoop}회차 ――");

        List<BattleStageData> fights = new List<BattleStageData>(), elites = new List<BattleStageData>();
        foreach (ChapterData ch in chapters)
        {
            if (ch == null) continue;
            if (ch.routeFights != null) foreach (BattleStageData st in ch.routeFights) if (st != null && !fights.Contains(st)) fights.Add(st);
            if (ch.routeElites != null) foreach (BattleStageData st in ch.routeElites) if (st != null && !elites.Contains(st)) elites.Add(st);
        }
        List<BackdropType> backdrops = new List<BackdropType>((BackdropType[])Enum.GetValues(typeof(BackdropType)));

        BattleStageData fallback = PickEndlessBoss(false);   // 적 무리 목록이 비었을 때 지도 전투가 데려올 적
        SetState(GameState.Route);
        RouteScreen.Play(routeRows, fallback, fights, elites, $"{EndlessLoop}회차 · {BattleManager.HiddenName}", EndlessBoss, backdrops: backdrops);
    }

    /// <summary>챕터 보스 무대 중 하나 (방금 나온 보스는 되도록 피한다)</summary>
    private BattleStageData PickEndlessBoss(bool remember)
    {
        List<BattleStageData> bosses = new List<BattleStageData>();
        foreach (ChapterData ch in chapters) if (ch != null && ch.battle != null && !bosses.Contains(ch.battle)) bosses.Add(ch.battle);
        if (bosses.Count == 0) return null;
        if (bosses.Count > 1 && lastEndlessBoss != null) bosses.Remove(lastEndlessBoss);
        BattleStageData pick = bosses[UnityEngine.Random.Range(0, bosses.Count)];
        if (remember) lastEndlessBoss = pick;
        return pick;
    }

    /// <summary>지도 꼭대기: 정체를 숨긴 보스와 싸운다 (배경도 무작위)</summary>
    private void EndlessBoss()
    {
        BattleStageData boss = PickEndlessBoss(true);
        if (boss == null) { NextEndlessLoop(); return; }
        endlessBossBattle = true;
        BattleManager.NextBackdrop = (BackdropType)UnityEngine.Random.Range(0, Enum.GetValues(typeof(BackdropType)).Length);
        EnterBattle(boss, $"{EndlessLoop}회차 보스: 정체 불명");
    }

    /// <summary>쓰러졌을 때: 서버의 판을 닫고 순위를 패배 화면에 덧붙인다</summary>
    private void EndEndlessRun()
    {
        if (string.IsNullOrEmpty(rankingServerUrl)) return;
        GameStatePanel.ExtraLine = $"<color={UITheme.HexMuted}>랭킹 서버에 기록을 올리는 중…</color>";
        Ranking.EndRun(r =>
        {
            GameStatePanel.ExtraLine = r == null
                ? $"<color={UITheme.HexMuted}>랭킹 서버에 닿지 못했다 (기록은 이 기기에만 남는다)</color>"
                : r.rank > 0
                    ? $"<color={UITheme.HexGold}>{Ranking.PlayerName}</color>  랭킹 <color={UITheme.HexGold}>{r.rank}위</color> / {r.total}명  (내 최고 {r.best}회차)"
                    : $"<color={UITheme.HexMuted}>보스를 한 번 넘기면 랭킹에 이름이 오른다</color>";
        });
    }

    /// <summary>회차에 맞춰 다음 전투의 적을 강화한다 (EnterBattle이 부른다)</summary>
    private void ApplyEndlessBoost(bool boss)
    {
        int tier = Mathf.Max(0, EndlessLoop - 1);
        BattleManager.NextEnemyHpScale = 1f + endlessHpPerLoop * tier + (boss ? endlessBossHpBonus : 0f);
        BattleManager.NextEnemyPower = tier / Mathf.Max(1, endlessPowerEvery) + (boss && tier > 0 ? 1 : 0);
        BattleManager.NextHideIdentity = boss;
        // 3회차부터 적마다 무작위 특성이 붙는다 (2회차마다 하나씩, 최대 3개 · 수치는 4회차마다 +1)
        BattleManager.NextExtraTraits = Mathf.Min(3, tier / 2 + (boss && tier >= 2 ? 1 : 0));
        BattleManager.NextExtraTraitAmount = 1 + tier / 4;
    }

    /// <summary>무한 모드에서 이겼을 때: 지도 전투면 지도로, 보스면 기록 · 회복하고 다음 회차</summary>
    private void OnEndlessWon(List<CardData> absorbed)
    {
        if (absorbed != null && absorbed.Count > 0) AbsorbCards(absorbed);
        foreach (RunItem item in OwnedItems)
            if (item != null && item.onBattleWon != null) item.onBattleWon(this);

        if (routeBattleWon != null)
        {
            Action back = routeBattleWon;
            routeBattleWon = null;
            SetState(GameState.Route);
            back();
            return;
        }

        endlessBossBattle = false;
        if (lastEndlessBoss != null) Debug.Log($"{EndlessLoop}회차 보스의 정체: {lastEndlessBoss.stageName}");
        if (EndlessLoop > EndlessBest) { try { PlayerPrefs.SetInt(EndlessBestKey, EndlessLoop); PlayerPrefs.Save(); } catch { } }
        if (!string.IsNullOrEmpty(rankingServerUrl)) Ranking.Checkpoint(EndlessLoop);   // 보스를 넘길 때마다 서명해서 올린다 (도중에 꺼도 기록이 남게)
        if (playerUnit != null && endlessLoopHealRatio > 0f) playerUnit.Heal(Mathf.CeilToInt(playerUnit.MaxHp * endlessLoopHealRatio));
        NextEndlessLoop();
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
