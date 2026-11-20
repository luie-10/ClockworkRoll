using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 잠시 멈춤 메뉴 — 화면 오른쪽 위의 [메뉴] 버튼 또는 Esc · F10.
///   [계속하기] / [저장하고 나가기] (시작 화면으로 — 거기서 '이어하기'로 돌아온다)
/// 저장은 진행하는 동안 알아서 남는다 (챕터가 시작될 때 · 갈래길에서 칸 하나를 마칠 때).
/// 전투 한복판에서 나가면 그 전투를 시작하기 전 지점부터 이어진다 — 메뉴에 그렇게 적어 둔다.
/// 직접 붙일 필요 없음 — GameManager가 PauseMenu.Ensure()로 만든다 (전용 Canvas).
/// </summary>
public class PauseMenu : MonoBehaviour
{
    private const int SortingOrder = 850;   // 게임 UI 전부의 위, 시작 화면(900) · 로딩 화면(950) 아래

    private static PauseMenu instance;

    /// <summary>메뉴 창이 열려 있는 동안 true</summary>
    public static bool IsOpen { get { return instance != null && instance.panel.activeSelf; } }

    private GameObject panel, openButton;
    private Text noteText;

    public static void Ensure()
    {
        if (instance != null || !Application.isPlaying) return;

        GameObject go = new GameObject("PauseMenuCanvas");
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();

        instance = go.AddComponent<PauseMenu>();
        instance.Build(go.transform);
    }

