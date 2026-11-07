using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전투 승리 연출 + 흡수할 태엽판 고르기.
///
///   1) 화면 가운데 "승리!"가 크게 떴다가
///   2) 쓰러뜨린 적들의 태엽판이 카드로 펼쳐진다 → 전투마다 확정으로 한 장을 골라 흡수
///   3) [흡수] 버튼 → 고른 태엽판이 덱에 ◈로 들어간다 (이미 가진 것이면 침식 +1)
///
/// 직접 붙일 필요 없음 — BattleManager가 승리 시 VictoryScreen.Play()로 띄운다 (전용 Canvas를 알아서 만든다).
/// </summary>
public class VictoryScreen : MonoBehaviour
{
    private static VictoryScreen instance;

    /// <summary>열려 있는 동안 true (테스트 자동 진행이 확인용으로 사용)</summary>
    public static bool IsOpen { get; private set; }

    private RectTransform root;
    private Text bannerText, subText, hintText;
    private RectTransform cardRow, cardScroll;   // cardRow = 격자 (스크롤되는 내용), cardScroll = 보이는 창
    private ScrollRect scroll;
    private Button confirmButton;
    private Text confirmLabel;

    private readonly List<CardData> pool = new List<CardData>();
    private readonly List<CardData> picked = new List<CardData>();
    private readonly List<Image> cardFrames = new List<Image>();
    private int pickCount;
    private bool confirmed;

    private static readonly Color frameNormal = new Color(0.08f, 0.07f, 0.06f, 0.95f);
    private static readonly Color framePicked = new Color(0.42f, 0.3f, 0.08f, 0.98f);

    /// <summary>
    /// 승리 연출 → 고르기. 끝나면 onDone(고른 태엽판 목록)을 부른다.
    /// count = 고를 수 있는 장수. ownedSources = 이미 흡수한 태엽판 이름 (침식 경고용)
    /// </summary>
    public static Coroutine Play(MonoBehaviour host, int count, List<CardData> spoils, ICollection<string> ownedSources,
                                 Action<List<CardData>> onDone)
    {
        if (instance == null) instance = Create();
        return host.StartCoroutine(instance.PlayRoutine(count, spoils, ownedSources, onDone));
    }

    /// <summary>테스트 자동 진행용: 앞에서부터 고를 수 있는 만큼 골라 바로 흡수</summary>
    public static void AutoPick()
    {
        if (instance == null || !IsOpen) return;
        for (int i = 0; i < instance.pool.Count && instance.picked.Count < instance.pickCount; i++)
            instance.Toggle(i);
        instance.confirmed = true;
    }

    private IEnumerator PlayRoutine(int count, List<CardData> spoils, ICollection<string> ownedSources,
                                    Action<List<CardData>> onDone)
    {
        IsOpen = true;
        confirmed = false;
        picked.Clear();
        pool.Clear();
        foreach (CardData c in spoils)
        {
            if (c != null && !pool.Contains(c)) pool.Add(c);   // 같은 태엽판은 한 번만 보여준다
        }
        pickCount = Mathf.Min(Mathf.Max(0, count), pool.Count);

        root.gameObject.SetActive(true);
        cardScroll.gameObject.SetActive(false);
        confirmButton.gameObject.SetActive(false);
        subText.text = "";
        hintText.text = "";

        // 1) 승리! — 크게 튀어나왔다가 자리 잡는다
        bannerText.text = "승리!";
        RectTransform banner = bannerText.rectTransform;
        banner.anchoredPosition = new Vector2(0f, 0f);
        for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.5f;
            banner.localScale = Vector3.one * Mathf.Lerp(2.2f, 1f, 1f - Mathf.Pow(1f - k, 3f));
            bannerText.color = new Color(1f, 0.85f, 0.4f, k);
            yield return null;
        }
        banner.localScale = Vector3.one;
        yield return new WaitForSecondsRealtime(0.9f);

        // 2) 위로 올라가며 흡수 고르기 화면으로
        for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
        {
            banner.anchoredPosition = new Vector2(0f, Mathf.Lerp(0f, 300f, t / 0.3f));
            yield return null;
        }
        banner.anchoredPosition = new Vector2(0f, 300f);

        if (pickCount <= 0)
        {
            subText.text = "삼킬 기억이 없다.";
            yield return new WaitForSecondsRealtime(2.2f);
        }
        else
        {
            subText.text = $"쓰러뜨린 적의 기억을 삼킨다 — <color=#ffd159>{pickCount}장</color> 고르기";
            BuildCards(ownedSources);
            cardScroll.gameObject.SetActive(true);
            scroll.verticalNormalizedPosition = 1f;   // 맨 위부터
            confirmButton.gameObject.SetActive(true);
            RefreshPicks();
            while (!confirmed) yield return null;
        }

