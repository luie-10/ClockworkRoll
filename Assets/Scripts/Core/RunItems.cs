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
/// 최대 기력은 전투의 판도를 통째로 바꾸므로(기력 3 → 4면 보스 승률이 두 배 가까이 뛴다) 기력을 주는 부품은 둘뿐이고, 하나는 대가가 따른다.
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
            description = "최대 체력 +5\n체력 20 회복",
            onGain = gm => { gm.playerUnit.ChangeMaxHp(5); gm.playerUnit.Heal(20); },
        },
        new RunItem
        {
            name = "황동 톱니",
            description = "최대 체력 +12",
            onGain = gm => gm.playerUnit.ChangeMaxHp(12),
        },
        new RunItem
        {
            name = "덧댄 철판",
            description = "최대 체력 +8",
            onGain = gm => gm.playerUnit.ChangeMaxHp(8),
        },
        new RunItem
        {
            name = "시계공의 구급함",
            description = "체력 30 회복",
            onGain = gm => gm.playerUnit.Heal(30),
        },
        new RunItem
        {
            name = "무거운 태엽통",
            description = "최대 기력 +1\n최대 체력 -10",
            onGain = gm => { gm.AddMaxKi(1); gm.playerUnit.ChangeMaxHp(-10); },
        },
    };
}
