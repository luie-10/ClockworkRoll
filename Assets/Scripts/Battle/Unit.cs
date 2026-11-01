using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>라운드 단위 상태이상 (녹은 따로 — 누적되고 주사위를 굴릴 때마다 벗겨지는 방식이라)</summary>
public enum StatusType
{
    Strength,   // 힘: 공격 주사위 위력 +n
    Endurance,  // 인내: 수비 주사위 위력 +n
    Feeble,     // 허약: 공격 주사위 위력 -n
    Fragile,    // 취약: 공격 주사위에 맞을 때 피해 +n
    Paralysis   // 마비: 주사위 n개가 최솟값으로 굴러감 (굴릴 때마다 1씩 줄어듦)
}

/// <summary>
/// 씬에 배치되는 3D 전투 유닛 (플레이어 / 적 공용).
/// 클릭 판정을 위해 Collider 가 반드시 필요하다.
/// </summary>
public class Unit : MonoBehaviour
{
    public string unitName = "이름 없는 유닛";
    public bool isPlayer = false;

    [SerializeField] private int maxHp = 50;

    [Header("피격 연출")]
    public Renderer bodyRenderer;   // 비워두면 자식의 Renderer를 자동으로 찾음
    public Color hitColor = new Color(0.9f, 0.2f, 0.2f);
    public Color deadColor = new Color(0.3f, 0.3f, 0.3f);

    [Header("애니메이션 (선택 — 캐릭터 모델용, 비워두면 자식에서 자동 탐색)")]
    public Animator animator;

    [Header("내성 (참격/관통/타격 공격을 맞을 때 피해 배율: 보통 x1.0 / 약점 x1.5 / 취약 x2.0)")]
    public ResistanceSet resistance = new ResistanceSet();

    public int MaxHp { get { return maxHp; } }
    public int Hp { get; private set; }
    public bool IsAlive { get { return Hp > 0; } }

    /// <summary>적 AI가 사용하는 태엽판(카드) 목록 — SpawnEnemies에서 EnemyData로부터 복사됨</summary>
    public List<CardData> personalDeck = new List<CardData>();

    [Tooltip("적: 한 라운드(시계판 한 바퀴)에 하는 행동 수")]
    public int actionsPerRound = 1;

    public event Action<Unit, int> OnDamaged;   // (피격 유닛, 피해량)
    public event Action<Unit> OnDead;

    private Color originalColor = Color.white;

