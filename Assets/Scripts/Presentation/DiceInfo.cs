using System.Text;

/// <summary>주사위/책장을 짧은 글자로 표시하는 공용 도우미 (머리 위 상태창, 정보창에서 사용)</summary>
public static class DiceInfo
{
    public static string Glyph(DiceDetail d)
    {
        switch (d)
        {
            case DiceDetail.Slash:  return "참";
            case DiceDetail.Thrust: return "관";
            case DiceDetail.Blunt:  return "타";
            case DiceDetail.Guard:  return "가";
            case DiceDetail.Evade:  return "회";
            default:                return "?";
        }
    }

    public static string Name(DiceDetail d)
    {
        switch (d)
        {
            case DiceDetail.Slash:  return "참격";
            case DiceDetail.Thrust: return "관통";
            case DiceDetail.Blunt:  return "타격";
            case DiceDetail.Guard:  return "가드";
            case DiceDetail.Evade:  return "회피";
            default:                return d.ToString();
        }
    }

    /// <summary>주사위 종류 색 (합 주사위 표시와 같은 색)</summary>
    public static string ColorHex(DiceDetail d)
    {
        switch (d)
        {
            case DiceDetail.Slash:  return "#ff6152";
            case DiceDetail.Thrust: return "#ff9e40";
            case DiceDetail.Blunt:  return "#ffdb52";
            case DiceDetail.Guard:  return "#61a3ff";
            case DiceDetail.Evade:  return "#66eb94";
            default:                return "#ffffff";
        }
    }

    /// <summary>한 줄 요약 (색 글자): "참1~4 타1~4" — 효과가 붙은 주사위에는 *</summary>
    public static string Summary(CardData card)
    {
        if (card == null || card.diceList == null) return "";
        StringBuilder sb = new StringBuilder();
        foreach (DiceData die in card.diceList)
        {
            if (die == null) continue;
            if (sb.Length > 0) sb.Append(' ');
            sb.Append($"<color={ColorHex(die.diceDetail)}>{Glyph(die.diceDetail)}</color>{die.minRoll}~{die.maxRoll}");
            if (die.effects != null && die.effects.Count > 0) sb.Append("<color=#ff5577>*</color>");
        }
        return sb.ToString();
    }

    /// <summary>여러 줄 상세 (정보창용): 주사위마다 종류/범위 + 효과 문구</summary>
    public static string Detail(CardData card)
    {
        if (card == null || card.diceList == null) return "";
        StringBuilder sb = new StringBuilder();
        foreach (DiceData die in card.diceList)
        {
            if (die == null) continue;
            sb.Append($"  <color={ColorHex(die.diceDetail)}>{Name(die.diceDetail)}</color> {die.minRoll}~{die.maxRoll}");
            if (die.effects != null)
            {
                foreach (DiceEffect fx in die.effects)
                {
                    if (fx != null) sb.Append($"  <color=#ff8fa6>{fx.Describe()}</color>");
                }
            }
            sb.Append('\n');
        }
        return sb.ToString().TrimEnd('\n');
    }
}
