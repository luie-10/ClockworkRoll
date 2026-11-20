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

    /// <summary>시계판에서 차지하는 칸 — "3칸" 또는 "즉발"</summary>
    public static string LengthLabel(CardData card)
    {
        return card != null && card.instant ? "<color=#ffe08a>즉발</color>" : $"{TimedAction.LengthOf(card)}칸";
    }

    /// <summary>즉발 효과 한 줄씩 ("⚡ 태엽판 2장 뽑기")</summary>
    public static string InstantLines(CardData card, string indent = "")
    {
        if (card == null || !card.instant || card.instantEffects == null) return "";
        StringBuilder sb = new StringBuilder();
        foreach (DiceEffect fx in card.instantEffects)
        {
            if (fx == null) continue;
            string text = fx.Describe();
            int close = text.IndexOf(']');
            if (text.StartsWith("[") && close > 0) text = text.Substring(close + 1).Trim();   // [사용시] 같은 시점 말머리는 떼어 낸다
            if (sb.Length > 0) sb.Append('\n');
            sb.Append($"{indent}<color=#ffe08a>⚡</color> {text}");
        }
        return sb.ToString();
    }

    /// <summary>한 줄 요약 (색 글자): "참1~4 타1~4" — 효과가 붙은 칸에는 *</summary>
    public static string Summary(CardData card)
    {
        if (card != null && card.instant) return "<color=#ffe08a>⚡ 즉발</color>";
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
        if (card != null && card.instant) return InstantLines(card, "  ");
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
            case StatusType.Plating:   return "철갑";
            default:                   return t.ToString();
        }
    }

    /// <summary>이로운 상태 = 금색, 해로운 상태 = 보라색</summary>
    public static string StatusColorHex(StatusType t)
    {
        if (t == StatusType.Plating) return "#9fc4e0";   // 쇠 색
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
        if (u.Burn > 0) AppendStatus(sb, "과열", "#ff8033", u.Burn, 0);

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
            case DiceEffectType.Rust:           return "<color=#d98040>녹</color>: 칸의 최댓값 -n, 칸을 쓸 때마다 1씩 벗겨짐";
            case DiceEffectType.Strength:       return "<color=#ffcc59>힘</color>: 이번 라운드 동안 공격 칸 위력 +n";
            case DiceEffectType.Endurance:      return "<color=#ffcc59>인내</color>: 이번 라운드 동안 수비 칸 위력 +n";
            case DiceEffectType.Feeble:         return "<color=#bf8cff>허약</color>: 이번 라운드 동안 공격 칸 위력 -n";
            case DiceEffectType.Fragile:        return "<color=#bf8cff>취약</color>: 이번 라운드 동안 공격에 맞을 때 피해 +n";
            case DiceEffectType.Paralysis:      return "<color=#bf8cff>마비</color>: 칸 n개가 최솟값이 됨";
            case DiceEffectType.DestroyNextDie: return "<color=#bf8cff>파괴</color>: 이번 라운드에 상대의 다음 칸 하나가 사라짐";
            case DiceEffectType.Overwind:       return "<color=#ff6b6b>과감기</color>: 체력 n을 태워 이 태엽판의 모든 칸 위력 +n (체력은 1 아래로 내려가지 않음)";
            case DiceEffectType.Resonance:      return "<color=#ffd159>울림</color>: 공격이 적중하면 다른 모든 적에게도 피해 n (범위 공격)";
            case DiceEffectType.Corrode:        return "<color=#d98040>부식</color>: 적중하면 상대의 녹(다음 턴 녹 포함) x n 만큼 추가 피해";
            case DiceEffectType.Plating:        return "<color=#9fc4e0>철갑</color>: 이번 라운드 동안 공격에 맞을 때마다 받는 피해 -n";
            case DiceEffectType.OnTheHour:      return "<color=#ffd98a>정각</color>: 3·6·9·12시에 놓이면 이 칸 위력 +n";
            case DiceEffectType.Lifesteal:      return "<color=#7ddc8c>흡혈</color>: 적중하면 준 피해만큼 체력 회복 (최대 n)";
            case DiceEffectType.Execute:        return "<color=#ffd98a>마무리</color>: 상대의 체력이 절반 이하이면 이 칸 위력 +n";
            case DiceEffectType.LastStand:      return "<color=#ff6b6b>배수진</color>: 내 체력이 절반 이하이면 이 칸 위력 +n";
            case DiceEffectType.Combo:          return "<color=#ffd98a>연쇄</color>: 이번 라운드에 먼저 굴린 내 칸 하나마다 위력 +1 (최대 n). 늦은 시각에 놓을수록 세다";
            case DiceEffectType.Unopposed:      return "<color=#ffd98a>빈틈</color>: 맞서는 상대 칸이 없는 시각에 놓이면 이 칸 위력 +n";
            case DiceEffectType.Dawn:           return "<color=#ffd98a>아침</color>: 시계판 1~6시(아침)에 놓이면 이 칸 위력 +n";
            case DiceEffectType.Dusk:           return "<color=#8fa8ff>밤</color>: 시계판 7~12시(밤)에 놓이면 이 칸 위력 +n";
            case DiceEffectType.Burn:           return "<color=#ff8033>과열</color>: 라운드가 시작될 때마다 그만큼 피해를 입고 절반으로 식는다 (철갑으로 못 막는다)";
            case DiceEffectType.Gamble:         return "<color=#ffb347>도박</color>: 굴릴 때 반반 확률로 이 칸 위력 +n 또는 -n";
            case DiceEffectType.Jackpot:        return "<color=#ffb347>대박</color>: 굴린 눈이 이 칸의 최댓값이면 위력 +n";
            case DiceEffectType.DoubleOrNothing:return "<color=#ffb347>모 아니면 도</color>: 굴릴 때 반반 확률로 눈이 두 배가 되거나 0이 된다";
            case DiceEffectType.CoinFlip:       return "<color=#ffb347>동전 던지기</color>: 앞면이면 상대에게 피해 n, 뒷면이면 자신에게 피해 n의 절반 (체력 1은 남는다)";
            case DiceEffectType.Roulette:       return "<color=#ffb347>룰렛</color>: 여섯 칸 중 하나가 걸린다: 다음 라운드 기력 +1 · 태엽판 n장 뽑기 · 힘 n · 체력 2n 회복 / 자신에게 녹 n · 마비 1";
            case DiceEffectType.AllIn:          return "<color=#ffb347>올인</color>: 쓸 때 남은 기력을 모두 태워, 기력 1당 이 태엽판의 모든 칸 위력 +n";
            case DiceEffectType.Damage:         return "<color=#ff7a6b>피해</color>: 지정한 적에게 곧바로 피해 n (취약 · 철갑 적용)";
            case DiceEffectType.DamageAll:      return "<color=#ff7a6b>광역 피해</color>: 모든 적에게 곧바로 피해 n";
            default:                            return null;
        }
    }

    /// <summary>태엽판 자체에 붙은 키워드 (소멸 · 선천). 없으면 빈 문자열. 예: "소멸 · 선천"</summary>
    public static string CardKeywords(CardData card)
    {
        if (card == null) return "";
        if (card.exhaust && card.innate) return "소멸 · 선천";
        return card.exhaust ? "소멸" : card.innate ? "선천" : "";
    }

    /// <summary>태엽판에 쓰인 효과들의 용어 설명을 중복 없이 모은다 (없으면 빈 문자열).</summary>
    public static string Keywords(CardData card)
    {
        if (card == null || card.diceList == null) return "";
        HashSet<DiceEffectType> seen = new HashSet<DiceEffectType>();
        StringBuilder sb = new StringBuilder();
        if (card.requireTension > 0)
            sb.Append("<color=#ff9a4d>장력</color>: 서로의 칸이 맞부딪힐 때마다 차오르는 게이지(0~3단계). 이 단계 이상이어야 낼 수 있다");
        if (card.instant)
        {
            if (sb.Length > 0) sb.Append('\n');
            sb.Append("<color=#ffe08a>즉발</color>: 시계판에 놓지 않고, 손패에서 누르는 즉시 기력을 내고 효과가 발동한다");
            if (card.instantEffects != null)
                foreach (DiceEffect fx in card.instantEffects)
                {
                    if (fx == null || !seen.Add(fx.effect)) continue;
                    string k = Keyword(fx.effect);
                    if (k == null) continue;
                    sb.Append('\n');
                    sb.Append(k);
                }
        }
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
            case CardRarity.Scrap:      return "고철";
            case CardRarity.Handmade:   return "수제";
            case CardRarity.Precision:  return "정밀";
            case CardRarity.Masterwork: return "걸작";
            case CardRarity.Forbidden:  return "금단";
            default:               return r.ToString();
        }
    }

    public static Color RarityColor(CardRarity r)
    {
        switch (r)
        {
            case CardRarity.Scrap:      return new Color(0.66f, 0.58f, 0.52f);   // 고철: 녹 낀 회갈색
            case CardRarity.Handmade:   return new Color(0.86f, 0.56f, 0.34f);   // 수제: 구리
            case CardRarity.Precision:  return new Color(0.40f, 0.82f, 0.76f);   // 정밀: 푸르게 담금질한 쇠
            case CardRarity.Masterwork: return new Color(1.00f, 0.80f, 0.30f);   // 걸작: 금
            case CardRarity.Forbidden:  return new Color(0.92f, 0.30f, 0.34f);   // 금단: 달군 쇠의 붉은빛
            default:               return Color.white;
        }
    }
}
