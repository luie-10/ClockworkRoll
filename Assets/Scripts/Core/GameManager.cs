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
        PauseMenu.Ensure();                    // 오른쪽 위 [메뉴] — 저장하고 나가기

        // 시작 화면 → 로딩 → 첫 챕터. '다시 시작'으로 씬을 다시 불렀으면 시작 화면 없이 바로 덮고 들어간다
        bool restarted = skipTitleOnce;
        skipTitleOnce = false;
        if (showTitleScreen && !restarted)
        {
            // 저장이 있으면 시작 화면에 '이어하기'가 생긴다
            SaveData save = SaveSystem.Load(StoryKey);
            TitleScreen.Show(gameTitle, story != null ? story.storyTitle : "", titleTagline, () => BeginGame(false),
                             save != null ? DescribeSave(save) : null,
                             save != null ? (Action)(() => { resume = save; BeginGame(false); }) : null);
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
        LoadingScreen.Play(story != null ? story.storyTitle : gameTitle, resume != null ? "멈춰 둔 태엽을 다시 감는 중…" : "태엽을 감는 중…",
                           Prewarm(), start, 1.6f, coverInstantly);
    }

    /// <summary>새 판이면 첫 챕터부터, 이어하기면 저장해 둔 지점부터</summary>
    private void StartRun()
    {
        SaveData save = resume;
        resume = null;
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
            SaveSystem.Delete();   // 끝까지 갔다 — 이어할 것이 없다
            Debug.Log("── 모든 챕터 클리어! 게임 클리어 ──");
            return;
        }

        Debug.Log($"── [{ChapterIndex + 1}] {chapters[ChapterIndex].chapterTitle} ──");
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
                clone.name = card.cardName + " ◈";
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

        Debug.Log($"── 이어하기: {DescribeSave(s)} ──");

        if (s.onRoute)
        {
            ChapterIndex = s.chapterIndex;
            ChapterData here = chapters[ChapterIndex], next = NextBattleChapter();
            if (here != null && here.battle != null && next != null && routeRows > 0)
            {
                SetState(GameState.Route);
                RouteScreen.Play(routeRows, here.battle, here.routeFights, here.routeElites, next.battle.stageName, NextChapter, s.routeSeed, s.routePath);
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
