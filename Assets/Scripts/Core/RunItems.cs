using System;
using System.Collections.Generic;

/// <summary>부품 1종 — 얻는 순간 효과가 적용되고, 이번 판 동안 유지된다.</summary>
public class RunItem
{
    public string name, description;
    /// <summary>그림 — IconPainter의 문양 이름 (IconPainter.ItemSpring 등)</summary>
    public string icon;
    /// <summary>얻는 순간 한 번</summary>
    public Action<GameManager> onGain;
    /// <summary>전투가 시작될 때마다 (첫 라운드 전). 상태이상은 "다음 턴"으로 걸어야 첫 라운드에 들어온다</summary>
    public Action<BattleManager> onBattleStart;
    /// <summary>전투에서 이길 때마다</summary>
    public Action<GameManager> onBattleWon;
}

/// <summary>
/// 부품 목록 (궤짝 · 이벤트에서 얻는다). 이번 판에서 같은 부품은 한 번만 나온다.
/// 새 부품은 All에 추가하면 된다 (그림은 IconPainter에).
///
/// 밸런스 기준 (플레이어 체력 60 · 기력 3):
/// - 최대 기력은 전투의 판도를 통째로 바꾸므로(기력 3 → 4면 보스 승률이 두 배 가까이 뛴다) 기력을 주는 부품은 둘뿐이고, 둘 다 대가가 따른다.
///   하나는 최대 체력(영구), 하나는 지금 체력(일시)을 내놓는다.
/// - 나머지는 "체력 12" 한 덩어리와 비슷한 값어치로 맞춘다: 체력을 덜 주는 부품은 전투마다 발동하는 효과를 대신 갖는다.
/// - 같은 축에서 서로를 완전히 누르는 부품이 없게 한다 (전에는 덧댄 철판이 황동 톱니의 하위 호환이었다).
/// </summary>
public static class RunItems
{
    public static readonly List<RunItem> All = new List<RunItem>
    {
        // ── 기력 (대가가 따른다) ──
        new RunItem
        {
            name = "여분의 태엽",
            description = "최대 기력 +1\n최대 체력 -6",
            icon = IconPainter.ItemSpring,
            onGain = gm => { gm.AddMaxKi(1); gm.playerUnit.ChangeMaxHp(-6); },
        },
        new RunItem
        {
            name = "무거운 태엽통",
            description = "최대 기력 +1\n지금 체력 -15",
            icon = IconPainter.ItemBarrel,
            onGain = gm =>
            {
                gm.AddMaxKi(1);
                int lost = Math.Min(15, gm.playerUnit.Hp - 1);   // 부품 때문에 쓰러지지는 않는다
                if (lost > 0) gm.playerUnit.TakeDamage(lost);
            },
        },

        // ── 체력 ──
        new RunItem
        {
            name = "황동 톱니",
            description = "최대 체력 +12",
            icon = IconPainter.ItemCog,
            onGain = gm => gm.playerUnit.ChangeMaxHp(12),
        },
        new RunItem
        {
            name = "덧댄 철판",
            description = "최대 체력 +6\n전투 첫 라운드 철갑 2",
            icon = IconPainter.ItemPlate,
            onGain = gm => gm.playerUnit.ChangeMaxHp(6),
            onBattleStart = bm => bm.PlayerUnit.AddStatus(StatusType.Plating, 2, true),
        },
        new RunItem
        {
            name = "기름 깡통",
            description = "최대 체력 +5\n체력 20 회복",
            icon = IconPainter.ItemOilCan,
            onGain = gm => { gm.playerUnit.ChangeMaxHp(5); gm.playerUnit.Heal(20); },
        },
        new RunItem
        {
            name = "시계공의 구급함",
            description = "체력 20 회복\n전투에서 이길 때마다 체력 4 회복",
            icon = IconPainter.ItemMedkit,
            onGain = gm => gm.playerUnit.Heal(20),
            onBattleWon = gm => gm.playerUnit.Heal(4),
        },

        // ── 전투마다 발동 ──
        new RunItem
        {
            name = "숫돌",
            description = "전투 첫 라운드 힘 2",
            icon = IconPainter.ItemWhetstone,
            onBattleStart = bm => bm.PlayerUnit.AddStatus(StatusType.Strength, 2, true),
        },
        new RunItem
        {
            name = "팽팽한 용수철",
            description = "전투를 장력 2칸에서 시작\n(3칸 = 1단계)",
            icon = IconPainter.ItemCoil,
            onBattleStart = bm => bm.GrantTension(2),
        },
        new RunItem
        {
            name = "방청유",
            description = "전투 첫 라운드\n모든 적에게 녹 2",
            icon = IconPainter.ItemRustOil,
            onBattleStart = bm =>
            {
                foreach (Unit e in bm.Enemies)
                {
                    if (e != null) e.AddRust(2, true);
                }
            },
        },
    };
}
