using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    /// <summary>적 AI가 사용하는 책장(카드) 목록 — SpawnEnemies에서 EnemyData로부터 복사됨</summary>
    public List<CardData> personalDeck = new List<CardData>();

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

    // ─────────────────────────── 상태이상 (출혈) ───────────────────────────

    /// <summary>현재 출혈 수치 — 공격 주사위를 굴릴 때마다 이만큼 피해를 받고 2/3로 줄어든다.</summary>
    public int Bleed { get; private set; }

    /// <summary>다음 턴에 들어올 출혈 ([적중시] 다음 턴 출혈 n)</summary>
    public int PendingBleed { get; private set; }

    public void AddBleed(int amount, bool nextTurn)
    {
        if (!IsAlive || amount <= 0) return;

        if (nextTurn) PendingBleed += amount;
        else Bleed += amount;
    }

    /// <summary>새 턴 시작 — 대기 중이던 상태이상을 실제로 적용한다.</summary>
    public void ActivatePendingStatus()
    {
        Bleed += PendingBleed;
        PendingBleed = 0;
    }

    /// <summary>공격 주사위를 굴릴 때 호출. 출혈 피해를 주고 수치를 2/3로 줄인다. 실제 피해량을 돌려준다.</summary>
    public int TriggerBleed()
    {
        if (!IsAlive || Bleed <= 0) return 0;

        int damage = Bleed;
        Bleed = Bleed * 2 / 3;   // 정수 나눗셈 = 소수점 버림 (1 → 0, 2 → 1, 3 → 2)
        TakeDamage(damage);
        return damage;
    }

    public void ClearStatus()
    {
        Bleed = 0;
        PendingBleed = 0;
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

    // 마우스를 올리면 이름 / 체력 / 내성 / 장착 책장 정보창
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
