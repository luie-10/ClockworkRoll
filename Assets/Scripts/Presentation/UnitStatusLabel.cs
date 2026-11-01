using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 유닛 머리 위의 이름표 + 체력바 (+ 녹 · 상태이상).
///
///   ┌─────────────────┐
///   │  골목 쥐         │  ← 이름 (아군 하늘색 / 적 주황)
///   │ ████████░░ 24/30 │  ← 체력바 (초록→노랑→빨강) + 깎인 만큼 흰 잔상이 천천히 줄어듦
///   │  녹 2 (+1) 힘 1  │  ← 상태이상이 있을 때만 (괄호 = 다음 턴)
///   └─────────────────┘
/// 어두운 상자를 뒤에 깔아서 어떤 배경에서도 잘 보인다.
/// 직접 붙일 필요 없음 — BattleManager가 전투 시작 때 UnitStatusLabel.AttachTo()로 붙인다.
/// </summary>
public class UnitStatusLabel : MonoBehaviour
{
    public Unit target;
    public float heightOffset = 0.3f;

    private const float Width = 1.5f;
    private const float BarWidth = 1.28f;
    private const float BarHeight = 0.13f;

    private SpriteRenderer box, barBg, barTrail, barFill;
    private TextMesh nameText, hpText, statusText, intentTitle, intentDice;
    private float shownRatio = 1f;    // 체력바 (즉시)
    private float trailRatio = 1f;    // 흰 잔상 (천천히 따라감)
    private string lastStatus;
    private int lastHp = -1;
    private bool hasIntent, hasStatus, lastTargeted;
    private static readonly Color boxColor = new Color(0.03f, 0.03f, 0.04f, 0.82f);

    private static readonly Dictionary<Unit, UnitStatusLabel> labels = new Dictionary<Unit, UnitStatusLabel>();
    private static bool allVisible = true;

    /// <summary>
    /// 상태창 맨 위에 장착 태엽판을 표시한다.
    /// 적: "3~4시 ▶ 물어뜯기" + "참1~4 타1~4"
    /// </summary>
    public void SetIntent(string title, CardData card)
    {
        if (card == null) { ClearIntent(); return; }
        hasIntent = true;
        intentTitle.text = $"<color=#e8c27a>{title} ▶</color> {card.cardName}";
        intentDice.text = DiceInfo.Summary(card);
        intentTitle.gameObject.SetActive(true);
        intentDice.gameObject.SetActive(true);
        RefreshBox();
    }

    public void ClearIntent()
    {
        hasIntent = false;
        intentTitle.gameObject.SetActive(false);
        intentDice.gameObject.SetActive(false);
        RefreshBox();
    }

    public static UnitStatusLabel AttachTo(Unit unit)
    {
        if (unit == null) return null;
        if (labels.TryGetValue(unit, out UnitStatusLabel existing) && existing != null) return existing;

        UnitStatusLabel label = new GameObject("StatusLabel " + unit.unitName).AddComponent<UnitStatusLabel>();
        label.target = unit;
        label.Build();
        label.gameObject.SetActive(allVisible);
        labels[unit] = label;
        return label;
    }

    /// <summary>모든 이름표를 켜고 끈다 (스토리 중에는 BattleHUD가 끈다).</summary>
    public static void SetAllVisible(bool visible)
    {
        allVisible = visible;
        foreach (UnitStatusLabel l in labels.Values)
        {
            if (l != null) l.gameObject.SetActive(visible);
        }
    }

    private void Build()
    {
        Color nameColor = target.isPlayer ? new Color(0.55f, 0.85f, 1f) : new Color(1f, 0.62f, 0.45f);

        box = ProceduralSprites.WorldBox(transform, "Box", new Vector2(Width, 0.5f), boxColor, 20);
        // 별명 + 종류 (예: "찍찍이 골목 쥐" — 종류는 작고 흐리게)
        string number = target.slotNumber > 0 ? $"<color=#ffd159>[{target.slotNumber}]</color> " : "";   // 숫자 키로 대상 지정
        string display = !string.IsNullOrEmpty(target.kindName) && target.kindName != target.unitName
            ? $"{number}{target.unitName} <size=44><color=#c8c8c8>{target.kindName}</color></size>"
            : number + target.unitName;
        nameText = NewText("Name", display, new Vector3(0f, 0.11f, -0.01f), 0.024f, nameColor, 24);

        barBg = NewBar("BarBg", new Color(0.12f, 0.02f, 0.02f, 1f), 21);
        barTrail = NewBar("BarTrail", new Color(1f, 1f, 1f, 0.85f), 22);
        barFill = NewBar("BarFill", new Color(0.3f, 0.85f, 0.35f), 23);
        hpText = NewText("Hp", "", new Vector3(0f, -0.1f, -0.02f), 0.017f, Color.white, 25);

        statusText = NewText("Status", "", new Vector3(0f, -0.3f, -0.01f), 0.018f, Color.white, 24);
        statusText.gameObject.SetActive(false);

        // 장착 태엽판 (상자 위쪽 두 줄) — 처음엔 숨김
        intentTitle = NewText("IntentTitle", "", new Vector3(0f, 0.47f, -0.01f), 0.019f, Color.white, 24);
        intentDice = NewText("IntentDice", "", new Vector3(0f, 0.32f, -0.01f), 0.018f, Color.white, 24);
        intentTitle.gameObject.SetActive(false);
        intentDice.gameObject.SetActive(false);

        foreach (SpriteRenderer bar in new[] { barBg, barTrail, barFill })
            bar.transform.localPosition = new Vector3(-BarWidth * 0.5f, -0.1f, -0.005f);
        barBg.transform.localScale = new Vector3(BarWidth, BarHeight, 1f);
    }

