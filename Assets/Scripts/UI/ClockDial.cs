using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 전투 화면 오른쪽의 시계판 (1시~12시).
///
///            12
///        11  ●  1         ● 칸 위쪽 점 = 그 칸의 적 주사위 / 아래쪽 점 = 내 주사위 (시계판 한 칸 = 주사위 하나)
///      10    │    2       점 색 = 주사위 종류, 글자 = 참/관/타/가/회
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

    /// <summary>마우스가 올라가 있는 칸 (없으면 0). 숫자 키로 그 칸의 내 행동 대상을 바꿀 때 사용</summary>
    public static int HoveredHour { get { return instance != null ? instance.hoveredHour : 0; } }

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
        AddSideRing(c.enemyDot, enemyColor);   // 테두리 색 = 누구의 주사위인가 (빨강 = 적)
        c.enemyTag = UIBuilder.Label("Tag", c.enemyDot.transform, "", 15, Color.black, TextAnchor.MiddleCenter, false);
        UIBuilder.Stretch(c.enemyTag.rectTransform);
        c.enemyTag.raycastTarget = false;

        c.playerDot = NewImage("Player", t, ProceduralSprites.Circle(), playerColor);
        c.playerDot.rectTransform.sizeDelta = new Vector2(24f, 24f);
        c.playerDot.rectTransform.anchoredPosition = new Vector2(0f, -22f);
        AddSideRing(c.playerDot, playerColor);   // 파랑 = 나
        c.playerTag = UIBuilder.Label("Tag", c.playerDot.transform, "", 15, Color.black, TextAnchor.MiddleCenter, false);
        UIBuilder.Stretch(c.playerTag.rectTransform);
        c.playerTag.raycastTarget = false;
        return c;
    }

    private static void AddSideRing(Image dot, Color color)
    {
        Outline ring = dot.gameObject.AddComponent<Outline>();
        ring.effectColor = color;
        ring.effectDistance = new Vector2(2.5f, -2.5f);
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
            bool past = bm.CurrentHour > 0 && h < bm.CurrentHour;   // 이미 지나간 칸

            // 칸마다 그 칸에 배정된 주사위 하나: 색 = 주사위 종류, 글자 = 참/관/타/가/회
            DrawDie(c.enemyDot, c.enemyTag, enemy != null ? enemy.DieAt(h) : null, enemy != null && (past || !enemy.owner.IsAlive), enemy != null && enemy.hour == h);

            bool inPreview = pending != null && hoveredHour > 0 && h >= hoveredHour && h < hoveredHour + previewLength;
            if (inPreview && mine == null)
            {
                // 미리보기: 고른 태엽판의 주사위가 이 칸에 어떻게 놓이는지 반투명하게
                DiceData ghost = pending.diceList != null && h - hoveredHour < pending.diceList.Count ? pending.diceList[h - hoveredHour] : null;
                DrawDie(c.playerDot, c.playerTag, ghost, false, h == hoveredHour);
                if (ghost != null) c.playerDot.color = new Color(c.playerDot.color.r, c.playerDot.color.g, c.playerDot.color.b, 0.55f);
            }
            else
            {
                DrawDie(c.playerDot, c.playerTag, mine != null ? mine.DieAt(h) : null, mine != null && past, mine != null && mine.hour == h);
            }

            bool clash = enemy != null && mine != null;
            c.edge.enabled = clash || h == bm.CurrentHour;
            c.edge.effectColor = h == bm.CurrentHour ? Color.white : clashColor;

            c.bg.color = inPreview ? (previewOk ? validColor : invalidColor)
                       : h == bm.CurrentHour ? new Color(0.3f, 0.26f, 0.16f, 0.98f)
                       : cellColor;
            c.bg.rectTransform.localScale = Vector3.one * (h == bm.CurrentHour ? 1.18f : 1f);
            c.hourText.color = (enemy != null || mine != null || inPreview) ? new Color(1f, 1f, 1f, 0.3f) : new Color(0.9f, 0.85f, 0.75f);
        }

        centerText.text = bm.Phase == BattleManager.BattlePhase.EnemyTurn
            ? $"<b>{bm.CurrentHour}시</b>"
            : pending != null ? $"<color=#7fd4ff>{pending.cardName}</color>\n{previewLength}칸 · 주사위 {previewLength}개" : "계획";

        infoText.text = hoveredHour > 0 ? DescribeHour(bm, hoveredHour)
                      : "<color=#a8a8a8>칸에 마우스 = 그 칸의 주사위 (한 칸 = 주사위 하나) · 내 칸 위에서 숫자 키 = 그 행동의 대상 변경</color>";
    }

    /// <summary>
    /// 칸의 주사위 점. 색 = 주사위 종류(참격 빨강 · 관통 주황 · 타격 노랑 · 가드 파랑 · 회피 초록), 글자 = 종류 한 글자.
    /// 행동의 첫 칸은 조금 크게, 지나간 칸은 회색.
    /// </summary>
    private static void DrawDie(Image dot, Text tag, DiceData die, bool spent, bool first)
    {
        dot.enabled = die != null;
        tag.text = "";
        if (die == null) return;

        Color typeColor;
        ColorUtility.TryParseHtmlString(DiceInfo.ColorHex(die.diceDetail), out typeColor);
        dot.color = spent ? new Color(0.45f, 0.45f, 0.45f, 0.6f) : typeColor;
        dot.rectTransform.sizeDelta = Vector2.one * (first ? 28f : 24f);
        tag.text = DiceInfo.Glyph(die.diceDetail);
    }

    private static TimedAction FindCovering(System.Collections.Generic.IReadOnlyList<TimedAction> list, int hour)
    {
        foreach (TimedAction a in list)
        {
            if (a.Covers(hour)) return a;
        }
        return null;
    }

    /// <summary>"골목 쥐 [물어뜯기] 타1~4" 처럼 그 칸의 주사위 하나를 설명</summary>
    private static string DescribeDie(TimedAction a, int hour, string hex)
    {
        DiceData die = a.DieAt(hour);
        string dieText = die != null
            ? $"<color={DiceInfo.ColorHex(die.diceDetail)}>{DiceInfo.Name(die.diceDetail)} {die.minRoll}~{die.maxRoll}</color>"
            : "-";
        string aim = a.owner.isPlayer && a.target != null ? $" <color=#ffd159>→ [{a.target.slotNumber}] {a.target.unitName}</color>" : "";
        return $"<color={hex}>{a.owner.unitName} [{a.card.data.cardName} {hour - a.hour + 1}/{a.length}]</color> {dieText}{aim}";
    }

    private static string DescribeHour(BattleManager bm, int hour)
    {
        string s = $"<b>{hour}시</b>  ";
        System.Collections.Generic.List<string> foes = new System.Collections.Generic.List<string>();
        foreach (TimedAction a in bm.EnemyActions)
        {
            if (a.Covers(hour) && a.owner.IsAlive) foes.Add(DescribeDie(a, hour, "#ff6b54"));
        }
        TimedAction mine = FindCovering(bm.PlayerActions, hour);

        if (foes.Count == 0 && mine == null) return s + "<color=#a8a8a8>비어 있음</color>";
        s += string.Join("  /  ", foes);
        if (foes.Count > 0 && mine != null) s += "\n<color=#ffd159>↕ 합</color>  ";
        if (mine != null) s += DescribeDie(mine, hour, "#66bfff");
        else if (foes.Count > 0) s += "\n<color=#a8a8a8>내 주사위 없음 — 공격이면 그대로 맞는다</color>";
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
