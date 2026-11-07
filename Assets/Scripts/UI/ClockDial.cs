using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 시계판 (1시~12시). 평소엔 손패 뒤에 작게 숨어 있고, 카드를 고르거나 시계를 누르면 화면 가운데로 커져 나온다.
/// 그동안 주변은 뿌옇게 흐려지고, 바깥을 누르면(또는 Esc) 다시 카드 뒤로 돌아간다. 카드를 놓아도 돌아간다.
/// 시계가 진행되는 동안에는 왼쪽 위로 올라가 바늘이 천천히 도는 모습을 보여준다.
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

    // 카드 뒤 자리(평소) ↔ 화면 가운데(집중)
    private const float DockY = 335f, DockScale = 0.62f, FocusScale = 1.65f, CornerScale = 0.85f, MoveTime = 0.28f;
    private const float BlurAlpha = 0.95f;

    /// <summary>화면 가운데로 나와 있는가</summary>
    public static bool IsOpen { get { return instance != null && instance.open; } }

    /// <summary>시계가 진행 중이라 왼쪽 위로 올라가 있는가</summary>
    public static bool IsRunning { get { return instance != null && instance.running; } }

    private bool open, running, bodyHovered;
    private float focus;   // 0 = 카드 뒤, 1 = 화면 가운데
    private float corner;  // 0 = 카드 뒤, 1 = 왼쪽 위 (시계가 진행될 때)
    private Canvas sortCanvas;
    private Text dockHint;
    private GameObject overlay;
    private RawImage blur;
    private RenderTexture rtFull, rtHalf, rtSmall;

    /// <summary>시계를 화면 가운데로 꺼낸다 (카드를 고르거나 시계를 누를 때)</summary>
    public static void Open() { if (instance != null) instance.SetOpen(true); }

    /// <summary>시계를 카드 뒤로 돌려보낸다</summary>
    public static void Close() { if (instance != null) instance.SetOpen(false); }

    private void SetOpen(bool value)
    {
        if (open == value) return;
        open = value;
        for (int h = 1; h <= Hours; h++) cells[h].bg.raycastTarget = open;   // 카드 뒤에서는 칸을 못 누른다
        if (!open) hoveredHour = 0;
        infoText.gameObject.SetActive(open);
    }

    public void OnBodyEnter() { bodyHovered = true; }
    public void OnBodyExit() { bodyHovered = false; }
    public void OnBodyClick() { if (!open && !running) SetOpen(true); }

    /// <summary>시계 바깥을 눌렀다 — 고르던 태엽판은 취소하고 카드 뒤로</summary>
    public void OnOutsideClick()
    {
        if (BattleManager.Instance != null) BattleManager.Instance.CancelSelection();
        SetOpen(false);
    }

    private void Animate()
    {
        bool inBattle = GameManager.Instance == null || GameManager.Instance.State == GameManager.GameState.Battle;
        if (open && !inBattle) SetOpen(false);
        if (open && Input.GetKeyDown(KeyCode.Escape)) OnOutsideClick();

        focus = Mathf.MoveTowards(focus, open ? 1f : 0f, Time.unscaledDeltaTime / MoveTime);
        float t = focus * focus * (3f - 2f * focus);

        BattleManager bm = BattleManager.Instance;
        running = bm != null && bm.Phase == BattleManager.BattlePhase.EnemyTurn;
        if (running && open) SetOpen(false);
        corner = Mathf.MoveTowards(corner, running ? 1f : 0f, Time.unscaledDeltaTime / MoveTime);
        float c = corner * corner * (3f - 2f * corner);
        dockHint.gameObject.SetActive(!open && !running);

        // 움직이는 동안에는 흐린 막보다 위에, 카드 뒤로 돌아가면 손패보다 아래에
        bool onTop = focus > 0f;
        if (sortCanvas.overrideSorting != onTop)
        {
            sortCanvas.overrideSorting = onTop;
            if (onTop) sortCanvas.sortingOrder = 120;
        }

        RectTransform rt = (RectTransform)transform;
        Vector2 screen = rt.parent != null ? ((RectTransform)rt.parent).rect.size : new Vector2(1920f, 1080f);
        Vector2 home = new Vector2(0f, Mathf.Lerp(DockY, screen.y * 0.5f + 50f, t));
        Vector2 topLeft = new Vector2(-screen.x * 0.5f + 175f, screen.y - 170f);   // 시계가 도는 동안의 자리
        rt.anchoredPosition = Vector2.Lerp(home, topLeft, c);
        float dock = DockScale * (bodyHovered && !open ? 1.08f : 1f);
        rt.localScale = Vector3.one * Mathf.Lerp(Mathf.Lerp(dock, FocusScale, t), CornerScale, c);

        overlay.SetActive(focus > 0f);
        if (focus > 0f)
        {
            blur.color = new Color(0.6f, 0.64f, 0.72f, BlurAlpha * t);
            blur.raycastTarget = open;
        }
    }

    void LateUpdate()
    {
        if (focus > 0f) RefreshBlur();
    }

    /// <summary>무대를 작게 찍어 여러 번 줄인 뒤 크게 펼친다 → 뿌옇게 번진 배경 (셰이더 없이)</summary>
    private void RefreshBlur()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        RenderTexture before = cam.targetTexture;
        cam.targetTexture = rtFull;
        cam.Render();
        cam.targetTexture = before;
        Graphics.Blit(rtFull, rtHalf);
        Graphics.Blit(rtHalf, rtSmall);
    }

    private static RenderTexture NewTarget(int w, int h, int depth)
    {
        RenderTexture rt = new RenderTexture(w, h, depth) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        rt.Create();
        return rt;
    }

    /// <summary>시계 뒤에 깔리는 흐린 막 (전체 화면, 누르면 닫힘)</summary>
    private void BuildFocusOverlay()
    {
        sortCanvas = gameObject.AddComponent<Canvas>();
        gameObject.AddComponent<GraphicRaycaster>();   // 중첩 Canvas는 자기 Raycaster가 있어야 클릭된다

        rtFull = NewTarget(480, 270, 16);
        rtHalf = NewTarget(240, 135, 0);
        rtSmall = NewTarget(120, 68, 0);

        overlay = new GameObject("ClockFocusCanvas");
        Canvas canvas = overlay.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 110;   // 전투 UI 위, 시계(120) 아래, 정보창(490~) 아래
        overlay.AddComponent<GraphicRaycaster>();

        RectTransform rt = UIBuilder.Rect("Blur", overlay.transform);
        UIBuilder.Stretch(rt);
        blur = rt.gameObject.AddComponent<RawImage>();
        blur.texture = rtSmall;
        rt.gameObject.AddComponent<ClockDialOutside>().dial = this;
        overlay.SetActive(false);

        for (int h = 1; h <= Hours; h++) cells[h].bg.raycastTarget = false;
        infoText.gameObject.SetActive(false);
    }

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
        // 축 = 시계 면의 가운데 (커지고 움직여도 시계 중심 기준). 위치는 Animate가 매 프레임 잡는다
        UIBuilder.Place(root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 270f / 470f), new Vector2(0f, DockY), new Vector2(400f, 470f));
        root.localScale = Vector3.one * DockScale;

        // 손패 바로 뒤에 놓는다 (카드가 시계를 덮는다)
        Transform handPanel = parent.Find("HandPanel");
        if (handPanel != null) root.SetSiblingIndex(handPanel.GetSiblingIndex());

        instance = root.gameObject.AddComponent<ClockDial>();
        instance.Build(root);
        instance.BuildFocusOverlay();
    }

    private void Build(RectTransform root)
    {
        // 시계 면 (위쪽 400x400)
        RectTransform face = UIBuilder.Rect("Face", root);
        UIBuilder.Place(face, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -200f), new Vector2(400f, 400f));

        Image disc = NewImage("Disc", face, ProceduralSprites.Circle(), new Color(0.09f, 0.08f, 0.07f, 0.9f));
        disc.rectTransform.sizeDelta = new Vector2(Radius * 2f + CellSize, Radius * 2f + CellSize);
        disc.raycastTarget = true;   // 카드 뒤에 있을 때 누르는 곳
        disc.gameObject.AddComponent<ClockDialBody>().dial = this;
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
        UIBuilder.Place(infoText.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(-36f, 60f));
        infoText.raycastTarget = false;
        infoText.horizontalOverflow = HorizontalWrapMode.Wrap;
        infoText.resizeTextForBestFit = true;
        infoText.resizeTextMinSize = 12;
        infoText.resizeTextMaxSize = 18;
        infoText.color = UITheme.TextMuted;

        // 카드 뒤에 있을 때 시계 위에 뜨는 안내
        dockHint = UIBuilder.Label("DockHint", root, "눌러서 시계판 보기", 26, UITheme.Brass, TextAnchor.MiddleCenter);
        UIBuilder.Place(dockHint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, -8f), new Vector2(400f, 40f));
        dockHint.raycastTarget = false;
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
        Animate();

        BattleManager bm = BattleManager.Instance;
        if (bm == null) return;

        // 바늘: 진행 중이면 그 시각, 계획 중이면 12시
        float targetAngle = -(bm.CurrentHour % Hours) * 30f;
        float z = Mathf.MoveTowardsAngle(hand.localEulerAngles.z, targetAngle, Time.unscaledDeltaTime * 110f);   // 바늘이 칸을 천천히 훑는다
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
            : pending != null ? $"<color=#7fd4ff>{pending.cardName}</color>\n{previewLength}칸" : "계획";

        infoText.text = hoveredHour > 0 ? DescribeHour(bm, hoveredHour)
                      : "<color=#a8a8a8>칸에 마우스 = 그 칸의 내용 · 내 칸 위에서 숫자 키 = 그 행동의 대상 변경\n바깥을 누르면 카드 뒤로 돌아간다</color>";
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
        else if (foes.Count > 0) s += "\n<color=#a8a8a8>내 칸 없음 — 공격이면 그대로 맞는다</color>";
        return s;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        if (overlay != null) Destroy(overlay);
        foreach (RenderTexture rt in new[] { rtFull, rtHalf, rtSmall }) if (rt != null) rt.Release();
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

/// <summary>시계판 몸통 — 카드 뒤에 있을 때 누르면 화면 가운데로 꺼낸다.</summary>
public class ClockDialBody : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public ClockDial dial;

    public void OnPointerEnter(PointerEventData eventData) { dial.OnBodyEnter(); }
    public void OnPointerExit(PointerEventData eventData) { dial.OnBodyExit(); }
    public void OnPointerClick(PointerEventData eventData) { dial.OnBodyClick(); }
}

/// <summary>흐린 막 — 시계 바깥을 누르면 닫는다.</summary>
public class ClockDialOutside : MonoBehaviour, IPointerClickHandler
{
    public ClockDial dial;

    public void OnPointerClick(PointerEventData eventData) { dial.OnOutsideClick(); }
}
