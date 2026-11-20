using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 정밀 · 걸작 이상 태엽판을 쓸 때 화면을 가로지르는 컷인.
///   정밀: 어두운 띠가 펼쳐지고 그림 · 이름이 옆에서 미끄러져 들어온다
///   걸작: 위아래 검은 띠(레터박스)가 내려오고, 금빛 줄기가 띠를 훑고, 더 오래 머문다
/// 내 태엽판은 왼쪽에서, 적의 태엽판은 오른쪽에서 들어온다.
/// Play는 전투가 기다려야 할 시간(초)을 돌려준다 — 컷인이 끝까지 사라지기 전에 합이 시작된다.
/// </summary>
public class SkillCutIn : MonoBehaviour
{
    private static SkillCutIn instance;

    /// <summary>컷인을 띄울 만한 태엽판인가 (정밀 이상)</summary>
    public static bool IsSpecial(CardData card)
    {
        return card != null && card.rarity >= CardRarity.Precision;
    }

    public static bool IsMasterwork(CardData card)
    {
        return card != null && card.rarity >= CardRarity.Masterwork;
    }

    private CanvasGroup group;
    private RectTransform band, content, sweep, topBar, bottomBar, art;
    private Image bandLineTop, bandLineBottom, artImage, sweepImage;
    private Text nameText, subText;
    private Coroutine running;

    /// <summary>컷인을 띄우고, 전투가 기다릴 시간을 돌려준다</summary>
    public static float Play(CardData card, Unit owner)
    {
        if (card == null) return 0f;
        if (instance == null) instance = Build();
        if (instance.running != null) instance.StopCoroutine(instance.running);
        bool fromLeft = owner == null || owner.isPlayer;
        bool artTier = IsMasterwork(card);
        instance.running = instance.StartCoroutine(instance.Run(card, owner, fromLeft, artTier));
        return artTier ? 0.85f : 0.55f;
    }

    private IEnumerator Run(CardData card, Unit owner, bool fromLeft, bool artTier)
    {
        Color rarity = DiceInfo.RarityColor(card.rarity);
        float side = fromLeft ? -1f : 1f;
        float total = artTier ? 1.35f : 0.95f;

        // 내용 채우기
        artImage.sprite = card.cardArt != null ? card.cardArt : Illustrator.CardArt(card);
        if (artImage.sprite != null)
        {
            float aspect = artImage.sprite.rect.width / Mathf.Max(1f, artImage.sprite.rect.height);
            art.sizeDelta = new Vector2(136f * aspect, 136f);
        }
        nameText.text = card.cardName;
        nameText.color = rarity;
        string who = owner != null ? owner.unitName : "";
        subText.text = $"{who}  ·  {DiceInfo.RarityName(card.rarity)} 태엽판";
        bandLineTop.color = bandLineBottom.color = rarity;
        sweepImage.color = UITheme.Alpha(rarity, artTier ? 0.55f : 0.32f);

        // 그림은 들어오는 쪽, 글자는 그 옆 (가운데 기준)
        art.anchoredPosition = new Vector2(side * 520f, 0f);
        RectTransform nameRt = nameText.rectTransform, subRt = subText.rectTransform;
        TextAnchor anchor = fromLeft ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
        nameText.alignment = subText.alignment = anchor;
        float textX = side * (520f - art.sizeDelta.x * 0.5f - 30f);
        nameRt.pivot = subRt.pivot = new Vector2(fromLeft ? 0f : 1f, 0.5f);
        nameRt.anchoredPosition = new Vector2(textX, 18f);
        subRt.anchoredPosition = new Vector2(textX, -46f);

        topBar.gameObject.SetActive(artTier);
        bottomBar.gameObject.SetActive(artTier);
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        float t = 0f;
        while (t < total)
        {
            t += Time.unscaledDeltaTime;
            float open = Ease(Mathf.Clamp01(t / 0.14f));
            float close = Mathf.Clamp01((total - t) / 0.22f);

            group.alpha = close;
            band.localScale = new Vector3(1f, open, 1f);

            // 글자 · 그림: 옆에서 미끄러져 들어와 천천히 흐른다
            float slide = Ease(Mathf.Clamp01((t - 0.05f) / 0.24f));
            float drift = (t / total) * 26f;
            content.anchoredPosition = new Vector2(side * ((1f - slide) * 360f - drift), 0f);

            // 빛줄기: 띠를 한 번 훑고 지나간다
            float s = Mathf.Clamp01((t - 0.08f) / (artTier ? 0.55f : 0.45f));
            sweep.anchoredPosition = new Vector2(Mathf.Lerp(side * 1200f, -side * 1200f, s), 0f);

            if (artTier)
            {
                float bars = Ease(Mathf.Clamp01(t / 0.2f)) * close;
                topBar.sizeDelta = new Vector2(0f, 84f * bars);
                bottomBar.sizeDelta = new Vector2(0f, 84f * bars);
            }
            yield return null;
        }
        gameObject.SetActive(false);
        running = null;
    }

