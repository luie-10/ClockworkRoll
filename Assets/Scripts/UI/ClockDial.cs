using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 전투 화면 오른쪽의 시계판 (1시~12시).
///
///            12
///        11  ●  1         ● 칸 위쪽 빨간 점 = 적 행동이 차지한 칸 (시작 칸에는 적 이름 첫 글자)
///      10    │    2       ● 칸 아래쪽 파란 점 = 내 행동이 차지한 칸 (시작 칸에는 태엽판 이름 첫 글자)
///      9     ┼    3       금색 테두리 = 둘이 겹침 → 합이 일어나는 칸
///      8          4       바늘 = 진행 중인 시각
///        7   6  5
///   ┌──────────────────────┐
///   │ 마우스를 올린 칸 설명  │
///   └──────────────────────┘
///
/// - 태엽판을 고른 채 칸에 마우스를 올리면 놓일 자리가 초록(가능)/빨강(불가)으로 미리 보인다.
/// - 칸 클릭: 고른 태엽판 배치 / 아무것도 안 골랐으면 그 칸의 내 행동 회수.
/// 직접 붙일 필요 없음 — BattleManager가 전투 시작 때 ClockDial.Ensure()로 전투 HUD 안에 만든다.
/// </summary>
public class ClockDial : MonoBehaviour
{
    private const int Hours = BattleManager.ClockHours;
    private const float Radius = 148f;
    private const float CellSize = 64f;

    private static ClockDial instance;

    private readonly Cell[] cells = new Cell[Hours + 1];   // 1~12 사용
    private RectTransform hand;
    private Text centerText, infoText;
    private int hoveredHour;
    private int drawnVersion = -1, drawnHover = -1, drawnHour = -1;
    private CardData drawnPending;

    private static readonly Color cellColor = new Color(0.07f, 0.07f, 0.09f, 0.95f);
    private static readonly Color enemyColor = new Color(1f, 0.42f, 0.33f);
    private static readonly Color playerColor = new Color(0.4f, 0.75f, 1f);
    private static readonly Color clashColor = new Color(1f, 0.82f, 0.35f);
    private static readonly Color validColor = new Color(0.3f, 0.85f, 0.4f, 0.95f);
    private static readonly Color invalidColor = new Color(0.8f, 0.2f, 0.2f, 0.95f);

    private class Cell
    {
        public Image bg, enemyDot, playerDot;
        public Outline edge;
        public Text hourText, enemyTag, playerTag;
    }

    /// <summary>시계판이 없으면 만든다 (전투 HUD가 있으면 그 안에 → 스토리 중엔 같이 숨겨짐).</summary>
    public static void Ensure()
    {
        if (instance != null) return;

        Transform parent;
        BattleHUD hud = FindAnyObjectByType<BattleHUD>();
        if (hud != null)
        {
            parent = hud.transform;
        }
        else
        {
            GameObject canvasGO = new GameObject("ClockDialCanvas");
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();
            parent = canvasGO.transform;
        }

        RectTransform root = UIBuilder.Rect("ClockDial", parent);
        // 자원 패널(우상단) 바로 아래, 턴 종료 버튼 위
        UIBuilder.Place(root, Vector2.one, Vector2.one, Vector2.one, new Vector2(-20f, -200f), new Vector2(400f, 470f));
        instance = root.gameObject.AddComponent<ClockDial>();
        instance.Build(root);
    }

    private void Build(RectTransform root)
    {
        Image panel = UIBuilder.Panel("Panel", root);
        UIBuilder.Stretch(panel.rectTransform);
        panel.raycastTarget = false;

        // 시계 면 (위쪽 400x400)
        RectTransform face = UIBuilder.Rect("Face", root);
        UIBuilder.Place(face, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -200f), new Vector2(400f, 400f));

        Image disc = NewImage("Disc", face, ProceduralSprites.Circle(), new Color(0.12f, 0.1f, 0.07f, 0.85f));
        disc.rectTransform.sizeDelta = new Vector2(Radius * 2f + CellSize, Radius * 2f + CellSize);
        Image rim = NewImage("Rim", face, ProceduralSprites.Circle(), new Color(0.75f, 0.6f, 0.35f, 0.5f));
        rim.rectTransform.sizeDelta = new Vector2(Radius * 2f - CellSize + 8f, Radius * 2f - CellSize + 8f);
        Image inner = NewImage("Inner", face, ProceduralSprites.Circle(), new Color(0.05f, 0.05f, 0.06f, 0.92f));
        inner.rectTransform.sizeDelta = new Vector2(Radius * 2f - CellSize, Radius * 2f - CellSize);