        root.gameObject.SetActive(false);
        IsOpen = false;
        if (onDone != null) onDone(new List<CardData>(picked));
    }

    // ─────────────────────────── 카드 고르기 ───────────────────────────

    private void BuildCards(ICollection<string> ownedSources)
    {
        for (int i = cardRow.childCount - 1; i >= 0; i--) Destroy(cardRow.GetChild(i).gameObject);
        cardFrames.Clear();

        for (int i = 0; i < pool.Count; i++)
        {
            CardData card = pool[i];
            int index = i;

            Image frame = UIBuilder.Panel("Card " + card.cardName, cardRow, frameNormal);
            frame.rectTransform.sizeDelta = new Vector2(250f, 330f);
            frame.raycastTarget = true;
            Button button = frame.gameObject.AddComponent<Button>();
            button.targetGraphic = frame;
            button.onClick.AddListener(() => Toggle(index));
            cardFrames.Add(frame);

            string rarityHex = "#" + ColorUtility.ToHtmlStringRGB(DiceInfo.RarityColor(card.rarity));
            bool again = ownedSources != null && ownedSources.Contains(card.cardName);
            string text =
                $"<size=26><b>{card.cardName}</b></size>\n" +
                $"<color={rarityHex}>{DiceInfo.RarityName(card.rarity)}</color> · <color=#8cd9ff>기력 {card.cost}</color> · <color=#ffd159>{TimedAction.LengthOf(card)}칸</color>\n\n" +
                DiceInfo.Detail(card) +
                (again ? "\n\n<color=#d9a6ff>이미 삼킨 기억 — 침식 +1</color>" : "\n\n<color=#d9a6ff>◈ 흡수 · 침식 1</color>");

            Text label = UIBuilder.Label("Text", frame.transform, text, 19, Color.white, TextAnchor.UpperLeft);
            UIBuilder.Stretch(label.rectTransform, 14f);
            label.rectTransform.offsetMax = new Vector2(-14f, -104f);   // 위쪽은 그림 칸
            AddCardArt(frame.rectTransform, card);
            CardHover hover = frame.gameObject.AddComponent<CardHover>();
            hover.card = card;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 12;
            label.resizeTextMaxSize = 19;
            label.verticalOverflow = VerticalWrapMode.Truncate;
        }
    }

    /// <summary>카드 타일 위쪽에 그림 칸 (카드 그림이 없으면 자동 일러스트) — 갈림길 보상 화면도 같이 쓴다</summary>
    public static void AddCardArt(RectTransform frame, CardData card)
    {
        RectTransform art = UIBuilder.Rect("Art", frame);
        art.anchorMin = new Vector2(0f, 1f); art.anchorMax = Vector2.one; art.pivot = new Vector2(0.5f, 1f);
        art.anchoredPosition = new Vector2(0f, -12f);
        art.sizeDelta = new Vector2(-24f, 86f);
        Image img = art.gameObject.AddComponent<Image>();
        img.sprite = card.cardArt != null ? card.cardArt : Illustrator.CardArt(card);
        img.raycastTarget = false;
        Outline o = art.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(0f, 0f, 0f, 0.8f);
        o.effectDistance = new Vector2(1f, -1f);
    }

    private void Toggle(int index)
    {
        CardData card = pool[index];
        if (picked.Contains(card)) picked.Remove(card);
        else if (picked.Count < pickCount) picked.Add(card);
        else if (pickCount == 1) { picked.Clear(); picked.Add(card); }   // 1장만 고를 땐 바로 바꿔 고르기
        RefreshPicks();
    }

    private void RefreshPicks()
    {
        for (int i = 0; i < cardFrames.Count; i++)
        {
            bool on = picked.Contains(pool[i]);
            cardFrames[i].color = on ? framePicked : frameNormal;
            cardFrames[i].GetComponent<CardHover>().baseScale = on ? 1.06f : 1f;
        }

        bool ready = picked.Count == pickCount;
        confirmButton.interactable = ready;
        confirmLabel.text = ready ? $"흡수 ({picked.Count}장)" : $"{picked.Count} / {pickCount}장 선택";
        hintText.text = pool.Count > 6
            ? "마우스를 올리면 자세히 · 클릭해서 고르기 · <color=#dcb06b>마우스 휠로 스크롤해서 더 보기</color>"
            : "마우스를 올리면 자세히 · 클릭해서 고르기";
    }

    // ─────────────────────────── 생성 ───────────────────────────

    private static VictoryScreen Create()
    {
        GameObject go = new GameObject("VictoryScreenCanvas");
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 600;   // 모든 전투 UI 위
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();

        VictoryScreen screen = go.AddComponent<VictoryScreen>();

        Image dim = UIBuilder.Panel("Dim", go.transform, new Color(0f, 0f, 0f, 0.72f), false);
        UIBuilder.Stretch(dim.rectTransform);
        dim.raycastTarget = true;   // 뒤의 전투 화면 클릭 막기
        screen.root = dim.rectTransform;

        screen.bannerText = UIBuilder.Label("Banner", dim.transform, "승리!", 140, new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter);
        UIBuilder.Place(screen.bannerText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200f, 200f));
        screen.bannerText.fontStyle = FontStyle.Bold;

        screen.subText = UIBuilder.Label("Sub", dim.transform, "", 30, Color.white, TextAnchor.MiddleCenter);
        UIBuilder.Place(screen.subText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 190f), new Vector2(1400f, 110f));

        // 카드 격자: 한 줄에 6장, 넘치면 마우스 휠 / 드래그 / 오른쪽 막대로 위아래 스크롤
        screen.cardScroll = UIBuilder.Rect("CardScroll", dim.transform);
        UIBuilder.Place(screen.cardScroll, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -25f), new Vector2(1640f, 390f));
        Image hit = screen.cardScroll.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);   // 카드 사이 빈 곳에서도 휠이 먹도록

        RectTransform viewport = UIBuilder.Rect("Viewport", screen.cardScroll);
        UIBuilder.Stretch(viewport);
        viewport.offsetMax = new Vector2(-24f, 0f);   // 오른쪽은 스크롤 막대 자리
        viewport.gameObject.AddComponent<RectMask2D>();

        screen.cardRow = UIBuilder.Rect("Cards", viewport);
        screen.cardRow.anchorMin = new Vector2(0f, 1f);
        screen.cardRow.anchorMax = Vector2.one;
        screen.cardRow.pivot = new Vector2(0.5f, 1f);
        screen.cardRow.sizeDelta = Vector2.zero;
        GridLayoutGroup grid = screen.cardRow.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(250f, 330f);
        grid.spacing = new Vector2(16f, 18f);
        grid.padding = new RectOffset(0, 0, 18, 18);   // 골랐을 때 살짝 커지는 카드가 잘리지 않게
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 6;
        grid.childAlignment = TextAnchor.UpperCenter;
        ContentSizeFitter fit = screen.cardRow.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Image barBg = UIBuilder.Panel("Scrollbar", screen.cardScroll, new Color(1f, 1f, 1f, 0.08f), false);
        UIBuilder.Place(barBg.rectTransform, new Vector2(1f, 0f), Vector2.one, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(10f, -20f));
        RectTransform slide = UIBuilder.Rect("Sliding Area", barBg.transform);
        UIBuilder.Stretch(slide);
        Image handle = UIBuilder.Panel("Handle", slide, new Color(UITheme.Brass.r, UITheme.Brass.g, UITheme.Brass.b, 0.85f), false);
        UIBuilder.Stretch(handle.rectTransform);
        Scrollbar bar = barBg.gameObject.AddComponent<Scrollbar>();
        bar.direction = Scrollbar.Direction.BottomToTop;
        bar.handleRect = handle.rectTransform;
        bar.targetGraphic = handle;

        screen.scroll = screen.cardScroll.gameObject.AddComponent<ScrollRect>();
        screen.scroll.viewport = viewport;
        screen.scroll.content = screen.cardRow;
        screen.scroll.horizontal = false;
        screen.scroll.movementType = ScrollRect.MovementType.Clamped;
        screen.scroll.scrollSensitivity = 45f;
        screen.scroll.verticalScrollbar = bar;
        screen.scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        screen.hintText = UIBuilder.Label("Hint", dim.transform, "", 22, new Color(0.75f, 0.75f, 0.75f), TextAnchor.MiddleCenter);
        UIBuilder.Place(screen.hintText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -240f), new Vector2(1100f, 40f));

        screen.confirmButton = UIBuilder.Button("Confirm", dim.transform, "흡수", new Color(0.55f, 0.38f, 0.12f), 32);
        UITheme.StyleButton(screen.confirmButton, true, 28);
        UIBuilder.Place((RectTransform)screen.confirmButton.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -300f), new Vector2(300f, 72f));
        screen.confirmLabel = screen.confirmButton.GetComponentInChildren<Text>();
        screen.confirmButton.onClick.AddListener(() => screen.confirmed = true);

        dim.gameObject.SetActive(false);
        return screen;
    }

    void OnDestroy()
    {
        if (instance == this) { instance = null; IsOpen = false; }
    }
}
