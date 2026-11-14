using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// UI의 '움직임' 모음 — 전부 연출 전용이라 빼도 게임은 그대로 동작한다.
//   UIButtonFeel : 버튼에 마우스를 올리면 살짝 커지고, 누르면 눌린다
//   UIPop        : 창 · 정보창이 켜질 때 작게 시작해 떠오르듯 나타난다
//   UITextPunch  : 숫자(기력 등)가 바뀌는 순간 글자가 한 번 튄다
//   PhaseBanner  : 계획 ↔ 시계 진행으로 넘어갈 때 화면을 가로지르는 띠
// 모두 unscaled 시간으로 움직인다 (히트스톱 · 일시정지 중에도 UI는 반응해야 한다).

/// <summary>버튼의 손맛: 올리면 커지고 누르면 눌린다. UITheme.StyleButton / UIBuilder.Button이 알아서 붙인다.</summary>
public class UIButtonFeel : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    public float hoverScale = 1.045f;
    public float pressScale = 0.955f;
    public float speed = 18f;

    private Button button;
    private Vector3 baseScale = Vector3.one;
    private float factor = 1f;
    private bool hovered, pressed;

    public static void Ensure(Button b)
    {
        if (b == null || !Application.isPlaying) return;
        if (b.GetComponent<UIButtonFeel>() == null) b.gameObject.AddComponent<UIButtonFeel>();
    }

    void Awake()
    {
        button = GetComponent<Button>();
        baseScale = transform.localScale;
    }

    void Update()
    {
        bool live = button == null || button.interactable;
        float target = !live ? 1f : pressed ? pressScale : hovered ? hoverScale : 1f;
        if (Mathf.Approximately(factor, target)) return;

        factor = Mathf.Lerp(factor, target, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));
        if (Mathf.Abs(factor - target) < 0.001f) factor = target;
        transform.localScale = baseScale * factor;
    }

    void OnDisable()
    {
        hovered = pressed = false;
        factor = 1f;
        transform.localScale = baseScale;
    }

    public void OnPointerEnter(PointerEventData e) { hovered = true; }
    public void OnPointerExit(PointerEventData e) { hovered = pressed = false; }
    public void OnPointerDown(PointerEventData e) { pressed = true; }
    public void OnPointerUp(PointerEventData e) { pressed = false; }
}

/// <summary>켜질 때마다 작게 시작해 제 크기로 떠오르며 나타난다 (SetActive(true)가 곧 재생).</summary>
public class UIPop : MonoBehaviour
{
    public float fromScale = 0.94f;
    public float duration = 0.18f;

    private CanvasGroup group;
    private Vector3 baseScale = Vector3.one;
    private bool captured;
    private float t = 1f;

    /// <summary>fromScale = 1이면 크기는 그대로 두고 서서히 나타나기만 한다</summary>
    public static UIPop Ensure(GameObject go, float fromScale = 0.94f, float duration = 0.18f)
    {
        if (go == null) return null;
        UIPop pop = go.GetComponent<UIPop>();
        if (pop == null) pop = go.AddComponent<UIPop>();
        pop.fromScale = fromScale;
        pop.duration = duration;
        return pop;
    }

    void OnEnable()
    {
        if (!captured)
        {
            baseScale = transform.localScale;
            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            captured = true;
        }
        t = 0f;
        Apply();
    }

    void Update()
    {
        if (t >= 1f) return;
        t = Mathf.Min(1f, t + Time.unscaledDeltaTime / Mathf.Max(0.01f, duration));
        Apply();
    }

    void OnDisable()
    {
        if (!captured) return;
        t = 1f;
        Apply();   // 다음에 켜질 때 엉뚱한 크기에서 시작하지 않게
    }

    private void Apply()
    {
        float k = 1f - (1f - t) * (1f - t) * (1f - t);
        transform.localScale = baseScale * Mathf.Lerp(fromScale, 1f, k);
        if (group != null) group.alpha = Mathf.Clamp01(t * 2.2f);
    }
}

/// <summary>글자 내용이 바뀌는 순간 한 번 튄다 (기력 숫자 등).</summary>
[RequireComponent(typeof(Text))]
public class UITextPunch : MonoBehaviour
{
    public float punchScale = 1.2f;
    public float duration = 0.25f;

    private Text label;
    private string last;
    private Vector3 baseScale = Vector3.one;
    private float t = 1f;

    public static void Ensure(Text text)
    {
        if (text == null || !Application.isPlaying) return;
        if (text.GetComponent<UITextPunch>() == null) text.gameObject.AddComponent<UITextPunch>();
    }