    void LateUpdate()
    {
        if (target == null) { Destroy(gameObject); return; }   // 유닛이 사라지면(다음 무대) 같이 사라짐

        // 위치: 유닛 머리 위, 카메라를 정면으로
        Vector3 pos = target.transform.position + Vector3.up * 2.2f;
        if (target.bodyRenderer != null)
        {
            Bounds b = target.bodyRenderer.bounds;
            pos = new Vector3(b.center.x, b.max.y + heightOffset, b.center.z);
        }
        transform.position = pos;

        Camera cam = Camera.main;
        if (cam != null) transform.rotation = cam.transform.rotation;

        UpdateBar();
        UpdateStatus();
        UpdateTargeted();
        RefreshBox();   // 글자 폭이 바뀐 직후 메시 크기를 다시 재서 상자를 맞춘다
    }

    /// <summary>지금 노리는 적이면 상자가 황동색으로 물든다</summary>
    private void UpdateTargeted()
    {
        BattleManager bm = BattleManager.Instance;
        bool targeted = bm != null && !target.isPlayer && bm.FocusTarget == target && target.IsAlive;
        if (targeted == lastTargeted) return;
        lastTargeted = targeted;
        box.color = targeted ? new Color(0.32f, 0.22f, 0.05f, 0.92f) : boxColor;
    }

    private void UpdateBar()
    {
        float ratio = target.MaxHp > 0 ? Mathf.Clamp01((float)target.Hp / target.MaxHp) : 0f;
        shownRatio = ratio;
        // 잔상: 줄어들 땐 천천히, 회복할 땐 즉시
        trailRatio = ratio > trailRatio ? ratio : Mathf.MoveTowards(trailRatio, ratio, Time.deltaTime * 0.6f);

        barFill.transform.localScale = new Vector3(BarWidth * shownRatio, BarHeight, 1f);
        barTrail.transform.localScale = new Vector3(BarWidth * trailRatio, BarHeight, 1f);

        // 체력 비율에 따라 초록 → 노랑 → 빨강
        barFill.color = ratio > 0.5f
            ? Color.Lerp(new Color(1f, 0.85f, 0.2f), new Color(0.3f, 0.85f, 0.35f), (ratio - 0.5f) * 2f)
            : Color.Lerp(new Color(0.95f, 0.2f, 0.15f), new Color(1f, 0.85f, 0.2f), ratio * 2f);

        if (target.Hp != lastHp)
        {
            lastHp = target.Hp;
            hpText.text = target.IsAlive ? $"{target.Hp} / {target.MaxHp}" : "쓰러짐";
        }
    }

    private void UpdateStatus()
    {
        // 녹 + 힘/인내/허약/취약/마비 한 줄 (색 글자, 괄호 = 다음 턴에 들어올 수치)
        string status = target.IsAlive ? DiceInfo.StatusLine(target) : "";

        if (status == lastStatus) return;
        lastStatus = status;

        hasStatus = status.Length > 0;
        statusText.gameObject.SetActive(hasStatus);
        statusText.text = status;
        RefreshBox();
    }

    /// <summary>
    /// 상자 크기: 기본(이름+체력) 위아래 ±0.25, 다음 행동 줄이 있으면 위로 0.33, 상태이상 줄이 있으면 아래로 0.2 늘린다.
    /// 장착 태엽판 글자가 길면 상자 폭도 넓힌다.
    /// </summary>
    private void RefreshBox()
    {
        float top = 0.25f + (hasIntent ? 0.33f : 0f);
        float bottom = -0.25f - (hasStatus ? 0.2f : 0f);

        float width = Mathf.Max(Width, nameText.GetComponent<MeshRenderer>().bounds.size.x + 0.24f);   // 긴 별명도 상자 안에
        if (hasIntent)
        {
            float textWidth = Mathf.Max(intentTitle.GetComponent<MeshRenderer>().bounds.size.x,
                                        intentDice.GetComponent<MeshRenderer>().bounds.size.x);
            width = Mathf.Max(width, textWidth + 0.24f);
        }
        if (hasStatus)
            width = Mathf.Max(width, statusText.GetComponent<MeshRenderer>().bounds.size.x + 0.24f);

        box.size = new Vector2(width, top - bottom);
        box.transform.localPosition = new Vector3(0f, (top + bottom) * 0.5f, 0f);
    }

    private SpriteRenderer NewBar(string name, Color color, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = ProceduralSprites.Square();   // 피벗이 왼쪽이라 X 스케일로 바 길이 조절
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    private TextMesh NewText(string name, string text, Vector3 localPos, float charSize, Color color, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;

        TextMesh tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontSize = 64;
        tm.characterSize = charSize;
        tm.fontStyle = FontStyle.Bold;
        tm.color = color;

        Font font = DamagePopup.GetBuiltinFont();
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        if (font != null)
        {
            tm.font = font;
            mr.sharedMaterial = font.material;
        }
        mr.sortingOrder = order;
        return tm;
    }

    void OnDestroy()
    {
        if (target != null && labels.TryGetValue(target, out UnitStatusLabel l) && l == this)
            labels.Remove(target);
    }
}