    private void Build(Transform canvas)
    {
        // 오른쪽 위의 작은 버튼
        Button open = UIBuilder.Button("MenuButton", canvas, "메뉴 (Esc)", UITheme.InkSolid, 20);
        UITheme.StyleButton(open, false, 20);
        UIBuilder.Place((RectTransform)open.transform, Vector2.one, Vector2.one, Vector2.one, new Vector2(-22f, -18f), new Vector2(150f, 44f));
        open.onClick.AddListener(() => SetOpen(true));
        openButton = open.gameObject;

        // 어두운 바탕 + 가운데 창
        Image dim = UIBuilder.Panel("Dim", canvas, UITheme.Scrim, false);
        dim.sprite = null;
        UIBuilder.Stretch(dim.rectTransform);
        dim.raycastTarget = true;   // 뒤의 게임 화면을 누르지 못하게
        panel = dim.gameObject;

        Vector2 center = new Vector2(0.5f, 0.5f);
        Image window = UIBuilder.Panel("Window", dim.transform, UITheme.Window);
        UIBuilder.Place(window.rectTransform, center, center, center, Vector2.zero, new Vector2(560f, 740f));

        UITheme.Elevate(window, 0.7f);

        Text title = UIBuilder.Label("Title", window.transform, "잠시 멈춤", 40, UITheme.Brass, TextAnchor.MiddleCenter);
        title.fontStyle = FontStyle.Bold;
        UIBuilder.Place(title.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(0f, 56f));

        noteText = UIBuilder.Label("Note", window.transform, "", 20, UITheme.TextMuted, TextAnchor.MiddleCenter);
        UIBuilder.Place(noteText.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(-60f, 64f));

        Button resume = UIBuilder.Button("Resume", window.transform, "계속하기", UITheme.BrassFill, 28);
        UITheme.StyleButton(resume, true, 28);
        UIBuilder.Place((RectTransform)resume.transform, center, center, center, new Vector2(0f, 118f), new Vector2(380f, 72f));
        resume.onClick.AddListener(() => SetOpen(false));

        Button quit = UIBuilder.Button("SaveAndQuit", window.transform, "저장하고 나가기", UITheme.InkSolid, 26);
        UITheme.StyleButton(quit, false, 26);
        UIBuilder.Place((RectTransform)quit.transform, center, center, center, new Vector2(0f, 30f), new Vector2(380f, 66f));
        quit.onClick.AddListener(SaveAndQuit);

        // 소리 크기 (바로 저장된다)
        VolumeRow(window.transform, "효과음", -56f, () => Sfx.Volume, v => { Sfx.Volume = v; Sfx.Play(SfxId.Tock); });
        VolumeRow(window.transform, "음악 · 환경음", -112f, () => AudioDirector.MusicVolume, v => AudioDirector.MusicVolume = v);

        // UI 스타일: 기본 ↔ 픽셀 (글자는 그대로, 판 · 테두리 · 시계판 · 문양만 바뀐다)
        Text styleLabel = UIBuilder.Label("Style Label", window.transform, "UI 스타일 (F8)", 22, UITheme.TextMain, TextAnchor.MiddleLeft);
        UIBuilder.Place(styleLabel.rectTransform, center, center, center, new Vector2(-95f, -168f), new Vector2(190f, 44f));
        Button style = UIBuilder.Button("Style", window.transform, "", UITheme.InkSolid, 22);
        UITheme.StyleButton(style, false, 22);
        UIBuilder.Place((RectTransform)style.transform, center, center, center, new Vector2(108f, -168f), new Vector2(184f, 44f));
        Text styleValue = style.GetComponentInChildren<Text>();
        System.Action refreshStyle = () => { if (styleValue != null) styleValue.text = PixelStyle.On ? "픽셀" : "기본"; };
        refreshStyle();
        PixelStyle.Changed += refreshStyle;
        style.onClick.AddListener(PixelStyle.Toggle);

        // 튜토리얼: 켬 / 끔 (켤 때는 본 설명도 처음부터 다시)
        Text tutLabel = UIBuilder.Label("Tutorial Label", window.transform, "튜토리얼", 22, UITheme.TextMain, TextAnchor.MiddleLeft);
        UIBuilder.Place(tutLabel.rectTransform, center, center, center, new Vector2(-95f, -224f), new Vector2(190f, 44f));
        Button tut = UIBuilder.Button("Tutorial", window.transform, "", UITheme.InkSolid, 22);
        UITheme.StyleButton(tut, false, 22);
        UIBuilder.Place((RectTransform)tut.transform, center, center, center, new Vector2(108f, -224f), new Vector2(184f, 44f));
        Text tutValue = tut.GetComponentInChildren<Text>();
        System.Action refreshTut = () => { if (tutValue != null) tutValue.text = TutorialCoach.Enabled ? "켬" : "끔"; };
        refreshTut();
        refreshOnOpen = refreshTut;   // 설명 상자에서 껐을 수도 있으니 열 때마다 다시 읽는다
        tut.onClick.AddListener(() =>
        {
            if (TutorialCoach.Enabled) TutorialCoach.Enabled = false;
            else TutorialCoach.ResetProgress();   // 다시 켜면 처음부터 다시 보여 준다
            refreshTut();
        });

        // 화면 배율 (모든 UI 크기 — Ctrl + '+' / '-' 로도)
        ScaleRow(window.transform, -280f);

        Text hint = UIBuilder.Label("Hint", window.transform, "시작 화면에서 '이어하기'로 돌아올 수 있다", 17, UITheme.TextMuted, TextAnchor.MiddleCenter);
        UIBuilder.Place(hint.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(-40f, 26f));

        UIPop.Ensure(panel, 1f, 0.16f);
        UIPop.Ensure(window.gameObject, 0.95f, 0.18f);
        panel.SetActive(false);

        // 화면 위쪽 가운데에 잠깐 뜨는 알림 (F8로 스타일을 바꿨을 때)
        toast = UIBuilder.Label("Toast", canvas, "", 24, UITheme.BrassBright, TextAnchor.MiddleCenter);
        UIBuilder.Place(toast.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(700f, 40f));
        toast.raycastTarget = false;
        Canvas toastCanvas = toast.gameObject.AddComponent<Canvas>();   // 시작 화면(900) · 로딩 화면(950) 위에서도 보이게
        toastCanvas.overrideSorting = true;
        toastCanvas.sortingOrder = 960;
        toast.gameObject.SetActive(false);
    }

    private Text toast;
    private float toastTime;

    private void ShowToast(string message)
    {
        if (toast == null) return;
        toast.text = message;
        toast.gameObject.SetActive(true);
        toastTime = 1.6f;
    }