        // 바늘 (가운데에서 위로 뻗은 막대, Z 회전으로 시각을 가리킴)
        Image handImg = NewImage("Hand", face, null, new Color(0.95f, 0.85f, 0.6f, 0.95f));
        hand = handImg.rectTransform;
        hand.pivot = new Vector2(0.5f, 0f);
        hand.sizeDelta = new Vector2(6f, Radius - CellSize * 0.5f - 6f);
        Image hub = NewImage("Hub", face, ProceduralSprites.Circle(), new Color(0.95f, 0.85f, 0.6f, 1f));
        hub.rectTransform.sizeDelta = new Vector2(18f, 18f);

        centerText = UIBuilder.Label("Center", face, "", 20, Color.white, TextAnchor.MiddleCenter);
        centerText.rectTransform.sizeDelta = new Vector2(170f, 90f);
        centerText.rectTransform.anchoredPosition = new Vector2(0f, -48f);
        centerText.raycastTarget = false;

        for (int h = 1; h <= Hours; h++) cells[h] = BuildCell(face, h);

        // 아래쪽 설명 줄
        infoText = UIBuilder.Label("Info", root, "", 18, Color.white, TextAnchor.MiddleCenter);
        UIBuilder.Place(infoText.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(-20f, 62f));
        infoText.raycastTarget = false;
        infoText.horizontalOverflow = HorizontalWrapMode.Wrap;
        infoText.resizeTextForBestFit = true;
        infoText.resizeTextMinSize = 12;
        infoText.resizeTextMaxSize = 18;
    }

    private Cell BuildCell(RectTransform face, int hour)
    {
        float angle = (90f - hour * 30f) * Mathf.Deg2Rad;   // 12시가 위, 시계 방향
        Vector2 pos = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Radius;

        Cell c = new Cell();
        c.bg = NewImage("Hour" + hour, face, ProceduralSprites.Circle(), cellColor);
        c.bg.raycastTarget = true;
        c.bg.rectTransform.sizeDelta = new Vector2(CellSize, CellSize);
        c.bg.rectTransform.anchoredPosition = pos;
        c.edge = c.bg.gameObject.AddComponent<Outline>();
        c.edge.effectDistance = new Vector2(3f, -3f);
        c.edge.enabled = false;

        ClockDialCell hit = c.bg.gameObject.AddComponent<ClockDialCell>();
        hit.dial = this;
        hit.hour = hour;

        Transform t = c.bg.transform;
        c.hourText = UIBuilder.Label("Num", t, hour.ToString(), 24, new Color(0.9f, 0.85f, 0.75f), TextAnchor.MiddleCenter);
        UIBuilder.Stretch(c.hourText.rectTransform);
        c.hourText.raycastTarget = false;

        c.enemyDot = NewImage("Enemy", t, ProceduralSprites.Circle(), enemyColor);
        c.enemyDot.rectTransform.sizeDelta = new Vector2(24f, 24f);
        c.enemyDot.rectTransform.anchoredPosition = new Vector2(0f, 22f);
        c.enemyTag = UIBuilder.Label("Tag", c.enemyDot.transform, "", 15, Color.black, TextAnchor.MiddleCenter, false);
        UIBuilder.Stretch(c.enemyTag.rectTransform);
        c.enemyTag.raycastTarget = false;

        c.playerDot = NewImage("Player", t, ProceduralSprites.Circle(), playerColor);
        c.playerDot.rectTransform.sizeDelta = new Vector2(24f, 24f);
        c.playerDot.rectTransform.anchoredPosition = new Vector2(0f, -22f);
        c.playerTag = UIBuilder.Label("Tag", c.playerDot.transform, "", 15, Color.black, TextAnchor.MiddleCenter, false);
        UIBuilder.Stretch(c.playerTag.rectTransform);
        c.playerTag.raycastTarget = false;
        return c;
    }

    private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        RectTransform rt = UIBuilder.Rect(name, parent);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    // ─────────────────────────── 입력 (ClockDialCell이 호출) ───────────────────────────

    public void OnCellEnter(int hour) { hoveredHour = hour; }

    public void OnCellExit(int hour) { if (hoveredHour == hour) hoveredHour = 0; }

    public void OnCellClick(int hour)
    {
        if (BattleManager.Instance != null) BattleManager.Instance.OnHourClicked(hour);
    }

    // ─────────────────────────── 그리기 ───────────────────────────

    void Update()
    {
        BattleManager bm = BattleManager.Instance;
        if (bm == null) return;

        // 바늘: 진행 중이면 그 시각, 계획 중이면 12시
        float targetAngle = -(bm.CurrentHour % Hours) * 30f;
        float z = Mathf.MoveTowardsAngle(hand.localEulerAngles.z, targetAngle, Time.unscaledDeltaTime * 360f);
        hand.localEulerAngles = new Vector3(0f, 0f, z);

        // 바뀐 게 있을 때만 다시 칠한다
        if (bm.TimelineVersion == drawnVersion && hoveredHour == drawnHover &&
            bm.CurrentHour == drawnHour && bm.PendingCard == drawnPending) return;
        drawnVersion = bm.TimelineVersion;
        drawnHover = hoveredHour;
        drawnHour = bm.CurrentHour;
        drawnPending = bm.PendingCard;

        Redraw(bm);
    }

    private void Redraw(BattleManager bm)
    {
        // 배치 미리보기: 고른 태엽판이 마우스를 올린 칸부터 몇 칸을 차지하는가
        CardData pending = bm.PendingCard;
        int previewLength = pending != null ? TimedAction.LengthOf(pending) : 0;
        bool previewOk = pending != null && hoveredHour > 0 && bm.CanPlace(hoveredHour, previewLength);

        for (int h = 1; h <= Hours; h++)
        {
            Cell c = cells[h];
            TimedAction enemy = FindCovering(bm.EnemyActions, h);
            TimedAction mine = FindCovering(bm.PlayerActions, h);

            DrawDot(c.enemyDot, c.enemyTag, enemy, h, enemy != null ? Initial(enemy.owner.unitName) : "");
            DrawDot(c.playerDot, c.playerTag, mine, h, mine != null ? Initial(mine.card.data.cardName) : "");

            bool clash = enemy != null && mine != null;
            c.edge.enabled = clash || h == bm.CurrentHour;
            c.edge.effectColor = h == bm.CurrentHour ? Color.white : clashColor;

            bool inPreview = pending != null && hoveredHour > 0 && h >= hoveredHour && h < hoveredHour + previewLength;
            c.bg.color = inPreview ? (previewOk ? validColor : invalidColor)
                       : h == bm.CurrentHour ? new Color(0.3f, 0.26f, 0.16f, 0.98f)
                       : cellColor;
            c.bg.rectTransform.localScale = Vector3.one * (h == bm.CurrentHour ? 1.18f : 1f);
            c.hourText.color = (enemy != null || mine != null) ? new Color(1f, 1f, 1f, 0.35f) : new Color(0.9f, 0.85f, 0.75f);
        }

        centerText.text = bm.Phase == BattleManager.BattlePhase.EnemyTurn
            ? $"<b>{bm.CurrentHour}시</b>"
            : pending != null ? $"<color=#7fd4ff>{pending.cardName}</color>\n{previewLength}칸" : "계획";

        infoText.text = hoveredHour > 0 ? DescribeHour(bm, hoveredHour)
                      : "<color=#a8a8a8>칸에 마우스를 올리면 그 시각의 행동이 보인다</color>";
    }

    /// <summary>행동이 차지한 칸이면 점을 켠다. 시작 칸은 진하게 + 글자, 이어지는 칸은 작고 흐리게.</summary>
    private static void DrawDot(Image dot, Text tag, TimedAction action, int hour, string label)
    {
        dot.enabled = action != null;
        tag.text = "";
        if (action == null) return;

        bool start = action.hour == hour;
        Color baseColor = action.owner.isPlayer ? playerColor : enemyColor;
        dot.color = action.resolved ? new Color(0.45f, 0.45f, 0.45f, 0.6f)
                  : start ? baseColor : new Color(baseColor.r, baseColor.g, baseColor.b, 0.45f);
        dot.rectTransform.sizeDelta = Vector2.one * (start ? 26f : 14f);
        if (start) tag.text = label;
    }

    private static TimedAction FindCovering(System.Collections.Generic.IReadOnlyList<TimedAction> list, int hour)
    {
        foreach (TimedAction a in list)
        {
            if (a.Covers(hour)) return a;
        }
        return null;
    }

    private static string Initial(string s)
    {
        return string.IsNullOrEmpty(s) ? "?" : s.Substring(0, 1);
    }

    private static string DescribeHour(BattleManager bm, int hour)
    {
        string s = $"<b>{hour}시</b>  ";
        TimedAction enemy = FindCovering(bm.EnemyActions, hour);
        TimedAction mine = FindCovering(bm.PlayerActions, hour);

        if (enemy == null && mine == null) return s + "<color=#a8a8a8>비어 있음</color>";
        if (enemy != null)
            s += $"<color=#ff6b54>{enemy.owner.unitName} [{enemy.card.data.cardName}] {enemy.HourLabel()}</color> {DiceInfo.Summary(enemy.card.data)}";
        if (enemy != null && mine != null) s += "\n<color=#ffd159>↕ 합</color>  ";
        if (mine != null)
            s += $"<color=#66bfff>{mine.owner.unitName} [{mine.card.data.cardName}] {mine.HourLabel()}</color> {DiceInfo.Summary(mine.card.data)}";
        return s;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}

/// <summary>시계판의 칸 하나 — 마우스 올림/클릭을 ClockDial에 전달한다.</summary>
public class ClockDialCell : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public ClockDial dial;
    public int hour;

    public void OnPointerEnter(PointerEventData eventData) { dial.OnCellEnter(hour); }
    public void OnPointerExit(PointerEventData eventData) { dial.OnCellExit(hour); }
    public void OnPointerClick(PointerEventData eventData) { dial.OnCellClick(hour); }
}
