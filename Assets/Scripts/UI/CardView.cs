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

    // 흡수 카드(◈) 전용 색
    private static readonly Color absorbedBgColor = new Color(0.35f, 0.22f, 0.45f);
    private static readonly Color absorbedNameColor = new Color(0.85f, 0.60f, 1f);
    // 일반 카드는 배경이 밝으므로(종이/돌/금색) 이름을 어두운 색으로
    private static readonly Color normalNameColor = new Color(0.1f, 0.1f, 0.1f);
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

        // 효과 줄이 많아도 판 밖으로 넘치지 않게 글자 크기 자동 맞춤
        FitText(diceText, 11);
        FitText(nameText, 14);
        FitText(costText, 11);
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
            normalColor = absorbed ? absorbedBgColor : DiceInfo.RarityColor(card.data.rarity);
            if (backgroundImage != null) backgroundImage.color = normalColor;
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
            nameText.color = absorbed ? absorbedNameColor : normalNameColor;
        }

        if (costText != null)
            costText.text = d.requireEmotionLevel > 0
                ? $"기력 {d.cost} · {TimedAction.LengthOf(d)}칸 · 장력{d.requireEmotionLevel}"
                : $"기력 {d.cost} · {TimedAction.LengthOf(d)}칸";   // 칸 = 시계판에서 차지하는 시간

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

        if (backgroundImage != null && d.cardArt != null)
            backgroundImage.sprite = d.cardArt;

        usableState = -1;   // 다음 Update에서 사용 가능 여부를 다시 칠한다
    }

    /// <summary>대상 지정 대기 중 표시 (노란색 하이라이트 + 살짝 커짐).</summary>
    public void SetSelected(bool selected)
    {
        this.selected = selected;
        if (backgroundImage != null)
            backgroundImage.color = selected ? selectedColor : normalColor;
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
