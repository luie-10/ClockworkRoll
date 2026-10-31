using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 손패에 표시되는 카드 UI 한 장.
/// - 등급(Paper~EGO)에 따라 배경 색이 달라진다.
/// - 적에게서 흡수한 카드(◈)는 보라색 톤과 침식 수치가 표시된다.
/// - 클릭하면 BattleManager로 전달된다.
/// </summary>
public class CardView : MonoBehaviour, IPointerClickHandler
{
    [Header("UI 참조 (프리팹에 연결)")]
    [SerializeField] private Text nameText;
    [SerializeField] private Text costText;
    [SerializeField] private Text diceText;
    [SerializeField] private Image backgroundImage;

    [Header("선택 표시")]
    [SerializeField] private Color selectedColor = new Color(1f, 0.9f, 0.4f);

    public CardInstance BoundCard { get; private set; }

    private System.Action<CardView> onClicked;
    private Color normalColor = Color.white;

    // 흡수 카드(◈) 전용 색
    private static readonly Color absorbedBgColor = new Color(0.35f, 0.22f, 0.45f);
    private static readonly Color absorbedNameColor = new Color(0.85f, 0.60f, 1f);
    // 일반 카드는 배경이 밝으므로(종이/돌/금색) 이름을 어두운 색으로
    private static readonly Color normalNameColor = new Color(0.1f, 0.1f, 0.1f);

    /// <summary>HandManager가 카드 데이터를 묶어줄 때 호출.</summary>
    public void Bind(CardInstance card, System.Action<CardView> onClicked)
    {
        BoundCard = card;
        card.view = this;
        this.onClicked = onClicked;

        if (card.data != null)
        {
            bool absorbed = card.entry != null && card.entry.absorbed;
            normalColor = absorbed ? absorbedBgColor : GetRarityColor(card.data.rarity);
            if (backgroundImage != null) backgroundImage.color = normalColor;
        }

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

        if (costText != null) costText.text = $"기력 {d.cost}";

        // 주사위 요약 표시 (예: "참격 2~6" 한 줄씩) + 흡수 카드면 침식 수치
        if (diceText != null)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            if (d.diceList != null)
            {
                foreach (DiceData die in d.diceList)
                {
                    sb.AppendLine($"{GetDiceLabel(die.diceDetail)} {die.minRoll}~{die.maxRoll}");

                    // 주사위 효과(스킬)는 바로 아래 줄에 작게 표시 — 예: " └ [적중시] 다음 턴 출혈 1"
                    if (die.effects != null)
                    {
                        foreach (DiceEffect fx in die.effects)
                        {
                            if (fx != null) sb.AppendLine($" └ {fx.Describe()}");
                        }
                    }
                }
            }
            if (absorbed) sb.AppendLine($"침식 {entry.corruption}");
            diceText.text = sb.ToString();
        }

        if (backgroundImage != null && d.cardArt != null)
            backgroundImage.sprite = d.cardArt;
    }

    /// <summary>대상 지정 대기 중 표시 (노란색 하이라이트).</summary>
    public void SetSelected(bool selected)
    {
        if (backgroundImage != null)
            backgroundImage.color = selected ? selectedColor : normalColor;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (onClicked != null) onClicked(this);
    }

    private static string GetDiceLabel(DiceDetail detail)
    {
        switch (detail)
        {
            case DiceDetail.Slash:  return "참격";
            case DiceDetail.Thrust: return "관통";
            case DiceDetail.Blunt:  return "타격";
            case DiceDetail.Guard:  return "가드";
            case DiceDetail.Evade:  return "회피";
            default:                return detail.ToString();
        }
    }

    private static Color GetRarityColor(CardRarity rarity)
    {
        switch (rarity)
        {
            case CardRarity.Paper: return new Color(0.92f, 0.92f, 0.88f);   // 종이
            case CardRarity.Stone: return new Color(0.72f, 0.78f, 0.82f);   // 돌
            case CardRarity.Iron:  return new Color(0.62f, 0.66f, 0.72f);   // 철
            case CardRarity.Gold:  return new Color(1.00f, 0.85f, 0.35f);   // 예술
            case CardRarity.EGO:   return new Color(0.78f, 0.45f, 0.95f);   // E.G.O
            default:               return Color.white;
        }
    }
}