    /// <summary>"이름   [−]  80%  [+]" 한 줄 — 10%씩 줄이고 키운다</summary>
    /// <summary>화면 배율 줄: 70% ~ 150%, 10%씩</summary>
    private static void ScaleRow(Transform parent, float y)
    {
        Vector2 center = new Vector2(0.5f, 0.5f);
        Text label = UIBuilder.Label("Scale Label", parent, "화면 배율", 22, UITheme.TextMain, TextAnchor.MiddleLeft);
        UIBuilder.Place(label.rectTransform, center, center, center, new Vector2(-95f, y), new Vector2(190f, 44f));
        Text value = UIBuilder.Label("Scale Value", parent, "", 22, UITheme.Brass, TextAnchor.MiddleCenter);
        UIBuilder.Place(value.rectTransform, center, center, center, new Vector2(108f, y), new Vector2(80f, 44f));
        System.Action refresh = () => value.text = Mathf.RoundToInt(UIScale.Value * 100f) + "%";
        refresh();
        Button down = UIBuilder.Button("Scale Down", parent, "−", UITheme.InkSolid, 26);
        UITheme.StyleButton(down, false, 26);
        UIBuilder.Place((RectTransform)down.transform, center, center, center, new Vector2(40f, y), new Vector2(48f, 44f));
        down.onClick.AddListener(() => { UIScale.Value = UIScale.Value - UIScale.Step; refresh(); });
        Button up = UIBuilder.Button("Scale Up", parent, "+", UITheme.InkSolid, 26);
        UITheme.StyleButton(up, false, 26);
        UIBuilder.Place((RectTransform)up.transform, center, center, center, new Vector2(176f, y), new Vector2(48f, 44f));
        up.onClick.AddListener(() => { UIScale.Value = UIScale.Value + UIScale.Step; refresh(); });
        scaleRefresh = refresh;
    }

    private static System.Action scaleRefresh;

    /// <summary>화면 위쪽 가운데에 잠깐 뜨는 알림 (다른 기능이 쓴다)</summary>
    public static void Toast(string message)
    {
        if (instance != null) instance.ShowToast(message);
        if (scaleRefresh != null) scaleRefresh();
    }

    private static void VolumeRow(Transform parent, string name, float y, System.Func<float> get, System.Action<float> set)
    {
        Vector2 center = new Vector2(0.5f, 0.5f);
        Text label = UIBuilder.Label(name + " Label", parent, name, 22, UITheme.TextMain, TextAnchor.MiddleLeft);
        UIBuilder.Place(label.rectTransform, center, center, center, new Vector2(-95f, y), new Vector2(190f, 44f));

        Text value = UIBuilder.Label(name + " Value", parent, "", 22, UITheme.Brass, TextAnchor.MiddleCenter);
        UIBuilder.Place(value.rectTransform, center, center, center, new Vector2(108f, y), new Vector2(80f, 44f));
        System.Action refresh = () => value.text = Mathf.RoundToInt(get() * 100f) + "%";
        refresh();

        Button down = UIBuilder.Button(name + " Down", parent, "−", UITheme.InkSolid, 26);
        UITheme.StyleButton(down, false, 26);
        UIBuilder.Place((RectTransform)down.transform, center, center, center, new Vector2(40f, y), new Vector2(48f, 44f));
        down.onClick.AddListener(() => { set(Mathf.Round(Mathf.Clamp01(get() - 0.1f) * 10f) / 10f); refresh(); });

        Button up = UIBuilder.Button(name + " Up", parent, "+", UITheme.InkSolid, 26);
        UITheme.StyleButton(up, false, 26);
        UIBuilder.Place((RectTransform)up.transform, center, center, center, new Vector2(176f, y), new Vector2(48f, 44f));
        up.onClick.AddListener(() => { set(Mathf.Round(Mathf.Clamp01(get() + 0.1f) * 10f) / 10f); refresh(); });
    }

