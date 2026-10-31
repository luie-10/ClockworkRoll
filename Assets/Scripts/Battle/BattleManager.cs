using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 한 무대(전투)의 진행을 담당.
///
/// [자원] 기력 — 턴마다 회복되고, 공격 주사위로 합을 이기면 추가 회복된다.
/// [합]   양쪽 주사위를 앞에서부터 한 칸씩 비교.
///        - 타입 상성 보정: 참격 > 관통 > 타격 > 참격 (우위 시 굴림 +2)
///        - 회피는 상대 공격 주사위 상대로 굴림 +2
///        - 동점은 양쪽 모두 무효 (팽팽하게 밀림)
/// [침식] 흡수 카드(◈) 사용 시 확률적으로 발동해 사용자가 피해를 입는다.
/// [연출] 공격자가 응답자 곁으로 이동, 카메라 포커스, 감정 단계 라이팅.
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
    public Text emotionText;
    [Tooltip("감정 다음 단계까지의 진행도 게이지 (Image Type = Filled)")]
    public Image emotionGauge;
    [Tooltip("'내 턴' / '적의 턴' 표시")]
    public Text phaseText;
    [Tooltip("내 턴이 아닐 때는 자동으로 비활성화")]
    public Button endTurnButton;
    public Text battleLogText;
    [Tooltip("전투 로그에 남겨둘 최대 줄 수 (스크롤해서 볼 수 있음)")]
    public int maxLogLines = 80;

    [Header("기력 (자원)")]
    public int maxKi = 20;       // 기력 상한

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
    [Tooltip("유닛 머리 위에 HP/출혈 표시")]
    public bool showStatusLabels = true;
    [Tooltip("합 중 머리 위에 책장 이름 + 주사위(굴림 애니메이션) 표시")]
    public bool showDiceDisplay = true;
    [Tooltip("주사위가 굴러가는 시간(초)")]
    public float diceRollTime = 0.45f;
    [Tooltip("주사위가 부딪힐 때 앞으로 튀어나가는 거리")]
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
    public int EmotionLevel { get; private set; }

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

    private BattleStageData stage;
    private Unit playerUnit;
    private readonly List<Unit> enemies = new List<Unit>();
    private readonly List<string> logLines = new List<string>();
    private CardInstance pendingCard;   // 클릭해서 대상을 기다리는 중인 카드
    private CardInstance responseCard;  // 적의 턴에 첫 공격을 받아칠 응수 책장 (나를 클릭해서 장착)
    private readonly Dictionary<Unit, CardInstance> enemyIntents = new Dictionary<Unit, CardInstance>();   // 적이 이번 턴에 장착한 책장
    private int emotionPoints;
    private int lastDamage;                  // 마지막 공격의 실제 피해 (내성 적용 후, 연출용)
    private Resistance lastResistance;

    // 합 하나 안에서만 유효한 효과 (합이 끝나면 비운다)
    private readonly Dictionary<Unit, int> nextDieBonus = new Dictionary<Unit, int>();   // [다음 주사위 위력 +n]
    private readonly HashSet<Unit> destroyNextDie = new HashSet<Unit>();                 // 이 유닛의 다음 주사위는 파괴됨

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Update()
    {
        // 턴 종료 버튼은 내 턴 + 합 연출이 끝난 상태에서만 누를 수 있다
        if (endTurnButton != null)
            endTurnButton.interactable = Phase == BattlePhase.PlayerTurn && !IsBusy;
    }

    // ─────────────────────────── 전투 시작 ───────────────────────────

    /// <summary>GameManager가 전투를 걸 때 호출.</summary>
    public void StartBattle(BattleStageData stageData, Unit player)
    {
        stage = stageData;
        playerUnit = player;
        CurrentKi = stage.startKi;
        EmotionLevel = 0;
        emotionPoints = 0;
        pendingCard = null;
        responseCard = null;
        enemyIntents.Clear();
        IsBusy = false;
        Phase = BattlePhase.Ready;

        logLines.Clear();
        if (stageNameText != null) stageNameText.text = stage.stageName;

        SpawnEnemies();

        if (playerUnit != null)
        {
            playerUnit.CaptureHome();
            playerUnit.ClearStatus();   // 이전 무대의 출혈이 넘어오지 않게
        }
        AttachStatusLabels();
        FaceOpponents();
        handManager.BuildDeck(GetPlayerDeck());

        if (StageLighting.Instance != null) StageLighting.Instance.SetEmotion(0);
        StageBackdrop.Show(stage.backdrop);   // 무대에 맞는 배경으로 전환
        StageBackdrop.SetTempo(0);
        UpdateHUD();

        StartCoroutine(BattleStartRoutine());
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
        Log($"── 무대 [{stage.stageName}] 시작 ──");
        yield return new WaitForSeconds(0.6f);
        StartPlayerTurn();
    }

    // ─────────────────────────── 턴 흐름 ───────────────────────────

    private void StartPlayerTurn()
    {
        if (!playerUnit.IsAlive) { EndBattle(false); return; }
        if (CheckBattleEnd()) return;

        // 새 턴: "다음 턴 출혈"처럼 대기 중이던 상태이상이 모두 적용된다
        playerUnit.ActivatePendingStatus();
        foreach (Unit e in enemies)
        {
            if (e.IsAlive) e.ActivatePendingStatus();
        }

        Phase = BattlePhase.PlayerTurn;
        CurrentKi = Mathf.Min(CurrentKi + stage.kiPerTurn, maxKi);
        handManager.RefillHand();
        RollEnemyIntents();
        UpdateHUD();
        Log("── 내 턴: 책장 → 적 클릭 = 공격 / 책장 → 나 클릭 = 응수 장착 ──");
    }

    /// <summary>
    /// 적마다 이번 턴에 쓸 책장을 미리 정해서 머리 위 상태창에 보여준다.
    /// 이 책장으로 내 공격에 응수하고, 적의 턴에도 이 책장으로 공격한다.
    /// </summary>
    private void RollEnemyIntents()
    {
        enemyIntents.Clear();
        foreach (Unit e in enemies)
        {
            if (!e.IsAlive) continue;

            CardInstance intent = GetRandomEnemyCard(e);
            if (intent == null) continue;
            enemyIntents[e] = intent;

            UnitStatusLabel label = UnitStatusLabel.AttachTo(e);
            if (label != null) label.SetIntent("장착", intent.data);
        }
    }

    /// <summary>유닛이 지금 장착한 책장 (적 = 이번 턴 행동, 플레이어 = 응수 책장). 없으면 null.</summary>
    public CardData GetEquippedCard(Unit unit)
    {
        if (unit == null) return null;
        if (unit.isPlayer) return responseCard != null ? responseCard.data : null;
        return enemyIntents.TryGetValue(unit, out CardInstance c) && c != null ? c.data : null;
    }

    /// <summary>'턴 종료' 버튼에 연결해서 사용.</summary>
    public void EndPlayerTurn()
    {
        if (Phase != BattlePhase.PlayerTurn || IsBusy) return;
        CancelPendingCard();
        StartCoroutine(EnemyTurnRoutine());
    }

    private IEnumerator EnemyTurnRoutine()
    {
        Phase = BattlePhase.EnemyTurn;
        UpdateHUD();
        Log("── 적의 턴 ──");

        foreach (Unit enemy in enemies)
        {
            if (!enemy.IsAlive) continue;

            yield return new WaitForSeconds(0.7f);

            // 적은 내 턴 동안 머리 위에 보여준 '장착' 책장으로 공격한다
            enemyIntents.TryGetValue(enemy, out CardInstance enemyCard);
            if (enemyCard == null)
            {
                Log($"{enemy.unitName}: 사용할 책장이 없다.");
                continue;
            }

            // 플레이어는 내 턴에 '응수'로 장착해 둔 책장이 있을 때만 맞받아친다 (손패에서 멋대로 꺼내 쓰지 않음)
            CardInstance playerResponse = responseCard;
            if (playerResponse != null)
            {
                responseCard = null;
                Log($"응수! [{playerResponse.data.cardName}]로 {enemy.unitName}의 공격을 받아친다.");
            }
            else
            {
                Log($"{enemy.unitName}의 [{enemyCard.data.cardName}] — 응수할 책장이 없다!");
            }

            UnitStatusLabel enemyLabel = UnitStatusLabel.AttachTo(enemy);
            if (enemyLabel != null) enemyLabel.ClearIntent();   // 사용 중 → 합 주사위 표시로 넘어감

            yield return StartCoroutine(ResolveClashRoutine(enemy, enemyCard, playerUnit, playerResponse));

            if (playerResponse != null)
            {
                handManager.DiscardReserved(playerResponse);   // 응수 책장은 한 번 쓰면 버림패로
                UnitStatusLabel playerLabel = UnitStatusLabel.AttachTo(playerUnit);
                if (playerLabel != null) playerLabel.ClearIntent();
            }

            if (!playerUnit.IsAlive) { EndBattle(false); yield break; }
        }
        enemyIntents.Clear();

        yield return new WaitForSeconds(0.4f);
        StartPlayerTurn();
    }

    // ─────────────────────────── 카드 사용 ───────────────────────────

    /// <summary>손패 카드 클릭 (HandManager가 연결해줌). 대기 상태로 만들고 상대 클릭을 기다린다.</summary>
    public void OnCardClicked(CardView view)
    {
        if (Phase != BattlePhase.PlayerTurn || IsBusy) return;

        CardInstance card = view.BoundCard;
        if (card == null || card.data == null) return;

        if (card.data.cost > CurrentKi) { Log("기력이 부족하다!"); return; }

        if (card.data.requireEmotionLevel > EmotionLevel)
        {
            Log($"감정 단계 {card.data.requireEmotionLevel} 이상이 필요한 책장이다.");
            return;
        }

        if (pendingCard == card) { CancelPendingCard(); return; }   // 다시 클릭 = 선택 취소

        CancelPendingCard();
        pendingCard = card;
        view.SetSelected(true);
        Log($"[{card.data.cardName}] 사용 대상을 클릭하세요. (다시 클릭하면 취소)");
    }

    /// <summary>3D 유닛(적)을 클릭했을 때 Unit이 호출.</summary>
    public void OnUnitClicked(Unit unit)
    {
        if (Phase != BattlePhase.PlayerTurn || IsBusy) return;
        if (unit == null || !unit.IsAlive) return;

        // 나 자신 클릭 = 응수 책장 장착 / 해제
        if (unit.isPlayer) { ToggleResponse(); return; }

        if (pendingCard == null) return;
        CardInstance card = pendingCard;
        CancelPendingCard();

        StartCoroutine(UseCardRoutine(card, unit));
    }

    /// <summary>
    /// 응수 책장: 적의 턴에 들어오는 첫 공격을 이 책장으로 맞받아친다.
    /// 책장을 고른 상태로 나를 클릭 → 기력을 내고 장착 (머리 위에 표시).
    /// 아무것도 안 고르고 나를 클릭 → 장착 해제 (기력 반환, 손패로 돌아옴).
    /// </summary>
    private void ToggleResponse()
    {
        if (pendingCard == null)
        {
            if (responseCard == null) return;
            CurrentKi = Mathf.Min(CurrentKi + responseCard.data.cost, maxKi);
            handManager.ReturnToHand(responseCard);
            Log($"응수 해제: [{responseCard.data.cardName}] (기력 {responseCard.data.cost} 반환)");
            responseCard = null;
            UnitStatusLabel.AttachTo(playerUnit)?.ClearIntent();
            UpdateHUD();
            return;
        }

        CardInstance card = pendingCard;
        CancelPendingCard();

        // 이미 장착한 게 있으면 손패로 되돌리고 교체
        if (responseCard != null)
        {
            CurrentKi = Mathf.Min(CurrentKi + responseCard.data.cost, maxKi);
            handManager.ReturnToHand(responseCard);
        }

        CurrentKi -= card.data.cost;
        handManager.ReserveCard(card);
        responseCard = card;
        UnitStatusLabel.AttachTo(playerUnit)?.SetIntent("응수", card.data);
        UpdateHUD();
        Log($"응수 장착: [{card.data.cardName}] — 적의 턴에 첫 공격을 받아친다. (나를 다시 클릭하면 해제)");

        // 흡수 카드면 장착하는 순간 침식 판정
        CheckCorruption(card);
    }

    /// <summary>흡수 카드(◈)의 침식 판정. 침식으로 전투가 끝났으면 true.</summary>
    private bool CheckCorruption(CardInstance card)
    {
        if (card.entry == null || !card.entry.absorbed || !playerUnit.IsAlive) return false;

        float chance = GameManager.Instance.CorruptionChance(card.entry.corruption);
        if (Random.value >= chance) return false;

        int damage = corruptionBaseDamage + Mathf.Max(0, card.entry.corruption - 1);
        Log($"기억이 침식했다! [{card.data.cardName}]에 깃든 원한이 밖으로 샌다.");
        playerUnit.TakeDamage(damage);
        return CheckBattleEnd();
    }

    private IEnumerator UseCardRoutine(CardInstance card, Unit target)
    {
        IsBusy = true;
        yield return StartCoroutine(UseCardBody(card, target));
        IsBusy = false;
    }

    private IEnumerator UseCardBody(CardInstance card, Unit target)
    {
        // 1) 기력 지불 + 손패에서 제거
        CurrentKi -= card.data.cost;
        UpdateHUD();
        handManager.UseCard(card);

        // 2) 흡수 카드(◈)의 침식 판정 — 삼킨 기억이 도사리고 있다
        if (CheckCorruption(card)) yield break;

        // 3) 상대는 머리 위에 보여준 '장착' 책장으로 응수 → 합 진행
        enemyIntents.TryGetValue(target, out CardInstance enemyResponse);
        if (enemyResponse == null) enemyResponse = GetRandomEnemyCard(target);
        yield return StartCoroutine(ResolveClashRoutine(playerUnit, card, target, enemyResponse));

        // 4) 승패 확인
        CheckBattleEnd();
    }

    // ─────────────────────────── 합(주사위 대결) ───────────────────────────

    private IEnumerator ResolveClashRoutine(Unit attacker, CardInstance atkCard, Unit defender, CardInstance defCard)
    {
        string defName = defCard != null ? defCard.data.cardName : "없음";
        Log($"합: {attacker.unitName}[{atkCard.data.cardName}] vs {defender.unitName}[{defName}]");

        // 연출: 카메라가 응답자에게 살짝 다가가고, 공격자가 곁으로 걸어간다
        if (BattleCamera.Instance != null) BattleCamera.Instance.Focus(defender);

        // 2.5D: 합을 하는 두 캐릭터가 서로를 바라본다
        attacker.FaceTowards(defender.transform.position);
        defender.FaceTowards(attacker.transform.position);

        Vector3 attackerHome = attacker.transform.position;
        bool stepped = TryGetStandPosition(attacker, defender, out Vector3 standPos);
        if (stepped) yield return StartCoroutine(attacker.MoveToRoutine(standPos, 0.25f));

        List<DiceData> atkDice = atkCard.data.diceList;
        List<DiceData> defDice = defCard != null ? defCard.data.diceList : null;

        int count = Mathf.Max(atkDice != null ? atkDice.Count : 0, defDice != null ? defDice.Count : 0);

        // 연출: 양쪽 머리 위에 책장 이름 + 주사위 표시
        ClashDiceDisplay atkDisplay = null, defDisplay = null;
        if (clashFx && showDiceDisplay)
        {
            atkDisplay = ClashDiceDisplay.Show(attacker, atkCard.data);
            if (defCard != null) defDisplay = ClashDiceDisplay.Show(defender, defCard.data);
        }

        // [사용시] 효과: 주사위를 굴리기 전에 양쪽 책장 모두 발동 (공격자 먼저)
        nextDieBonus.Clear();
        destroyNextDie.Clear();
        TriggerCardUse(attacker, defender, atkCard.data);
        if (defCard != null) TriggerCardUse(defender, attacker, defCard.data);

        for (int i = 0; i < count; i++)
        {
            if (!attacker.IsAlive || !defender.IsAlive) break;

            yield return new WaitForSeconds(clashFx ? 0.3f : 0.5f);   // 주사위 하나당 연출 딜레이

            bool atkHas = atkDice != null && i < atkDice.Count;
            bool defHas = defDice != null && i < defDice.Count;

            DiceData atkDie = atkHas ? atkDice[i] : null;
            DiceData defDie = defHas ? defDice[i] : null;

            // [상대의 다음 주사위 파괴]: 굴리기 전에 사라진다 (없는 주사위 = 자동 패배)
            atkDie = TryDestroyDie(attacker, atkDie);
            defDie = TryDestroyDie(defender, defDie);

            // 출혈: 공격 주사위를 굴리려는 순간 피해 (출혈로 쓰러지면 주사위를 굴리지 못한다)
            TriggerBleedIfAttack(attacker, atkDie);
            TriggerBleedIfAttack(defender, defDie);
            if (!attacker.IsAlive || !defender.IsAlive) break;

            // 주사위 굴림 (없는 쪽은 자동 패배). bonus = 상성·힘·허약 등으로 더해진 값 (머리 위 주사위에 표시)
            int atkBonus = 0, defBonus = 0;
            int atkRoll = atkDie != null ? ComputeRoll(attacker, atkDie, defDie, out atkBonus) : -1;
            int defRoll = defDie != null ? ComputeRoll(defender, defDie, atkDie, out defBonus) : -1;

            string atkLabel = atkDie != null ? $"{atkDie.diceDetail} {atkRoll}" : "-";
            string defLabel = defDie != null ? $"{defDie.diceDetail} {defRoll}" : "-";
            Log($"주사위 {i + 1}: {attacker.unitName}[{atkLabel}] vs {defender.unitName}[{defLabel}]");

            // 연출: 머리 위 주사위가 동시에 굴러가다 결과 값에 멈춘다
            if (atkDisplay != null || defDisplay != null)
            {
                Coroutine atkSpin = atkDisplay != null
                    ? StartCoroutine(atkDisplay.RollRoutine(i, atkDie, atkRoll, atkBonus, diceRollTime)) : null;
                Coroutine defSpin = defDisplay != null
                    ? StartCoroutine(defDisplay.RollRoutine(i, defDie, defRoll, defBonus, diceRollTime)) : null;
                if (atkSpin != null) yield return atkSpin;
                if (defSpin != null) yield return defSpin;
            }

            // 연출: 양쪽이 서로 부딪히고, 굴림 값이 머리 위에 뜬다
            if (clashFx)
                yield return StartCoroutine(ClashImpactRoutine(attacker, atkDie, atkRoll, atkBonus,
                                                              defender, defDie, defRoll, defBonus));

            // 연출: 이긴 주사위는 번쩍, 진 주사위는 흔들리며 흐려진다 (아래 결과 연출과 동시에)
            ShowDiceResult(atkDisplay, atkRoll, defRoll);
            ShowDiceResult(defDisplay, defRoll, atkRoll);

            if (atkRoll == defRoll)
            {
                // 동점은 양쪽 모두 무효 — 팽팽하게 밀린다
                Log("팽팽하게 밀렸다 — 양쪽 주사위가 함께 무효화된다!");
                if (clashFx) yield return StartCoroutine(TieFxRoutine(attacker, defender));
            }
            else if (atkRoll > defRoll)
            {
                bool hit = ApplyDie(attacker, defender, atkDie, atkRoll);
                ResolveDieEffects(attacker, atkDie, defender, defDie, hit);
                if (clashFx) yield return StartCoroutine(OutcomeFxRoutine(attacker, defender, atkDie, atkRoll));
            }
            else
            {
                bool hit = ApplyDie(defender, attacker, defDie, defRoll);
                ResolveDieEffects(defender, defDie, attacker, atkDie, hit);
                if (clashFx) yield return StartCoroutine(OutcomeFxRoutine(defender, attacker, defDie, defRoll));
            }
        }

        nextDieBonus.Clear();
        destroyNextDie.Clear();

        // 연출: 주사위 표시를 걷고, 원래 자리로 복귀, 카메라 해제
        if (atkDisplay != null) atkDisplay.Hide();
        if (defDisplay != null) defDisplay.Hide();
        if (stepped) yield return StartCoroutine(attacker.MoveToRoutine(attackerHome, 0.25f));
        if (BattleCamera.Instance != null) BattleCamera.Instance.Release();
    }

    private void ShowDiceResult(ClashDiceDisplay display, int myRoll, int theirRoll)
    {
        if (display == null) return;

        ClashDiceDisplay.Result result = myRoll == theirRoll ? ClashDiceDisplay.Result.Tie
                                       : myRoll > theirRoll ? ClashDiceDisplay.Result.Win
                                       : ClashDiceDisplay.Result.Lose;
        StartCoroutine(display.ResultRoutine(result));
    }

    private int RollBase(DiceData die)
    {
        // int 오버로드는 상한 미포함이므로 +1 (최댓값도 나오도록)
        return Random.Range(die.minRoll, die.maxRoll + 1);
    }

    /// <summary>
    /// 주사위를 굴리고 보정을 적용한다. bonus = 기본 굴림에 더해진 총합 (음수 가능).
    /// 순서: 마비(최솟값 고정) → 힘/허약/인내 → [다음 주사위 위력 +n] → 상성/회피 보정
    /// </summary>
    private int ComputeRoll(Unit self, DiceData mine, DiceData theirs, out int bonus)
    {
        bonus = 0;
        int roll = RollBase(mine);

        if (self.ConsumeParalysis())
        {
            roll = mine.minRoll;
            Log($"  → {self.unitName}은(는) 마비되어 주사위가 최솟값({roll})으로 굴러간다!");
        }

        if (mine.diceType == DiceType.Attack)
            bonus += self.GetStatus(StatusType.Strength) - self.GetStatus(StatusType.Feeble);
        else
            bonus += self.GetStatus(StatusType.Endurance);

        if (nextDieBonus.TryGetValue(self, out int power) && power > 0)
        {
            bonus += power;
            nextDieBonus.Remove(self);
        }

        if (theirs != null)
        {
            // 공격 대 공격: 상성 순환(참격>관통>타격>참격) 우위 보정
            if (mine.diceType == DiceType.Attack && theirs.diceType == DiceType.Attack &&
                advantageOver.TryGetValue(mine.diceDetail, out DiceDetail beaten) &&
                theirs.diceDetail == beaten)
            {
                bonus += typeAdvantageBonus;
                Log($"  → {mine.diceDetail}은(는) {theirs.diceDetail}에 강하다! 굴림 +{typeAdvantageBonus}");
            }

            // 회피: 상대 공격 주사위를 상대로는 굴림 보정
            if (mine.diceDetail == DiceDetail.Evade && theirs.diceType == DiceType.Attack)
            {
                bonus += typeAdvantageBonus;
                Log($"  → 회피가 공격을 겨냥한다! 굴림 +{typeAdvantageBonus}");
            }
        }

        return Mathf.Max(roll + bonus, 0);
    }

    /// <summary>파괴 예약이 걸린 유닛이면 이번 주사위를 없앤다 (null 반환).</summary>
    private DiceData TryDestroyDie(Unit owner, DiceData die)
    {
        if (die == null || !destroyNextDie.Remove(owner)) return die;

        Log($"  → {owner.unitName}의 {DiceInfo.Name(die.diceDetail)} 주사위가 부서졌다!");
        if (clashFx) DamagePopup.Spawn(HeadPosition(owner) + Vector3.up * 0.8f, "주사위 파괴!", statusDebuffColor, 1f);
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
                lastDamage = damage;
                lastResistance = res;

                target.TakeDamage(damage);
                AddEmotion(2);
                string resNote = res == Resistance.Normal ? ""
                    : $" ({ResistanceSet.Label(res)} x{ResistanceSet.Multiplier(res):0.0})";
                Log($"{source.unitName}의 {die.diceDetail} 주사위 → {target.unitName}에게 피해 {damage}!{resNote}");

                // 공격 주사위로 합을 이기면 기력이 조금 회복된다 — 밀어붙일수록 몰아치는 규칙
                if (source.isPlayer)
                {
                    CurrentKi = Mathf.Min(CurrentKi + 1, maxKi);
                    UpdateHUD();
                }
                return true;

            case DiceType.Defense:
                // 기본 규칙: 수비 주사위가 합을 이기면 상대 주사위를 막는다(피해 없음).
                Log($"{source.unitName}의 {die.diceDetail} 주사위가 상대 주사위를 막았다!");
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
        }
        if (loseDie != null)
            TriggerDiceEffects(loser, winner, loseDie, DiceTrigger.OnClashLose);
    }

    /// <summary>책장의 모든 주사위에 붙은 [사용시] 효과를 발동한다.</summary>
    private void TriggerCardUse(Unit self, Unit opponent, CardData card)
    {
        if (card == null || card.diceList == null) return;
        foreach (DiceData die in card.diceList)
        {
            if (die != null) TriggerDiceEffects(self, opponent, die, DiceTrigger.OnUse);
        }
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
            case DiceEffectType.Bleed:
                if (!opponent.IsAlive) return;
                opponent.AddBleed(fx.amount, fx.nextTurn);
                Log($"  {fx.Describe()} → {opponent.unitName}");
                if (clashFx)
                    DamagePopup.Spawn(HeadPosition(opponent) + Vector3.up * 0.8f,
                        fx.nextTurn ? $"출혈 +{fx.amount} (다음 턴)" : $"출혈 +{fx.amount}", bleedPopupColor, 0.8f);
                break;

            case DiceEffectType.RecoverKi:
                if (!self.isPlayer) return;   // 기력은 플레이어 자원
                CurrentKi = Mathf.Min(CurrentKi + fx.amount, maxKi);
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
                if (clashFx) StatusPopup(self, $"다음 주사위 +{fx.amount}", statusBuffColor);
                break;

            case DiceEffectType.DestroyNextDie:
                if (!opponent.IsAlive) return;
                destroyNextDie.Add(opponent);
                Log($"  {fx.Describe()} → {opponent.unitName}");
                break;
        }
    }

    private void ApplyStatus(Unit unit, StatusType type, DiceEffect fx, Color color)
    {
        if (!unit.IsAlive) return;
        unit.AddStatus(type, fx.amount, fx.nextTurn);
        Log($"  {fx.Describe()} → {unit.unitName}");
        if (clashFx)
            StatusPopup(unit, fx.nextTurn ? $"{DiceInfo.StatusName(type)} +{fx.amount} (다음 턴)"
                                          : $"{DiceInfo.StatusName(type)} +{fx.amount}", color);
    }

    private void StatusPopup(Unit unit, string text, Color color)
    {
        DamagePopup.Spawn(HeadPosition(unit) + Vector3.up * 0.8f, text, color, 0.8f);
    }

    /// <summary>공격 주사위를 굴리려는 유닛에게 출혈 피해를 준다.</summary>
    private void TriggerBleedIfAttack(Unit unit, DiceData die)
    {
        if (die == null || die.diceType != DiceType.Attack || unit.Bleed <= 0) return;

        int damage = unit.TriggerBleed();
        if (damage <= 0) return;

        Log($"{unit.unitName}의 상처에서 피가 흐른다! 출혈 피해 {damage} (남은 출혈 {unit.Bleed})");
        if (clashFx)
            DamagePopup.Spawn(HeadPosition(unit) + Vector3.up * 0.4f, $"출혈 -{damage}", bleedPopupColor, 1.1f);
    }

    // ─────────────────────────── 합 연출 (Clash FX) ───────────────────────────

    private static readonly Color attackPopupColor = new Color(1f, 0.55f, 0.45f);
    private static readonly Color defensePopupColor = new Color(0.55f, 0.8f, 1f);
    private static readonly Color boostedPopupColor = new Color(1f, 0.85f, 0.2f);
    private static readonly Color damagePopupColor = new Color(1f, 0.2f, 0.15f);
    private static readonly Color bleedPopupColor = new Color(0.85f, 0.1f, 0.25f);
    private static readonly Color healPopupColor = new Color(0.45f, 1f, 0.55f);
    private static readonly Color statusBuffColor = new Color(1f, 0.8f, 0.35f);
    private static readonly Color statusDebuffColor = new Color(0.75f, 0.55f, 1f);

    /// <summary>주사위 한 쌍이 부딪히는 순간: 굴림 값 표시 → 양쪽 돌진 → 중간 지점에 불꽃.</summary>
    private IEnumerator ClashImpactRoutine(Unit a, DiceData aDie, int aRoll, int aBonus,
                                           Unit b, DiceData bDie, int bRoll, int bBonus)
    {
        // 머리 위 주사위 표시를 쓰면 숫자는 거기서 보이므로 팝업은 생략
        if (!showDiceDisplay)
        {
            ShowRollPopup(a, aDie, aRoll, aBonus);
            ShowRollPopup(b, bDie, bRoll, bBonus);
        }

        // 주사위가 있는 쪽만 상대를 향해 튀어나간다 (캐릭터 모델이면 주사위 종류 애니메이션도 재생)
        Coroutine aLunge = null, bLunge = null;
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

        // 돌진이 완전히 끝나 제자리로 돌아올 때까지 기다린다 (다음 움직임과 겹치면 위치가 어긋남)
        if (aLunge != null) yield return aLunge;
        if (bLunge != null) yield return bLunge;
    }

    /// <summary>합에서 이긴 주사위의 결과 연출 (공격 = 피해·넉백·흔들림, 수비 = 막음/회피 표시).</summary>
    private IEnumerator OutcomeFxRoutine(Unit winner, Unit loser, DiceData die, int roll)
    {
        if (die.diceType == DiceType.Attack)
        {
            loser.PlayAnim("Hit");
            int damage = lastDamage;   // ApplyDie에서 내성까지 적용된 실제 피해
            DamagePopup.Spawn(HeadPosition(loser) + Vector3.up * 0.4f, $"-{damage}", damagePopupColor, 1.5f);
            if (lastResistance != Resistance.Normal)
            {
                Color resColor;
                ColorUtility.TryParseHtmlString(ResistanceSet.ColorHex(lastResistance), out resColor);
                DamagePopup.Spawn(HeadPosition(loser) + Vector3.up * 0.4f,
                    $"{ResistanceSet.Label(lastResistance)}! x{ResistanceSet.Multiplier(lastResistance):0.0}", resColor, 1f);
            }
            ClashSpark.Burst(loser.transform.position + Vector3.up * 0.8f, damagePopupColor, 24, 5f);
            Coroutine knockback = StartCoroutine(loser.KnockbackRoutine(winner.transform.position, knockbackDistance, 0.4f));

            bool bigHit = damage >= bigHitThreshold;
            StageBackdrop.Impact(bigHit ? 1f : 0.45f);   // 타격 임팩트로 배경이 반응 (태엽 감김 / 가로등 깜빡임)
            if (BattleCamera.Instance != null)
                BattleCamera.Instance.Shake(bigHit ? 0.35f : 0.12f, bigHit ? 0.35f : 0.2f);

            if (bigHit) yield return StartCoroutine(HitStopRoutine(hitStopTime));
            yield return knockback;
        }
        else
        {
            StageBackdrop.Impact(0.2f);
            string label = die.diceDetail == DiceDetail.Evade ? "회피!" : "막음!";
            DamagePopup.Spawn(HeadPosition(winner) + Vector3.up * 0.4f, label, defensePopupColor, 1.2f);
            ClashSpark.Burst(winner.transform.position + Vector3.up * 0.8f, defensePopupColor, 10, 2.5f);
            yield return StartCoroutine(loser.KnockbackRoutine(winner.transform.position, knockbackDistance * 0.4f, 0.3f));
        }
    }

    /// <summary>동점: 양쪽이 함께 튕겨난다.</summary>
    private IEnumerator TieFxRoutine(Unit a, Unit b)
    {
        Vector3 mid = (a.transform.position + b.transform.position) * 0.5f + Vector3.up * 1.2f;
        DamagePopup.Spawn(mid, "팽팽!", Color.white, 1.3f);
        ClashSpark.Burst(mid, Color.white, 20, 4f);

        Coroutine aBack = StartCoroutine(a.KnockbackRoutine(b.transform.position, knockbackDistance * 0.6f, 0.35f));
        Coroutine bBack = StartCoroutine(b.KnockbackRoutine(a.transform.position, knockbackDistance * 0.6f, 0.35f));
        if (BattleCamera.Instance != null) BattleCamera.Instance.Shake(0.1f, 0.15f);
        StageBackdrop.Impact(0.3f);

        yield return aBack;
        yield return bBack;
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

    // ─────────────────────────── 감정 단계 ───────────────────────────

    private void AddEmotion(int amount)
    {
        emotionPoints += amount;
        while (EmotionLevel < 5 && emotionPoints >= (EmotionLevel + 1) * 10)
        {
            EmotionLevel++;
            Log($"감정 단계 상승! LV.{EmotionLevel}");

            // 연출: 감정이 오를수록 무대의 빛이 물든다
            if (StageLighting.Instance != null)
                StageLighting.Instance.SetEmotion(EmotionLevel);
            StageBackdrop.SetTempo(EmotionLevel);   // 감정이 오를수록 배경이 고조된다
        }
        UpdateHUD();
    }

    // ─────────────────────────── 승패 판정 ───────────────────────────

    private bool CheckBattleEnd()
    {
        if (Phase == BattlePhase.End) return true;

        if (!playerUnit.IsAlive) { EndBattle(false); return true; }

        bool allDead = enemies.Count == 0;   // 적을 안 배치한 무대는 자동 클리어 처리
        foreach (Unit e in enemies)
        {
            if (e.IsAlive) { allDead = false; break; }
        }

        if (allDead) { EndBattle(true); return true; }
        return false;
    }

    private void EndBattle(bool victory)
    {
        if (Phase == BattlePhase.End) return;
        Phase = BattlePhase.End;
        Time.timeScale = 1f;   // 히트스톱 도중에 끝나도 시간이 멈춘 채로 남지 않게
        UpdateHUD();

        if (BattleCamera.Instance != null) BattleCamera.Instance.Release();

        if (victory)
        {
            Log("── 무대 클리어! ──");
            GameManager.Instance.OnBattleWon();
        }
        else
        {
            Log("── 패배... ──");
            GameManager.Instance.OnBattleLost();
        }
    }

    // ─────────────────────────── 적 생성 ───────────────────────────

    private void SpawnEnemies()
    {
        // 이전 무대의 유닛 정리
        foreach (Unit e in enemies)
        {
            if (e != null) Destroy(e.gameObject);
        }
        enemies.Clear();

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

                SpriteCharacter sc = go.AddComponent<SpriteCharacter>();
                Color tint = info.enemy.sprite != null ? Color.white : info.enemy.tintColor;
                sc.Setup(info.enemy.sprite, info.enemy.spriteHeight, tint);   // Unit을 붙이기 전에 외형 완성
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

            unit.Init(info.enemy.enemyName, info.enemy.maxHp, info.enemy.deck, info.enemy.resistance);
            unit.CaptureHome();
            unit.OnDead += HandleUnitDead;
            enemies.Add(unit);
        }

        if (enemies.Count == 0)
            Debug.LogWarning("무대에 배치된 적이 없습니다! Battle Stage 에셋에서 적을 추가하세요.");
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

    /// <summary>플레이어와 적 머리 위에 HP/출혈 표시를 붙인다 (이미 있으면 건너뜀).</summary>
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
    }

    // ─────────────────────────── 기타 유틸 ───────────────────────────

    private CardInstance GetRandomEnemyCard(Unit enemy)
    {
        if (enemy.personalDeck == null || enemy.personalDeck.Count == 0) return null;
        CardData data = enemy.personalDeck[Random.Range(0, enemy.personalDeck.Count)];
        return new CardInstance(data);
    }

    /// <summary>공격자가 응답자 바로 앞에 설 위치를 계산. 충분히 가까우면 이동하지 않음.</summary>
    private bool TryGetStandPosition(Unit attacker, Unit defender, out Vector3 standPos)
    {
        standPos = attacker.transform.position;

        Vector3 toDefender = defender.transform.position - attacker.transform.position;
        toDefender.y = 0f;

        float dist = toDefender.magnitude;
        if (dist < 0.01f) return false;

        Vector3 dir = toDefender / dist;
        standPos = defender.transform.position - dir * 1.2f;
        standPos.y = attacker.transform.position.y;

        return dist > 2.0f;
    }

    private void CancelPendingCard()
    {
        if (pendingCard != null && pendingCard.view != null)
            pendingCard.view.SetSelected(false);
        pendingCard = null;
    }

    private void UpdateHUD()
    {
        if (kiText != null) kiText.text = $"기력 {CurrentKi}";

        // 감정: 단계 + 다음 단계까지 진행도 (10pt마다 1단계, 최대 5)
        int levelStart = EmotionLevel * 10;
        float progress = EmotionLevel >= 5 ? 1f : Mathf.Clamp01((emotionPoints - levelStart) / 10f);
        if (emotionText != null)
            emotionText.text = EmotionLevel >= 5 ? "감정 LV.5 (최대)" : $"감정 LV.{EmotionLevel}  {emotionPoints - levelStart}/10";
        if (emotionGauge != null) emotionGauge.fillAmount = progress;

        if (phaseText != null)
        {
            switch (Phase)
            {
                case BattlePhase.PlayerTurn: phaseText.text = "<color=#7fd4ff>내 턴</color>"; break;
                case BattlePhase.EnemyTurn:  phaseText.text = "<color=#ff8a6b>적의 턴</color>"; break;
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
        string line = message.StartsWith("──") ? $"<color=#e8c27a>{message}</color>" : message;
        logLines.Insert(0, line);                          // 최신 로그가 위로 (아래로 스크롤하면 이전 기록)
        while (logLines.Count > Mathf.Max(1, maxLogLines)) logLines.RemoveAt(logLines.Count - 1);
        battleLogText.text = string.Join("\n", logLines);
    }
}
