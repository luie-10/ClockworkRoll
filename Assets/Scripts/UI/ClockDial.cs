using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 시계판 (1시~12시). 평소엔 손패 뒤에 작게 숨어 있고, 카드를 고르거나 시계를 누르면 화면 가운데로 커져 나온다.
/// 그동안 주변은 뿌옇게 흐려지고, 바깥을 누르면(또는 Esc) 다시 카드 뒤로 돌아간다. 카드를 놓아도 돌아간다.
/// 시계가 진행되는 동안에는 왼쪽 위로 올라가 바늘이 천천히 도는 모습을 보여준다.
///
///            12
///        11  ●  1         ● 칸 바깥쪽 패 = 그 칸의 적 주사위 / 안쪽 패 = 내 주사위 (하나뿐이면 가운데)
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
    private const float CellSize = 64f;      // 칸 한 자리의 폭 (원판 크기 계산용)
    private const float SocketSize = 58f;    // 칸 그림의 지름 (안쪽 황동 테에 닿지 않게 조금 작다)

    private static ClockDial instance;

    /// <summary>마우스가 올라가 있는 칸 (없으면 0). 숫자 키로 그 칸의 내 행동 대상을 바꿀 때 사용</summary>
    public static int HoveredHour { get { return instance != null ? instance.hoveredHour : 0; } }

    private readonly Cell[] cells = new Cell[Hours + 1];   // 1~12 사용
    private RectTransform hand, handShadow;
    private Text centerText, infoText;
    private readonly Text[] timeLabels = new Text[2];   // 문자판의 "아침" / "밤" — 시간대 태엽판을 고를 때만 보인다
    private int hoveredHour;
    private int drawnVersion = -1, drawnHover = -1, drawnHour = -1;
    private CardData drawnPending;

    // 카드 뒤 자리(평소) ↔ 화면 가운데(집중)
    private const float DockY = 335f, DockScale = 0.62f, FocusScale = 1.65f, CornerScale = 0.85f, MoveTime = 0.2f;
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
        if (open && Input.GetKeyDown(KeyCode.Escape) && !PauseMenu.IsOpen && !PauseMenu.EscConsumed) OnOutsideClick();

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

    private static readonly Color cellColor = UITheme.Raised;
    private static readonly Color ringIdle = UITheme.Alpha(UITheme.Brass, 0.55f);     // 칸 둘레의 얇은 황동 테
    private static readonly Color hourInk = Color.Lerp(UITheme.TextMain, UITheme.BrassBright, 0.5f);   // 숫자: 황동 기가 도는 글자색
    // 밤 칸 (7~12시): 푸른 기가 도는 바탕 · 테 · 숫자 — 아침 칸(1~6시)은 원래의 황동빛
    private static readonly Color nightCell = Color.Lerp(UITheme.Raised, new Color(0.12f, 0.16f, 0.34f, 0.98f), 0.6f);
    private static readonly Color nightRing = new Color(0.56f, 0.66f, 1f, 0.55f);
    private static readonly Color nightInk = new Color(0.72f, 0.8f, 1f);
    private static readonly Color spentColor = UITheme.Spent;
    private static readonly Color enemyColor = UITheme.Enemy;
    private static readonly Color playerColor = UITheme.Player;
    private static readonly Color clashColor = UITheme.Gold;
    private static readonly Color validColor = UITheme.Alpha(UITheme.Good, 0.95f);
    private static readonly Color invalidColor = UITheme.Alpha(UITheme.Danger, 0.95f);

    private class Cell
    {
        public Image bg, enemyDot, playerDot;
        public Image ring, glow, enemyRing, playerRing;   // 칸 테두리 / 빛무리 / 주사위 패의 편 가름 테
        public Text hourText, enemyTag, playerTag;
        public Vector2 dir;   // 시계 중심에서 이 칸으로 향하는 방향 (패를 바깥 = 적 / 안쪽 = 나로 나눠 놓는다)
        public Color idleBg, idleRing, ink;   // 아침 칸 / 밤 칸의 평소 색
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

        // 문자판: 황동 테가 둘린 검은 법랑판 + 눈금 (그림자까지 한 장에 그려져 있다 — ClockArt)
        float discSize = (Radius * 2f + CellSize) * ClockArt.FacePad;
        Image disc = NewImage("Disc", face, ClockArt.Face, Color.white);
        disc.rectTransform.sizeDelta = new Vector2(discSize, discSize);
        disc.raycastTarget = true;   // 카드 뒤에 있을 때 누르는 곳
        disc.gameObject.AddComponent<ClockDialBody>().dial = this;

        // 바늘 (속이 뚫린 창 모양, Z 회전으로 시각을 가리킴) + 문자판에 지는 그림자 + 축 뚜껑
        float reach = Radius - CellSize * 0.5f - 4f;                 // 축에서 바늘 끝까지
        float handH = reach / (1f - ClockArt.HandPivot);
        Vector2 handSize = new Vector2(handH * ClockArt.HandW / ClockArt.HandH, handH);
        Image shadowImg = NewImage("HandShadow", face, ClockArt.Hand, UITheme.Alpha(Color.black, 0.45f));
        handShadow = shadowImg.rectTransform;
        handShadow.pivot = new Vector2(0.5f, ClockArt.HandPivot);
        handShadow.sizeDelta = handSize;
        handShadow.anchoredPosition = new Vector2(3f, -4f);
        Image handImg = NewImage("Hand", face, ClockArt.Hand, Color.white);
        hand = handImg.rectTransform;
        hand.pivot = new Vector2(0.5f, ClockArt.HandPivot);
        hand.sizeDelta = handSize;
        Image hub = NewImage("Hub", face, ClockArt.Hub, Color.white);
        hub.rectTransform.sizeDelta = new Vector2(24f, 24f);

        centerText = UIBuilder.Label("Center", face, "", 20, hourInk, TextAnchor.MiddleCenter);
        centerText.rectTransform.sizeDelta = new Vector2(170f, 90f);
        centerText.rectTransform.anchoredPosition = new Vector2(0f, -48f);
        centerText.raycastTarget = false;

        // 문자판 안쪽에 시간대 이름: 오른쪽 반(1~6시) = 아침, 왼쪽 반(7~12시) = 밤
        timeLabels[0] = FaceLabel(face, "아침", new Vector2(64f, 22f), hourInk);
        timeLabels[1] = FaceLabel(face, "밤", new Vector2(-64f, 22f), nightInk);

        for (int h = 1; h <= Hours; h++) cells[h] = BuildCell(face, h);

        // 아래쪽 설명 줄
        infoText = UIBuilder.Label("Info", root, "", 18, UITheme.TextMain, TextAnchor.MiddleCenter);
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
        c.dir = pos.normalized;
        bool night = BattleManager.IsNightHour(hour);
        c.idleBg = night ? nightCell : cellColor;
        c.idleRing = night ? nightRing : ringIdle;
        c.ink = night ? nightInk : hourInk;
        // 칸 = 움푹 들어간 소켓 + 얇은 황동 테. 합이 일어나는 칸 / 지금 시각은 테가 밝아지고 둘레에 빛무리가 돈다
        c.bg = NewImage("Hour" + hour, face, ClockArt.Socket, cellColor);
        c.bg.raycastTarget = true;
        c.bg.rectTransform.sizeDelta = new Vector2(SocketSize, SocketSize);
        c.bg.rectTransform.anchoredPosition = pos;
        c.glow = NewImage("Glow", c.bg.transform, ClockArt.Glow, Color.clear);
        c.glow.rectTransform.sizeDelta = Vector2.one * SocketSize * 1.45f;   // 옆 칸까지 번지지 않게
        c.glow.enabled = false;
        c.ring = NewImage("Ring", c.bg.transform, ClockArt.Ring, ringIdle);
        c.ring.rectTransform.sizeDelta = Vector2.one * (SocketSize + 3f);

        ClockDialCell hit = c.bg.gameObject.AddComponent<ClockDialCell>();
        hit.dial = this;
        hit.hour = hour;

        Transform t = c.bg.transform;
        c.hourText = UIBuilder.Label("Num", t, hour.ToString(), 24, hourInk, TextAnchor.MiddleCenter);
        c.hourText.fontStyle = FontStyle.Bold;
        UIBuilder.Stretch(c.hourText.rectTransform);
        c.hourText.raycastTarget = false;

        // 주사위 패: 둘레 색 = 누구의 주사위인가 (빨강 = 적, 파랑 = 나), 안쪽 색 = 주사위 종류
        c.enemyDot = BuildToken(t, "Enemy", 22f, enemyColor, out c.enemyRing, out c.enemyTag);
        c.playerDot = BuildToken(t, "Player", -22f, playerColor, out c.playerRing, out c.playerTag);
        return c;
    }

    private static Image BuildToken(Transform cell, string name, float y, Color side, out Image ring, out Text tag)
    {
        ring = NewImage(name + "Ring", cell, ProceduralSprites.Circle(), side);
        ring.rectTransform.anchoredPosition = new Vector2(0f, y);
        Image dark = NewImage("Gap", ring.transform, ProceduralSprites.Circle(), UITheme.Alpha(UITheme.Backdrop, 1f));   // 테와 패 사이의 가는 틈
        dark.rectTransform.anchorMin = Vector2.zero;
        dark.rectTransform.anchorMax = Vector2.one;
        dark.rectTransform.offsetMin = new Vector2(2f, 2f);
        dark.rectTransform.offsetMax = new Vector2(-2f, -2f);

        Image dot = NewImage(name, cell, ClockArt.Token, side);
        dot.rectTransform.sizeDelta = new Vector2(24f, 24f);
        dot.rectTransform.anchoredPosition = new Vector2(0f, y);
        tag = UIBuilder.Label("Tag", dot.transform, "", 15, UITheme.TextOnBrass, TextAnchor.MiddleCenter, false);
        tag.fontStyle = FontStyle.Bold;
        UIBuilder.Stretch(tag.rectTransform);
        tag.raycastTarget = false;
        return dot;
    }

    private static Text FaceLabel(RectTransform face, string text, Vector2 pos, Color color)
    {
        Text t = UIBuilder.Label("Time " + text, face, text, 17, UITheme.Alpha(color, 0.7f), TextAnchor.MiddleCenter, false);
        t.rectTransform.sizeDelta = new Vector2(60f, 26f);
        t.rectTransform.anchoredPosition = pos;
        t.raycastTarget = false;
        t.gameObject.SetActive(false);   // 시간대 태엽판을 고를 때만 켠다
        return t;
    }

    /// <summary>태엽판에 [아침] · [밤] 칸이 있는가 (있을 때만 시계판에 아침 · 밤을 보여 준다)</summary>
    public static bool UsesTime(CardData card)
    {
        if (card == null || card.diceList == null) return false;
        foreach (DiceData die in card.diceList)
        {
            if (die == null || die.effects == null) continue;
            foreach (DiceEffect fx in die.effects)
                if (fx != null && (fx.effect == DiceEffectType.Dawn || fx.effect == DiceEffectType.Dusk)) return true;
        }
        return false;
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
        handShadow.localEulerAngles = hand.localEulerAngles;

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

        // 아침(1~6시) · 밤(7~12시)은 [아침] · [밤] 칸이 있는 태엽판을 고른 동안에만 보여 준다 (평소엔 시계판을 깔끔하게)
        bool showTime = UsesTime(pending);
        foreach (Text label in timeLabels) if (label != null) label.gameObject.SetActive(showTime);
        int previewLength = pending != null ? TimedAction.LengthOf(pending) : 0;
        bool previewOk = pending != null && hoveredHour > 0 && bm.CanPlace(hoveredHour, previewLength);

        for (int h = 1; h <= Hours; h++)
        {
            Cell c = cells[h];
            TimedAction enemy = FindCovering(bm.EnemyActions, h);
            TimedAction mine = FindCovering(bm.PlayerActions, h);
            bool past = bm.CurrentHour > 0 && h < bm.CurrentHour;   // 이미 지나간 칸

            // 칸마다 그 칸에 배정된 주사위 하나: 색 = 주사위 종류, 글자 = 참/관/타/가/회
            DrawDie(c.enemyDot, c.enemyRing, enemyColor, c.enemyTag, enemy != null ? enemy.DieAt(h) : null, enemy != null && (past || !enemy.owner.IsAlive), enemy != null && enemy.hour == h);

            bool inPreview = pending != null && hoveredHour > 0 && h >= hoveredHour && h < hoveredHour + previewLength;
            if (inPreview && mine == null)
            {
                // 미리보기: 고른 태엽판의 주사위가 이 칸에 어떻게 놓이는지 반투명하게
                DiceData ghost = pending.diceList != null && h - hoveredHour < pending.diceList.Count ? pending.diceList[h - hoveredHour] : null;
                DrawDie(c.playerDot, c.playerRing, playerColor, c.playerTag, ghost, false, h == hoveredHour);
                if (ghost != null)
                {
                    c.playerDot.color = new Color(c.playerDot.color.r, c.playerDot.color.g, c.playerDot.color.b, 0.55f);
                    c.playerRing.color = UITheme.Alpha(playerColor, 0.55f);
                }
            }
            else
            {
                DrawDie(c.playerDot, c.playerRing, playerColor, c.playerTag, mine != null ? mine.DieAt(h) : null, mine != null && past, mine != null && mine.hour == h);
            }

            LayoutTokens(c);

            bool clash = enemy != null && mine != null;
            bool now = h == bm.CurrentHour;
            c.glow.enabled = clash || now;
            c.glow.color = now ? UITheme.Alpha(UITheme.BrassBright, 0.75f) : UITheme.Alpha(clashColor, 0.55f);
            c.ring.color = now ? UITheme.BrassBright : clash ? clashColor : (showTime ? c.idleRing : ringIdle);

            c.bg.color = inPreview ? (previewOk ? validColor : invalidColor)
                       : h == bm.CurrentHour ? UITheme.BrassDeep
                       : (showTime ? c.idleBg : cellColor);
            c.bg.rectTransform.localScale = Vector3.one * (h == bm.CurrentHour ? 1.18f : 1f);

            // 숫자: 패가 놓인 칸은 감춘다 (패 밑에 흐린 숫자가 비치면 지저분하다 — 몇 시인지는 자리와 설명 줄로 안다)
            bool occupied = c.enemyDot.enabled || c.playerDot.enabled;
            c.hourText.color = occupied ? Color.clear : inPreview ? UITheme.Alpha(UITheme.TextMain, 0.45f) : (showTime ? c.ink : hourInk);
        }

        centerText.text = bm.Phase == BattleManager.BattlePhase.EnemyTurn
            ? $"<b>{bm.CurrentHour}시</b>"
            : pending != null ? $"<color={UITheme.HexKi}>{pending.cardName}</color>\n{previewLength}칸" : "계획";

        infoText.text = hoveredHour > 0 ? DescribeHour(bm, hoveredHour, showTime)
                      : $"<color={UITheme.HexMuted}>칸에 마우스 = 그 칸의 내용 · 내 칸 위에서 숫자 키 = 그 행동의 대상 변경\n바깥을 누르면 카드 뒤로 돌아간다</color>";
    }

    /// <summary>
    /// 칸의 주사위 점. 색 = 주사위 종류(참격 빨강 · 관통 주황 · 타격 노랑 · 가드 파랑 · 회피 초록), 글자 = 종류 한 글자.
    /// 행동의 첫 칸은 조금 크게, 지나간 칸은 회색.
    /// </summary>
    private static void DrawDie(Image dot, Image ring, Color side, Text tag, DiceData die, bool spent, bool first)
    {
        dot.enabled = die != null;
        ring.gameObject.SetActive(die != null);
        tag.text = "";
        if (die == null) return;

        Color typeColor;
        ColorUtility.TryParseHtmlString(DiceInfo.ColorHex(die.diceDetail), out typeColor);
        dot.color = spent ? spentColor : typeColor;
        ring.color = spent ? spentColor : side;
        dot.rectTransform.sizeDelta = Vector2.one * (first ? 28f : 24f);
        ring.rectTransform.sizeDelta = dot.rectTransform.sizeDelta + Vector2.one * 8f;
        tag.text = DiceInfo.Glyph(die.diceDetail);
    }

    /// <summary>
    /// 칸 안의 패 자리 잡기 — 패가 칸 밖(옆 칸 · 테두리)으로 삐져나가지 않게 한다.
    ///   하나만 있으면 칸 가운데에 크게
    ///   둘 다 있으면 시계 중심 방향으로 나란히: 바깥쪽 = 나, 안쪽 = 적 (어느 시각에서도 같은 규칙)
    /// </summary>
    private static void LayoutTokens(Cell c)
    {
        bool hasE = c.enemyDot.enabled, hasP = c.playerDot.enabled;
        bool both = hasE && hasP;
        const float split = 13f;
        if (hasE) PlaceToken(c.enemyDot, c.enemyRing, c.enemyTag, both ? -c.dir * split : Vector2.zero, both ? 0.76f : 1.1f);
        if (hasP) PlaceToken(c.playerDot, c.playerRing, c.playerTag, both ? c.dir * split : Vector2.zero, both ? 0.76f : 1.1f);
    }

    private static void PlaceToken(Image dot, Image ring, Text tag, Vector2 pos, float scale)
    {
        Vector2 size = dot.rectTransform.sizeDelta * scale;   // DrawDie가 정한 크기 (행동의 첫 칸은 조금 크다)
        dot.rectTransform.sizeDelta = size;
        dot.rectTransform.anchoredPosition = pos;
        ring.rectTransform.sizeDelta = size + Vector2.one * 7f;   // 편 가름 테 2px + 틈 1.5px
        ring.rectTransform.anchoredPosition = pos;
        tag.fontSize = Mathf.RoundToInt(size.x * 0.58f);
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
        string aim = a.owner.isPlayer && a.target != null ? $" <color={UITheme.HexGold}>→ [{a.target.slotNumber}] {a.target.unitName}</color>" : "";
        return $"<color={hex}>{a.owner.unitName} [{a.card.data.cardName} {hour - a.hour + 1}/{a.length}]</color> {dieText}{aim}";
    }

    private static string DescribeHour(BattleManager bm, int hour, bool showTime)
    {
        string s = !showTime ? $"<b>{hour}시</b>  " : BattleManager.IsNightHour(hour) ? $"<b>{hour}시</b> <color=#8fa8ff>밤</color>  " : $"<b>{hour}시</b> <color=#ffd98a>아침</color>  ";
        System.Collections.Generic.List<string> foes = new System.Collections.Generic.List<string>();
        foreach (TimedAction a in bm.EnemyActions)
        {
            if (a.Covers(hour) && a.owner.IsAlive) foes.Add(DescribeDie(a, hour, "#ff6b54"));
        }
        TimedAction mine = FindCovering(bm.PlayerActions, hour);

        if (foes.Count == 0 && mine == null) return s + $"<color={UITheme.HexMuted}>비어 있음</color>";
        s += string.Join("  /  ", foes);
        if (foes.Count > 0 && mine != null) s += $"\n<color={UITheme.HexGold}>↕ 합</color>  ";
        if (mine != null) s += DescribeDie(mine, hour, "#66bfff");
        else if (foes.Count > 0) s += $"\n<color={UITheme.HexMuted}>내 칸 없음. 공격이면 그대로 맞는다</color>";
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
