using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>주사위/태엽판을 짧은 글자로 표시하는 공용 도우미 (머리 위 상태창, 정보창에서 사용)</summary>
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

    // ─────────────────────────── 상태이상 ───────────────────────────

    public static string StatusName(StatusType t)
    {
        switch (t)
        {
            case StatusType.Strength:  return "힘";
            case StatusType.Endurance: return "인내";
            case StatusType.Feeble:    return "허약";
            case StatusType.Fragile:   return "취약";
            case StatusType.Paralysis: return "마비";
            default:                   return t.ToString();
        }
    }

    /// <summary>이로운 상태 = 금색, 해로운 상태 = 보라색</summary>
    public static string StatusColorHex(StatusType t)
    {
        return t == StatusType.Strength || t == StatusType.Endurance ? "#ffcc59" : "#bf8cff";
    }

    /// <summary>
    /// 유닛의 상태이상 한 줄 (색 글자): "녹 2 (+1)  힘 1  마비 2 (+1)". 없으면 빈 문자열.
    /// (+n)은 다음 턴에 들어올 수치.
    /// </summary>
    public static string StatusLine(Unit u)
    {
        StringBuilder sb = new StringBuilder();
        if (u.Rust > 0 || u.PendingRust > 0)
            AppendStatus(sb, "녹", "#d98040", u.Rust, u.PendingRust);

        foreach (StatusType t in (StatusType[])System.Enum.GetValues(typeof(StatusType)))
        {
            int now = u.GetStatus(t), next = u.GetPendingStatus(t);
            if (now > 0 || next > 0) AppendStatus(sb, StatusName(t), StatusColorHex(t), now, next);
        }
        return sb.ToString();
    }

    private static void AppendStatus(StringBuilder sb, string name, string hex, int now, int next)
    {
        if (sb.Length > 0) sb.Append("  ");
        sb.Append($"<color={hex}>{name} {now}");
        if (next > 0) sb.Append($" (+{next})");
        sb.Append("</color>");
    }

    // ─────────────────────────── 용어 설명 ───────────────────────────

    /// <summary>효과 종류별 용어 설명 (태엽판 정보창 아래 "용어" 칸). 설명이 필요 없는 효과는 null.</summary>
    public static string Keyword(DiceEffectType type)
    {
        switch (type)
        {
            case DiceEffectType.Rust:           return "<color=#d98040>녹</color>: 굴리는 주사위의 최댓값 -n, 주사위를 굴릴 때마다 1씩 벗겨짐";
            case DiceEffectType.Strength:       return "<color=#ffcc59>힘</color>: 이번 라운드 동안 공격 주사위 위력 +n";
            case DiceEffectType.Endurance:      return "<color=#ffcc59>인내</color>: 이번 라운드 동안 수비 주사위 위력 +n";
            case DiceEffectType.Feeble:         return "<color=#bf8cff>허약</color>: 이번 라운드 동안 공격 주사위 위력 -n";
            case DiceEffectType.Fragile:        return "<color=#bf8cff>취약</color>: 이번 라운드 동안 공격에 맞을 때 피해 +n";
            case DiceEffectType.Paralysis:      return "<color=#bf8cff>마비</color>: 주사위 n개가 최솟값으로 굴러감";
            case DiceEffectType.DestroyNextDie: return "<color=#bf8cff>파괴</color>: 이번 라운드에 상대가 다음으로 굴릴 주사위 하나가 사라짐";
            default:                            return null;
        }
    }

    /// <summary>태엽판에 쓰인 효과들의 용어 설명을 중복 없이 모은다 (없으면 빈 문자열).</summary>
    public static string Keywords(CardData card)
    {
        if (card == null || card.diceList == null) return "";
        HashSet<DiceEffectType> seen = new HashSet<DiceEffectType>();
        StringBuilder sb = new StringBuilder();
        foreach (DiceData die in card.diceList)
        {
            if (die == null || die.effects == null) continue;
            foreach (DiceEffect fx in die.effects)
            {
                if (fx == null || !seen.Add(fx.effect)) continue;
                string k = Keyword(fx.effect);
                if (k == null) continue;
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(k);
            }
        }
        return sb.ToString();
    }

    /// <summary>등급 이름 / 색</summary>
    public static string RarityName(CardRarity r)
    {
        switch (r)
        {
            case CardRarity.Paper: return "주석";
            case CardRarity.Stone: return "황동";
            case CardRarity.Iron:  return "강철";
            case CardRarity.Gold:  return "금박";
            case CardRarity.EGO:   return "정지된 시간";
            default:               return r.ToString();
        }
    }

    public static Color RarityColor(CardRarity r)
    {
        switch (r)
        {
            case CardRarity.Paper: return new Color(0.92f, 0.92f, 0.88f);   // 주석
            case CardRarity.Stone: return new Color(0.72f, 0.78f, 0.82f);   // 황동
            case CardRarity.Iron:  return new Color(0.62f, 0.66f, 0.72f);   // 강철
            case CardRarity.Gold:  return new Color(1.00f, 0.85f, 0.35f);   // 금박
            case CardRarity.EGO:   return new Color(0.78f, 0.45f, 0.95f);   // 정지된 시간
            default:               return Color.white;
        }
    }
}