    void Awake()
    {
        Hp = maxHp;

        if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<Renderer>();
        if (bodyRenderer != null) originalColor = GetBodyColor();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    /// <summary>유닛 초기화 (전투 시작 시 BattleManager가 호출)</summary>
    public void Init(string newName, int newMaxHp, List<CardData> deck = null, ResistanceSet newResistance = null)
    {
        unitName = newName;
        maxHp = newMaxHp;
        Hp = newMaxHp;
        personalDeck = deck != null ? new List<CardData>(deck) : new List<CardData>();
        if (newResistance != null) resistance = newResistance.Clone();
        ClearStatus();

        if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<Renderer>();
        if (bodyRenderer != null) SetBodyColor(originalColor);

        SpriteCharacter sc = GetComponent<SpriteCharacter>();
        if (sc != null) sc.ResetPose();   // 이전 무대에서 쓰러진 자세 등이 남지 않게
    }

    public void RestoreFullHp()
    {
        Hp = maxHp;
    }

    /// <summary>공격 주사위 피해에 내성 배율을 적용한 최종 피해 (반올림).</summary>
    public int ApplyResistance(int rawDamage, DiceDetail attackType, out Resistance applied)
    {
        applied = resistance != null ? resistance.Get(attackType) : Resistance.Normal;
        return Mathf.RoundToInt(rawDamage * ResistanceSet.Multiplier(applied));
    }

    public void TakeDamage(int amount)
    {
        if (!IsAlive || amount <= 0) return;

        Hp = Mathf.Max(0, Hp - amount);
        if (OnDamaged != null) OnDamaged(this, amount);

        StartCoroutine(HitFlashRoutine());

        if (Hp <= 0)
        {
            Die();
            if (OnDead != null) OnDead(this);
        }
    }

    // ─────────────────────────── 상태이상 (녹) ───────────────────────────

    /// <summary>
    /// 현재 녹 수치 — 굴리는 주사위의 최댓값이 이만큼 깎인다 (최솟값 아래로는 안 내려감).
    /// 주사위를 하나 굴릴 때마다 녹이 1씩 벗겨진다 (녹 3 → 다음 주사위들 -3, -2, -1).
    /// </summary>
    public int Rust { get; private set; }

    /// <summary>다음 라운드에 들어올 녹 ([적중시] 다음 턴 녹 n)</summary>
    public int PendingRust { get; private set; }

    public void AddRust(int amount, bool nextTurn)
    {
        if (!IsAlive || amount <= 0) return;

        if (nextTurn) PendingRust += amount;
        else Rust += amount;
    }

    /// <summary>녹이 반영된 주사위 최댓값</summary>
    public int RustedMax(DiceData die)
    {
        return Mathf.Max(die.minRoll, die.maxRoll - Rust);
    }

    /// <summary>주사위를 굴린 뒤 호출 — 녹이 1 벗겨진다.</summary>
    public void WearRust()
    {
        if (Rust > 0) Rust--;
    }

    /// <summary>새 라운드 시작 — 지난 라운드의 힘/허약 등은 사라지고, 대기 중이던 녹·상태이상이 들어온다.</summary>
    public void ActivatePendingStatus()
    {
        Rust += PendingRust;
        PendingRust = 0;

        for (int i = 0; i < status.Length; i++)
        {
            status[i] = pendingStatus[i];
            pendingStatus[i] = 0;
        }
    }

    public void ClearStatus()
    {
        Rust = 0;
        PendingRust = 0;
        Array.Clear(status, 0, status.Length);
        Array.Clear(pendingStatus, 0, pendingStatus.Length);
    }

    // ─────────────────────────── 상태이상 (힘 · 인내 · 허약 · 취약 · 마비) ───────────────────────────
    // 이번 턴 동안만 유지된다 (다음 턴이 시작되면 사라짐). "다음 턴 n"은 pending에 쌓였다가 다음 턴에 들어온다.

    private readonly int[] status = new int[Enum.GetValues(typeof(StatusType)).Length];
    private readonly int[] pendingStatus = new int[Enum.GetValues(typeof(StatusType)).Length];

    public int GetStatus(StatusType type) { return status[(int)type]; }
    public int GetPendingStatus(StatusType type) { return pendingStatus[(int)type]; }

    public void AddStatus(StatusType type, int amount, bool nextTurn)
    {
        if (!IsAlive || amount <= 0) return;

        if (nextTurn) pendingStatus[(int)type] += amount;
        else status[(int)type] += amount;
    }

    /// <summary>주사위를 굴릴 때 호출. 마비가 있으면 1 소모하고 true (= 이 주사위는 최솟값).</summary>
    public bool ConsumeParalysis()
    {
        if (status[(int)StatusType.Paralysis] <= 0) return false;
        status[(int)StatusType.Paralysis]--;
        return true;
    }

    /// <summary>체력 회복 (최대 체력까지). 실제 회복량을 돌려준다.</summary>
    public int Heal(int amount)
    {
        if (!IsAlive || amount <= 0) return 0;
        int before = Hp;
        Hp = Mathf.Min(maxHp, Hp + amount);
        return Hp - before;
    }

    // ─────────────────────────── 이동 연출 ───────────────────────────

    /// <summary>전투 시작 시 기본 위치를 기록 (합이 끝나면 이 자리로 복귀)</summary>
    public void CaptureHome()
    {
        HomePosition = transform.position;
    }

    public Vector3 HomePosition { get; private set; }

    /// <summary>target 위치로 duration초 만큼 부드럽게 이동 (코루틴으로 사용)</summary>
    public IEnumerator MoveToRoutine(Vector3 target, float duration)
    {
        if (duration <= 0f)
        {
            transform.position = target;
            yield break;
        }

        Vector3 start = transform.position;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t / duration));
            yield return null;
        }

        transform.position = target;
    }

    // ─────────────────────────── 합 연출 ───────────────────────────

    /// <summary>toward 방향으로 짧게 돌진했다가 제자리로 돌아온다 (주사위가 부딪히는 순간).</summary>
    public IEnumerator LungeRoutine(Vector3 toward, float distance, float duration)
    {
        Vector3 dir = toward - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) yield break;

        Vector3 start = transform.position;
        Vector3 peak = start + dir.normalized * distance;

        yield return MoveToRoutine(peak, duration * 0.4f);   // 빠르게 나가고
        yield return MoveToRoutine(start, duration * 0.6f);  // 조금 천천히 복귀
    }

    /// <summary>from 반대쪽으로 밀려났다가 제자리로 돌아온다 (합에서 졌을 때).</summary>
    public IEnumerator KnockbackRoutine(Vector3 from, float distance, float duration)
    {
        Vector3 dir = transform.position - from;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) yield break;

        Vector3 start = transform.position;
        Vector3 pushed = start + dir.normalized * distance;

        yield return MoveToRoutine(pushed, duration * 0.25f);
        yield return MoveToRoutine(start, duration * 0.75f);
    }

    /// <summary>
    /// 캐릭터 모델용 애니메이션 트리거. Animator가 없거나 해당 트리거가 없으면 조용히 무시한다.
    /// (나중에 Mixamo 등 캐릭터를 넣으면 Animator에 Slash/Thrust/Blunt/Guard/Evade/Hit 트리거를 만들면 된다)
    /// </summary>
    public void PlayAnim(string trigger)
    {
        // 2.5D 스프라이트: 포즈 그림 교체 (그림이 없으면 기울이는 임시 동작)
        SpriteCharacter sc = GetComponent<SpriteCharacter>();
        if (sc != null) sc.SetPose(trigger);

        if (animator == null || animator.runtimeAnimatorController == null) return;

        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Trigger && p.name == trigger)
            {
                animator.SetTrigger(trigger);
                return;
            }
        }
    }

    /// <summary>2.5D 스프라이트 캐릭터면 worldPoint 쪽으로 좌우를 돌린다 (3D 모델/큐브면 아무 일 없음).</summary>
    public void FaceTowards(Vector3 worldPoint)
    {
        SpriteCharacter sc = GetComponent<SpriteCharacter>();
        if (sc != null) sc.Face(worldPoint);
    }

    // ─────────────────────────── 내부 처리 ───────────────────────────

    // 스프라이트는 SpriteRenderer.color, 3D 모델은 material.color로 색을 바꾼다
    private Color GetBodyColor()
    {
        SpriteRenderer sr = bodyRenderer as SpriteRenderer;
        return sr != null ? sr.color : bodyRenderer.material.color;
    }

    private void SetBodyColor(Color color)
    {
        SpriteRenderer sr = bodyRenderer as SpriteRenderer;
        if (sr != null) sr.color = color;
        else bodyRenderer.material.color = color;
    }

    private IEnumerator HitFlashRoutine()
    {
        if (bodyRenderer == null) yield break;

        SetBodyColor(hitColor);
        yield return new WaitForSeconds(0.12f);

        if (IsAlive && bodyRenderer != null)
            SetBodyColor(originalColor);
    }

    private void Die()
    {
        PlayAnim("Down");   // 쓰러지는 포즈 (스프라이트) / Down 트리거 (Animator)

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;   // 시체 클릭 방지

        if (bodyRenderer != null)
        {
            // 스프라이트는 원래 색을 어둡게 + 반투명 (그림이 그대로 보이게), 3D는 회색
            if (bodyRenderer is SpriteRenderer)
            {
                Color c = originalColor * deadColor;
                c.a = 0.6f;
                SetBodyColor(c);
            }
            else SetBodyColor(deadColor);
        }
    }

    // 유닛을 마우스로 클릭 → BattleManager에 전달 (카드 사용 대상 지정)
    void OnMouseDown()
    {
        // UI 위에서의 클릭은 무시 (손패 카드 클릭이 유닛 클릭으로 이어지는 것을 방지)
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            return;

        if (BattleManager.Instance != null)
            BattleManager.Instance.OnUnitClicked(this);
    }

    // 마우스를 올리면 이름 / 체력 / 내성 / 장착 태엽판 정보창
    void OnMouseEnter()
    {
        UnitTooltip.Show(this);
    }

    void OnMouseExit()
    {
        UnitTooltip.Hide(this);
    }

    void OnDisable()
    {
        UnitTooltip.Hide(this);
    }
}
