/// <summary>
/// 시계판 위에 놓인 행동 하나 = "몇 시부터 어떤 태엽판을 쓰는가".
/// 시계판 한 칸 = 주사위 하나: 첫 주사위는 시작 시각, 두 번째는 그다음 시각에 굴러간다.
/// 그래서 길이(칸) = 주사위 개수 — 주사위가 많은 큰 기술일수록 시계판을 오래 차지한다.
/// </summary>
public class TimedAction
{
    public Unit owner;
    public CardInstance card;
    public int hour;        // 시작 시각 (1~12)
    public int length;      // 차지하는 칸 수 (1 이상)
    public Unit target;     // 합 상대가 없을 때 일방 공격할 대상 (플레이어 행동만 사용)
    public bool resolved;   // 끝났는가 (마지막 칸까지 지나감)

    public int EndHour { get { return hour + length - 1; } }

    public bool Covers(int h) { return h >= hour && h <= EndHour; }

    /// <summary>h시 칸에 배정된 주사위 (시작 칸 = 첫 번째 주사위). 칸 밖이면 null.</summary>
    public DiceData DieAt(int h)
    {
        if (!Covers(h) || card == null || card.data == null || card.data.diceList == null) return null;
        int i = h - hour;
        return i < card.data.diceList.Count ? card.data.diceList[i] : null;
    }

    public bool Overlaps(TimedAction other)
    {
        return other != null && hour <= other.EndHour && other.hour <= EndHour;
    }

    /// <summary>시계판 칸 수 = 주사위 개수 (최소 1)</summary>
    public static int LengthOf(CardData card)
    {
        return card != null && card.diceList != null ? System.Math.Max(1, card.diceList.Count) : 1;
    }

    /// <summary>"3시" 또는 "3~5시"</summary>
    public string HourLabel()
    {
        return length > 1 ? $"{hour}~{EndHour}시" : $"{hour}시";
    }
}
