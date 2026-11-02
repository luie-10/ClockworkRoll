using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 손패에 표시되는 카드 UI 한 장.
/// - 등급(Paper~EGO)에 따라 배경 색이 달라진다.
/// - 적에게서 흡수한 카드(◈)는 보라색 톤과 침식 수치가 표시된다.
/// - 주사위 줄: ▶ 공격 / ◆ 수비 + 종류 색 + 범위, 효과는 그 아래 └ 줄.
/// - 마우스를 올리면 카드가 커지며 맨 앞으로 오고, 위에 상세 정보창(CardTooltip)이 뜬다.
/// - 기력/장력 단계가 모자라 지금 못 쓰는 카드는 흐려지고 기력 글자가 빨개진다.
/// - 클릭하면 BattleManager로 전달된다.
/// 프리팹 구조는 그대로 — 추가 연출(정렬용 Canvas, CanvasGroup 등)은 실행 중에 알아서 붙인다.
/// </summary>
public class CardView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI 참조 (프리팹에 연결)")]
    [SerializeField] private Text nameText;
    [SerializeField] private Text costText;
    [SerializeField] private Text diceText;
    [SerializeField] private Image backgroundImage;

    [Header("선택 표시")]
    [SerializeField] private Color selectedColor = new Color(1f, 0.9f, 0.4f);

    [Header("마우스 올림 연출")]
    [SerializeField] private float hoverScale = 1.15f;
    [SerializeField] private float selectedScale = 1.08f;
    [SerializeField] private float unusableAlpha = 0.55f;

    public CardInstance BoundCard { get; private set; }

    private System.Action<CardView> onClicked;
    private Color normalColor = Color.white;
    private Color costNormalColor;
    private bool hovered, selected;
    private int usableState = -1;   // -1 = 아직 모름, 0 = 못 씀, 1 = 쓸 수 있음

    private Canvas sortCanvas;      // 마우스를 올렸을 때 옆 카드들보다 앞에 그리기 위한 Canvas
    private CanvasGroup group;

    private Outline edge;           // 카드 테두리 (등급 색)
    private Text metaText;          // 이름 아래 "1칸 · 주석"
    private Image artImage;         // 이름 아래 그림 (카드 그림이 없으면 자동 일러스트)
    private Color edgeColor = Color.white;

    // 카드 몸통은 어두운 먹색 하나로 통일하고, 등급은 테두리 색으로만 구분한다 (밝은 바탕 + 밝은 글자 문제 해결)
    private static readonly Color cardInk = new Color(0.085f, 0.08f, 0.09f, 1f);
    private static readonly Color selectedInk = new Color(0.2f, 0.16f, 0.07f, 1f);
    // 흡수 카드(◈) 전용 색
    private static readonly Color absorbedBgColor = new Color(0.15f, 0.09f, 0.2f, 1f);
    private static readonly Color absorbedNameColor = new Color(0.88f, 0.68f, 1f);
    private static readonly Color absorbedEdgeColor = new Color(0.72f, 0.45f, 0.95f);
    private static readonly Color unusableCostColor = new Color(1f, 0.35f, 0.3f);

    void Awake()
    {
        // 피벗을 아래쪽으로: 커질 때 손패 받침대 아래로 파고들지 않고 위로 솟는다 (레이아웃 그룹이 피벗을 고려해 배치함)
        RectTransform rect = (RectTransform)transform;
        rect.pivot = new Vector2(0.5f, 0.1f);

        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();

        sortCanvas = GetComponent<Canvas>();
        if (sortCanvas == null) sortCanvas = gameObject.AddComponent<Canvas>();
        if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();   // 중첩 Canvas는 자기 Raycaster가 있어야 클릭된다

        ApplyLayout();

        // 효과 줄이 많아도 판 밖으로 넘치지 않게 글자 크기 자동 맞춤
        FitText(diceText, 11);
        FitText(nameText, 14);
        FitText(costText, 11);
    }

    /// <summary>
    /// 카드 배치 (프리팹 구조는 그대로 두고 자리/색만 잡는다):
    ///   ┌──────────────────┐  테두리 = 등급 색 (흡수 카드는 보라)
    ///   │ (2) 카드 이름     │  (2) = 기력 배지
    ///   │     1칸 · 주석    │
    ///   │ [   그림    ]     │  cardArt (없으면 Illustrator가 주사위 종류로 그린 문양)
    ///   │ ▶ 참격 2~6        │  주사위 판이 남은 공간을 꽉 채운다
    ///   │  └ [적중시] ...   │
    ///   └──────────────────┘
    /// </summary>
    private void ApplyLayout()
    {
        RectTransform self = (RectTransform)transform;
        self.sizeDelta = new Vector2(self.sizeDelta.x, 250f);

        if (backgroundImage != null)
        {
            backgroundImage.color = cardInk;
            backgroundImage.pixelsPerUnitMultiplier = 1.6f;
            edge = backgroundImage.GetComponent<Outline>();
            if (edge != null) edge.effectDistance = new Vector2(2f, -2f);
        }

        if (costText != null)
        {
            RectTransform plate = costText.transform.parent as RectTransform;
            if (plate != null && plate != transform)
            {
                plate.anchorMin = plate.anchorMax = plate.pivot = new Vector2(0f, 1f);
                plate.anchoredPosition = new Vector2(9f, -9f);
                plate.sizeDelta = new Vector2(38f, 38f);
                Image badge = plate.GetComponent<Image>();
                if (badge != null)
                {
                    badge.sprite = ProceduralSprites.Circle();
                    badge.type = Image.Type.Simple;
                    badge.color = new Color(0.05f, 0.09f, 0.14f, 1f);
                    Outline ring = badge.GetComponent<Outline>();
                    if (ring == null) ring = badge.gameObject.AddComponent<Outline>();
                    ring.effectColor = new Color(UITheme.Ki.r, UITheme.Ki.g, UITheme.Ki.b, 0.7f);
                    ring.effectDistance = new Vector2(1.5f, -1.5f);
                }
            }
            UIBuilder.Stretch(costText.rectTransform);
            UITheme.StyleText(costText, 22, UITheme.Ki, FontStyle.Bold);
            costText.alignment = TextAnchor.MiddleCenter;
        }

        if (nameText != null)
        {
            RectTransform rt = nameText.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(54f, -8f);
            rt.sizeDelta = new Vector2(-60f, 26f);
            UITheme.StyleText(nameText, 19, UITheme.TextMain, FontStyle.Bold);
            nameText.alignment = TextAnchor.MiddleLeft;

            metaText = UIBuilder.Label("MetaText", transform, "", 14, UITheme.TextMuted, TextAnchor.MiddleLeft, false);
            RectTransform m = metaText.rectTransform;
            m.anchorMin = new Vector2(0f, 1f); m.anchorMax = Vector2.one; m.pivot = new Vector2(0f, 1f);
            m.anchoredPosition = new Vector2(54f, -33f);
            m.sizeDelta = new Vector2(-60f, 18f);
            metaText.supportRichText = true;
        }

        if (diceText != null)
        {
            RectTransform plate = diceText.transform.parent as RectTransform;
            if (plate != null && plate != transform)
            {
                plate.anchorMin = Vector2.zero; plate.anchorMax = Vector2.one;
                plate.offsetMin = new Vector2(8f, 8f);
                plate.offsetMax = new Vector2(-8f, -124f);   // 위로 이름(58) + 그림(62)
                Image bg = plate.GetComponent<Image>();
                if (bg != null) bg.color = new Color(0f, 0f, 0f, 0.32f);
            }
            UIBuilder.Stretch(diceText.rectTransform, 8f);
            UITheme.StyleText(diceText, 16, UITheme.TextMain);
            diceText.alignment = TextAnchor.UpperLeft;
            diceText.lineSpacing = 1.05f;
        }

        // 그림 칸 (이름 아래, 테두리는 얇은 어둠)
        RectTransform art = UIBuilder.Rect("Art", transform);
        art.anchorMin = new Vector2(0f, 1f); art.anchorMax = Vector2.one; art.pivot = new Vector2(0.5f, 1f);
        art.anchoredPosition = new Vector2(0f, -58f);
        art.sizeDelta = new Vector2(-16f, 62f);
        artImage = art.gameObject.AddComponent<Image>();
        artImage.raycastTarget = false;
        Outline frame = art.gameObject.AddComponent<Outline>();
        frame.effectColor = new Color(0f, 0f, 0f, 0.8f);
        frame.effectDistance = new Vector2(1f, -1f);
    }

    private static void FitText(Text t, int minSize)
    {
        if (t == null) return;
        t.supportRichText = true;
        t.resizeTextForBestFit = true;
        t.resizeTextMaxSize = t.fontSize;
        t.resizeTextMinSize = Mathf.Min(minSize, t.fontSize);
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
    }

    /// <summary>HandManager가 카드 데이터를 묶어줄 때 호출.</summary>
    public void Bind(CardInstance card, System.Action<CardView> onClicked)
    {
        BoundCard = card;
        card.view = this;
        this.onClicked = onClicked;

        if (card.data != null)
        {
            bool absorbed = card.entry != null && card.entry.absorbed;
            normalColor = absorbed ? absorbedBgColor : cardInk;
            Color rarity = DiceInfo.RarityColor(card.data.rarity);
            edgeColor = absorbed ? absorbedEdgeColor : new Color(rarity.r, rarity.g, rarity.b, 0.85f);
            SetSelected(selected);
        }
        if (costText != null) costNormalColor = costText.color;

        Refresh();
    }

    /// <summary>코드로 UI 요소를 직접 연결할 때 사용 (테스트 부트스트래퍼 등).</summary>
    public void Configure(Text nameText, Text costText, Text diceText, Image backgroundImage)
    {
        this.nameText = nameText;
        this.costText = costText;
        this.diceText = diceText;
        this.backgroundImage = backgroundImage;
    }

    public void Refresh()
    {
        if (BoundCard == null || BoundCard.data == null) return;

        CardData d = BoundCard.data;
        DeckEntry entry = BoundCard.entry;
        bool absorbed = entry != null && entry.absorbed;

        if (nameText != null)
        {
            nameText.text = absorbed ? $"◈ {d.cardName}" : d.cardName;
            nameText.color = absorbed ? absorbedNameColor : UITheme.TextMain;
        }

        if (costText != null) costText.text = d.cost.ToString();   // 배지 안의 숫자 = 기력

        if (metaText != null)
        {
            // 칸 = 시계판에서 차지하는 시간
            string rarityHex = ColorUtility.ToHtmlStringRGB(DiceInfo.RarityColor(d.rarity));
            string meta = $"{TimedAction.LengthOf(d)}칸 · <color=#{rarityHex}>{DiceInfo.RarityName(d.rarity)}</color>";
            if (d.requireEmotionLevel > 0) meta += $" · <color=#ff7070>장력{d.requireEmotionLevel}</color>";
            metaText.text = meta;
        }

        // 주사위 줄 (예: "▶ 참격 2~6") + 효과 줄 (" └ [적중시] 다음 턴 녹 1") + 흡수 카드면 침식 수치
        if (diceText != null)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            if (d.diceList != null)
            {
                foreach (DiceData die in d.diceList)
                {
                    if (die == null) continue;
                    string mark = die.diceType == DiceType.Attack ? "▶" : "◆";
                    string hex = DiceInfo.ColorHex(die.diceDetail);
                    sb.AppendLine($"<color={hex}>{mark} <b>{DiceInfo.Name(die.diceDetail)}</b></color> <b>{die.minRoll}~{die.maxRoll}</b>");

                    if (die.effects != null)
                    {
                        foreach (DiceEffect fx in die.effects)
                        {
                            if (fx != null) sb.AppendLine($"<color=#ffb3c4> └ {fx.Describe()}</color>");
                        }
                    }
                }
            }
            if (absorbed) sb.AppendLine($"<color=#d9a6ff>침식 {entry.corruption}</color>");
            diceText.text = sb.ToString().TrimEnd('\n', '\r');
        }

        if (artImage != null) artImage.sprite = d.cardArt != null ? d.cardArt : Illustrator.CardArt(d);

        usableState = -1;   // 다음 Update에서 사용 가능 여부를 다시 칠한다
    }

    /// <summary>대상 지정 대기 중 표시 (금색 테두리 + 따뜻한 바탕 + 살짝 커짐).</summary>
    public void SetSelected(bool selected)
    {
        this.selected = selected;
        if (backgroundImage != null)
            backgroundImage.color = selected ? selectedInk : normalColor;
        if (edge != null)
        {
            edge.effectColor = selected ? selectedColor : edgeColor;
            edge.effectDistance = selected ? new Vector2(3f, -3f) : new Vector2(2f, -2f);
        }
    }

    void Update()
    {
        // 마우스를 올린 채 전투가 끝나 스토리로 넘어가면 (HUD가 숨겨짐) 올림 상태를 풀어 카드가 남아 보이지 않게
        if (hovered && GameManager.Instance != null && GameManager.Instance.State != GameManager.GameState.Battle)
            OnPointerExit(null);

        // 크기: 마우스 올림 > 선택 > 기본, 부드럽게 따라감 (히트스톱 중에도 움직이도록 unscaled)
        float target = hovered ? hoverScale : selected ? selectedScale : 1f;
        float s = Mathf.MoveTowards(transform.localScale.x, target, Time.unscaledDeltaTime * 2.5f);
        transform.localScale = new Vector3(s, s, 1f);

        UpdateUsable();
    }

    /// <summary>지금 낼 수 있는 카드인지 (기력 · 장력 단계)에 따라 흐리게 / 기력 글자 빨갛게.</summary>
    private void UpdateUsable()
    {
        if (BoundCard == null || BoundCard.data == null) return;

        BattleManager bm = BattleManager.Instance;
        bool usable = bm == null ||
                      (BoundCard.data.cost <= bm.CurrentKi && BoundCard.data.requireEmotionLevel <= bm.EmotionLevel);
        int state = usable ? 1 : 0;
        if (state == usableState) return;
        usableState = state;

        if (group != null) group.alpha = usable ? 1f : unusableAlpha;
        if (costText != null) costText.color = usable ? costNormalColor : unusableCostColor;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        SetOnTop(true);
        CardTooltip.Show(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        SetOnTop(false);
        CardTooltip.Hide(this);
    }

    /// <summary>옆 카드들보다 앞에 그린다 (손패가 겹쳐 있어도 커진 카드가 가려지지 않게).</summary>
    private void SetOnTop(bool onTop)
    {
        if (sortCanvas == null) return;
        Canvas parent = transform.parent != null ? transform.parent.GetComponentInParent<Canvas>() : null;
        sortCanvas.overrideSorting = onTop;
        if (onTop) sortCanvas.sortingOrder = (parent != null ? parent.sortingOrder : 0) + 10;

        // 전투 HUD는 반투명이지만, 보고 있는 카드만큼은 불투명하게 (HUD가 보일 때만 마우스가 닿으므로 스토리 중엔 해당 없음)
        if (group != null) group.ignoreParentGroups = onTop;
    }

    void OnDisable()
    {
        hovered = false;
        SetOnTop(false);
        CardTooltip.Hide(this);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (onClicked != null) onClicked(this);
    }
}