    void Awake()
    {
        label = GetComponent<Text>();
        baseScale = transform.localScale;
        last = label.text;
    }

    void LateUpdate()
    {
        if (label.text != last)
        {
            last = label.text;
            t = 0f;
        }
        if (t >= 1f) return;

        t = Mathf.Min(1f, t + Time.unscaledDeltaTime / Mathf.Max(0.01f, duration));
        float k = (1f - t) * (1f - t);
        transform.localScale = baseScale * Mathf.Lerp(1f, punchScale, k);
    }

    void OnDisable()
    {
        t = 1f;
        transform.localScale = baseScale;
    }
}

/// <summary>
/// 단계 전환 띠 — 계획 단계가 열리거나 시계가 돌기 시작할 때 화면을 가로질러 잠깐 뜬다.
/// 클릭은 가로채지 않는다. BattleHUD가 시작할 때 PhaseBanner.Ensure()로 만든다 (전용 Canvas).
/// </summary>
public class PhaseBanner : MonoBehaviour
{
    private const float Life = 1.15f;
    private const int SortingOrder = 55;   // 시계판(50) 바로 위, 정보창(500) · 승리 화면(600)보다 아래

    private static PhaseBanner instance;

    private CanvasGroup group;
    private RectTransform band;
    private Text label;
    private Image lineTop, lineBottom;
    private float age = Life;
    private BattleManager.BattlePhase lastPhase = BattleManager.BattlePhase.Ready;

    public static void Ensure()
    {
        if (instance != null || !Application.isPlaying) return;

        GameObject go = new GameObject("PhaseBannerCanvas");
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        instance = go.AddComponent<PhaseBanner>();
        instance.group = go.AddComponent<CanvasGroup>();
        instance.group.alpha = 0f;
        instance.group.interactable = false;
        instance.group.blocksRaycasts = false;

        // 가운데가 짙고 양 끝으로 사라지는 띠
        instance.band = UIBuilder.Rect("Band", go.transform);
        UIBuilder.Place(instance.band, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(0f, 92f));
        Image bg = instance.band.gameObject.AddComponent<Image>();
        bg.sprite = ProceduralSprites.HorizontalFade();
        bg.color = new Color(0.02f, 0.02f, 0.03f, 0.78f);
        bg.raycastTarget = false;

        instance.lineTop = Line(instance.band, 1f);
        instance.lineBottom = Line(instance.band, 0f);

        instance.label = UIBuilder.Label("Label", instance.band, "", 46, UITheme.Brass, TextAnchor.MiddleCenter);
        instance.label.fontStyle = FontStyle.Bold;
        UIBuilder.Stretch(instance.label.rectTransform);
    }

    private static Image Line(RectTransform parent, float edge)
    {
        RectTransform rt = UIBuilder.Rect("Line", parent);
        UIBuilder.Place(rt, new Vector2(0f, edge), new Vector2(1f, edge), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 2f));
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = ProceduralSprites.HorizontalFade();
        img.raycastTarget = false;
        return img;
    }

    private void Play(string text, Color color)
    {
        label.text = text;
        label.color = color;
        lineTop.color = lineBottom.color = new Color(color.r, color.g, color.b, 0.8f);
        age = 0f;
    }

    void Update()
    {
        BattleManager bm = BattleManager.Instance;
        if (bm != null && bm.Phase != lastPhase)
        {
            lastPhase = bm.Phase;
            bool inBattle = GameManager.Instance == null || GameManager.Instance.State == GameManager.GameState.Battle;
            if (inBattle && !VictoryScreen.IsOpen)
            {
                if (lastPhase == BattleManager.BattlePhase.PlayerTurn) Play("계  획", UITheme.Ki);
                else if (lastPhase == BattleManager.BattlePhase.EnemyTurn) Play("시 계  진 행", UITheme.Brass);
            }
        }

        if (age >= Life)
        {
            if (group.alpha != 0f) group.alpha = 0f;
            return;
        }

        age += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(age / Life);
        float fadeIn = Mathf.Clamp01(t / 0.16f), fadeOut = Mathf.Clamp01((1f - t) / 0.28f);
        group.alpha = Mathf.Min(fadeIn, fadeOut);

        // 띠는 가운데에서 위아래로 펼쳐지고, 글자는 오른쪽에서 들어와 천천히 왼쪽으로 흘러간다
        float open = 1f - (1f - fadeIn) * (1f - fadeIn);
        band.localScale = new Vector3(1f, Mathf.Lerp(0.2f, 1f, open), 1f);
        label.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(90f, 0f, open) - 40f * t, 0f);
    }
}
