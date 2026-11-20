using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 장력 게이지 — 기력 패널 바로 위에 붙는다 (씬에 따로 둘 필요 없음, BattleManager가 Ensure로 만든다).
/// 서로의 칸이 맞부딪힐 때마다 차오르고, 칸 3개(1~3단계)가 차례로 채워진다. 숫자 대신 칸 안의 눈금 · 금빛으로 단계를 보여 준다.
/// </summary>
public class TensionGauge : MonoBehaviour
{
    private static TensionGauge instance;

    private Image[] fills;
    private Image[] frames;
    private float[] shown;       // 화면에 보이는 채움 (부드럽게 따라간다)
    private float pulse;         // 단계가 오를 때 번쩍

    private static readonly Color fillColor = UITheme.Tension;
    private static readonly Color fullColor = UITheme.Gold;
    private static readonly Color frameIdle = UITheme.Alpha(UITheme.TextMain, 0.12f);
    private static readonly Color frameFull = UITheme.Alpha(UITheme.Gold, 0.9f);

    /// <summary>기력 표시(kiText) 위에 장력 게이지를 만든다. 이미 있으면 그대로.</summary>
    public static TensionGauge Ensure(Text kiText)
    {
        if (instance != null) return instance;
        if (kiText == null) return null;

        // 기력 글자가 패널 안에 있으면 그 패널 위에, 아니면 글자 위에 붙인다
        RectTransform anchor = kiText.rectTransform;
        if (kiText.transform.parent != null && kiText.transform.parent.GetComponent<Image>() != null)
            anchor = (RectTransform)kiText.transform.parent;

        const float height = 32f;   // 글자 없이 게이지만
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
        panel.raycastTarget = true;   // 마우스를 올리면 설명

        TensionGauge g = root.gameObject.AddComponent<TensionGauge>();
        g.Build(root);
        g.border = root.Find("Frame") != null ? root.Find("Frame").GetComponent<Image>() : null;
        HoverHint.Attach(root.gameObject, Describe);
        instance = g;
        return g;
    }

    private void Build(RectTransform root)
    {
        fills = new Image[BattleManager.MaxTensionStage];
        frames = new Image[BattleManager.MaxTensionStage];
        shown = new float[BattleManager.MaxTensionStage];

        RectTransform row = UIBuilder.Rect("Segments", root);
        UIBuilder.Place(row, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-20f, 16f));
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 6f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        // 한 단계 = 맞부딪힘 몇 번 — 칸 안에 눈금을 그어 "다음 단계까지 몇 번 남았나"가 숫자 없이 보이게
        int per = BattleManager.Instance != null ? Mathf.Max(1, BattleManager.Instance.tensionPerStage) : 3;
        for (int i = 0; i < fills.Length; i++)
        {
            fills[i] = UIBuilder.Gauge("Stage" + (i + 1), row, fillColor);
            frames[i] = fills[i].transform.parent.GetComponent<Image>();
            if (frames[i] != null) frames[i].raycastTarget = false;
            for (int t = 1; t < per; t++)
            {
                RectTransform tick = UIBuilder.Rect("Tick", fills[i].transform.parent);
                tick.anchorMin = new Vector2(t / (float)per, 0f);
                tick.anchorMax = new Vector2(t / (float)per, 1f);
                tick.pivot = new Vector2(0.5f, 0.5f);
                tick.sizeDelta = new Vector2(2f, -4f);
                tick.anchoredPosition = Vector2.zero;
                Image img = tick.gameObject.AddComponent<Image>();
                img.color = UITheme.Alpha(UITheme.Backdrop, 0.85f);
                img.raycastTarget = false;
            }
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

        // 단계가 오르면 게이지 전체가 잠깐 부풀고, 테두리가 금빛으로 번쩍인다
        pulse = Mathf.MoveTowards(pulse, 0f, Time.unscaledDeltaTime * 2.5f);
        transform.localScale = Vector3.one * (1f + 0.08f * pulse);
        if (border != null)
            border.color = Color.Lerp(stage > 0 ? UITheme.Alpha(UITheme.Gold, 0.35f + 0.15f * stage) : UITheme.BrassLine, UITheme.Gold, pulse);
    }

    private Image border;

    /// <summary>마우스를 올렸을 때의 설명 (지금 단계 · 쌓인 양도 함께)</summary>
    private static string Describe()
    {
        BattleManager bm = BattleManager.Instance;
        int per = bm != null ? Mathf.Max(1, bm.tensionPerStage) : 3;
        string now = bm != null
            ? $"  <color={UITheme.HexTension}>{bm.TensionStage}단계</color>  <color={UITheme.HexMuted}>({bm.TensionPoints} / {per * BattleManager.MaxTensionStage})</color>"
            : "";
        return $"<b><color={UITheme.HexTension}>장력</color></b>{now}\n" +
               $"<color={UITheme.HexMuted}>서로의 칸이 맞부딪힐 때마다 차오른다. 칸 하나 = 한 단계, 눈금 하나 = 맞부딪힘 한 번.</color>\n" +
               $"<color={UITheme.HexGold}>단계마다</color> 이기면 삼킬 수 있는 기억 +1 · 장력이 필요한 태엽판을 낼 수 있다.\n" +
               $"<color={UITheme.HexMuted}>금빛으로 찬 칸 = 채운 단계</color>";
    }

    /// <summary>단계가 올랐을 때 BattleManager가 부른다</summary>
    public static void Pulse()
    {
        if (instance != null) instance.pulse = 1f;
    }
}
