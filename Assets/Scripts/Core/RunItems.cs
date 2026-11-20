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
    /// <summary>내 칸을 굴릴 때마다: (시각 1~12, 그 칸) → 더할 위력. 시계판의 아침(1~6시) · 밤(7~12시) 부품이 쓴다</summary>
    public Func<int, DiceData, int> rollBonus;
    /// <summary>라운드가 시작될 때마다 (기력이 찬 뒤, 손패를 받은 뒤)</summary>
    public Action<BattleManager> onRoundStart;
    /// <summary>적을 쓰러뜨릴 때마다</summary>
    public Action<BattleManager, Unit> onEnemyKilled;
    /// <summary>즉발 태엽판을 쓸 때마다</summary>
    public Action<BattleManager, CardData> onInstantUsed;
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

        // ── 아침 · 밤 (시계판 1~6시 = 아침, 7~12시 = 밤 — 그 시각에 굴리는 내 칸에만 붙는다) ──
        new RunItem
        {
            name = "해시계 조각",
            description = "아침(1~6시)에 굴리는\n내 공격 칸 위력 +1",
            icon = IconPainter.ItemSun,
            rollBonus = (hour, die) => !BattleManager.IsNightHour(hour) && die.diceType == DiceType.Attack ? 1 : 0,
        },
        new RunItem
        {
            name = "새벽 이슬병",
            description = "최대 체력 +4\n아침(1~6시)에 굴리는 내 수비 칸 위력 +1",
            icon = IconPainter.ItemDew,
            onGain = gm => gm.playerUnit.ChangeMaxHp(4),
            rollBonus = (hour, die) => !BattleManager.IsNightHour(hour) && die.diceType == DiceType.Defense ? 1 : 0,
        },
        new RunItem
        {
            name = "수탉 태엽 인형",
            description = "6시(해 뜨는 시각)에 굴리는\n내 칸 위력 +3",
            icon = IconPainter.ItemRooster,
            rollBonus = (hour, die) => hour == 6 ? 3 : 0,
        },
        new RunItem
        {
            name = "등잔",
            description = "최대 체력 +4\n밤(7~12시)에 굴리는 내 수비 칸 위력 +1",
            icon = IconPainter.ItemLamp,
            onGain = gm => gm.playerUnit.ChangeMaxHp(4),
            rollBonus = (hour, die) => BattleManager.IsNightHour(hour) && die.diceType == DiceType.Defense ? 1 : 0,
        },
        new RunItem
        {
            name = "올빼미 렌즈",
            description = "밤(7~12시)에 굴리는\n내 공격 칸 위력 +1",
            icon = IconPainter.ItemOwl,
            rollBonus = (hour, die) => BattleManager.IsNightHour(hour) && die.diceType == DiceType.Attack ? 1 : 0,
        },
        new RunItem
        {
            name = "달빛 태엽",
            description = "12시(자정)에 굴리는\n내 칸 위력 +3",
            icon = IconPainter.ItemMoon,
            rollBonus = (hour, die) => hour == 12 ? 3 : 0,
        },

        // ── 언제나 ──
        new RunItem
        {
            name = "톱니 목걸이",
            description = "최대 체력 +5\n전투 첫 라운드 인내 2",
            icon = IconPainter.ItemNecklace,
            onGain = gm => gm.playerUnit.ChangeMaxHp(5),
            onBattleStart = bm => bm.PlayerUnit.AddStatus(StatusType.Endurance, 2, true),
        },
        new RunItem
        {
            name = "금 간 돋보기",
            description = "전투 첫 라운드\n모든 적 취약 1",
            icon = IconPainter.ItemCrackedLens,
            onBattleStart = bm =>
            {
                foreach (Unit e in bm.Enemies) if (e != null) e.AddStatus(StatusType.Fragile, 1, true);
            },
        },
        new RunItem
        {
            name = "기름 먹인 장갑",
            description = "체력 10 회복\n전투에서 이길 때마다 최대 체력 +1",
            icon = IconPainter.ItemGlove,
            onGain = gm => gm.playerUnit.Heal(10),
            onBattleWon = gm => gm.playerUnit.ChangeMaxHp(1),
        },
        // ── 라운드마다 ──
        new RunItem
        {
            name = "녹슨 회중시계",
            description = "2라운드부터 라운드가 시작될 때마다\n장력 1칸",
            icon = IconPainter.ItemCog,
            onRoundStart = bm => { if (bm.Round >= 2) bm.GrantTension(1); },
        },
        new RunItem
        {
            name = "태엽 심장",
            description = "라운드가 시작될 때마다\n체력 2 회복",
            icon = IconPainter.ItemNecklace,
            onRoundStart = bm =>
            {
                int healed = bm.PlayerUnit.Heal(2);
                if (healed > 0) bm.ItemPopup(bm.PlayerUnit, $"+{healed}", null);
            },
        },
        new RunItem
        {
            name = "녹슨 방패",
            description = "최대 체력 +3\n라운드가 시작될 때마다 철갑 1",
            icon = IconPainter.ItemPlate,
            onGain = gm => gm.playerUnit.ChangeMaxHp(3),
            onRoundStart = bm => bm.PlayerUnit.AddStatus(StatusType.Plating, 1, false),
        },
        new RunItem
        {
            name = "깨진 모래시계",
            description = "최대 체력 -5\n라운드가 시작될 때마다 태엽판 1장 더 뽑기",
            icon = IconPainter.ItemCrackedLens,
            onGain = gm => gm.playerUnit.ChangeMaxHp(-5),
            onRoundStart = bm => bm.DrawCards(1),
        },
        new RunItem
        {
            name = "선발 장갑",
            description = "전투 첫 라운드에\n태엽판 2장 더 뽑기",
            icon = IconPainter.ItemGlove,
            onRoundStart = bm => { if (bm.Round == 1) bm.DrawCards(2); },
        },

        // ── 적을 쓰러뜨릴 때 ──
        new RunItem
        {
            name = "사냥꾼의 이빨",
            description = "적을 쓰러뜨릴 때마다\n체력 5 회복",
            icon = IconPainter.ItemWhetstone,
            onEnemyKilled = (bm, enemy) =>
            {
                int healed = bm.PlayerUnit.Heal(5);
                if (healed > 0) bm.ItemPopup(bm.PlayerUnit, $"사냥꾼의 이빨 +{healed}", $"부품 [사냥꾼의 이빨]: 체력 +{healed}");
            },
        },
        new RunItem
        {
            name = "전리품 주머니",
            description = "적을 쓰러뜨릴 때마다\n다음 라운드 기력 +1",
            icon = IconPainter.ItemBarrel,
            onEnemyKilled = (bm, enemy) =>
            {
                bm.GainKiNextRound(1);
                bm.ItemPopup(bm.PlayerUnit, "전리품! 기력 +1", "부품 [전리품 주머니]: 다음 라운드 기력 +1");
            },
        },

        // ── 즉발 태엽판 ──
        new RunItem
        {
            name = "휴대용 태엽 열쇠",
            description = "즉발 태엽판을 쓸 때마다\n체력 2 회복",
            icon = IconPainter.ItemSpring,
            onInstantUsed = (bm, card) => bm.PlayerUnit.Heal(2),
        },
        new RunItem
        {
            name = "번개 축전기",
            description = "즉발 태엽판을 쓸 때마다\n지정한 적에게 피해 2",
            icon = IconPainter.ItemBoiler,
            onInstantUsed = (bm, card) =>
            {
                Unit target = bm.FocusTarget != null && bm.FocusTarget.IsAlive ? bm.FocusTarget : null;
                if (target == null) foreach (Unit e in bm.Enemies) if (e != null && e.IsAlive) { target = e; break; }
                if (target != null) bm.ItemDamage(target, 2, "축전기");
            },
        },

        // ── 굴릴 때 ──
        new RunItem
        {
            name = "종지기의 망치",
            description = "정각(3·6·9·12시)에 굴리는\n내 칸 위력 +1",
            icon = IconPainter.ItemRooster,
            rollBonus = (hour, die) => hour % 3 == 0 ? 1 : 0,
        },
        new RunItem
        {
            name = "묵직한 추",
            description = "내 공격 칸 위력 +1\n내 수비 칸 위력 -1",
            icon = IconPainter.ItemCog,
            rollBonus = (hour, die) => die.diceType == DiceType.Attack ? 1 : -1,
        },
        new RunItem
        {
            name = "은 방울",
            description = "전투 첫 라운드\n모든 적 허약 1",
            icon = IconPainter.ItemLamp,
            onBattleStart = bm =>
            {
                foreach (Unit e in bm.Enemies) if (e != null) e.AddStatus(StatusType.Feeble, 1, true);
            },
        },

        new RunItem
        {
            name = "달군 보일러",
            description = "전투 첫 라운드\n모든 적에게 과열 3",
            icon = IconPainter.ItemBoiler,
            onBattleStart = bm =>
            {
                foreach (Unit e in bm.Enemies) if (e != null) e.AddBurn(3);
            },
        },
    };
}