    private static float Ease(float x) { return 1f - (1f - x) * (1f - x) * (1f - x); }

    // ─────────────────────────── 생성 ───────────────────────────

    private static SkillCutIn Build()
    {
        GameObject go = new GameObject("SkillCutInCanvas");
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;   // 시계판(120) 위, 정보창(490~) 아래
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform root = UIBuilder.Rect("Root", go.transform);
        UIBuilder.Stretch(root);
        SkillCutIn c = root.gameObject.AddComponent<SkillCutIn>();
        c.group = root.gameObject.AddComponent<CanvasGroup>();
        c.group.blocksRaycasts = false;
        c.group.interactable = false;

        // 걸작: 위아래 레터박스
        c.topBar = Bar(root, "TopBar", true);
        c.bottomBar = Bar(root, "BottomBar", false);

        // 띠
        Vector2 mid = new Vector2(0.5f, 0.5f);
        Image bandImg = Plain("Band", root, new Color(0.03f, 0.03f, 0.05f, 0.86f));
        c.band = bandImg.rectTransform;
        UIBuilder.Place(c.band, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), mid, new Vector2(0f, 300f), new Vector2(0f, 176f));
        bandImg.gameObject.AddComponent<RectMask2D>();
        c.bandLineTop = Plain("LineTop", c.band, Color.white);
        UIBuilder.Place(c.bandLineTop.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 4f));
        c.bandLineBottom = Plain("LineBottom", c.band, Color.white);
        UIBuilder.Place(c.bandLineBottom.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 4f));

        // 빛줄기 (띠 안을 훑는다)
        c.sweepImage = Plain("Sweep", c.band, Color.white);
        c.sweepImage.sprite = Sprite.Create(ImpactFX.GlowTexture(), new Rect(0, 0, ImpactFX.GlowTexture().width, ImpactFX.GlowTexture().height), mid);
        c.sweep = c.sweepImage.rectTransform;
        UIBuilder.Place(c.sweep, mid, mid, mid, Vector2.zero, new Vector2(520f, 420f));

        // 내용: 그림 + 이름 + 부제
        c.content = UIBuilder.Rect("Content", c.band);
        UIBuilder.Place(c.content, mid, mid, mid, Vector2.zero, new Vector2(1920f, 200f));
        c.artImage = Plain("Art", c.content, Color.white);
        c.artImage.preserveAspect = true;
        c.art = c.artImage.rectTransform;
        UIBuilder.Place(c.art, mid, mid, mid, Vector2.zero, new Vector2(150f, 150f));

        c.nameText = UIBuilder.Label("Name", c.content, "", 66, Color.white, TextAnchor.MiddleLeft);
        c.nameText.fontStyle = FontStyle.Bold;
        c.nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
        UIBuilder.Place(c.nameText.rectTransform, mid, mid, mid, Vector2.zero, new Vector2(900f, 90f));
        c.subText = UIBuilder.Label("Sub", c.content, "", 24, UITheme.TextMuted, TextAnchor.MiddleLeft);
        c.subText.horizontalOverflow = HorizontalWrapMode.Overflow;
        UIBuilder.Place(c.subText.rectTransform, mid, mid, mid, Vector2.zero, new Vector2(900f, 34f));

        root.gameObject.SetActive(false);
        return c;
    }

    private static Image Plain(string name, Transform parent, Color color)
    {
        RectTransform rt = UIBuilder.Rect(name, parent);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static RectTransform Bar(Transform parent, string name, bool top)
    {
        Image img = Plain(name, parent, new Color(0f, 0f, 0f, 0.92f));
        RectTransform rt = img.rectTransform;
        rt.anchorMin = new Vector2(0f, top ? 1f : 0f);
        rt.anchorMax = new Vector2(1f, top ? 1f : 0f);
        rt.pivot = new Vector2(0.5f, top ? 1f : 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, 0f);
        return rt;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
