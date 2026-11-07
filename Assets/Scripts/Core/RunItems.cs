using System;
using System.Collections.Generic;

/// <summary>부품 1종 — 얻는 순간 효과가 적용되고, 이번 판 동안 유지된다.</summary>
public class RunItem
{
    public string name, description;
    public Action<GameManager> onGain;
}

/// <summary>
/// 부품 목록 (궤짝 · 이벤트에서 얻는다). 이번 판에서 같은 부품은 한 번만 나온다.
/// 새 부품은 All에 추가하면 된다.
/// </summary>
public static class RunItems
{
    public static readonly List<RunItem> All = new List<RunItem>
    {
        new RunItem
        {
            name = "여분의 태엽",
            description = "최대 기력 +1",
            onGain = gm => gm.AddMaxKi(1),
        },
        new RunItem
        {
            name = "기름 깡통",
            description = "최대 기력 +1\n체력 15 회복",
            onGain = gm => { gm.AddMaxKi(1); gm.playerUnit.Heal(15); },
        },
        new RunItem
        {
            name = "황동 톱니",
            description = "최대 기력 +1\n최대 체력 +5",
            onGain = gm => { gm.AddMaxKi(1); gm.playerUnit.ChangeMaxHp(5); },
        },
        new RunItem
        {
            name = "무거운 태엽통",
            description = "최대 기력 +2\n최대 체력 -10",
            onGain = gm => { gm.AddMaxKi(2); gm.playerUnit.ChangeMaxHp(-10); },
        },
    };
}
