using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 손패 카드에 마우스를 올리면 카드 바로 위에 뜨는 상세 정보창.
///
///   톱니 물림            돌 · 기력 2
///   "맞물린 톱니는 놓아주지 않는다."
///   ─ 주사위 ─
///     참격 3~6  [적중시] 다음 턴 마비 1
///     타격 2~5  [적중시] 다음 턴 취약 1
///   ─ 용어 ─
///   마비: 주사위 n개가 최솟값으로 굴러감
///   취약: 이번 턴 동안 공격에 맞을 때 피해 +n
///
/// 직접 붙일 필요 없음 — CardView가 마우스 올림/내림 때 Show/Hide를 부르면 전용 Canvas를 알아서 만든다.
/// </summary>
public class CardTooltip : MonoBehaviour
{
    private const float PanelWidth = 460f;

    private static CardTooltip instance;

    private RectTransform canvasRect, panel;
    private Text text;
    private RectTransform current;   // 정보창을 띄운 카드 (손패 카드나 보상 타일)

    public static void Show(CardView view)
    {
        if (view == null || view.BoundCard == null || view.BoundCard.data == null) return;
        if (instance == null) instance = Create();
        instance.current = (RectTransform)view.transform;
        instance.text.text = BuildText(view.BoundCard.data, view.BoundCard.entry);
    }

    public static void Hide(CardView view)
    {
        if (instance != null && view != null && instance.current == (RectTransform)view.transform) instance.current = null;
    }

    /// <summary>보상 화면의 카드 타일 위에 띄운다 (entry = 덱에 있는 카드면 침식 표시용)</summary>
    public static void Show(RectTransform tile, CardData card, DeckEntry entry = null)
    {
        if (tile == null || card == null) return;
        if (instance == null) instance = Create();
        instance.current = tile;
        instance.text.text = BuildText(card, entry);
    }

    public static void Hide(RectTransform tile)
    {
        if (instance != null && instance.current == tile) instance.current = null;
    }

    private static CardTooltip Create()
    {
        GameObject go = new GameObject("CardTooltipCanvas");
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 650;   // 손패 · 승리/갈림길 화면(600)보다 위, 결과 화면(700)보다 아래
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        CardTooltip tip = go.AddComponent<CardTooltip>();
        tip.canvasRect = (RectTransform)go.transform;

        // 폭은 고정, 높이만 내용에 맞춰 늘어나는 패널
        Image bg = UIBuilder.Panel("Tooltip", go.transform, new Color(0.03f, 0.03f, 0.04f, 0.94f));
        bg.raycastTarget = false;
        tip.panel = bg.rectTransform;
        tip.panel.pivot = new Vector2(0.5f, 0f);
        tip.panel.anchorMin = tip.panel.anchorMax = Vector2.zero;
        tip.panel.sizeDelta = new Vector2(PanelWidth, 100f);

        VerticalLayoutGroup layout = bg.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 12, 12);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        ContentSizeFitter fitter = bg.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        tip.text = UIBuilder.Label("Text", bg.transform, "", 21, Color.white, TextAnchor.UpperLeft);
        tip.text.horizontalOverflow = HorizontalWrapMode.Wrap;
        tip.text.lineSpacing = 1.1f;

        bg.gameObject.SetActive(false);
        return tip;
    }

    void LateUpdate()
    {
        bool show = current != null && current.gameObject.activeInHierarchy;
        if (panel.gameObject.activeSelf != show) panel.gameObject.SetActive(show);
        if (!show) return;

        // 카드 윗변 가운데의 화면 좌표 → 이 Canvas 좌표 (카드 Canvas가 Overlay든 Camera든 동작)
        RectTransform cardRect = current;
        Canvas cardCanvas = current.GetComponentInParent<Canvas>();
        Camera cam = cardCanvas != null && cardCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? cardCanvas.worldCamera : null;

        Vector3[] corners = new Vector3[4];
        cardRect.GetWorldCorners(corners);   // 0 왼쪽 아래, 1 왼쪽 위, 2 오른쪽 위, 3 오른쪽 아래
        Vector2 screenTop = RectTransformUtility.WorldToScreenPoint(cam, (corners[1] + corners[2]) * 0.5f);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenTop, null, out Vector2 local);
        Vector2 canvasSize = canvasRect.rect.size;
        Vector2 pos = local + canvasSize * 0.5f + new Vector2(0f, 12f);   // 왼쪽 아래(0,0) 기준 좌표로

        // 화면 밖으로 나가지 않게
        Vector2 size = panel.rect.size;
        pos.x = Mathf.Clamp(pos.x, size.x * 0.5f + 8f, canvasSize.x - size.x * 0.5f - 8f);
        pos.y = Mathf.Min(pos.y, canvasSize.y - size.y - 8f);

        panel.anchoredPosition = pos;
    }

    private static string BuildText(CardData d, DeckEntry entry)
    {
        bool absorbed = entry != null && entry.absorbed;

        string rarityHex = "#" + ColorUtility.ToHtmlStringRGB(DiceInfo.RarityColor(d.rarity));
        string s = $"<size=26><b>{(absorbed ? "◈ " : "")}{d.cardName}</b></size>   " +
                   $"<color={rarityHex}>{DiceInfo.RarityName(d.rarity)}</color> · <color=#8cd9ff>기력 {d.cost}</color> · <color=#ffd159>{TimedAction.LengthOf(d)}칸</color>";

        if (!string.IsNullOrEmpty(d.cardDescription))
            s += $"\n<color=#a8a8a8><i>{d.cardDescription}</i></color>";

        string detail = DiceInfo.Detail(d);
        if (detail.Length > 0) s += "\n<color=#e8c27a>─ 칸 ─</color>\n" + detail;

        string keywords = DiceInfo.Keywords(d);
        if (keywords.Length > 0) s += "\n<color=#e8c27a>─ 용어 ─</color>\n<size=18>" + keywords + "</size>";

        if (absorbed)
            s += $"\n<color=#d9a6ff>흡수한 기억 · 침식 {entry.corruption} — 쓸 때마다 기억이 새어 나와 나를 해칠 수 있다</color>";
        return s;
    }
}

/// <summary>
/// 보상 화면의 카드 타일에 붙인다: 마우스를 올리면 살짝 커지고 정보창(용어 설명 포함)이 뜬다.
/// 손패 카드와 같은 정보를 보여주어, 고르기 전에 무슨 카드인지 헷갈리지 않게 한다.
/// </summary>
public class CardHover : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
{
    public CardData card;
    public DeckEntry entry;
    [Tooltip("평소 크기 (골라 둔 카드는 조금 크게)")]
    public float baseScale = 1f;
    public float hoverScale = 1.05f;

    private bool hovered;

    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e)
    {
        hovered = true;
        CardTooltip.Show((RectTransform)transform, card, entry);
    }

    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e)
    {
        hovered = false;
        CardTooltip.Hide((RectTransform)transform);
    }

    void Update()
    {
        float target = baseScale * (hovered ? hoverScale : 1f);
        float s = Mathf.MoveTowards(transform.localScale.x, target, Time.unscaledDeltaTime * 1.5f);
        transform.localScale = new Vector3(s, s, 1f);
    }

    void OnDisable()
    {
        hovered = false;
        CardTooltip.Hide((RectTransform)transform);
    }
}
