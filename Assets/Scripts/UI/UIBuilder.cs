using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 코드로 UI를 만드는 공용 도우미 (런타임 TestBoot와 에디터 자동 세팅이 같이 쓴다).
/// 패널은 반투명 어두운 둥근 상자 — 3D 배경 위에서도 글자가 잘 보이게.
/// </summary>
public static class UIBuilder
{
    /// <summary>패널에 쓸 둥근 상자 스프라이트. 에디터에서는 저장된 PNG 에셋을 넣어준다 (씬에 저장되도록).</summary>
    public static Sprite BoxSprite;

    public static readonly Color PanelColor = new Color(0.04f, 0.04f, 0.05f, 0.4f);   // 반투명: 뒤의 무대가 비쳐 보임
    public static readonly Color PanelBorder = new Color(0.75f, 0.6f, 0.35f, 0.9f);   // 황동색 테두리

    private static Font font;

    public static Font DefaultFont
    {
        get
        {
            if (font == null)
            {
                try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
                if (font == null) { try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }
            }
            return font;
        }
    }

    private static Sprite Box { get { return BoxSprite != null ? BoxSprite : ProceduralSprites.RoundedBox(); } }

    // ─────────────────────────── 기본 ───────────────────────────

    public static RectTransform Rect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    public static void Stretch(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    /// <summary>어두운 둥근 패널 + 얇은 황동 테두리.</summary>
    public static Image Panel(string name, Transform parent, Color? color = null, bool border = true)
    {
        RectTransform rt = Rect(name, parent);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = Box;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1f;
        img.color = color ?? PanelColor;

        if (border)
        {
            Outline o = rt.gameObject.AddComponent<Outline>();
            o.effectColor = PanelBorder;
            o.effectDistance = new Vector2(2f, -2f);
        }
        return img;
    }

    public static Text Label(string name, Transform parent, string text, int size, Color color, TextAnchor anchor, bool outline = true)
    {
        RectTransform rt = Rect(name, parent);
        Text t = rt.gameObject.AddComponent<Text>();
        t.font = DefaultFont;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = anchor;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;   // 글자가 클릭(카드/적)을 가로채지 않게

        if (outline)
        {
            Outline o = rt.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.9f);
            o.effectDistance = new Vector2(1.5f, -1.5f);
        }
        return t;
    }

    public static Button Button(string name, Transform parent, string label, Color color, int fontSize = 30)
    {
        Image img = Panel(name, parent, color);
        Button button = img.gameObject.AddComponent<Button>();
        button.targetGraphic = img;

        ColorBlock cb = button.colors;
        cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
        cb.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        cb.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
        button.colors = cb;

        Text t = Label("Label", img.transform, label, fontSize, Color.white, TextAnchor.MiddleCenter);
        Stretch(t.rectTransform);
        return button;
    }

    /// <summary>왼→오로 차오르는 게이지. 반환값은 채움 Image (fillAmount 0~1).</summary>
    public static Image Gauge(string name, Transform parent, Color fillColor)
    {
        Image bg = Panel(name, parent, new Color(0f, 0f, 0f, 0.6f), false);

        RectTransform fillRt = Rect("Fill", bg.transform);
        Stretch(fillRt, 3f);
        Image fill = fillRt.gameObject.AddComponent<Image>();
        fill.sprite = Box;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 0f;
        fill.color = fillColor;
        fill.raycastTarget = false;
        return fill;
    }

    /// <summary>
    /// 마우스 휠/드래그로 스크롤되는 로그 패널. 반환값은 내용 Text (BattleManager.battleLogText에 연결).
    /// 구조: Panel(ScrollRect) → Title / Viewport(RectMask2D) → Content(Text + ContentSizeFitter) / Scrollbar
    /// </summary>
    public static Text ScrollLog(string name, Transform parent, string title)
    {
        Image panel = Panel(name, parent);
        RectTransform panelRt = panel.rectTransform;

        Text titleText = Label("Title", panelRt, title, 22, new Color(1f, 0.82f, 0.5f), TextAnchor.UpperLeft);
        Place(titleText.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, 1f), new Vector2(16f, -10f), new Vector2(-32f, 30f));

        RectTransform viewport = Rect("Viewport", panelRt);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(14f, 12f);
        viewport.offsetMax = new Vector2(-26f, -44f);
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform content = Rect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        Text text = content.gameObject.AddComponent<Text>();
        text.font = DefaultFont;
        text.fontSize = 20;
        text.lineSpacing = 1.1f;
        text.color = new Color(0.92f, 0.92f, 0.92f);
        text.alignment = TextAnchor.UpperLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 스크롤바 (오른쪽 얇은 막대)
        Image barBg = Panel("Scrollbar", panelRt, new Color(1f, 1f, 1f, 0.08f), false);
        Place(barBg.rectTransform, new Vector2(1f, 0f), Vector2.one, new Vector2(1f, 0.5f), new Vector2(-8f, -16f), new Vector2(10f, -56f));
        RectTransform slide = Rect("Sliding Area", barBg.transform);
        Stretch(slide);
        Image handle = Panel("Handle", slide, new Color(0.85f, 0.7f, 0.45f, 0.8f), false);
        Stretch(handle.rectTransform);
        Scrollbar bar = barBg.gameObject.AddComponent<Scrollbar>();
        bar.direction = Scrollbar.Direction.BottomToTop;
        bar.handleRect = handle.rectTransform;
        bar.targetGraphic = handle;

        // 패널 자체가 휠 입력을 받도록 (배경 Image가 raycast 대상)
        ScrollRect scroll = panel.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 35f;
        scroll.verticalScrollbar = bar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scroll.verticalScrollbarSpacing = 4f;

        return text;
    }
}