    /// <summary>메뉴를 쓸 수 있는 때인가 (시작 · 로딩 · 결과 화면에서는 숨긴다)</summary>
    private static bool Available()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || TitleScreen.IsOpen || LoadingScreen.IsOpen) return false;
        return gm.State != GameManager.GameState.GameOver && gm.State != GameManager.GameState.Clear;
    }

    void Update()
    {
        // F8: UI 스타일 기본 ↔ 픽셀 (어느 화면에서나 바로 비교할 수 있게)
        if (Input.GetKeyDown(KeyCode.F8))
        {
            PixelStyle.Toggle();
            ShowToast(PixelStyle.On ? "UI 스타일: 픽셀  (F8 = 기본으로)" : "UI 스타일: 기본  (F8 = 픽셀로)");
        }
        if (toast != null && toastTime > 0f)
        {
            toastTime -= Time.unscaledDeltaTime;
            toast.color = UITheme.Alpha(UITheme.BrassBright, Mathf.Clamp01(toastTime / 0.4f));
            if (toastTime <= 0f) toast.gameObject.SetActive(false);
        }

        bool available = Available();
        if (openButton.activeSelf != (available && !panel.activeSelf)) openButton.SetActive(available && !panel.activeSelf);

        if (!available)
        {
            if (panel.activeSelf) SetOpen(false);
            return;
        }
        if (Input.GetKeyDown(KeyCode.F10)) SetOpen(!panel.activeSelf);
        else if (Input.GetKeyDown(KeyCode.Escape))
        {
            // Esc: 열려 있으면 닫는다. 닫혀 있으면 — Esc를 먼저 쓰는 창(대사 · 덱 보기 · 시계판 · 승리 화면)이
            // 이번 프레임에도, 직전 프레임에도 없을 때만 연다 (Update 순서와 상관없이 한 번의 Esc가 두 번 쓰이지 않게)
            if (panel.activeSelf) { SetOpen(false); escFrame = Time.frameCount; }
            else if (!othersWantedEsc && !OthersWantEsc()) { SetOpen(true); escFrame = Time.frameCount; }
        }
    }

    void LateUpdate()
    {
        othersWantedEsc = OthersWantEsc();
    }

    private bool othersWantedEsc;
    private int escFrame = -1;

    /// <summary>이번 프레임의 Esc를 멈춤 메뉴가 썼다 — 다른 창은 이 Esc에 반응하지 않는다</summary>
    public static bool EscConsumed { get { return instance != null && instance.escFrame == Time.frameCount; } }

    /// <summary>다른 곳이 Esc를 받아야 하는 상태인가 (그동안은 멈춤 메뉴가 Esc로 열리지 않는다)</summary>
    private static bool OthersWantEsc()
    {
        return DeckViewer.IsOpen || ClockDial.IsOpen || VictoryScreen.IsOpen
            || (DialogueManager.Instance != null && DialogueManager.Instance.IsPlaying);
    }

    private System.Action refreshOnOpen;

    private void SetOpen(bool open)
    {
        if (panel.activeSelf == open) return;
        panel.SetActive(open);
        Time.timeScale = open ? 0f : 1f;   // 열려 있는 동안 전투 · 대사가 흘러가지 않게

        if (open && refreshOnOpen != null) refreshOnOpen();
        if (open && GameManager.Instance != null && GameManager.Instance.IsEndless)
            noteText.text = $"무한 모드 {GameManager.Instance.EndlessLoop}회차. 무한 모드는 저장되지 않는다.\n나가면 이번 도전은 끝난다.";
        else if (open && GameManager.Instance != null)
        {
            noteText.text = GameManager.Instance.State == GameManager.GameState.Battle
                ? $"지금 나가면 <color={UITheme.HexBrass}>이 전투를 시작하기 전</color>부터 이어진다.\n(전투 한복판은 저장되지 않는다)"
                : "여기까지의 진행은 저장되어 있다.";
        }
    }

    private void SaveAndQuit()
    {
        Time.timeScale = 1f;
        if (GameManager.Instance != null) GameManager.Instance.QuitToTitle();
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            Time.timeScale = 1f;
        }
    }
}
