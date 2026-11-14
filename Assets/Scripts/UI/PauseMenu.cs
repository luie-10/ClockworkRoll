using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 잠시 멈춤 메뉴 — 화면 오른쪽 위의 [메뉴] 버튼 또는 F10.
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
        Button open = UIBuilder.Button("MenuButton", canvas, "메뉴 (F10)", UITheme.InkSolid, 20);
        UITheme.StyleButton(open, false, 20);
        UIBuilder.Place((RectTransform)open.transform, Vector2.one, Vector2.one, Vector2.one, new Vector2(-22f, -18f), new Vector2(150f, 44f));
        open.onClick.AddListener(() => SetOpen(true));
        openButton = open.gameObject;

        // 어두운 바탕 + 가운데 창
        Image dim = UIBuilder.Panel("Dim", canvas, new Color(0f, 0f, 0f, 0.66f), false);
        dim.sprite = null;
        UIBuilder.Stretch(dim.rectTransform);
        dim.raycastTarget = true;   // 뒤의 게임 화면을 누르지 못하게
        panel = dim.gameObject;

        Vector2 center = new Vector2(0.5f, 0.5f);
        Image window = UIBuilder.Panel("Window", dim.transform, new Color(0.05f, 0.05f, 0.065f, 0.98f));
        UIBuilder.Place(window.rectTransform, center, center, center, Vector2.zero, new Vector2(560f, 430f));

        Text title = UIBuilder.Label("Title", window.transform, "잠시 멈춤", 40, UITheme.Brass, TextAnchor.MiddleCenter);
        title.fontStyle = FontStyle.Bold;
        UIBuilder.Place(title.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(0f, 56f));

        noteText = UIBuilder.Label("Note", window.transform, "", 20, UITheme.TextMuted, TextAnchor.MiddleCenter);
        UIBuilder.Place(noteText.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(-60f, 64f));

        Button resume = UIBuilder.Button("Resume", window.transform, "계속하기", UITheme.BrassFill, 28);
        UITheme.StyleButton(resume, true, 28);
        UIBuilder.Place((RectTransform)resume.transform, center, center, center, new Vector2(0f, -18f), new Vector2(380f, 72f));
        resume.onClick.AddListener(() => SetOpen(false));

        Button quit = UIBuilder.Button("SaveAndQuit", window.transform, "저장하고 나가기", UITheme.InkSolid, 26);
        UITheme.StyleButton(quit, false, 26);
        UIBuilder.Place((RectTransform)quit.transform, center, center, center, new Vector2(0f, -108f), new Vector2(380f, 66f));
        quit.onClick.AddListener(SaveAndQuit);

        Text hint = UIBuilder.Label("Hint", window.transform, "시작 화면에서 '이어하기'로 돌아올 수 있다", 17, UITheme.TextMuted, TextAnchor.MiddleCenter);
        UIBuilder.Place(hint.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(-40f, 26f));

        UIPop.Ensure(panel, 1f, 0.16f);
        UIPop.Ensure(window.gameObject, 0.95f, 0.18f);
        panel.SetActive(false);
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
        bool available = Available();
        if (openButton.activeSelf != (available && !panel.activeSelf)) openButton.SetActive(available && !panel.activeSelf);

        if (!available)
        {
            if (panel.activeSelf) SetOpen(false);
            return;
        }
        if (Input.GetKeyDown(KeyCode.F10)) SetOpen(!panel.activeSelf);
    }

    private void SetOpen(bool open)
    {
        if (panel.activeSelf == open) return;
        panel.SetActive(open);
        Time.timeScale = open ? 0f : 1f;   // 열려 있는 동안 전투 · 대사가 흘러가지 않게

        if (open && GameManager.Instance != null)
        {
            noteText.text = GameManager.Instance.State == GameManager.GameState.Battle
                ? "지금 나가면 <color=#e8c27a>이 전투를 시작하기 전</color>부터 이어진다.\n(전투 한복판은 저장되지 않는다)"
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
