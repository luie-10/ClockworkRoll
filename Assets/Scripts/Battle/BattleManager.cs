using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 한 무대(전투)의 진행을 담당.
///
/// [시계판] 한 라운드 = 시곗바늘 한 바퀴(1~12시). 시계판 한 칸 = 주사위 하나.
///          태엽판을 시각에 놓으면 주사위가 한 칸씩 이어서 배정되고, 같은 칸의 적 주사위와만 합을 한다.
/// [자원] 기력 — 라운드마다 최대치까지 다시 찬다. 최대치는 부품(이번 판)과 스킬(이번 전투)로 늘어난다.
/// [합]   양쪽 주사위를 앞에서부터 한 칸씩 비교.
///        - 타입 상성 보정: 참격 > 관통 > 타격 > 참격 (우위 시 굴림 +2)
///        - 회피는 상대 공격 주사위 상대로 굴림 +2
///        - 동점은 양쪽 모두 무효
///        - 회피 성공 보상은 태엽판마다 회피 칸의 [합 승리시] 효과로 정한다
/// [장력] 서로의 칸이 맞부딪힐 때마다 차오른다 (0~3단계, 전투 동안 유지).
///        단계만큼 승리 시 삼킬 수 있는 태엽판이 늘고, 장력 단계가 필요한 태엽판을 낼 수 있게 된다.
/// [침식] 흡수 카드(◈) 사용 시 확률적으로 발동해 사용자가 피해를 입는다.
/// [연출] 공격자가 응답자 곁으로 이동, 카메라 포커스.
/// </summary>
public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }

    public enum BattlePhase { Ready, PlayerTurn, EnemyTurn, End }

    [Header("씬 배치 참조")]
    public HandManager handManager;

    [Header("HUD (비워도 동작은 함)")]
    public Text stageNameText;
    public Text kiText;          // 기력 표시
    [Tooltip("'내 턴' / '적의 턴' 표시")]
    public Text phaseText;
    [Tooltip("내 턴이 아닐 때는 자동으로 비활성화")]
    public Button endTurnButton;
    [Tooltip("화면 전투 기록 (비워두면 콘솔에만 기록). Show Battle Log를 끄면 연결돼 있어도 숨긴다")]
    public Text battleLogText;
    [Tooltip("체크: 화면에 전투 기록 패널을 보여줌 / 해제: 숨김 (기록은 콘솔에 계속 남음)")]
    public bool showBattleLog = false;
    [Tooltip("전투 로그에 남겨둘 최대 줄 수 (스크롤해서 볼 수 있음)")]
    public int maxLogLines = 80;

    [Header("기력 (자원)")]

    [Header("장력 (합 게이지)")]
    [Tooltip("장력 한 단계를 채우는 데 필요한 합(맞부딪힘) 횟수")]
    public int tensionPerStage = 3;

    [Header("시계판")]
    [Tooltip("시곗바늘이 한 시각을 넘어갈 때의 대기 시간(초)")]
    public float hourTickTime = 0.35f;
    [Tooltip("행동이 있는 시각에 바늘이 머무는 시간(초) — 시계판 진행을 천천히 보여준다")]
    public float busyHourTime = 0.8f;
    [Tooltip("아무 행동도 없는 시각을 지나는 시간(초)")]
    public float emptyHourTime = 0.4f;

    [Header("합 규칙")]
    [Tooltip("상성 우위 시 굴림 보정치")]
    public int typeAdvantageBonus = 2;

    [Header("침식 (흡수 카드 부작용)")]
    [Tooltip("침식 발동 시 기본 피해 (침식 1당 +1)")]
    public int corruptionBaseDamage = 2;

    [Header("2.5D (2D 캐릭터 + 3D 배경)")]
    [Tooltip("체크: 적을 2D 스프라이트로 생성 / 해제: 3D 큐브")]
    public bool spriteMode = true;
    [Tooltip("스프라이트 적이 서는 바닥 높이 (Spawn Position의 Y 대신 사용)")]
    public float spriteGroundY = 0f;

    [Header("합 연출 (끄면 기존처럼 로그만)")]
    public bool clashFx = true;
    [Tooltip("유닛 머리 위에 HP/상태이상 표시")]
    public bool showStatusLabels = true;
    [Tooltip("합 중 머리 위에 태엽판 이름 + 칸(굴림 애니메이션) 표시")]
    public bool showDiceDisplay = true;
    [Tooltip("칸이 굴러가는 시간(초)")]
    public float diceRollTime = 0.45f;
    [Tooltip("칸이 부딪힐 때 앞으로 튀어나가는 거리")]
    public float lungeDistance = 0.6f;
    [Tooltip("돌진 1회에 걸리는 시간(초)")]
    public float lungeDuration = 0.3f;
    [Tooltip("합에서 졌을 때 밀려나는 거리")]
    public float knockbackDistance = 0.8f;
    [Tooltip("이 이상 피해면 히트스톱(순간 정지) + 큰 흔들림")]
    public int bigHitThreshold = 8;
    [Tooltip("히트스톱 길이(초, 실제 시간)")]
    public float hitStopTime = 0.08f;

    public BattlePhase Phase { get; private set; } = BattlePhase.Ready;
    public int CurrentKi { get; private set; }

    /// <summary>라운드마다 차오르는 기력 = 부품으로 늘어난 최대치 + 이번 전투에 스킬로 늘린 양</summary>
    public int MaxKi { get { return (GameManager.Instance != null ? GameManager.Instance.MaxKi : 3) + battleMaxKi; } }

    private int battleMaxKi;   // 이번 전투 동안만 늘어난 최대 기력 ([이번 전투 최대 기력 +n])
    private int pendingKi;     // 다음 라운드에 최대치 위로 더 받는 기력 ([기력 회복]은 시계가 도는 중에 발동하므로)

    /// <summary>다음 라운드에 최대치 위로 더 받을 기력 (기력 게이지의 흐린 구슬)</summary>
    public int PendingKi { get { return pendingKi; } }
    private int round;         // 이번 전투의 라운드 번호 (1부터) — 시계추형 / 감김 특성이 쓴다

    /// <summary>장력 최대 단계</summary>
    public const int MaxTensionStage = 3;

    /// <summary>쌓인 장력 (합 1번 = 1). 0 ~ tensionPerStage x 3</summary>
    public int TensionPoints { get; private set; }

    /// <summary>현재 장력 단계 (0~3)</summary>
    public int TensionStage { get { return Mathf.Min(MaxTensionStage, TensionPoints / Mathf.Max(1, tensionPerStage)); } }

    /// <summary>시계판 칸 수 (1시~12시)</summary>
    public const int ClockHours = 12;

    /// <summary>진행 중인 시각 (1~12). 계획 단계에서는 0.</summary>
    public int CurrentHour { get; private set; }

    /// <summary>일방 공격 대상 (적을 클릭해서 지정)</summary>
    public Unit FocusTarget { get; private set; }

    /// <summary>합 연출이 진행 중이면 true — 이 동안에는 카드/턴 종료 입력을 받지 않는다.</summary>
    public bool IsBusy { get; private set; }

    // 상성 순환: 참격 > 관통 > 타격 > 참격
    private static readonly Dictionary<DiceDetail, DiceDetail> advantageOver =
        new Dictionary<DiceDetail, DiceDetail>
    {
        { DiceDetail.Slash, DiceDetail.Thrust },   // 참격은 관통을 자른다
        { DiceDetail.Thrust, DiceDetail.Blunt },   // 관통은 타격을 뚫는다
        { DiceDetail.Blunt, DiceDetail.Slash },    // 타격은 참격을 흔든다
    };

    /// <summary>현재 무대의 적들 (디버그 창 등 읽기용)</summary>
    public IReadOnlyList<Unit> Enemies { get { return enemies; } }
    public Unit PlayerUnit { get { return playerUnit; } }

    private BattleStageData stage;

    /// <summary>다음 전투 한 번만 쓸 배경 (지도 전투에서 챕터가 고른 것). 쓰고 나면 비운다</summary>
    public static BackdropType? NextBackdrop;

    /// <summary>다음 전투 한 번만 적에게 걸 강화 (무한 모드: 회차마다 체력 배율 · 위력 +n · 보스 정체 숨김). 쓰고 나면 되돌린다</summary>
    public static float NextEnemyHpScale = 1f;
    public static int NextEnemyPower;
    public static bool NextHideIdentity;
    /// <summary>다음 전투의 적마다 무작위로 덧붙일 특성 수와 그 수치 (무한 모드 — 한 번 쓰고 0으로)</summary>
    public static int NextExtraTraits, NextExtraTraitAmount = 1;

    /// <summary>이름을 숨긴 적에게 보여 줄 이름</summary>
    public const string HiddenName = "???";
    private Unit playerUnit;
    private readonly List<Unit> enemies = new List<Unit>();
    private readonly List<string> logLines = new List<string>();
    private CardInstance pendingCard;   // 클릭해서 대상을 기다리는 중인 카드
    private readonly List<TimedAction> enemyActions = new List<TimedAction>();    // 적이 이번 라운드 시계판에 올린 행동
    private readonly List<TimedAction> playerActions = new List<TimedAction>();   // 내가 시계판에 올린 행동
    private int lastDamage;                  // 마지막 공격의 실제 피해 (내성 적용 후, 연출용)
    private Resistance lastResistance;

    // 한 라운드(시계 한 바퀴) 안에서만 유효한 효과 (라운드가 끝나면 비운다)
    private readonly Dictionary<Unit, int> nextDieBonus = new Dictionary<Unit, int>();   // [다음 주사위 위력 +n]
    private readonly HashSet<Unit> destroyNextDie = new HashSet<Unit>();                 // 이 유닛의 다음 주사위는 파괴됨
    // [과감기]로 오른 주사위 위력 — (주인, 주사위) 단위 (같은 태엽판을 쓰는 적이 여럿이어도 섞이지 않게)
    private readonly Dictionary<(Unit, DiceData), int> cardPower = new Dictionary<(Unit, DiceData), int>();
    private readonly Dictionary<TimedAction, ClashDiceDisplay> displays = new Dictionary<TimedAction, ClashDiceDisplay>();   // 진행 중인 행동의 머리 위 주사위 줄

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 전투 기록 패널은 기본으로 숨긴다 — 이미 만들어진 씬에도 적용 (ScrollRect가 붙은 패널째로 끔)
        if (!showBattleLog && battleLogText != null)
        {
            ScrollRect scroll = battleLogText.GetComponentInParent<ScrollRect>();
            GameObject panel = scroll != null ? scroll.gameObject : battleLogText.gameObject;   // ScrollLog: 패널에 ScrollRect가 붙어 있음
            panel.SetActive(false);
            battleLogText = null;
        }
    }

    void Update()
    {
        // 턴 종료 버튼은 내 턴 + 합 연출이 끝난 상태에서만 누를 수 있다
        if (endTurnButton != null)
            endTurnButton.interactable = Phase == BattlePhase.PlayerTurn && !IsBusy;
        if (autoButton != null)
            autoButton.interactable = Phase == BattlePhase.PlayerTurn && !IsBusy;

        // A: 자동 배치
        if (Input.GetKeyDown(KeyCode.A) && !TutorialCoach.IsOpen && !PauseMenu.IsOpen && !DeckViewer.IsOpen) AutoPlace();

        // 숫자 키 1~9: 대상 지정 (적 머리 위의 [번호]). 시계판의 내 행동 칸에 마우스를 올린 채 누르면 그 행동의 대상만 바꾼다
        if (Phase == BattlePhase.PlayerTurn && !IsBusy && !TutorialCoach.IsOpen)
        {
            for (int i = 0; i < 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                {
                    SelectTargetByNumber(i + 1);
                    break;
                }
            }
        }
    }

    /// <summary>번호(1부터)로 적 지정. 시계판에서 내 행동 칸을 가리키고 있으면 그 행동의 대상을 바꾼다.</summary>
    public void SelectTargetByNumber(int number)
    {
        Unit enemy = enemies.Find(e => e != null && e.slotNumber == number);
        if (enemy == null || !enemy.IsAlive) return;

        int hovered = ClockDial.HoveredHour;
        TimedAction placed = hovered > 0 ? playerActions.Find(a => a.Covers(hovered)) : null;
        if (placed != null)
        {
            placed.target = enemy;
            Log($"[{placed.card.data.cardName}] {placed.HourLabel()}의 대상 → [{number}] {enemy.unitName}");
            TimelineChanged();
            return;
        }

        FocusTarget = enemy;
        Log($"대상 지정: [{number}] {enemy.unitName} (이후 놓는 태엽판이 이 적을 노린다)");
        TimelineChanged();
    }

    // ─────────────────────────── 전투 시작 ───────────────────────────

    /// <summary>GameManager가 전투를 걸 때 호출.</summary>
    public void StartBattle(BattleStageData stageData, Unit player)
    {
        stage = stageData;
        playerUnit = player;
        battleMaxKi = 0;
        pendingKi = 0;
        round = 0;
        CurrentKi = 0;   // 첫 라운드가 시작될 때 채워진다
        TensionPoints = 0;
        pendingCard = null;
        enemyActions.Clear();
        playerActions.Clear();
        CurrentHour = 0;
        FocusTarget = null;
        IsBusy = false;
        Phase = BattlePhase.Ready;

        logLines.Clear();
        if (stageNameText != null) stageNameText.text = NextHideIdentity ? HiddenName : stage.stageName;

        // 시계판 UI (씬에 없으면 전투 HUD 안에 자동 생성) + 턴 종료 버튼은 "시계 진행" 버튼으로
        ClockDial.Ensure();
        DeckViewer.Ensure();   // 왼쪽 아래 [덱 보기] 버튼 (Tab)
        TensionGauge.Ensure(kiText);   // 기력 위의 장력 게이지
        KiGauge.Ensure(kiText);        // 기력: 숫자 대신 구슬
        if (endTurnButton != null)
        {
            Text label = endTurnButton.GetComponentInChildren<Text>();
            if (label != null) label.text = "시계 진행 ▶";
            EndTurnNudge.Ensure(endTurnButton);   // 낼 카드가 없으면 버튼 둘레가 빛나 재촉한다
            EnsureAutoButton();
        }

        SpawnEnemies();
        ApplyEnemyBoost();

        if (playerUnit != null)
        {
            playerUnit.CaptureHome();
            DressUnit(playerUnit);
            playerUnit.ClearStatus();   // 이전 무대의 녹·상태이상이 넘어오지 않게
        }

        // 부품: 전투가 시작될 때마다 발동하는 효과 (철갑 · 힘 · 녹은 첫 라운드에 들어온다)
        if (GameManager.Instance != null && playerUnit != null)
        {
            foreach (RunItem item in GameManager.Instance.OwnedItems)
            {
                if (item == null || item.onBattleStart == null) continue;
                item.onBattleStart(this);
                Log($"부품 [{item.name}]: {item.description.Replace("\n", " ")}");
            }
        }
        AttachStatusLabels();
        FaceOpponents();
        handManager.BuildDeck(GetPlayerDeck());

        if (StageLighting.Instance != null) StageLighting.Instance.SetEmotion(0);
        // 무대에 맞는 배경으로 전환 (변형은 보통 무작위). 지도 전투는 챕터가 고른 배경이 먼저
        if (NextBackdrop.HasValue) StageBackdrop.Show(NextBackdrop.Value);
        else StageBackdrop.Show(stage.backdrop, stage.backdropVariant);
        NextBackdrop = null;
        StageBackdrop.SetTempo(0);
        UpdateHUD();
        EnemyChatter.BattleStarted();   // 적들의 말풍선 (계획 단계에 이따금 한마디)

        StartCoroutine(BattleStartRoutine());
    }

    /// <summary>유닛(플레이어) 그림이 비어 있으면 자동 일러스트를 입힌다 (한 번만 — 이미 그림이 있으면 그대로)</summary>
    public static void DressUnit(Unit unit)
    {
        if (unit == null) return;
        SpriteCharacter sc = unit.GetComponent<SpriteCharacter>();
        if (sc == null || sc.sprite != null) return;

        Illustrator.CharacterArt gen = Illustrator.Character(unit.unitName, sc.tint);
        PoseSprites poses = sc.poses != null && !sc.poses.IsEmpty ? sc.poses : gen.poses;
        sc.Setup(gen.idle, sc.height, Color.white);
        sc.poses = poses;
        unit.RememberBodyColor();   // 피격 깜빡임 뒤 예전 실루엣 색으로 돌아가지 않게
    }

    private List<DeckEntry> GetPlayerDeck()
    {
        // 무대가 덱 오버라이드를 갖고 있으면 그것을, 아니면 보유 덱을 사용
        if (stage.deckOverride != null && stage.deckOverride.Count > 0)
        {
            List<DeckEntry> list = new List<DeckEntry>();
            foreach (CardData card in stage.deckOverride)
            {
                if (card != null) list.Add(new DeckEntry(card, false, 0));
            }
            return list;
        }
        return GameManager.Instance.OwnedDeck;
    }

    private IEnumerator BattleStartRoutine()
    {
        Log($"―― 무대 [{stage.stageName}] 시작 ――");
        yield return new WaitForSeconds(0.6f);
        StartPlayerTurn();
    }

    // ─────────────────────────── 라운드 흐름 (시계판) ───────────────────────────
    //
    // 한 라운드 = 시계판 한 바퀴 (1시 ~ 12시).
    //  1) 계획: 적의 행동이 시계판에 미리 보인다. 나는 태엽판을 원하는 시각에 놓는다.
    //  2) 진행: 시곗바늘이 1시부터 돈다. 시계판 한 칸 = 주사위 하나.
    //     - 같은 칸에 내 주사위와 적 주사위가 있으면 → 그 주사위끼리 합 (먼저 시작한 행동이 공격자)
    //     - 상대 주사위가 없는 칸의 공격 주사위 → 일방 공격 / 수비 주사위 → 아무 일 없음
    //  3) 12시가 지나면 다음 라운드.

    /// <summary>새 라운드의 계획 단계 시작</summary>
    private void StartPlayerTurn()
    {
        if (!playerUnit.IsAlive) { EndBattle(false); return; }
        if (CheckBattleEnd()) return;

        // 새 라운드: 지난 라운드의 힘/허약 등은 사라지고, "다음 턴 n"으로 대기 중이던 상태이상이 들어온다
        playerUnit.ActivatePendingStatus();
        foreach (Unit e in enemies)
        {
            if (e.IsAlive) e.ActivatePendingStatus();
        }

        // 과열: 라운드가 시작될 때 타오르고 절반으로 식는다 (이걸로 쓰러질 수도 있다)
        ApplyBurn();
        if (!playerUnit.IsAlive) { EndBattle(false); return; }
        if (CheckBattleEnd()) return;
        diceRolled.Clear();   // [연쇄]는 라운드마다 처음부터 센다

        round++;
        ApplyRoundStartTraits();

        Phase = BattlePhase.PlayerTurn;
        CurrentHour = 0;
        CurrentKi = MaxKi + pendingKi;   // 라운드마다 최대치까지 다시 찬다 (남은 기력은 넘어가지 않는다)
        pendingKi = 0;
        playerActions.Clear();
        handManager.RefillHand();
        RollEnemyActions();
        if (FocusTarget == null || !FocusTarget.IsAlive) FocusTarget = FirstAliveEnemy();
        RunItemHook(item => item.onRoundStart != null, item => item.onRoundStart(this));
        TimelineChanged();
        UpdateHUD();
        Log("―― 계획: 태엽판 → 시계판의 시각 클릭 = 배치 (같은 시각의 적 칸과 합) ――");
    }

    /// <summary>
    /// 적마다 이번 라운드에 쓸 태엽판과 시각을 정해서 시계판과 머리 위 상태창에 보여준다.
    /// 같은 적의 행동끼리는 겹치지 않는다 (다른 적과는 겹칠 수 있음).
    /// </summary>
    private void RollEnemyActions()
    {
        enemyActions.Clear();
        foreach (Unit e in enemies)
        {
            if (!e.IsAlive) continue;

            int count = Mathf.Max(1, e.actionsPerRound);
            if (e.behavior == EnemyBehavior.Berserker && e.Hp * 2 <= e.MaxHp)
            {
                count++;   // 광폭형: 체력이 절반 이하면 한 번 더 움직인다
                Log($"{e.unitName}이(가) 날뛴다! 이번 라운드 행동 +1");
                if (clashFx) StatusPopup(e, "광폭! 행동 +1", overwindColor);
            }

            for (int n = 0; n < count; n++)
            {
                CardInstance card = PickEnemyCard(e);
                if (card == null) break;

                int length = TimedAction.LengthOf(card.data);
                for (int attempt = 0; attempt < 20; attempt++)
                {
                    int hour = PickEnemyHour(e, length, attempt);
                    TimedAction a = new TimedAction { owner = e, card = card, hour = hour, length = length, target = playerUnit };
                    if (enemyActions.Exists(o => o.owner == e && o.Overlaps(a))) continue;
                    enemyActions.Add(a);
                    break;
                }
            }
        }
        enemyActions.Sort((a, b) => a.hour.CompareTo(b.hour));
        RefreshIntentLabels();
    }

    /// <summary>행동 유형에 따라 태엽판을 놓을 시작 시각을 고른다. 자리가 계속 겹치면(attempt가 커지면) 아무 시각.</summary>
    private int PickEnemyHour(Unit e, int length, int attempt)
    {
        int maxStart = Mathf.Max(1, ClockHours - length + 1);
        int anyHour = Random.Range(1, maxStart + 1);
        if (attempt >= 12) return anyHour;

        EnemyBehavior b = e.behavior;
        if (b == EnemyBehavior.Pendulum) b = round % 2 == 1 ? EnemyBehavior.Rusher : EnemyBehavior.Lurker;

        switch (b)
        {
            case EnemyBehavior.Rusher:
                return Random.Range(1, Mathf.Min(4, maxStart) + 1);

            case EnemyBehavior.Lurker:
                return Random.Range(Mathf.Max(1, maxStart - 3), maxStart + 1);

            case EnemyBehavior.Chimer:
                List<int> chimes = new List<int>();
                for (int h = 3; h <= maxStart; h += 3) chimes.Add(h);
                return chimes.Count > 0 ? chimes[Random.Range(0, chimes.Count)] : anyHour;

            default:
                return anyHour;
        }
    }

    /// <summary>이번에 쓸 태엽판 — 순서형은 덱을 위에서부터 차례대로, 나머지는 무작위.</summary>
    private CardInstance PickEnemyCard(Unit e)
    {
        if (e.behavior != EnemyBehavior.Pattern || e.personalDeck == null || e.personalDeck.Count == 0)
            return GetRandomEnemyCard(e);

        CardData data = e.personalDeck[e.patternStep % e.personalDeck.Count];
        e.patternStep++;
        return new CardInstance(data);
    }

    /// <summary>라운드가 시작될 때 걸리는 적 특성 (껍질 · 자가 수리 · 녹 기운 · 무리 · 감김)</summary>
    private void ApplyRoundStartTraits()
    {
        foreach (Unit e in enemies)
        {
            if (e == null || !e.IsAlive) continue;
            foreach (EnemyTraitEntry entry in new List<EnemyTraitEntry>(e.AllTraits()))
            {
            int n = entry.amount;
            string traitName = EnemyTypeInfo.Name(entry.trait);

            switch (entry.trait)
            {
                case EnemyTrait.Shell:
                    e.AddStatus(StatusType.Plating, n, false);
                    Log($"  [{traitName}] {e.unitName} 철갑 {n}");
                    if (clashFx) StatusPopup(e, $"철갑 +{n}", platingColor);
                    break;

                case EnemyTrait.Regen:
                    int healed = e.Heal(n);
                    if (healed <= 0) break;
                    Log($"  [{traitName}] {e.unitName} 체력 +{healed}");
                    if (clashFx) StatusPopup(e, $"+{healed}", healPopupColor);
                    break;

                case EnemyTrait.RustAura:
                    if (playerUnit == null || !playerUnit.IsAlive) break;
                    playerUnit.AddRust(n, false);
                    Log($"  [{traitName}] {e.unitName} → {playerUnit.unitName} 녹 {n}");
                    if (clashFx) StatusPopup(playerUnit, $"녹 +{n}", rustPopupColor);
                    break;

                case EnemyTrait.Pack:
                    if (!enemies.Exists(o => o != null && o != e && o.IsAlive)) break;
                    e.AddStatus(StatusType.Strength, n, false);
                    Log($"  [{traitName}] {e.unitName} 힘 {n}");
                    if (clashFx) StatusPopup(e, $"힘 +{n}", statusBuffColor);
                    break;

                case EnemyTrait.Winding:
                    int wound = Mathf.Min(EnemyTypeInfo.WindingCap, (round - 1) / EnemyTypeInfo.WindingInterval * n);
                    if (wound <= 0) break;
                    e.AddStatus(StatusType.Strength, wound, false);
                    Log($"  [{traitName}] {e.unitName} 힘 {wound}");
                    if (clashFx) StatusPopup(e, $"감김! 힘 +{wound}", statusBuffColor);
                    break;

                case EnemyTrait.Guardian:
                    Unit ward = null;
                    foreach (Unit o in enemies)
                        if (o != null && o != e && o.IsAlive && o.Hp < o.MaxHp && (ward == null || (float)o.Hp / o.MaxHp < (float)ward.Hp / ward.MaxHp)) ward = o;
                    if (ward == null) ward = e;
                    ward.AddStatus(StatusType.Plating, n, false);
                    Log($"  [{traitName}] {e.unitName} → {ward.unitName} 철갑 {n}");
                    if (clashFx) StatusPopup(ward, $"수호 철갑 +{n}", platingColor);
                    break;

                case EnemyTrait.Hex:
                    if (playerUnit == null || !playerUnit.IsAlive) break;
                    playerUnit.AddStatus(StatusType.Feeble, n, false);
                    Log($"  [{traitName}] {e.unitName} → {playerUnit.unitName} 허약 {n}");
                    if (clashFx) StatusPopup(playerUnit, $"저주 허약 +{n}", statusDebuffColor);
                    break;
            }
            }
        }
    }

    /// <summary>적 머리 위 상태창에 "다음에 발동할 행동"을 보여준다.</summary>
    private void RefreshIntentLabels()
    {
        foreach (Unit e in enemies)
        {
            UnitStatusLabel label = UnitStatusLabel.AttachTo(e);
            if (label == null) continue;

            TimedAction next = enemyActions.Find(a => a.owner == e && !a.resolved);
            if (next != null && e.IsAlive) label.SetIntent(next.HourLabel(), next.card.data);
            else label.ClearIntent();
        }
    }

    /// <summary>이 유닛이 이번 라운드에 시계판에 올린 행동 (시각 순). 정보창용.</summary>
    public List<TimedAction> GetActionsOf(Unit unit)
    {
        List<TimedAction> list = new List<TimedAction>();
        if (unit == null) return list;
        list.AddRange((unit.isPlayer ? playerActions : enemyActions).FindAll(a => a.owner == unit));
        list.Sort((a, b) => a.hour.CompareTo(b.hour));
        return list;
    }

    /// <summary>'시계 진행' 버튼 (기존 턴 종료 버튼)에 연결해서 사용.</summary>
    public void EndPlayerTurn()
    {
        if (Phase != BattlePhase.PlayerTurn || IsBusy) return;
        CancelPendingCard();
        ClockDial.Close();
        StartCoroutine(ClockRoutine());
    }

    /// <summary>
    /// 시곗바늘이 1시부터 12시까지 돈다. 시계판 한 칸 = 주사위 하나.
    /// 각 시각마다: 그 시각에 시작하는 행동의 [사용시] 발동 → 그 칸의 주사위끼리 맞붙음 → 끝나는 행동 정리.
    /// </summary>
    private IEnumerator ClockRoutine()
    {
        Phase = BattlePhase.EnemyTurn;
        IsBusy = true;
        UpdateHUD();
        Log("―― 시계 진행 ――");
        Sfx.Play(SfxId.ClockStart);
        nextDieBonus.Clear();
        destroyNextDie.Clear();
        cardPower.Clear();

        for (int h = 1; h <= ClockHours; h++)
        {
            CurrentHour = h;
            TimelineChanged();
            UpdateHUD();
            bool busy = enemyActions.Exists(a => a.Covers(h)) || playerActions.Exists(a => a.Covers(h));
            Sfx.Play(busy ? SfxId.Tock : SfxId.Tick, busy ? 0.9f : 0.6f, 0.02f);   // 초침 소리 (행동이 있는 시각은 묵직하게)
            if (h % 3 == 0) Sfx.Play(SfxId.Chime, 0.45f, 0f);                       // 정각의 종
            yield return new WaitForSeconds(Mathf.Max(hourTickTime, busy ? busyHourTime : emptyHourTime));

            // 1) 이 시각에 시작하는 행동: [사용시] 효과 + 머리 위 주사위 줄 표시 (내 흡수 태엽판은 침식 판정)
            List<TimedAction> starting = enemyActions.FindAll(a => a.hour == h);
            starting.AddRange(playerActions.FindAll(a => a.hour == h));
            foreach (TimedAction a in starting)
            {
                if (!a.owner.IsAlive) continue;
                if (a.owner.isPlayer && CheckCorruption(a.card)) { EndRound(); yield break; }

                Unit opponent = a.owner.isPlayer ? AliveTargetFor(a) : playerUnit;
                if (clashFx && opponent != null && SkillCutIn.IsSpecial(a.card.data))
                    yield return StartCoroutine(SpecialCardRoutine(a.owner, a.card.data));
                // 태엽판 고유 연출 (시작)
                if (clashFx && opponent != null && CardSignatures.Has(a.card.data))
                    yield return StartCoroutine(CardSignatures.OnStart(this, a.owner, opponent, a.card.data));
                if (opponent != null) TriggerCardUse(a.owner, opponent, a.card.data);
                if (clashFx && showDiceDisplay) displays[a] = ClashDiceDisplay.Show(a.owner, a.card.data);
            }

            // 2) 이 칸의 주사위끼리 맞붙는다
            yield return StartCoroutine(ResolveHourRoutine(h));
            if (CheckBattleEnd()) { EndRound(); yield break; }

            // 3) 이 시각에 끝나는 행동 정리 (쓴 태엽판은 버림패로)
            foreach (TimedAction a in AllActions())
            {
                if (a.EndHour == h) FinishAction(a);
            }

            // 다음 칸에 할 일이 없는 유닛은 제자리로 돌아간다 (이어서 싸우는 유닛은 그 자리에 남음)
            yield return StartCoroutine(ReturnIdleUnitsRoutine(h + 1));
            RefreshIntentLabels();
            TimelineChanged();
        }

        EndRound();
        yield return new WaitForSeconds(0.4f);
        StartPlayerTurn();
    }

    /// <summary>
    /// 한 시각(칸)의 주사위 대결.
    /// - 내 주사위는 그 칸의 적 주사위 하나와 합 (노리는 적의 것이 있으면 그것, 없으면 아무 적).
    /// - 합 상대가 없는 공격 주사위는 일방 공격, 상대가 없는 수비 주사위는 아무 일도 없다.
    /// </summary>
    private IEnumerator ResolveHourRoutine(int h)
    {
        TimedAction mine = playerUnit.IsAlive ? playerActions.Find(a => a.Covers(h)) : null;
        List<TimedAction> foes = enemyActions.FindAll(a => a.Covers(h) && a.owner.IsAlive);

        TimedAction partner = null;
        if (mine != null && foes.Count > 0)
        {
            Unit aim = AliveTargetFor(mine);
            partner = foes.Find(f => f.owner == aim);
            if (partner == null) partner = foes[0];
        }

        foreach (TimedAction foe in foes)
        {
            if (!foe.owner.IsAlive || !playerUnit.IsAlive) continue;

            if (foe == partner)
            {
                Log($"<color={UITheme.HexBrass}>{h}시, 합!</color> {foe.owner.unitName} ↔ {playerUnit.unitName}");
                // 먼저 시작한 행동이 공격자 (같은 시각에 시작했으면 적)
                if (foe.hour <= mine.hour) yield return StartCoroutine(ExchangeRoutine(foe, mine, h, null));
                else yield return StartCoroutine(ExchangeRoutine(mine, foe, h, null));
            }
            else if (foe.DieAt(h) != null && foe.DieAt(h).diceType == DiceType.Attack)
            {
                Log($"<color={UITheme.HexBrass}>{h}시</color> {foe.owner.unitName}의 일방 공격!");
                yield return StartCoroutine(ExchangeRoutine(foe, null, h, playerUnit));
            }
        }

        if (mine != null && partner == null && playerUnit.IsAlive && mine.DieAt(h) != null && mine.DieAt(h).diceType == DiceType.Attack)
        {
            Unit target = AliveTargetFor(mine);
            if (target != null)
            {
                Log($"<color={UITheme.HexBrass}>{h}시</color> {playerUnit.unitName}의 일방 공격!");
                yield return StartCoroutine(ExchangeRoutine(mine, null, h, target));
            }
        }
    }

    /// <summary>
    /// 주사위 한 쌍의 대결 (defAct가 null이면 loneTarget에게 일방 공격).
    /// 굴림 → 머리 위 주사위 연출 → 부딪힘 → 승패 적용 → 효과 발동.
    /// </summary>
    private IEnumerator ExchangeRoutine(TimedAction atkAct, TimedAction defAct, int hour, Unit loneTarget)
    {
        Unit attacker = atkAct.owner;
        Unit defender = defAct != null ? defAct.owner : loneTarget;
        if (defender == null || !attacker.IsAlive || !defender.IsAlive) yield break;

        DiceData atkDie = atkAct.DieAt(hour);
        DiceData defDie = defAct != null ? defAct.DieAt(hour) : null;

        // 연출: 카메라가 맞는 쪽에 살짝 다가가고, 두 캐릭터가 서로를 바라본다
        if (BattleCamera.Instance != null) BattleCamera.Instance.Focus(defender, attacker);
        attacker.FaceTowards(defender.transform.position);
        defender.FaceTowards(attacker.transform.position);

        // 연출: 서로에게 다가간다 (합 = 둘 다 가운데로, 일방 공격 = 공격자만 상대 앞으로)
        yield return StartCoroutine(ApproachRoutine(attacker, defender, defAct != null));

        // [상대의 다음 주사위 파괴]: 굴리기 전에 사라진다 (없는 주사위 = 자동 패배)
        atkDie = TryDestroyDie(attacker, atkDie);
        defDie = TryDestroyDie(defender, defDie);
        if (atkDie == null && defDie == null) yield break;

        if (atkDie != null && defDie != null && atkDie.diceType == DiceType.Defense && defDie.diceType == DiceType.Defense)
        {
            Log("  서로 막기만 하고 지나갔다.");
            yield break;
        }

        // 태엽판 고유 연출: 이 칸이 굴러가기 직전 (예: 열두 번의 타종 — 칸마다 종 3개)
        if (clashFx)
        {
            if (atkDie != null && CardSignatures.Has(atkAct.card.data))
                yield return StartCoroutine(CardSignatures.BeforeDie(this, attacker, defender, atkAct.card.data, hour - atkAct.hour, hour));
            if (defAct != null && defDie != null && CardSignatures.Has(defAct.card.data))
                yield return StartCoroutine(CardSignatures.BeforeDie(this, defender, attacker, defAct.card.data, hour - defAct.hour, hour));
        }

        displays.TryGetValue(atkAct, out ClashDiceDisplay atkDisplay);
        ClashDiceDisplay defDisplay = null;
        if (defAct != null) displays.TryGetValue(defAct, out defDisplay);
        int atkIndex = hour - atkAct.hour;
        int defIndex = defAct != null ? hour - defAct.hour : 0;

        // 주사위 굴림 (없는 쪽은 자동 패배). bonus = 상성·힘·허약 등으로 더해진 값 (머리 위 주사위에 표시)
        int atkBonus = 0, defBonus = 0;
        int atkRoll = atkDie != null ? ComputeRoll(attacker, defender, atkDie, defDie, hour, out atkBonus) : -1;
        int defRoll = defDie != null ? ComputeRoll(defender, attacker, defDie, atkDie, hour, out defBonus) : -1;

        // 장력: 서로의 칸이 실제로 맞부딪혔다
        if (atkDie != null && defDie != null) AddTension(1);

        string atkLabel = atkDie != null ? $"{DiceInfo.Name(atkDie.diceDetail)} {atkRoll}" : "-";
        string defLabel = defDie != null ? $"{DiceInfo.Name(defDie.diceDetail)} {defRoll}" : "-";
        Log($"{hour}시: {attacker.unitName}[{atkLabel}] vs {defender.unitName}[{defLabel}]");

        // 연출: 머리 위 주사위가 동시에 굴러가다 결과 값에 멈춘다
        Sfx.Play(SfxId.DiceRoll);
        Coroutine atkSpin = atkDisplay != null
            ? StartCoroutine(atkDisplay.RollRoutine(atkIndex, atkDie, atkRoll, atkBonus, diceRollTime)) : null;
        Coroutine defSpin = defDisplay != null
            ? StartCoroutine(defDisplay.RollRoutine(defIndex, defDie, defRoll, defBonus, diceRollTime)) : null;
        if (atkSpin != null) yield return atkSpin;
        if (defSpin != null) yield return defSpin;

        // 연출: 양쪽이 서로 부딪히고, 굴림 값이 머리 위에 뜬다
        if (clashFx)
            yield return StartCoroutine(ClashImpactRoutine(attacker, atkDie, atkRoll, atkBonus,
                                                          defender, defDie, defRoll, defBonus,
                                                          atkDie != null ? atkAct.card.data : null,
                                                          defDie != null && defAct != null ? defAct.card.data : null));

        // 연출: 이긴 주사위는 번쩍, 진 주사위는 흔들리며 흐려진다 (아래 결과 연출과 동시에)
        ShowDiceResult(atkDisplay, atkRoll, defRoll);
        ShowDiceResult(defDisplay, defRoll, atkRoll);

        if (atkRoll == defRoll)
        {
            // 동점은 양쪽 모두 무효
            Log("동점! 양쪽 칸 모두 무효.");
            if (clashFx) yield return StartCoroutine(TieFxRoutine(attacker, defender));
        }
        else if (atkRoll > defRoll)
        {
            bool hit = ApplyDie(attacker, defender, atkDie, atkRoll);
            ResolveDieEffects(attacker, atkDie, defender, defDie, hit);
            if (clashFx) yield return StartCoroutine(OutcomeFxRoutine(attacker, defender, atkDie, atkRoll, atkAct.card.data, atkIndex));
        }
        else
        {
            bool hit = ApplyDie(defender, attacker, defDie, defRoll);
            ResolveDieEffects(defender, defDie, attacker, atkDie, hit);
            if (clashFx) yield return StartCoroutine(OutcomeFxRoutine(defender, attacker, defDie, defRoll, defAct != null ? defAct.card.data : null, defIndex));
        }
    }

    // ─────────────────────────── 장력 / 회피 보상 ───────────────────────────

    private static readonly Color tensionColor = new Color(1f, 0.6f, 0.3f);

    /// <summary>장력을 올린다. 단계가 오르면 알림.</summary>
    private void AddTension(int amount)
    {
        int max = Mathf.Max(1, tensionPerStage) * MaxTensionStage;
        if (amount <= 0 || TensionPoints >= max) return;

        int before = TensionStage;
        TensionPoints = Mathf.Min(max, TensionPoints + amount);
        if (TensionStage > before)
        {
            Log($"<color={UITheme.HexTension}>장력 {TensionStage}단계!</color> 삼킬 수 있는 기억 +{TensionStage}");
            TensionGauge.Pulse();
            if (StageLighting.Instance != null) StageLighting.Instance.SetEmotion(TensionStage);   // 장력이 오를수록 무대 조명이 달아오른다
            if (clashFx && playerUnit != null) StatusPopup(playerUnit, $"장력 {TensionStage}단계!", tensionColor);
        }
        UpdateHUD();
    }

    /// <summary>부품 등 전투 밖의 효과가 장력을 올려 줄 때 쓴다</summary>
    public void GrantTension(int amount) { AddTension(amount); }

    /// <summary>
    /// 시계판의 아침 · 밤: 1~6시 = 아침, 7~12시 = 밤 (한 라운드 안에서 아침이 지나고 밤이 온다).
    /// 태엽판의 [아침] · [밤] 칸과 아침 · 밤 부품이 굴리는 칸의 시각으로 이 값을 본다.
    /// </summary>
    public static bool IsNightHour(int hour) { return hour >= 7; }

    public static string TimeOfHour(int hour) { return IsNightHour(hour) ? "밤" : "아침"; }

    /// <summary>이 태엽판에 필요한 장력이 지금 쌓여 있는가</summary>
    public bool HasTensionFor(CardData card)
    {
        return card == null || card.requireTension <= TensionStage;
    }

    // ─────────────────────────── 합 이동 연출 ───────────────────────────

    [Header("합 이동 연출")]
    [Tooltip("합을 할 때 두 캐릭터 사이에 남기는 거리")]
    public float clashGap = 1.6f;
    [Tooltip("일방 공격 때 공격자가 상대 앞에 서는 거리")]
    public float strikeGap = 1.3f;
    [Tooltip("다가가는 / 돌아가는 시간(초)")]
    public float approachTime = 0.22f;

    /// <summary>
    /// 합: 두 캐릭터가 각자의 자리 사이 가운데로 걸어 나와 clashGap만큼 떨어져 선다.
    /// 일방 공격: 공격자만 상대의 지금 위치 앞까지 다가간다.
    /// 이미 그 자리에 있으면 움직이지 않는다 (같은 상대와 연달아 싸울 때).
    /// </summary>
    private IEnumerator ApproachRoutine(Unit attacker, Unit defender, bool clash)
    {
        Vector3 aHome = attacker.HomePosition, dHome = defender.HomePosition;
        Vector3 dir = dHome - aHome;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) yield break;
        dir.Normalize();

        Coroutine aMove = null, dMove = null;
        if (clash)
        {
            Vector3 mid = (aHome + dHome) * 0.5f;
            Vector3 aSpot = mid - dir * clashGap * 0.5f;
            Vector3 dSpot = mid + dir * clashGap * 0.5f;
            aSpot.y = attacker.transform.position.y;
            dSpot.y = defender.transform.position.y;
            aMove = MoveIfFar(attacker, aSpot);
            dMove = MoveIfFar(defender, dSpot);
        }
        else
        {
            Vector3 toward = defender.transform.position - attacker.transform.position;
            toward.y = 0f;
            Vector3 spot = defender.transform.position - (toward.sqrMagnitude > 0.0001f ? toward.normalized : dir) * strikeGap;
            spot.y = attacker.transform.position.y;
            aMove = MoveIfFar(attacker, spot);
        }
        if (aMove != null) yield return aMove;
        if (dMove != null) yield return dMove;
    }

    private Coroutine MoveIfFar(Unit unit, Vector3 spot)
    {
        if ((unit.transform.position - spot).sqrMagnitude < 0.01f) return null;
        return StartCoroutine(unit.MoveToRoutine(spot, approachTime));
    }

    /// <summary>nextHour 칸에 주사위가 없는 유닛(살아 있는)은 제자리로 돌아간다. 모두 동시에.</summary>
    private IEnumerator ReturnIdleUnitsRoutine(int nextHour)
    {
        List<Coroutine> moves = new List<Coroutine>();
        List<Unit> units = new List<Unit>(enemies);
        units.Add(playerUnit);

        foreach (Unit u in units)
        {
            if (u == null || !u.IsAlive) continue;
            bool busyNext = nextHour <= ClockHours &&
                            (u.isPlayer ? playerActions : enemyActions).Exists(a => a.owner == u && a.Covers(nextHour));
            if (busyNext) continue;

            Vector3 home = u.HomePosition;
            home.y = u.transform.position.y;
            Coroutine c = MoveIfFar(u, home);
            if (c != null) moves.Add(c);
        }
        foreach (Coroutine c in moves) yield return c;

        // 제자리로 돌아오면 다시 상대 진영을 바라본다
        if (moves.Count > 0) FaceOpponents();
    }

    private IEnumerable<TimedAction> AllActions()
    {
        foreach (TimedAction a in enemyActions) yield return a;
        foreach (TimedAction a in playerActions) yield return a;
    }

    /// <summary>행동이 끝남: 머리 위 주사위 줄을 걷고, 내 태엽판은 버림패로.</summary>
    private void FinishAction(TimedAction a)
    {
        if (a.resolved) return;
        a.resolved = true;

        if (displays.TryGetValue(a, out ClashDiceDisplay display))
        {
            if (display != null) display.Hide();
            displays.Remove(a);
        }
        if (a.owner.isPlayer && handManager.DiscardReserved(a.card))
            Log($"  [{a.card.data.cardName}] 소멸. 이번 전투에선 다시 안 나온다");
    }

    /// <summary>라운드 정리 (12시가 지났거나 전투가 끝남)</summary>
    private void EndRound()
    {
        CardSignatures.ClearAll();   // 고유 연출 소품 정리
        // 싸우다 멈춘 자리에 남지 않게 살아 있는 유닛은 제자리로 (다음 무대에서 이 위치가 기준점이 되므로)
        List<Unit> units = new List<Unit>(enemies);
        units.Add(playerUnit);
        foreach (Unit u in units)
        {
            if (u == null || !u.IsAlive) continue;
            Vector3 home = u.HomePosition;
            home.y = u.transform.position.y;
            u.transform.position = home;
        }

        foreach (TimedAction a in AllActions()) FinishAction(a);
        displays.Clear();
        enemyActions.Clear();
        playerActions.Clear();
        nextDieBonus.Clear();
        destroyNextDie.Clear();
        cardPower.Clear();
        CurrentHour = 0;
        IsBusy = false;
        if (BattleCamera.Instance != null) BattleCamera.Instance.Release();
        TimelineChanged();
    }

    private Unit AliveTargetFor(TimedAction action)
    {
        if (action.target != null && action.target.IsAlive) return action.target;
        if (FocusTarget != null && FocusTarget.IsAlive) return FocusTarget;
        return FirstAliveEnemy();
    }

    private Unit FirstAliveEnemy()
    {
        return enemies.Find(e => e != null && e.IsAlive);
    }

    // ─────────────────────────── 태엽판 배치 ───────────────────────────

    /// <summary>손패 카드 클릭 (HandManager가 연결해줌). 선택 상태로 만들고 시각/적 클릭을 기다린다.</summary>
    public void OnCardClicked(CardView view)
    {
        if (Phase != BattlePhase.PlayerTurn || IsBusy) return;

        CardInstance card = view.BoundCard;
        if (card == null || card.data == null) return;

        if (!HasTensionFor(card.data)) { Log($"장력이 부족하다! (장력 {card.data.requireTension}단계 필요)"); return; }
        if (card.data.cost > CurrentKi) { Log("기력이 부족하다!"); return; }

        if (pendingCard == card) { CancelPendingCard(); return; }   // 다시 클릭 = 선택 취소

        if (card.data.instant) { CancelPendingCard(); UseInstant(card); return; }

        CancelPendingCard();
        pendingCard = card;
        view.SetSelected(true);
        ClockDial.Open();   // 고른 태엽판을 놓을 시계판을 화면 가운데로
        TimelineChanged();
        Log($"[{card.data.cardName}] ({TimedAction.LengthOf(card.data)}칸) 시계판의 시각을 클릭하세요. (다시 클릭하면 취소)");
    }

    /// <summary>
    /// 유닛 클릭 (Unit이 호출).
    /// 적 클릭: 일방 공격 대상으로 지정. 태엽판을 고른 상태면 그 적의 다음 행동에 맞부딪히는 시각에 자동 배치.
    /// </summary>
    public void OnUnitClicked(Unit unit)
    {
        if (Phase != BattlePhase.PlayerTurn || IsBusy) return;
        if (unit == null || !unit.IsAlive || unit.isPlayer) return;

        FocusTarget = unit;
        TimelineChanged();

        if (pendingCard == null)
        {
            Log($"대상 지정: [{unit.slotNumber}] {unit.unitName} (이후 놓는 태엽판이 이 적을 노린다)");
            return;
        }

        int length = TimedAction.LengthOf(pendingCard.data);
        int hour = FindHourAgainst(unit, length);
        if (hour < 0) { Log("시계판에 놓을 자리가 없다!"); return; }
        PlaceCard(pendingCard, hour, unit);
    }

    /// <summary>시계판의 시각 칸 클릭 (ClockDial이 호출). 태엽판을 골랐으면 배치, 아니면 그 칸의 내 행동을 회수.</summary>
    public void OnHourClicked(int hour)
    {
        if (Phase != BattlePhase.PlayerTurn || IsBusy) return;

        if (pendingCard != null)
        {
            int length = TimedAction.LengthOf(pendingCard.data);
            if (!CanPlace(hour, length))
            {
                Log(hour + length - 1 > ClockHours
                    ? $"{hour}시부터는 {length}칸이 들어가지 않는다 (12시를 넘김)."
                    : "이미 내 행동이 있는 칸과 겹친다.");
                return;
            }
            PlaceCard(pendingCard, hour, null);
            return;
        }

        TimedAction placed = playerActions.Find(a => a.Covers(hour));
        if (placed != null) Unplace(placed);
    }

    /// <summary>hour부터 length칸에 내 행동을 놓을 수 있는가 (12시를 넘지 않고, 내 다른 행동과 겹치지 않음)</summary>
    public bool CanPlace(int hour, int length)
    {
        if (hour < 1 || hour + length - 1 > ClockHours) return false;
        TimedAction probe = new TimedAction { hour = hour, length = length };
        return !playerActions.Exists(a => a.Overlaps(probe));
    }

    /// <summary>
    /// 그 적의 다음 행동과 가장 많은 칸이 겹치는 빈 시각 (같으면 그 행동과 시작이 가까운 쪽).
    /// 겹칠 수 있는 자리가 없으면 아무 빈 시각, 그것도 없으면 -1.
    /// </summary>
    private int FindHourAgainst(Unit enemy, int length)
    {
        TimedAction next = enemyActions.Find(a => a.owner == enemy && !a.resolved);
        int best = -1, bestOverlap = 0, bestDistance = int.MaxValue;
        for (int h = 1; h + length - 1 <= ClockHours; h++)
        {
            if (!CanPlace(h, length)) continue;
            if (best < 0) best = h;   // 겹칠 자리가 없을 때의 대비: 가장 이른 빈 시각
            if (next == null) break;

            TimedAction probe = new TimedAction { hour = h, length = length };
            int overlap = 0;
            for (int t = h; t <= probe.EndHour; t++) if (next.Covers(t)) overlap++;
            int distance = Mathf.Abs(h - next.hour);
            if (overlap > bestOverlap || (overlap == bestOverlap && overlap > 0 && distance < bestDistance))
            {
                best = h;
                bestOverlap = overlap;
                bestDistance = distance;
            }
        }
        return best;
    }

    private void PlaceCard(CardInstance card, int hour, Unit target)
    {
        CancelPendingCard();
        ClockDial.Close();   // 놓았으면 시계는 카드 뒤로 (다음 태엽판 고르기)

        CurrentKi -= card.data.cost;
        handManager.ReserveCard(card);
        Sfx.Play(SfxId.CardPlace);

        TimedAction action = new TimedAction
        {
            owner = playerUnit, card = card, hour = hour,
            length = TimedAction.LengthOf(card.data),
            target = target != null ? target : FocusTarget,
        };
        playerActions.Add(action);

        // 칸마다 적 주사위가 있으면 합, 없으면 (공격 주사위라면) 일방 공격
        int clashes = 0;
        for (int t = action.hour; t <= action.EndHour; t++)
        {
            if (enemyActions.Exists(e => e.Covers(t) && e.owner.IsAlive)) clashes++;
        }
        Log(clashes > 0
            ? $"[{card.data.cardName}] → {action.HourLabel()} 배치. {action.length}칸 중 {clashes}칸에서 적 칸과 합!"
            : $"[{card.data.cardName}] → {action.HourLabel()} 배치. 겹치는 적 칸이 없어 일방 공격.");

        TimelineChanged();
        UpdateHUD();
    }

    /// <summary>배치한 행동 회수: 기력 반환, 손패로 되돌림</summary>
    private void Unplace(TimedAction action)
    {
        playerActions.Remove(action);
        CurrentKi += action.card.data.cost;
        handManager.ReturnToHand(action.card);
        Sfx.Play(SfxId.CardReturn);
        Log($"회수: [{action.card.data.cardName}] (기력 {action.card.data.cost} 반환)");
        TimelineChanged();
        UpdateHUD();
    }

    // ─────────────────────────── 즉발 태엽판 ───────────────────────────

    /// <summary>
    /// 즉발: 시계판에 놓지 않고 바로 쓴다 — 기력을 내고, 효과를 모두 발동하고, 버림패로 (소멸이면 사라진다).
    /// 대상은 지정해 둔 적 (없으면 첫 번째로 살아 있는 적). 기력 회복은 다음 라운드가 아니라 지금 바로 찬다.
    /// </summary>
    private void UseInstant(CardInstance card)
    {
        CardData d = card.data;
        Unit target = FocusTarget != null && FocusTarget.IsAlive ? FocusTarget : FirstAliveEnemy();
        CurrentKi -= d.cost;
        handManager.UseCard(card);
        Log($"<color=#ffe08a>⚡ 즉발 [{d.cardName}]</color>");
        Sfx.Play(SfxId.CardPlace);
        if (clashFx && playerUnit != null)
        {
            Vector3 chest = playerUnit.transform.position + Vector3.up * 1f;
            Color flash = new Color(1f, 0.88f, 0.5f);
            ImpactFX.Guard(chest, flash);
            StatusPopup(playerUnit, $"⚡ {d.cardName}", flash);
        }

        if (d.instantEffects != null)
        {
            foreach (DiceEffect fx in d.instantEffects)
            {
                if (fx == null) continue;
                if (fx.effect == DiceEffectType.RecoverKi)
                {
                    CurrentKi += fx.amount;   // 즉발은 지금 바로 찬다
                    Log($"  기력 +{fx.amount}");
                    if (clashFx) StatusPopup(playerUnit, $"기력 +{fx.amount}", new Color(0.55f, 0.85f, 1f));
                    continue;
                }
                ApplyDiceEffect(playerUnit, target, fx);
            }
        }
        RunItemHook(item => item.onInstantUsed != null, item => item.onInstantUsed(this, d));

        TimelineChanged();
        UpdateHUD();
        CheckBattleEnd();   // 즉발 피해로 마지막 적이 쓰러졌을 수도 있다
    }

    /// <summary>가진 부품 중 조건에 맞는 것의 효과를 차례로 (부품 하나가 틀려도 나머지는 돈다)</summary>
    private void RunItemHook(System.Predicate<RunItem> has, System.Action<RunItem> run)
    {
        if (GameManager.Instance == null || playerUnit == null) return;
        foreach (RunItem item in GameManager.Instance.OwnedItems)
        {
            if (item == null || !has(item)) continue;
            try { run(item); }
            catch (System.Exception e) { Debug.LogWarning($"부품 [{item.name}] 오류: {e.Message}"); }
        }
    }

    /// <summary>부품이 쓰는 기력 조작: 지금 기력 +n</summary>
    public void GainKiNow(int amount) { CurrentKi += amount; UpdateHUD(); }
    /// <summary>부품이 쓰는 기력 조작: 다음 라운드 기력 +n</summary>
    public void GainKiNextRound(int amount) { pendingKi += amount; UpdateHUD(); }
    /// <summary>부품이 쓰는 손패 조작</summary>
    public int DrawCards(int count) { return handManager.DrawCards(count); }
    /// <summary>부품이 쓰는 추가 피해 (취약 · 철갑 적용). 실제 피해를 돌려준다</summary>
    public int ItemDamage(Unit target, int amount, string label)
    {
        int dealt = DealExtraDamage(target, amount, label, overwindColor);
        if (dealt > 0) CheckBattleEnd();
        return dealt;
    }
    /// <summary>이번 전투의 라운드 번호 (1부터)</summary>
    public int Round { get { return round; } }
    /// <summary>부품이 쓰는 표시: 머리 위 작은 글자 + 전투 기록</summary>
    public void ItemPopup(Unit unit, string text, string log)
    {
        if (!string.IsNullOrEmpty(log)) Log(log);
        if (clashFx && unit != null) StatusPopup(unit, text, statusBuffColor);
    }

    // ─────────────────────────── 자동 배치 ───────────────────────────

    private Button autoButton;

    /// <summary>[시계 진행] 버튼 바로 위에 [자동 배치 (A)] 버튼을 붙인다 (한 번만)</summary>
    private void EnsureAutoButton()
    {
        if (autoButton != null || endTurnButton == null) return;
        RectTransform end = (RectTransform)endTurnButton.transform;
        autoButton = UIBuilder.Button("AutoPlace", end.parent, "자동 배치 (A)", UITheme.InkSolid, 22);
        UITheme.StyleButton(autoButton, false, 22);
        Vector2 size = new Vector2(end.rect.width > 1f ? end.rect.width : end.sizeDelta.x, 52f);
        UIBuilder.Place((RectTransform)autoButton.transform, end.anchorMin, end.anchorMax, end.pivot,
            end.anchoredPosition + new Vector2(0f, (end.rect.height > 1f ? end.rect.height : end.sizeDelta.y) * (1f - end.pivot.y) + size.y * end.pivot.y + 12f), size);
        autoButton.onClick.AddListener(AutoPlace);
    }

    /// <summary>
    /// 손패를 남은 기력 안에서 알아서 시계판에 놓는다 (이미 놓은 것은 그대로 두고 빈 자리만 채운다).
    /// 비싼 태엽판부터, 칸마다 점수를 매겨 가장 좋은 시각에:
    ///   적 칸과 겹치는 칸 +10 (합으로 막는다) · [아침]/[밤] 효과가 맞는 시간대 +3 · [빈틈]이 빈 칸 +3
    /// 대상은 겹친 적 행동의 주인 (없으면 지금 지정한 적).
    /// </summary>
    public void AutoPlace()
    {
        if (Phase != BattlePhase.PlayerTurn || IsBusy) return;
        CancelPendingCard();

        List<CardInstance> candidates = new List<CardInstance>(handManager.Hand);
        candidates.RemoveAll(c => c == null || c.data == null || c.data.instant);
        candidates.Sort((a, b) => b.data.cost != a.data.cost
            ? b.data.cost.CompareTo(a.data.cost)
            : TimedAction.LengthOf(b.data).CompareTo(TimedAction.LengthOf(a.data)));

        int placed = 0;
        foreach (CardInstance card in candidates)
        {
            if (card.data.cost > CurrentKi || !HasTensionFor(card.data)) continue;
            int length = TimedAction.LengthOf(card.data);
            int bestHour = -1, bestScore = int.MinValue;
            Unit bestTarget = null;
            for (int h = 1; h + length - 1 <= ClockHours; h++)
            {
                if (!CanPlace(h, length)) continue;
                int score = 0;
                Unit target = null;
                for (int t = h; t < h + length; t++)
                {
                    TimedAction foe = enemyActions.Find(e => e.Covers(t) && e.owner != null && e.owner.IsAlive && !e.resolved);
                    if (foe != null) { score += 10; if (target == null) target = foe.owner; }
                    DiceData die = card.data.diceList != null && t - h < card.data.diceList.Count ? card.data.diceList[t - h] : null;
                    if (die == null || die.effects == null) continue;
                    foreach (DiceEffect fx in die.effects)
                    {
                        if (fx == null) continue;
                        if (fx.effect == DiceEffectType.Dawn && !IsNightHour(t)) score += 3;
                        else if (fx.effect == DiceEffectType.Dusk && IsNightHour(t)) score += 3;
                        else if (fx.effect == DiceEffectType.Unopposed && foe == null) score += 3;
                    }
                }
                if (score > bestScore) { bestScore = score; bestHour = h; bestTarget = target; }
            }
            if (bestHour < 0) continue;
            PlaceCard(card, bestHour, bestTarget);
            placed++;
        }

        Log(placed > 0 ? $"자동 배치: 태엽판 {placed}장을 놓았다. (칸을 누르면 회수)" : "자동 배치: 더 놓을 수 있는 태엽판이 없다.");
    }

    /// <summary>지금 고른(배치 대기 중인) 태엽판. 없으면 null. 시계판 미리보기용.</summary>
    public CardData PendingCard { get { return pendingCard != null ? pendingCard.data : null; } }

    public IReadOnlyList<TimedAction> PlayerActions { get { return playerActions; } }
    public IReadOnlyList<TimedAction> EnemyActions { get { return enemyActions; } }

    /// <summary>시계판 내용이 바뀔 때마다 1씩 오른다 (ClockDial이 다시 그릴지 판단)</summary>
    public int TimelineVersion { get; private set; }

    private void TimelineChanged() { TimelineVersion++; }

    /// <summary>흡수 카드(◈)의 침식 판정. 침식으로 전투가 끝났으면 true.</summary>
    private bool CheckCorruption(CardInstance card)
    {
        if (card.entry == null || !card.entry.absorbed || !playerUnit.IsAlive) return false;

        float chance = GameManager.Instance.CorruptionChance(card.entry.corruption);
        if (Random.value >= chance) return false;

        int damage = corruptionBaseDamage + Mathf.Max(0, card.entry.corruption - 1);
        Log($"침식! [{card.data.cardName}]에 남은 원한이 새어 나온다.");
        playerUnit.TakeDamage(damage);
        Sfx.Play(SfxId.Corrupt);
        return CheckBattleEnd();
    }

    // ─────────────────────────── 합(주사위 대결) ───────────────────────────

    private void ShowDiceResult(ClashDiceDisplay display, int myRoll, int theirRoll)
    {
        if (display == null) return;

        ClashDiceDisplay.Result result = myRoll == theirRoll ? ClashDiceDisplay.Result.Tie
                                       : myRoll > theirRoll ? ClashDiceDisplay.Result.Win
                                       : ClashDiceDisplay.Result.Lose;
        StartCoroutine(display.ResultRoutine(result));
    }

    private int RollBase(Unit self, DiceData die)
    {
        // 녹: 최댓값이 깎인다. int 오버로드는 상한 미포함이므로 +1 (최댓값도 나오도록)
        int max = self.RustedMax(die);
        if (max < die.maxRoll) Log($"  → {self.unitName}의 칸이 녹슬었다! 최댓값 {die.maxRoll} → {max}");
        self.WearRust();   // 굴릴 때마다 녹이 1 벗겨진다
        return Random.Range(die.minRoll, max + 1);
    }

    private readonly Dictionary<Unit, int> diceRolled = new Dictionary<Unit, int>();   // 이번 라운드에 굴린 칸 수 ([연쇄]용)
    private static readonly Color burnColor = new Color(1f, 0.5f, 0.2f);

    /// <summary>과열이 붙은 유닛들이 타오른다 (철갑 · 취약과 무관한 고정 피해) → 과열은 절반으로</summary>
    private void ApplyBurn()
    {
        List<Unit> units = new List<Unit> { playerUnit };
        units.AddRange(enemies);
        foreach (Unit u in units)
        {
            if (u == null || !u.IsAlive || u.Burn <= 0) continue;
            int heat = u.Burn;
            u.TakeDamage(heat);
            u.CoolBurn();
            Log($"  과열! {u.unitName}이(가) 타오른다. 피해 {heat} (남은 과열 {u.Burn})");
            if (clashFx)
            {
                DamagePopup.Spawn(HeadPosition(u) + Vector3.up * 0.4f, $"과열 {heat}", burnColor, 1.1f);
                ClashSpark.Burst(u.transform.position + Vector3.up * 0.8f, burnColor, 16, 3.5f);
            }
        }
    }

    /// <summary>
    /// 주사위를 굴리고 보정을 적용한다. bonus = 기본 굴림에 더해진 총합 (음수 가능).
    /// 순서: 녹(최댓값 감소) → 마비(최솟값 고정) → 힘/허약/인내 → [다음 주사위 위력 +n] → 상성/회피 보정
    /// </summary>
    private int ComputeRoll(Unit self, Unit foe, DiceData mine, DiceData theirs, int hour, out int bonus)
    {
        bonus = 0;
        bool zeroOut = false;   // [모 아니면 도]의 "도"
        int roll = RollBase(self, mine);

        if (self.ConsumeParalysis())
        {
            roll = mine.minRoll;
            Log($"  → {self.unitName}은(는) 마비되어 칸이 최솟값({roll})으로 굴러간다!");
        }

        bonus += self.powerBonus;   // 무한 모드 회차 강화
        if (mine.diceType == DiceType.Attack)
            bonus += self.GetStatus(StatusType.Strength) - self.GetStatus(StatusType.Feeble);
        else
            bonus += self.GetStatus(StatusType.Endurance);


        if (nextDieBonus.TryGetValue(self, out int power) && power > 0)
        {
            bonus += power;
            nextDieBonus.Remove(self);
        }

        // [과감기]: 체력을 태워 감아 둔 위력
        if (cardPower.TryGetValue((self, mine), out int wound) && wound > 0) bonus += wound;

        // 부품: 굴리는 시각(아침 1~6시 · 밤 7~12시) · 칸 종류에 따라 내 칸 위력이 오른다
        if (self.isPlayer && GameManager.Instance != null)
        {
            foreach (RunItem item in GameManager.Instance.OwnedItems)
            {
                if (item == null || item.rollBonus == null) continue;
                int add = item.rollBonus(hour, mine);
                if (add == 0) continue;   // 깎는 부품(묵직한 추)도 있다
                bonus += add;
                Log($"  → 부품 [{item.name}]: {hour}시 위력 {(add > 0 ? "+" : "")}{add}");
                if (clashFx) StatusPopup(self, $"{item.name} {(add > 0 ? "+" : "")}{add}", onTheHourColor);
            }
        }

        // [정각]: 3·6·9·12시 칸에서 굴리면 위력이 오른다
        if (hour % 3 == 0 && mine.effects != null)
        {
            foreach (DiceEffect fx in mine.effects)
            {
                if (fx == null || fx.effect != DiceEffectType.OnTheHour) continue;
                bonus += fx.amount;
                Log($"  → 정각! {hour}시 종이 울린다! 위력 +{fx.amount}");
                if (clashFx) StatusPopup(self, $"정각 +{fx.amount}", onTheHourColor);
            }
        }

        // 상황으로 위력이 정해지는 효과: [마무리] · [배수진] · [연쇄] · [빈틈]
        diceRolled.TryGetValue(self, out int rolledBefore);
        if (mine.effects != null)
        {
            foreach (DiceEffect fx in mine.effects)
            {
                if (fx == null || fx.amount <= 0) continue;
                int gain = 0;
                string why = null;
                switch (fx.effect)
                {
                    case DiceEffectType.Execute:
                        if (foe != null && foe.IsAlive && foe.Hp * 2 <= foe.MaxHp) { gain = fx.amount; why = "마무리"; }
                        break;
                    case DiceEffectType.LastStand:
                        if (self.Hp * 2 <= self.MaxHp) { gain = fx.amount; why = "배수진"; }
                        break;
                    case DiceEffectType.Combo:
                        gain = Mathf.Min(fx.amount, rolledBefore);
                        why = "연쇄";
                        break;
                    case DiceEffectType.Unopposed:
                        if (theirs == null) { gain = fx.amount; why = "빈틈"; }
                        break;
                    case DiceEffectType.Dawn:
                        if (!IsNightHour(hour)) { gain = fx.amount; why = "아침"; }
                        break;
                    case DiceEffectType.Dusk:
                        if (IsNightHour(hour)) { gain = fx.amount; why = "밤"; }
                        break;
                    case DiceEffectType.Jackpot:
                        if (roll >= mine.maxRoll) { gain = fx.amount; why = "대박"; }
                        break;
                    case DiceEffectType.Gamble:
                        if (Random.value < 0.5f) { gain = fx.amount; why = "도박 성공"; }
                        else
                        {
                            bonus -= fx.amount;
                            Log($"  → 도박 실패! {self.unitName}의 위력 -{fx.amount}");
                            if (clashFx) StatusPopup(self, $"도박 실패 -{fx.amount}", statusDebuffColor);
                        }
                        break;
                    case DiceEffectType.DoubleOrNothing:
                        if (Random.value < 0.5f) { gain = roll; why = "모! 두 배"; }
                        else
                        {
                            zeroOut = true;   // 최종 위력 0
                            Log($"  → 도! {self.unitName}의 칸이 0으로 굴러간다");
                            if (clashFx) StatusPopup(self, "도! 0", statusDebuffColor);
                        }
                        break;
                }
                if (gain <= 0) continue;
                bonus += gain;
                Log($"  → {why}! {self.unitName}의 위력 +{gain}");
                if (clashFx) StatusPopup(self, $"{why} +{gain}", onTheHourColor);
            }
        }
        diceRolled[self] = rolledBefore + 1;

        // 적 특성 [정시]: 3·6·9·12시 칸에서 굴리면 위력이 오른다
        int punctual = self.TraitN(EnemyTrait.Punctual);
        if (hour % 3 == 0 && punctual > 0)
        {
            bonus += punctual;
            Log($"  → {self.unitName}의 정시! {hour}시, 위력 +{punctual}");
            if (clashFx) StatusPopup(self, $"정시 +{punctual}", onTheHourColor);
        }
        // [야행]: 밤 칸 / [분노]: 체력 절반 이하 / [매복]: 맞서는 칸이 없을 때
        int nocturnal = self.TraitN(EnemyTrait.Nocturnal);
        if (nocturnal > 0 && IsNightHour(hour))
        {
            bonus += nocturnal;
            Log($"  → {self.unitName}의 야행! {hour}시, 위력 +{nocturnal}");
            if (clashFx) StatusPopup(self, $"야행 +{nocturnal}", onTheHourColor);
        }
        int enrage = self.TraitN(EnemyTrait.Enrage);
        if (enrage > 0 && self.Hp * 2 <= self.MaxHp)
        {
            bonus += enrage;
            Log($"  → {self.unitName}의 분노! 위력 +{enrage}");
            if (clashFx) StatusPopup(self, $"분노 +{enrage}", overwindColor);
        }
        int ambush = self.TraitN(EnemyTrait.Ambush);
        if (ambush > 0 && theirs == null)
        {
            bonus += ambush;
            Log($"  → {self.unitName}의 매복! 위력 +{ambush}");
            if (clashFx) StatusPopup(self, $"매복 +{ambush}", onTheHourColor);
        }

        if (theirs != null)
        {
            // 공격 대 공격: 상성 순환(참격>관통>타격>참격) 우위 보정
            if (mine.diceType == DiceType.Attack && theirs.diceType == DiceType.Attack &&
                advantageOver.TryGetValue(mine.diceDetail, out DiceDetail beaten) &&
                theirs.diceDetail == beaten)
            {
                bonus += typeAdvantageBonus;
                Log($"  → {DiceInfo.Name(mine.diceDetail)}은(는) {DiceInfo.Name(theirs.diceDetail)}에 강하다! 굴림 +{typeAdvantageBonus}");
            }

            // 회피: 상대 공격 주사위를 상대로는 굴림 보정
            if (mine.diceDetail == DiceDetail.Evade && theirs.diceType == DiceType.Attack)
            {
                bonus += typeAdvantageBonus;
                Log($"  → 회피가 공격을 겨냥한다! 굴림 +{typeAdvantageBonus}");
            }
        }

        if (zeroOut) { bonus = -roll; return 0; }
        return Mathf.Max(roll + bonus, 0);
    }

    /// <summary>파괴 예약이 걸린 유닛이면 이번 주사위를 없앤다 (null 반환).</summary>
    private DiceData TryDestroyDie(Unit owner, DiceData die)
    {
        if (die == null || !destroyNextDie.Remove(owner)) return die;

        Log($"  → {owner.unitName}의 {DiceInfo.Name(die.diceDetail)} 칸이 부서졌다!");
        if (clashFx) DamagePopup.Spawn(HeadPosition(owner) + Vector3.up * 0.8f, "칸 파괴!", statusDebuffColor, 1f);
        Sfx.Play(SfxId.Break);
        return null;
    }

    /// <summary>합에서 이긴 주사위의 효과 적용. 공격 주사위가 실제로 피해를 줬으면(적중) true.</summary>
    private bool ApplyDie(Unit source, Unit target, DiceData die, int roll)
    {
        switch (die.diceType)
        {
            case DiceType.Attack:
                if (roll <= 0) return false;

                // 내성: 맞는 쪽이 이 공격 종류(참격/관통/타격)에 약하면 피해가 x1.5 / x2.0
                int damage = target.ApplyResistance(roll, die.diceDetail, out Resistance res);
                damage += target.GetStatus(StatusType.Fragile);   // 취약: 내성 배율 뒤에 고정 피해 +n
                int plate = target.GetStatus(StatusType.Plating);
                if (plate > 0)
                {
                    damage = Mathf.Max(0, damage - plate);   // 철갑: 맞을 때마다 피해 -n
                    Log($"  → {target.unitName}의 철갑이 피해를 {plate} 막았다");
                }
                lastDamage = damage;
                lastResistance = res;

                target.TakeDamage(damage);
                string resNote = res == Resistance.Normal ? ""
                    : $" ({ResistanceSet.Label(res)} x{ResistanceSet.Multiplier(res):0.0})";
                Log($"{source.unitName}의 {DiceInfo.Name(die.diceDetail)} 칸 → {target.unitName}에게 피해 {damage}!{resNote}");
                return true;

            case DiceType.Defense:
                // 기본 규칙: 수비 주사위가 합을 이기면 상대 주사위를 막는다(피해 없음).
                Log($"{source.unitName}의 {DiceInfo.Name(die.diceDetail)} 칸이 상대 칸을 막았다!");
                return false;
        }
        return false;
    }

    // ─────────────────────────── 주사위 효과 (스킬) ───────────────────────────

    /// <summary>
    /// 한 주사위 대결의 결과로 효과를 발동시킨다.
    /// 이긴 주사위: [합 승리시] + (피해를 줬으면) [적중시] / 진 주사위: [합 패배시]
    /// </summary>
    private void ResolveDieEffects(Unit winner, DiceData winDie, Unit loser, DiceData loseDie, bool hit)
    {
        if (winDie != null)
        {
            TriggerDiceEffects(winner, loser, winDie, DiceTrigger.OnClashWin);
            if (hit) TriggerDiceEffects(winner, loser, winDie, DiceTrigger.OnHit);

            // 적 특성 [흡혈]: 공격이 적중하면 회복 / [반격]: 수비 칸으로 합을 이기면 되받아친다
            int leech = winner.TraitN(EnemyTrait.Leech), counter = winner.TraitN(EnemyTrait.Counter);
            if (winner.IsAlive)
            {
                if (hit && leech > 0)
                {
                    int healed = winner.Heal(leech);
                    if (healed > 0)
                    {
                        Log($"  [흡혈] {winner.unitName} 체력 +{healed}");
                        if (clashFx) StatusPopup(winner, $"+{healed}", healPopupColor);
                    }
                }
                else if (counter > 0 && winDie.diceType == DiceType.Defense && loseDie != null)
                {
                    int countered = DealExtraDamage(loser, counter, "반격", defensePopupColor);
                    Log($"  [반격] {winner.unitName} → {loser.unitName}에게 피해 {countered}");
                }
            }
        }
        if (loseDie != null)
            TriggerDiceEffects(loser, winner, loseDie, DiceTrigger.OnClashLose);

        // 적 특성 [가시]: 공격에 맞으면 때린 쪽도 다친다
        int thorns = loser != null ? loser.TraitN(EnemyTrait.Thorns) : 0;
        if (hit && thorns > 0 && winner != null && winner.IsAlive)
        {
            int pricked = DealExtraDamage(winner, thorns, "가시", statusDebuffColor);
            Log($"  [가시] {loser.unitName} → {winner.unitName}에게 피해 {pricked}");
        }
    }

    /// <summary>태엽판의 모든 주사위에 붙은 [사용시] 효과를 발동한다.</summary>
    private void TriggerCardUse(Unit self, Unit opponent, CardData card)
    {
        if (card == null || card.diceList == null) return;
        foreach (DiceData die in card.diceList)
        {
            if (die == null) continue;
            // [사용시] 과감기는 태엽판 전체에 걸리므로 여기서 처리한다
            if (die.effects != null)
                foreach (DiceEffect fx in die.effects)
                {
                    if (fx != null && fx.trigger == DiceTrigger.OnUse && fx.effect == DiceEffectType.Overwind) Overwind(self, card, fx.amount);
                    if (fx != null && fx.effect == DiceEffectType.AllIn) AllIn(self, card, fx.amount);
                }
            TriggerDiceEffects(self, opponent, die, DiceTrigger.OnUse);
        }
    }

    /// <summary>올인: 남은 기력을 모두 태워 기력 1당 이 태엽판의 모든 칸 위력 +n (플레이어만)</summary>
    private void AllIn(Unit self, CardData card, int perKi)
    {
        if (!self.isPlayer || perKi <= 0) return;
        int spent = CurrentKi;
        if (spent <= 0)
        {
            Log("  올인하려 했지만 걸 기력이 없다!");
            if (clashFx) StatusPopup(self, "올인… 빈손", statusDebuffColor);
            return;
        }
        CurrentKi = 0;
        int gain = spent * perKi;
        foreach (DiceData d in card.diceList)
        {
            if (d == null) continue;
            cardPower.TryGetValue((self, d), out int now);
            cardPower[(self, d)] = now + gain;
        }
        UpdateHUD();
        Log($"  올인! 기력 {spent}을(를) 모두 걸었다! 이 태엽판 위력 +{gain}");
        if (clashFx) StatusPopup(self, $"올인! +{gain}", overwindColor);
        Sfx.Play(SfxId.Overwind);
    }

    /// <summary>룰렛: 여섯 칸 중 하나 (넷은 좋고 둘은 나쁘다)</summary>
    private void Roulette(Unit self, int n)
    {
        if (self == null || !self.IsAlive) return;
        n = Mathf.Max(1, n);
        int slot = Random.Range(0, 6);
        string result;
        bool good = slot < 4;
        switch (slot)
        {
            case 0:
                if (self.isPlayer) { pendingKi += 1; UpdateHUD(); result = "다음 라운드 기력 +1"; }
                else { self.AddStatus(StatusType.Strength, n, false); result = $"힘 {n}"; }
                break;
            case 1:
                if (self.isPlayer) { int got = handManager.DrawCards(n); result = $"태엽판 {got}장 뽑기"; }
                else { self.AddStatus(StatusType.Endurance, n, false); result = $"인내 {n}"; }
                break;
            case 2:
                self.AddStatus(StatusType.Strength, n, false);
                result = $"힘 {n}";
                break;
            case 3:
                int healed = self.Heal(n * 2);
                result = $"체력 +{healed}";
                break;
            case 4:
                self.AddRust(n, false);
                result = $"녹 {n}";
                break;
            default:
                self.AddStatus(StatusType.Paralysis, 1, false);
                result = "마비 1";
                break;
        }
        Log($"  룰렛이 돈다… {(good ? "당첨" : "꽝")}! {self.unitName}: {result}");
        if (clashFx) StatusPopup(self, $"룰렛 {(good ? "당첨" : "꽝")}: {result}", good ? statusBuffColor : statusDebuffColor);
        Sfx.Play(SfxId.Tock);
    }

    /// <summary>과감기: 체력 n을 태워(최소 1은 남는다) 이 태엽판의 모든 주사위 위력 +n</summary>
    private void Overwind(Unit self, CardData card, int amount)
    {
        if (!self.IsAlive || amount <= 0) return;
        int lost = Mathf.Min(amount, self.Hp - 1);
        if (lost > 0) self.TakeDamage(lost);
        foreach (DiceData d in card.diceList)
        {
            if (d == null) continue;
            cardPower.TryGetValue((self, d), out int now);
            cardPower[(self, d)] = now + amount;
        }
        Log($"  {self.unitName}이(가) 태엽을 과하게 감는다. 체력 -{lost}, 이 태엽판 위력 +{amount}");
        if (clashFx) StatusPopup(self, $"과감기! 위력 +{amount}", overwindColor);
        StageBackdrop.Impact(0.4f);
        Sfx.Play(SfxId.Overwind);
    }

    /// <summary>범위/추가 피해 공통: 취약 더하고 철갑 빼고 준다. 실제 피해를 돌려준다</summary>
    private int DealExtraDamage(Unit target, int amount, string label, Color color)
    {
        if (target == null || !target.IsAlive || amount <= 0) return 0;
        int damage = Mathf.Max(0, amount + target.GetStatus(StatusType.Fragile) - target.GetStatus(StatusType.Plating));
        if (damage <= 0) return 0;
        target.TakeDamage(damage);
        if (clashFx) DamagePopup.Spawn(HeadPosition(target) + Vector3.up * 0.4f, $"{label} {damage}", color, 1f);
        return damage;
    }

    private void TriggerDiceEffects(Unit self, Unit opponent, DiceData die, DiceTrigger trigger)
    {
        if (die.effects == null) return;

        foreach (DiceEffect fx in die.effects)
        {
            if (fx != null && fx.trigger == trigger)
                ApplyDiceEffect(self, opponent, fx);
        }
    }

    /// <summary>효과 1개 처리 — 새 효과(DiceEffectType)를 추가하면 여기에 case를 추가한다.</summary>
    private void ApplyDiceEffect(Unit self, Unit opponent, DiceEffect fx)
    {
        switch (fx.effect)
        {
            case DiceEffectType.Rust:
                if (!opponent.IsAlive) return;
                opponent.AddRust(fx.amount, fx.nextTurn);
                Sfx.Play(SfxId.Rust, 0.8f);
                Log($"  {fx.Describe()} → {opponent.unitName}");
                if (clashFx)
                    StatusPopup(opponent, fx.nextTurn ? $"녹 +{fx.amount} (다음 턴)" : $"녹 +{fx.amount}", rustPopupColor);
                break;

            case DiceEffectType.RecoverKi:
                if (!self.isPlayer) return;   // 기력은 플레이어 자원
                pendingKi += fx.amount;
                UpdateHUD();
                Log($"  {fx.Describe()}");
                break;

            case DiceEffectType.DrawCard:
                if (!self.isPlayer) return;   // 손패는 플레이어만 있다
                int drawn = handManager.DrawCards(fx.amount);
                Log($"  {fx.Describe()} ({drawn}장)");
                break;

            case DiceEffectType.Heal:
                int healed = self.Heal(fx.amount);
                if (healed > 0) Sfx.Play(SfxId.Heal, 0.8f);
                Log($"  {fx.Describe()} → {self.unitName} (+{healed})");
                if (clashFx && healed > 0) StatusPopup(self, $"+{healed}", healPopupColor);
                break;

            case DiceEffectType.Strength:
                ApplyStatus(self, StatusType.Strength, fx, statusBuffColor);
                break;

            case DiceEffectType.Endurance:
                ApplyStatus(self, StatusType.Endurance, fx, statusBuffColor);
                break;

            case DiceEffectType.Feeble:
                ApplyStatus(opponent, StatusType.Feeble, fx, statusDebuffColor);
                break;

            case DiceEffectType.Fragile:
                ApplyStatus(opponent, StatusType.Fragile, fx, statusDebuffColor);
                break;

            case DiceEffectType.Paralysis:
                ApplyStatus(opponent, StatusType.Paralysis, fx, statusDebuffColor);
                break;

            case DiceEffectType.PowerNextDie:
                nextDieBonus.TryGetValue(self, out int current);
                nextDieBonus[self] = current + fx.amount;
                Log($"  {fx.Describe()}");
                if (clashFx) StatusPopup(self, $"다음 칸 +{fx.amount}", statusBuffColor);
                break;

            case DiceEffectType.DestroyNextDie:
                if (!opponent.IsAlive) return;
                destroyNextDie.Add(opponent);
                Log($"  {fx.Describe()} → {opponent.unitName}");
                break;

            case DiceEffectType.Overwind:
                // [사용시]는 TriggerCardUse가 태엽판 전체에 건다. 그 밖의 시점이면: 체력을 태워 다음 주사위 위력 +n
                if (fx.trigger == DiceTrigger.OnUse || !self.IsAlive) return;
                int burned = Mathf.Min(fx.amount, self.Hp - 1);
                if (burned > 0) self.TakeDamage(burned);
                nextDieBonus.TryGetValue(self, out int wound);
                nextDieBonus[self] = wound + fx.amount;
                Log($"  {fx.Describe()} → 체력 -{burned}, 다음 칸 위력 +{fx.amount}");
                if (clashFx) StatusPopup(self, $"과감기! 다음 +{fx.amount}", overwindColor);
                break;

            case DiceEffectType.Resonance:
                // 범위 공격: 맞은 대상 말고 다른 모든 적에게 (적이 쓰면 다른 대상이 없으니 아무 일 없음)
                if (!self.isPlayer) return;
                int total = 0;
                foreach (Unit e in new List<Unit>(enemies))
                {
                    if (e == null || e == opponent || !e.IsAlive) continue;
                    total += DealExtraDamage(e, fx.amount, "울림", resonanceColor);
                }
                Log($"  {fx.Describe()} → 다른 적 모두 (총 {total})");
                if (total > 0) StageBackdrop.Impact(0.6f);
                break;

            case DiceEffectType.Corrode:
                int rust = opponent.Rust + opponent.PendingRust;
                int corroded = DealExtraDamage(opponent, rust * fx.amount, "부식", rustPopupColor);
                Log($"  {fx.Describe()} → {opponent.unitName} (녹 {rust} → 피해 {corroded})");
                break;

            case DiceEffectType.Plating:
                ApplyStatus(self, StatusType.Plating, fx, platingColor);
                break;

            case DiceEffectType.MaxKi:
                if (!self.isPlayer) return;   // 기력은 플레이어 자원
                battleMaxKi += fx.amount;
                UpdateHUD();
                Log($"  {fx.Describe()}");
                if (clashFx) StatusPopup(self, $"최대 기력 +{fx.amount}", kiPopupColor);
                break;

            case DiceEffectType.Lifesteal:
                // 방금 준 피해만큼 되찾는다 (최대 n)
                int drained = self.Heal(Mathf.Min(fx.amount, lastDamage));
                Log($"  {fx.Describe()} → {self.unitName} (+{drained})");
                if (clashFx && drained > 0) StatusPopup(self, $"흡혈 +{drained}", healPopupColor);
                break;

            case DiceEffectType.Tension:
                if (!self.isPlayer) return;   // 장력은 플레이어 자원
                AddTension(fx.amount);
                Log($"  {fx.Describe()}");
                if (clashFx) StatusPopup(self, $"장력 +{fx.amount}", statusBuffColor);
                break;

            case DiceEffectType.Burn:
                if (!opponent.IsAlive) return;
                opponent.AddBurn(fx.amount);
                Log($"  {fx.Describe()} → {opponent.unitName} (과열 {opponent.Burn})");
                if (clashFx) StatusPopup(opponent, $"과열 +{fx.amount}", burnColor);
                break;

            case DiceEffectType.OnTheHour:
            case DiceEffectType.Execute:
            case DiceEffectType.LastStand:
            case DiceEffectType.Combo:
            case DiceEffectType.Unopposed:
            case DiceEffectType.Dawn:
            case DiceEffectType.Dusk:
            case DiceEffectType.Gamble:
            case DiceEffectType.Jackpot:
            case DiceEffectType.DoubleOrNothing:
                break;   // 굴릴 때 ComputeRoll에서 적용

            case DiceEffectType.AllIn:
                break;   // [사용시]에 TriggerCardUse가 태엽판 전체에 건다

            case DiceEffectType.CoinFlip:
                if (Random.value < 0.5f)
                {
                    int heads = opponent != null && opponent.IsAlive ? DealExtraDamage(opponent, fx.amount, "앞면!", statusBuffColor) : 0;
                    Log($"  동전 던지기: 앞면! {(opponent != null ? opponent.unitName : "")}에게 피해 {heads}");
                }
                else
                {
                    int selfHit = Mathf.Min(Mathf.Max(1, fx.amount / 2), self.Hp - 1);
                    if (selfHit > 0) self.TakeDamage(selfHit);
                    Log($"  동전 던지기: 뒷면… {self.unitName} 체력 -{selfHit}");
                    if (clashFx) StatusPopup(self, $"뒷면… -{selfHit}", statusDebuffColor);
                }
                Sfx.Play(SfxId.Tock);
                break;

            case DiceEffectType.Roulette:
                Roulette(self, fx.amount);
                break;

            case DiceEffectType.Damage:
            {
                if (opponent == null || !opponent.IsAlive) return;
                int dealt = DealExtraDamage(opponent, fx.amount, "피해", damagePopupColor);
                Log($"  {fx.Describe()} → {opponent.unitName} (피해 {dealt})");
                if (clashFx)
                {
                    Vector3 p = opponent.transform.position + Vector3.up * 0.8f;
                    ImpactFX.Hit(p, self.transform.position, DiceDetail.Blunt, damagePopupColor, fx.amount >= bigHitThreshold);
                    Sfx.Play(SfxId.HitBlunt);
                }
                break;
            }

            case DiceEffectType.DamageAll:
            {
                int sum = 0;
                foreach (Unit e in (self.isPlayer ? new List<Unit>(enemies) : new List<Unit> { playerUnit }))
                {
                    if (e == null || !e.IsAlive) continue;
                    sum += DealExtraDamage(e, fx.amount, "광역", overwindColor);
                    if (clashFx) ImpactFX.Clash(e.transform.position + Vector3.up * 0.8f, overwindColor);
                }
                Log($"  {fx.Describe()} (합계 {sum})");
                if (clashFx) { StageBackdrop.Impact(0.7f); Sfx.Play(SfxId.HitBig, 0.8f); if (BattleCamera.Instance != null) BattleCamera.Instance.Shake(0.2f, 0.3f); }
                break;
            }
        }
    }

    private void ApplyStatus(Unit unit, StatusType type, DiceEffect fx, Color color)
    {
        if (!unit.IsAlive) return;
        unit.AddStatus(type, fx.amount, fx.nextTurn);
        bool good = type == StatusType.Strength || type == StatusType.Endurance || type == StatusType.Plating;
        Sfx.Play(good ? SfxId.Buff : SfxId.Debuff, 0.8f);
        Log($"  {fx.Describe()} → {unit.unitName}");
        if (clashFx)
            StatusPopup(unit, fx.nextTurn ? $"{DiceInfo.StatusName(type)} +{fx.amount} (다음 턴)"
                                          : $"{DiceInfo.StatusName(type)} +{fx.amount}", color);
    }

    private void StatusPopup(Unit unit, string text, Color color)
    {
        DamagePopup.Spawn(HeadPosition(unit) + Vector3.up * 0.8f, text, color, 0.8f);
    }

    // ─────────────────────────── 합 연출 (Clash FX) ───────────────────────────

    private static readonly Color attackPopupColor = new Color(1f, 0.55f, 0.45f);
    private static readonly Color defensePopupColor = new Color(0.55f, 0.8f, 1f);
    private static readonly Color boostedPopupColor = new Color(1f, 0.85f, 0.2f);
    private static readonly Color damagePopupColor = new Color(1f, 0.2f, 0.15f);
    private static readonly Color rustPopupColor = new Color(0.85f, 0.5f, 0.25f);
    private static readonly Color healPopupColor = new Color(0.45f, 1f, 0.55f);
    private static readonly Color statusBuffColor = new Color(1f, 0.8f, 0.35f);
    private static readonly Color statusDebuffColor = new Color(0.75f, 0.55f, 1f);
    private static readonly Color overwindColor = new Color(1f, 0.42f, 0.42f);
    private static readonly Color resonanceColor = new Color(1f, 0.82f, 0.35f);
    private static readonly Color platingColor = new Color(0.62f, 0.77f, 0.88f);
    private static readonly Color onTheHourColor = new Color(1f, 0.85f, 0.55f);
    private static readonly Color kiPopupColor = new Color(0.52f, 0.84f, 1f);

    /// <summary>주사위 한 쌍이 부딪히는 순간: 굴림 값 표시 → 양쪽 돌진 → 중간 지점에 불꽃.</summary>
    private IEnumerator ClashImpactRoutine(Unit a, DiceData aDie, int aRoll, int aBonus,
                                           Unit b, DiceData bDie, int bRoll, int bBonus,
                                           CardData aCard = null, CardData bCard = null)
    {
        // 머리 위 주사위 표시를 쓰면 숫자는 거기서 보이므로 팝업은 생략
        if (!showDiceDisplay)
        {
            ShowRollPopup(a, aDie, aRoll, aBonus);
            ShowRollPopup(b, bDie, bRoll, bBonus);
        }

        // 주사위가 있는 쪽만 상대를 향해 튀어나간다 (캐릭터 모델이면 주사위 종류 애니메이션도 재생)
        Coroutine aLunge = null, bLunge = null;
        // 공격하는 적은 가끔 한마디 외친다
        if (aDie != null && aDie.diceType == DiceType.Attack) EnemyBarks.Say(a, BarkKind.Attack, 0.3f);
        if (bDie != null && bDie.diceType == DiceType.Attack) EnemyBarks.Say(b, BarkKind.Attack, 0.3f);

        if (aDie != null)
        {
            a.PlayAnim(aDie.diceDetail.ToString());
            aLunge = StartCoroutine(a.LungeRoutine(b.transform.position, lungeDistance, lungeDuration));
        }
        if (bDie != null)
        {
            b.PlayAnim(bDie.diceDetail.ToString());
            bLunge = StartCoroutine(b.LungeRoutine(a.transform.position, lungeDistance, lungeDuration));
        }

        // 가장 앞으로 나간 순간(돌진의 40%)에 불꽃
        yield return new WaitForSeconds(lungeDuration * 0.4f);
        Vector3 mid = (a.transform.position + b.transform.position) * 0.5f + Vector3.up * 0.8f;
        ClashSpark.Burst(mid, new Color(1f, 0.9f, 0.6f), 14, 3.5f);
        Sfx.PlayClash(aDie, bDie);
        if (aDie != null && bDie != null) ImpactFX.Clash(mid, new Color(1f, 0.85f, 0.55f));   // 맞부딪힐 때만 (일방 공격은 맞는 쪽에서 터진다)
        // 정밀 · 걸작 태엽판이 부딪히면 그 등급 빛으로 한 번 더 크게 터진다
        CardData showy = PickShowy(aCard, bCard);
        if (showy != null) SpecialClashFx(mid, showy);

        // 돌진이 완전히 끝나 제자리로 돌아올 때까지 기다린다 (다음 움직임과 겹치면 위치가 어긋남)
        if (aLunge != null) yield return aLunge;
        if (bLunge != null) yield return bLunge;
    }

    /// <summary>합에서 이긴 주사위의 결과 연출 (공격 = 피해·넉백·흔들림, 수비 = 막음/회피 표시).</summary>
    private IEnumerator OutcomeFxRoutine(Unit winner, Unit loser, DiceData die, int roll, CardData winCard = null, int dieIndex = 0)
    {
        bool special = SkillCutIn.IsSpecial(winCard), art = SkillCutIn.IsMasterwork(winCard);
        if (die.diceType == DiceType.Attack)
        {
            loser.PlayAnim("Hit");
            if (loser.IsAlive) EnemyBarks.Say(loser, BarkKind.Hurt, 0.35f);
            int damage = lastDamage;   // ApplyDie에서 내성까지 적용된 실제 피해
            DamagePopup.Spawn(HeadPosition(loser) + Vector3.up * 0.4f, $"-{damage}", damagePopupColor, 1.5f);
            if (lastResistance != Resistance.Normal)
            {
                Color resColor;
                ColorUtility.TryParseHtmlString(ResistanceSet.ColorHex(lastResistance), out resColor);
                DamagePopup.Spawn(HeadPosition(loser) + Vector3.up * 0.4f,
                    $"{ResistanceSet.Label(lastResistance)}! x{ResistanceSet.Multiplier(lastResistance):0.0}", resColor, 1f);
            }
            bool bigHit = damage >= bigHitThreshold;
            Sfx.PlayHit(die.diceDetail, bigHit);
            Vector3 hitPoint = loser.transform.position + Vector3.up * 0.8f;
            ClashSpark.Burst(hitPoint, damagePopupColor, bigHit ? 36 : 24, bigHit ? 6.5f : 5f);
            ImpactFX.Hit(hitPoint, winner.transform.position, die.diceDetail, HitMarkColor(die.diceDetail), bigHit);
            StagePostFX.Punch(bigHit ? 1f : 0.3f);
            if (BattleCamera.Instance != null) BattleCamera.Instance.Punch(bigHit ? 1f : 0.35f);
            Coroutine knockback = StartCoroutine(loser.KnockbackRoutine(winner.transform.position, knockbackDistance, 0.4f));

            StageBackdrop.Impact(bigHit ? 1f : 0.45f);   // 타격 임팩트로 배경이 반응 (태엽 감김 / 가로등 깜빡임)
            if (BattleCamera.Instance != null)
                BattleCamera.Instance.Shake(bigHit ? 0.35f : 0.12f, bigHit ? 0.35f : 0.2f);

            if (special) SpecialHitFx(hitPoint, winner, loser, die, winCard);
            CardSignatures.OnHit(winner, loser, winCard, dieIndex, hitPoint);
            if (bigHit || art) yield return StartCoroutine(HitStopRoutine(art ? hitStopTime * 1.6f : hitStopTime));
            yield return knockback;
        }
        else
        {
            StageBackdrop.Impact(0.2f);
            string label = die.diceDetail == DiceDetail.Evade ? "회피!" : "막음!";
            Sfx.Play(die.diceDetail == DiceDetail.Evade ? SfxId.Evade : SfxId.Guard);
            DamagePopup.Spawn(HeadPosition(winner) + Vector3.up * 0.4f, label, defensePopupColor, 1.2f);
            ClashSpark.Burst(winner.transform.position + Vector3.up * 0.8f, defensePopupColor, 10, 2.5f);
            ImpactFX.Guard(Vector3.Lerp(winner.transform.position, loser.transform.position, 0.25f) + Vector3.up * 0.9f, defensePopupColor);
            if (special)
            {
                Color rc = DiceInfo.RarityColor(winCard.rarity);
                Vector3 gp = Vector3.Lerp(winner.transform.position, loser.transform.position, 0.25f) + Vector3.up * 0.9f;
                ImpactFX.Guard(gp, rc);
                FxQuad.Spawn(ImpactFX.RingTexture(), gp, rc, 0.45f, Vector2.one * 0.6f, Vector2.one * (art ? 4.2f : 3f));
                DamagePopup.Spawn(HeadPosition(winner) + Vector3.up * 1.5f, $"{winCard.cardName}!", rc, art ? 1.3f : 1.05f);
                if (BattleCamera.Instance != null) BattleCamera.Instance.Shake(art ? 0.2f : 0.1f, 0.25f);
            }
            yield return StartCoroutine(loser.KnockbackRoutine(winner.transform.position, knockbackDistance * (special ? 0.7f : 0.4f), 0.3f));
        }
    }

    // ─────────────────────────── 정밀 · 걸작 태엽판 연출 ───────────────────────────

    /// <summary>정밀 · 걸작 태엽판을 쓰기 시작할 때: 카메라가 쓰는 쪽을 잡고, 발밑에 등급 빛 오라 + 컷인</summary>
    private IEnumerator SpecialCardRoutine(Unit owner, CardData card)
    {
        bool art = SkillCutIn.IsMasterwork(card);
        Color rc = DiceInfo.RarityColor(card.rarity);
        if (BattleCamera.Instance != null) { BattleCamera.Instance.Focus(owner); BattleCamera.Instance.Punch(art ? 0.8f : 0.45f); }

        Vector3 feet = owner.transform.position + Vector3.up * 0.05f;
        Vector3 chest = owner.transform.position + Vector3.up * 1.0f;
        FxQuad.Spawn(ImpactFX.RingTexture(), feet, rc, 0.7f, Vector2.one * 0.6f, Vector2.one * (art ? 5.5f : 3.8f), 0f, 0f, true);
        FxQuad.Spawn(ImpactFX.RingTexture(), feet, Color.white * 0.8f, 0.5f, Vector2.one * 0.4f, Vector2.one * (art ? 3.6f : 2.4f), 0f, 0f, true);
        FxQuad.Spawn(ImpactFX.GlowTexture(), chest, rc, 0.6f, Vector2.one * 1.2f, Vector2.one * (art ? 4f : 2.8f));
        FxLight.Spawn(chest, rc, art ? 4.5f : 3f, art ? 12f : 8f, art ? 0.7f : 0.45f);
        ClashSpark.Burst(chest, rc, art ? 40 : 22, art ? 5.5f : 4f);
        if (art)
        {
            FxQuad.Spawn(ImpactFX.StarTexture(), chest, Color.white, 0.4f, Vector2.one * 3.5f, Vector2.one * 0.8f, 0f, 180f);
            StagePostFX.Punch(0.7f);
            StageBackdrop.Impact(0.6f);
            Sfx.Play(SfxId.Chime, 0.9f, 0f);
        }
        Sfx.Play(SfxId.Overwind, art ? 1f : 0.75f);
        Log($"<color=#{ColorUtility.ToHtmlStringRGB(rc)}>{owner.unitName}의 {DiceInfo.RarityName(card.rarity)} 태엽판 [{card.cardName}]!</color>");

        float wait = SkillCutIn.Play(card, owner);
        yield return new WaitForSeconds(wait);
    }

    /// <summary>둘 중 더 높은 등급의 정밀 이상 태엽판 (없으면 null)</summary>
    private static CardData PickShowy(CardData a, CardData b)
    {
        bool sa = SkillCutIn.IsSpecial(a), sb = SkillCutIn.IsSpecial(b);
        if (sa && sb) return a.rarity >= b.rarity ? a : b;
        return sa ? a : sb ? b : null;
    }

    private void SpecialClashFx(Vector3 mid, CardData card)
    {
        bool art = SkillCutIn.IsMasterwork(card);
        Color rc = DiceInfo.RarityColor(card.rarity);
        ImpactFX.Clash(mid, rc);
        ClashSpark.Burst(mid, rc, art ? 34 : 22, art ? 6f : 4.8f);
        FxQuad.Spawn(ImpactFX.RingTexture(), mid, rc, 0.4f, Vector2.one * 0.5f, Vector2.one * (art ? 4.4f : 3.2f));
        if (art) FxQuad.Spawn(ImpactFX.StarTexture(), mid, Color.white, 0.25f, Vector2.one * 3f, Vector2.one * 0.6f, Random.Range(0f, 90f), 240f);
        StagePostFX.Punch(art ? 0.6f : 0.35f);
        if (BattleCamera.Instance != null) BattleCamera.Instance.Shake(art ? 0.18f : 0.1f, 0.22f);
    }

    /// <summary>정밀 · 걸작 태엽판의 공격이 들어갔을 때: 등급 빛 자국을 한 겹 더, 태엽판 이름이 크게 뜬다</summary>
    private void SpecialHitFx(Vector3 hitPoint, Unit winner, Unit loser, DiceData die, CardData card)
    {
        bool art = SkillCutIn.IsMasterwork(card);
        Color rc = DiceInfo.RarityColor(card.rarity);
        ImpactFX.Hit(hitPoint + Vector3.up * 0.15f, winner.transform.position, die.diceDetail, rc, true);
        FxQuad.Spawn(ImpactFX.RingTexture(), hitPoint, rc, 0.5f, Vector2.one * 0.8f, Vector2.one * (art ? 6f : 4.2f));
        FxQuad.Spawn(ImpactFX.GlowTexture(), hitPoint, rc, 0.4f, Vector2.one * (art ? 4.5f : 3f), Vector2.one * 1f);
        ClashSpark.Burst(hitPoint, rc, art ? 50 : 30, art ? 8f : 6f);
        FxLight.Spawn(hitPoint, rc, art ? 5f : 3.5f, art ? 14f : 10f, art ? 0.45f : 0.3f);
        DamagePopup.Spawn(HeadPosition(loser) + Vector3.up * 1.7f, $"{card.cardName}!", rc, art ? 1.35f : 1.1f);
        if (BattleCamera.Instance != null)
        {
            BattleCamera.Instance.Shake(art ? 0.45f : 0.25f, art ? 0.45f : 0.3f);
            BattleCamera.Instance.Punch(1f);
        }
        StagePostFX.Punch(1f);
        StageBackdrop.Impact(1f);
        Sfx.Play(SfxId.HitBig, art ? 1f : 0.7f);
    }

    /// <summary>동점: 양쪽이 함께 튕겨난다.</summary>
    private IEnumerator TieFxRoutine(Unit a, Unit b)
    {
        Vector3 mid = (a.transform.position + b.transform.position) * 0.5f + Vector3.up * 1.2f;
        ClashSpark.Burst(mid, Color.white, 20, 4f);
        Sfx.Play(SfxId.Tie);
        ImpactFX.Clash(mid, Color.white);
        StagePostFX.Punch(0.25f);

        Coroutine aBack = StartCoroutine(a.KnockbackRoutine(b.transform.position, knockbackDistance * 0.6f, 0.35f));
        Coroutine bBack = StartCoroutine(b.KnockbackRoutine(a.transform.position, knockbackDistance * 0.6f, 0.35f));
        if (BattleCamera.Instance != null) BattleCamera.Instance.Shake(0.1f, 0.15f);
        StageBackdrop.Impact(0.3f);

        yield return aBack;
        yield return bBack;
    }

    /// <summary>타격 자국의 색 — 참격은 붉게, 관통은 차갑게, 타격은 뜨겁게</summary>
    private static Color HitMarkColor(DiceDetail detail)
    {
        switch (detail)
        {
            case DiceDetail.Slash:  return new Color(1f, 0.35f, 0.3f);
            case DiceDetail.Thrust: return new Color(0.55f, 0.85f, 1f);
            default:                return new Color(1f, 0.7f, 0.3f);
        }
    }

    /// <summary>큰 피해 순간 시간을 거의 멈춰서 타격감을 준다.</summary>
    private IEnumerator HitStopRoutine(float duration)
    {
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1f;
    }

    private void ShowRollPopup(Unit unit, DiceData die, int roll, int bonus)
    {
        if (die == null) return;

        string text = bonus != 0 ? $"{roll} ({bonus:+0;-0})" : roll.ToString();
        Color color = bonus > 0 ? boostedPopupColor : bonus < 0 ? statusDebuffColor
                    : die.diceType == DiceType.Attack ? attackPopupColor : defensePopupColor;
        DamagePopup.Spawn(HeadPosition(unit), text, color, 1f);
    }

    /// <summary>유닛 머리 위 위치 (Renderer 크기 기준, 없으면 대충 1.5m 위).</summary>
    private static Vector3 HeadPosition(Unit unit)
    {
        if (unit.bodyRenderer != null)
        {
            Bounds bounds = unit.bodyRenderer.bounds;
            return new Vector3(bounds.center.x, bounds.max.y + 0.3f, bounds.center.z);
        }
        return unit.transform.position + Vector3.up * 1.5f;
    }

    // ─────────────────────────── 승패 판정 ───────────────────────────

    /// <summary>디버그: 유닛을 즉사시킨다. 라운드 진행 중이면 진행 루프가 승패를 판정하고, 아니면 바로 판정한다.</summary>
    public void DebugKill(Unit unit)
    {
        if (unit == null || !unit.IsAlive || Phase == BattlePhase.End) return;

        Log($"[디버그] {unit.unitName} 즉사");
        unit.TakeDamage(unit.Hp);
        UpdateHUD();
        if (!IsBusy) CheckBattleEnd();
    }

    private bool CheckBattleEnd()
    {
        if (Phase == BattlePhase.End) return true;

        if (!playerUnit.IsAlive) { EndBattle(false); return true; }

        bool allDead = true;   // 살아 있는 적이 하나도 없으면 클리어 (적을 안 배치한 무대도 자동 클리어)
        foreach (Unit e in enemies)
        {
            if (e != null && e.IsAlive) { allDead = false; break; }
        }

        if (allDead) { EndBattle(true); return true; }
        return false;
    }

    private void EndBattle(bool victory)
    {
        if (Phase == BattlePhase.End) return;
        Phase = BattlePhase.End;
        ClockDial.Close();
        Time.timeScale = 1f;   // 히트스톱 도중에 끝나도 시간이 멈춘 채로 남지 않게
        UpdateHUD();

        if (BattleCamera.Instance != null) BattleCamera.Instance.Release();

        if (victory)
        {
            Log("―― 무대 클리어! ――");
            Sfx.Play(SfxId.Victory, 1f, 0f);
            StartCoroutine(VictoryRoutine());
        }
        else
        {
            Log("―― 패배... ――");
            Sfx.Play(SfxId.Defeat, 1f, 0f);
            GameManager.Instance.OnBattleLost();
        }
    }

    /// <summary>승리: "승리!" 연출 → 적의 태엽판 한 장을 골라 흡수 (전투마다 확정) → 다음 챕터로</summary>
    private IEnumerator VictoryRoutine()
    {
        yield return new WaitForSeconds(0.6f);   // 마지막 일격의 여운

        List<CardData> spoils = new List<CardData>();
        foreach (EnemySpawnInfo info in stage.enemies)
        {
            if (info != null && info.enemy != null && info.enemy.deck != null) spoils.AddRange(info.enemy.deck);
        }

        List<CardData> picked = null;
        int pickCount = 1 + TensionStage;   // 장력 단계만큼 더 삼킬 수 있다
        Log(TensionStage > 0 ? $"장력 {TensionStage}단계: 적의 기억 {pickCount}개를 삼킬 수 있다."
                             : "적의 기억 하나를 삼킬 수 있다.");
        yield return VictoryScreen.Play(this, pickCount, spoils, GameManager.Instance.AbsorbedSourceNames(), p => picked = p);

        GameManager.Instance.OnBattleWon(picked);
    }

    // ─────────────────────────── 적 생성 ───────────────────────────

    /// <summary>무한 모드의 강화를 방금 불러낸 적들에게 건다 (한 번 쓰면 되돌린다)</summary>
    private void ApplyEnemyBoost()
    {
        float hpScale = Mathf.Max(0.1f, NextEnemyHpScale);
        int power = Mathf.Max(0, NextEnemyPower);
        bool hide = NextHideIdentity;
        NextEnemyHpScale = 1f;
        NextEnemyPower = 0;
        NextHideIdentity = false;
        int extra = Mathf.Max(0, NextExtraTraits), extraN = Mathf.Max(1, NextExtraTraitAmount);
        NextExtraTraits = 0;
        NextExtraTraitAmount = 1;
        if (extra > 0) GiveRandomTraits(extra, extraN);
        if (hpScale == 1f && power == 0 && !hide) return;

        foreach (Unit e in enemies)
        {
            if (e == null) continue;
            if (hpScale != 1f) e.RestoreVitals(Mathf.Max(1, Mathf.RoundToInt(e.MaxHp * hpScale)), Mathf.Max(1, Mathf.RoundToInt(e.MaxHp * hpScale)));
            e.powerBonus = power;
            if (hide) { e.unitName = HiddenName; e.kindName = HiddenName; }
        }
        Log($"<color={UITheme.HexEnemy}>적 강화: 체력 x{hpScale:0.##} · 모든 칸 위력 +{power}</color>");
    }

    /// <summary>무한 모드에서 적마다 아직 없는 특성을 count개 무작위로 덧붙인다</summary>
    private void GiveRandomTraits(int count, int amount)
    {
        EnemyTrait[] pool =
        {
            EnemyTrait.Shell, EnemyTrait.Regen, EnemyTrait.RustAura, EnemyTrait.Punctual, EnemyTrait.Counter, EnemyTrait.Leech,
            EnemyTrait.Thorns, EnemyTrait.Enrage, EnemyTrait.Guardian, EnemyTrait.Nocturnal, EnemyTrait.Martyr, EnemyTrait.Ambush, EnemyTrait.Hex,
        };
        foreach (Unit e in enemies)
        {
            if (e == null) continue;
            List<EnemyTrait> options = new List<EnemyTrait>(pool);
            options.RemoveAll(t => e.TraitN(t) > 0);
            List<string> added = new List<string>();
            for (int i = 0; i < count && options.Count > 0; i++)
            {
                EnemyTrait t = options[UnityEngine.Random.Range(0, options.Count)];
                options.Remove(t);
                e.extraTraits.Add(new EnemyTraitEntry { trait = t, amount = amount });
                added.Add(EnemyTypeInfo.Name(t));
            }
            if (added.Count > 0) Log($"<color={UITheme.HexEnemy}>{e.unitName}에게 특성 [{string.Join("] [", added)}] 덧붙음</color>");
        }
    }

    private void SpawnEnemies()
    {
        // 이전 무대의 유닛 정리
        foreach (Unit e in enemies)
        {
            if (e != null) Destroy(e.gameObject);
        }
        enemies.Clear();

        HashSet<string> usedNames = new HashSet<string>();   // 같은 무대에서 별명이 겹치지 않게
        foreach (EnemySpawnInfo info in stage.enemies)
        {
            if (info == null || info.enemy == null) continue;

            GameObject go;
            if (info.enemy.modelPrefab != null)
            {
                go = Instantiate(info.enemy.modelPrefab, info.spawnPosition, Quaternion.identity);
            }
            else if (spriteMode)
            {
                // 2.5D: 바닥 위에 선 2D 스프라이트 (그림이 없으면 Tint Color 실루엣)
                go = new GameObject(info.enemy.enemyName);
                Vector3 pos = info.spawnPosition;
                pos.y = spriteGroundY;   // 스프라이트는 발이 루트 위치 → 바닥 높이에 맞춘다
                go.transform.position = pos;

                // 그림이 없으면 자동 일러스트 (이름으로 생김새, Tint Color로 색) — 포즈 그림도 같이
                SpriteCharacter sc = go.AddComponent<SpriteCharacter>();
                Sprite art = info.enemy.sprite;
                PoseSprites poses = info.enemy.poses;
                if (art == null)
                {
                    Illustrator.CharacterArt gen = Illustrator.Character(info.enemy.enemyName, info.enemy.tintColor);
                    art = gen.idle;
                    if (poses == null || poses.IsEmpty) poses = gen.poses;
                }
                sc.Setup(art, info.enemy.spriteHeight, Color.white);   // Unit을 붙이기 전에 외형 완성
                sc.poses = poses;
            }
            else
            {
                // 외형 프리팹이 없으면 기본 큐브로 생성
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.position = info.spawnPosition;
                go.transform.localScale = Vector3.one * 2f;

                Renderer r = go.GetComponent<Renderer>();
                if (r != null) r.material.color = info.enemy.tintColor;
            }

            Unit unit = go.GetComponent<Unit>();
            if (unit == null) unit = go.AddComponent<Unit>();

            unit.Init(PickNickname(info.enemy, usedNames), info.enemy.maxHp, info.enemy.deck, info.enemy.resistance);
            unit.kindName = info.enemy.enemyName;
            unit.enemyData = info.enemy;
            unit.slotNumber = enemies.Count + 1;   // 머리 위 [번호] — 숫자 키로 대상 지정
            unit.actionsPerRound = info.enemy.actionsPerRound;
            unit.behavior = info.enemy.behavior;
            unit.trait = info.enemy.trait;
            unit.traitAmount = info.enemy.traitAmount;
            if (info.enemy.extraTraits != null)
                foreach (EnemyTraitEntry x in info.enemy.extraTraits)
                    if (x != null && x.trait != EnemyTrait.None) unit.extraTraits.Add(new EnemyTraitEntry { trait = x.trait, amount = x.amount });
            unit.CaptureHome();
            unit.OnDead += HandleUnitDead;
            enemies.Add(unit);
        }

        if (enemies.Count == 0)
            Debug.LogWarning("무대에 배치된 적이 없습니다! Battle Stage 에셋에서 적을 추가하세요.");
    }

    /// <summary>EnemyData의 별명 중 이 무대에서 아직 안 쓴 것을 무작위로. 별명이 없거나 다 썼으면 종류 이름.</summary>
    private static string PickNickname(EnemyData data, HashSet<string> used)
    {
        List<string> free = new List<string>();
        if (data.nicknames != null)
        {
            foreach (string n in data.nicknames)
            {
                if (!string.IsNullOrWhiteSpace(n) && !used.Contains(n)) free.Add(n);
            }
        }
        string name = free.Count > 0 ? free[Random.Range(0, free.Count)] : data.enemyName;
        used.Add(name);
        return name;
    }

    /// <summary>전투 시작: 플레이어는 적들 쪽, 적들은 플레이어 쪽을 바라본다 (2.5D 스프라이트 좌우).</summary>
    private void FaceOpponents()
    {
        if (playerUnit == null || enemies.Count == 0) return;

        Vector3 enemyCenter = Vector3.zero;
        foreach (Unit e in enemies) enemyCenter += e.transform.position;
        enemyCenter /= enemies.Count;

        playerUnit.FaceTowards(enemyCenter);
        foreach (Unit e in enemies) e.FaceTowards(playerUnit.transform.position);
    }

    /// <summary>플레이어와 적 머리 위에 HP/상태이상 표시를 붙인다 (이미 있으면 건너뜀).</summary>
    private void AttachStatusLabels()
    {
        if (!showStatusLabels) return;

        if (playerUnit != null) UnitStatusLabel.AttachTo(playerUnit);
        foreach (Unit e in enemies)
        {
            if (e != null) UnitStatusLabel.AttachTo(e);
        }
    }

    private void HandleUnitDead(Unit unit)
    {
        Log($"{unit.unitName} 이(가) 쓰러졌다!");
        if (!unit.isPlayer) RunItemHook(item => item.onEnemyKilled != null, item => item.onEnemyKilled(this, unit));
        EnemyBarks.Say(unit, BarkKind.Death, 0.85f);   // 쓰러지며 남기는 한마디

        // 적 특성 [폭발]: 쓰러지면서 상대를 휩쓴다
        // 적 특성 [순교]: 쓰러지며 동료들을 북돋운다
        int martyr = unit.isPlayer ? 0 : unit.TraitN(EnemyTrait.Martyr);
        if (martyr > 0)
        {
            foreach (Unit o in enemies)
            {
                if (o == null || o == unit || !o.IsAlive) continue;
                o.AddStatus(StatusType.Strength, martyr, false);
                if (clashFx) StatusPopup(o, $"순교 힘 +{martyr}", statusBuffColor);
            }
            Log($"  [순교] {unit.unitName}이(가) 쓰러지며 동료들에게 힘 {martyr}");
        }

        int volatileN = unit.isPlayer ? 0 : unit.TraitN(EnemyTrait.Volatile);
        if (volatileN > 0 && playerUnit != null && playerUnit.IsAlive)
        {
            int blast = DealExtraDamage(playerUnit, volatileN, "폭발", overwindColor);
            Log($"  [폭발] {unit.unitName}이(가) 터졌다 → {playerUnit.unitName}에게 피해 {blast}");
            if (clashFx)
            {
                ClashSpark.Burst(unit.transform.position + Vector3.up * 0.8f, overwindColor, 30, 6f);
                ImpactFX.Clash(unit.transform.position + Vector3.up * 0.8f, overwindColor);
            }
            StageBackdrop.Impact(0.8f);
        }
    }

    // ─────────────────────────── 기타 유틸 ───────────────────────────

    private CardInstance GetRandomEnemyCard(Unit enemy)
    {
        if (enemy.personalDeck == null || enemy.personalDeck.Count == 0) return null;
        CardData data = enemy.personalDeck[Random.Range(0, enemy.personalDeck.Count)];
        return new CardInstance(data);
    }

    /// <summary>고르던 태엽판 취소 (시계판 바깥을 눌렀을 때)</summary>
    public void CancelSelection()
    {
        CancelPendingCard();
        TimelineChanged();
    }

    private void CancelPendingCard()
    {
        if (pendingCard != null && pendingCard.view != null)
            pendingCard.view.SetSelected(false);
        pendingCard = null;
    }

    private void UpdateHUD()
    {
        if (kiText != null) kiText.text = $"기력 {CurrentKi}/{MaxKi}" + (pendingKi > 0 ? $" <size=24>(+{pendingKi})</size>" : "");


        if (phaseText != null)
        {
            switch (Phase)
            {
                case BattlePhase.PlayerTurn: phaseText.text = $"<color={UITheme.HexKi}>계획: 태엽판을 시계판에</color>"; break;
                case BattlePhase.EnemyTurn:  phaseText.text = $"<color={UITheme.HexBrass}>시계 진행 {CurrentHour}시</color>"; break;
                case BattlePhase.End:        phaseText.text = "전투 종료"; break;
                default:                     phaseText.text = ""; break;
            }
        }
    }

    private void Log(string message)
    {
        Debug.Log($"[전투] {message}");

        if (battleLogText == null) return;

        // 턴/무대 구분선은 황동색으로 강조
        string line = message.StartsWith("――") ? $"<color={UITheme.HexBrass}>{message}</color>" : message;
        logLines.Insert(0, line);                          // 최신 로그가 위로 (아래로 스크롤하면 이전 기록)
        while (logLines.Count > Mathf.Max(1, maxLogLines)) logLines.RemoveAt(logLines.Count - 1);
        battleLogText.text = string.Join("\n", logLines);
    }
}
