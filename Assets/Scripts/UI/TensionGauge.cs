using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 장력 게이지 — 기력 패널 바로 위에 붙는다 (씬에 따로 둘 필요 없음, BattleManager가 Ensure로 만든다).
/// 서로의 칸이 맞부딪힐 때마다 차오르고, 칸 3개(1~3단계)가 차례로 채워진다.
/// </summary>
public class TensionGauge : MonoBehaviour
{
    private static TensionGauge instance;

    private Text label;
    private Image[] fills;
    private Image[] frames;
    private float[] shown;       // 화면에 보이는 채움 (부드럽게 따라간다)
    private float pulse;         // 단계가 오를 때 번쩍

    private static readonly Color fillColor = new Color(1f, 0.55f, 0.3f);
    private static readonly Color fullColor = new Color(1f, 0.78f, 0.35f);
    private static readonly Color frameIdle = new Color(1f, 1f, 1f, 0.12f);
    private static readonly Color frameFull = new Color(1f, 0.8f, 0.4f, 0.9f);

    /// <summary>기력 표시(kiText) 위에 장력 게이지를 만든다. 이미 있으면 그대로.</summary>
    public static TensionGauge Ensure(Text kiText)
    {
        if (instance != null) return instance;
        if (kiText == null) return null;

        // 기력 글자가 패널 안에 있으면 그 패널 위에, 아니면 글자 위에 붙인다
        RectTransform anchor = kiText.rectTransform;
        if (kiText.transform.parent != null && kiText.transform.parent.GetComponent<Image>() != null)
            anchor = (RectTransform)kiText.transform.parent;

        const float height = 58f;
        const float gap = 8f;
        RectTransform root = UIBuilder.Rect("TensionGauge", anchor.parent);
        root.anchorMin = anchor.anchorMin;
        root.anchorMax = anchor.anchorMax;
        root.pivot = anchor.pivot;
        float width = anchor.rect.width > 1f ? anchor.rect.width : 210f;
        root.sizeDelta = new Vector2(width, height);
        root.anchoredPosition = anchor.anchoredPosition +
            new Vector2(0f, anchor.rect.height * (1f - anchor.pivot.y) + gap + height * anchor.pivot.y);

        Image panel = root.gameObject.AddComponent<Image>();
        UITheme.StylePanel(panel, UITheme.Ink, UITheme.BrassLine);
        panel.raycastTarget = false;

        TensionGauge g = root.gameObject.AddComponent<TensionGauge>();
        g.Build(root);
        instance = g;
        return g;
    }

    private void Build(RectTransform root)
    {
        label = UIBuilder.Label("Label", root, "장력 0단계", 20, new Color(1f, 0.72f, 0.45f), TextAnchor.UpperCenter);
        UIBuilder.Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0f, -3f), new Vector2(-12f, 26f));
        label.raycastTarget = false;

        fills = new Image[BattleManager.MaxTensionStage];
        frames = new Image[BattleManager.MaxTensionStage];
        shown = new float[BattleManager.MaxTensionStage];

        RectTransform row = UIBuilder.Rect("Segments", root);
        UIBuilder.Place(row, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(-20f, 16f));
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 6f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        for (int i = 0; i < fills.Length; i++)
        {
            fills[i] = UIBuilder.Gauge("Stage" + (i + 1), row, fillColor);
            frames[i] = fills[i].transform.parent.GetComponent<Image>();
            if (frames[i] != null) frames[i].raycastTarget = false;
        }
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Update()
    {
        BattleManager bm = BattleManager.Instance;
        if (bm == null || fills == null) return;

        int stage = bm.TensionStage;
        int per = Mathf.Max(1, bm.tensionPerStage);

        for (int i = 0; i < fills.Length; i++)
        {
            float target = Mathf.Clamp01((bm.TensionPoints - i * per) / (float)per);
            shown[i] = Mathf.MoveTowards(shown[i], target, Time.unscaledDeltaTime * 3f);
            fills[i].fillAmount = shown[i];
            bool full = shown[i] >= 0.999f;
            fills[i].color = full ? fullColor : fillColor;
            if (frames[i] != null) frames[i].color = full ? frameFull : frameIdle;
        }

        string bonus = stage > 0 ? $" <size=16><color=#ffd159>(전리품 +{stage})</color></size>" : "";
        label.text = $"장력 {stage}단계{bonus}";

        // 단계가 오르면 글자가 잠깐 커진다
        pulse = Mathf.MoveTowards(pulse, 0f, Time.unscaledDeltaTime * 2.5f);
        label.transform.localScale = Vector3.one * (1f + 0.25f * pulse);
    }

    /// <summary>단계가 올랐을 때 BattleManager가 부른다</summary>
    public static void Pulse()
    {
        if (instance != null) instance.pulse = 1f;
    }
}
