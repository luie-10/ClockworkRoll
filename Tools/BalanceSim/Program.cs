// ClockworkRoll 밸런스 시뮬레이터 — Assets/Data의 에셋을 그대로 읽어 BattleManager 규칙으로 자동 대전한다.
//
// 실행 (Unity를 켜 둔 채로도 된다 — .NET SDK만 있으면 됨). 프로젝트 폴더에서:
//   dotnet run -c Release --project Tools/BalanceSim -- . 4000                세 가지 실력(숙련 · 보통 · 초보)으로 4000판씩
//   dotnet run -c Release --project Tools/BalanceSim -- . 4000 v              + 덱에 있는 태엽판별 보스 승률 변화
//   dotnet run -c Release --project Tools/BalanceSim -- . 1 cards             태엽판 · 적 · 무대 목록만 출력
//   dotnet run -c Release --project Tools/BalanceSim -- . 4000 run 실험.txt   에셋을 고치기 전에 수치를 덮어써서 시험
//     실험.txt 한 줄 예:  hp|종지기|48   act|녹슨 경비 인형|2   die|마지막 타종|0|4|8   cost|과부하|2   addenemy|하수구 입구|녹 먹는 벌레
//
// 전투 규칙(BattleManager) · 갈래길(RouteScreen) · 부품(RunItems)을 바꾸면 여기도 같이 고쳐야 결과가 맞는다.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

class Fx { public int trigger, effect, amount; public bool next; }
class Die { public int type, detail, min, max; public List<Fx> fx = new(); }
class Card { public string name = "", guid = ""; public int rarity, cost, reqTension; public List<Die> dice = new(); public int Len => Math.Max(1, dice.Count); }
class Enemy { public string name = ""; public int maxHp, actions = 1; public int[] res = new int[3]; public List<Card> deck = new(); }
class Stage { public string name = ""; public List<Enemy> enemies = new(); public List<Card> deckOverride = new(); }
class Chapter { public string title = ""; public Stage? battle; public List<Card> rewards = new(); public List<Stage> fights = new(), elites = new(); }
class Story { public List<Card> startingDeck = new(), rewardPool = new(); public List<Chapter> chapters = new(); }

static class T { public const int OnHit = 0, OnClashWin = 1, OnClashLose = 2, OnUse = 3; }
static class E
{
    public const int Rust = 0, RecoverKi = 1, DrawCard = 2, Heal = 3, Strength = 4, Endurance = 5, Feeble = 6, Fragile = 7, Paralysis = 8,
        PowerNextDie = 9, DestroyNextDie = 10, Overwind = 11, Resonance = 12, Corrode = 13, Plating = 14, OnTheHour = 15, MaxKi = 16;
}
static class S { public const int Strength = 0, Endurance = 1, Feeble = 2, Fragile = 3, Paralysis = 4, Plating = 5; }

static class Loader
{
    static readonly Dictionary<string, string> pathOf = new();
    static readonly Dictionary<string, object> cache = new();

    public static void Index(string root)
    {
        foreach (string meta in Directory.EnumerateFiles(Path.Combine(root, "Assets"), "*.asset.meta", SearchOption.AllDirectories))
        {
            if (meta.Contains("Flooded_Grounds") || meta.Contains("TextMesh")) continue;
            Match m = Regex.Match(File.ReadAllText(meta), @"guid: ([0-9a-f]+)");
            if (m.Success) pathOf[m.Groups[1].Value] = meta.Substring(0, meta.Length - 5);
        }
    }

    public static string Unq(string s)
    {
        s = s.Trim();
        if (s.Length >= 2 && s[0] == '"' && s[^1] == '"')
        {
            s = s.Substring(1, s.Length - 2);
            s = Regex.Replace(s, @"\\u([0-9A-Fa-f]{4})", m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());
            s = s.Replace("\\\"", "\"").Replace("\\n", "\n");
        }
        return s;
    }

    /// <summary>실험용 덮어쓰기: "hp|적|n", "act|적|n", "die|카드|칸|최소|최대", "cost|카드|n", "fx|카드|칸|효과번호|양", "tune|이름|값"</summary>
    public static void Patch(string file)
    {
        foreach (string raw in File.ReadAllLines(file, Encoding.UTF8))
        {
            string line = raw.Trim();
            if (line == "" || line.StartsWith("#")) continue;
            string[] p = line.Split('|').Select(x => x.Trim()).ToArray();
            int hits = 0;
            foreach (object o in cache.Values.ToList())
            {
                if (o is Enemy e && e.name == p[1]) { if (p[0] == "hp") { e.maxHp = int.Parse(p[2]); hits++; } if (p[0] == "act") { e.actions = int.Parse(p[2]); hits++; } }
                if (o is Card c && c.name == p[1])
                {
                    if (p[0] == "die") { c.dice[int.Parse(p[2])].min = int.Parse(p[3]); c.dice[int.Parse(p[2])].max = int.Parse(p[4]); hits++; }
                    if (p[0] == "cost") { c.cost = int.Parse(p[2]); hits++; }
                    if (p[0] == "fx") { c.dice[int.Parse(p[2])].fx[int.Parse(p[3])].amount = int.Parse(p[4]); hits++; }
                }
                if (o is Stage s && s.name == p[1])
                {
                    if (p[0] == "addenemy") { s.enemies.Add(cache.Values.OfType<Enemy>().First(x => x.name == p[2])); hits++; }
                    if (p[0] == "delenemy") { s.enemies.Remove(s.enemies.First(x => x.name == p[2])); hits++; }
                }
            }
            if (p[0] == "tune")
            {
                hits++;
                switch (p[1])
                {
                    case "RestHeal": Tune.RestHeal = double.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture); break;
                    case "PlayerHp": Tune.PlayerHp = int.Parse(p[2]); break;
                    case "ChapterHeal": Tune.ChapterHeal = double.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture); break;
                    case "OilHeal": Tune.OilHeal = int.Parse(p[2]); break;
                    case "OilMaxHp": Tune.OilMaxHp = int.Parse(p[2]); break;
                    case "BrassMaxHp": Tune.BrassMaxHp = int.Parse(p[2]); break;
                    case "HeavyHpCost": Tune.HeavyHpCost = int.Parse(p[2]); break;
                    case "Weights": Tune.TypeWeights = p[2].Split(',').Select(int.Parse).ToArray(); break;
                    default: hits = 0; break;
                }
            }
            if (hits == 0) Console.WriteLine("!! 적용 안 됨: " + line);
        }
    }

    static string[] Lines(string guid) => File.ReadAllLines(pathOf[guid], Encoding.UTF8);
    static string? Guid(string line) { Match m = Regex.Match(line, @"guid: ([0-9a-f]+)"); return m.Success ? m.Groups[1].Value : null; }
    static string Val(string line) => line.Substring(line.IndexOf(':') + 1).Trim();

    /// <summary>최상위(2칸 들여쓰기) 키별로 줄을 묶는다</summary>
    static Dictionary<string, List<string>> Sections(string[] lines)
    {
        Dictionary<string, List<string>> d = new();
        string cur = "";
        foreach (string l in lines)
        {
            Match m = Regex.Match(l, @"^  ([A-Za-z_]\w*):(.*)$");
            if (m.Success) { cur = m.Groups[1].Value; d[cur] = new List<string> { m.Groups[2].Value }; }
            else if (cur != "" && d.ContainsKey(cur)) d[cur].Add(l);
        }
        return d;
    }

    static List<string> Guids(Dictionary<string, List<string>> s, string key)
    {
        List<string> list = new();
        if (!s.TryGetValue(key, out List<string>? ls)) return list;
        foreach (string l in ls) { string? g = Guid(l); if (g != null && pathOf.ContainsKey(g)) list.Add(g); }
        return list;
    }

    public static Card Card(string guid)
    {
        if (cache.TryGetValue(guid, out object? o)) return (Card)o;
        Card c = new() { guid = guid };
        cache[guid] = c;
        Die? die = null; Fx? fx = null;
        foreach (string l in Lines(guid))
        {
            string t = l.Trim();
            if (l.StartsWith("  cardName:")) c.name = Unq(Val(l));
            else if (l.StartsWith("  rarity:")) c.rarity = int.Parse(Val(l));
            else if (l.StartsWith("  cost:")) c.cost = int.Parse(Val(l));
            else if (l.StartsWith("  requireEmotionLevel:") || l.StartsWith("  requireTension:")) c.reqTension = int.Parse(Val(l));
            else if (l.StartsWith("  - diceName:")) { die = new Die(); c.dice.Add(die); fx = null; }
            else if (die != null)
            {
                if (t.StartsWith("diceType:")) die.type = int.Parse(Val(l));
                else if (t.StartsWith("diceDetail:")) die.detail = int.Parse(Val(l));
                else if (t.StartsWith("minRoll:")) die.min = int.Parse(Val(l));
                else if (t.StartsWith("maxRoll:")) die.max = int.Parse(Val(l));
                else if (t.StartsWith("- trigger:")) { fx = new Fx { trigger = int.Parse(Val(l)), amount = 1 }; die.fx.Add(fx); }
                else if (fx != null && t.StartsWith("effect:")) fx.effect = int.Parse(Val(l));
                else if (fx != null && t.StartsWith("amount:")) fx.amount = int.Parse(Val(l));
                else if (fx != null && t.StartsWith("nextTurn:")) fx.next = Val(l) == "1";
            }
        }
        return c;
    }

    public static Enemy Enemy(string guid)
    {
        if (cache.TryGetValue(guid, out object? o)) return (Enemy)o;
        Enemy e = new();
        cache[guid] = e;
        string[] lines = Lines(guid);
        Dictionary<string, List<string>> s = Sections(lines);
        e.name = Unq(s["enemyName"][0]);
        e.maxHp = int.Parse(s["maxHp"][0]);
        if (s.ContainsKey("actionsPerRound")) e.actions = int.Parse(s["actionsPerRound"][0]);
        foreach (string l in s["resistance"])
        {
            string t = l.Trim();
            if (t.StartsWith("slash:")) e.res[0] = int.Parse(Val(l));
            if (t.StartsWith("thrust:")) e.res[1] = int.Parse(Val(l));
            if (t.StartsWith("blunt:")) e.res[2] = int.Parse(Val(l));
        }
        foreach (string g in Guids(s, "deck")) e.deck.Add(Card(g));
        return e;
    }

    public static Stage Stage(string guid)
    {
        if (cache.TryGetValue(guid, out object? o)) return (Stage)o;
        Stage st = new();
        cache[guid] = st;
        Dictionary<string, List<string>> s = Sections(Lines(guid));
        st.name = Unq(s["stageName"][0]);
        foreach (string l in s["enemies"]) if (l.Contains("enemy:")) { string? g = Guid(l); if (g != null) st.enemies.Add(Enemy(g)); }
        foreach (string g in Guids(s, "deckOverride")) st.deckOverride.Add(Card(g));
        return st;
    }

    public static Chapter Chapter(string guid)
    {
        Chapter c = new();
        Dictionary<string, List<string>> s = Sections(Lines(guid));
        c.title = Unq(s["chapterTitle"][0]);
        string? b = Guid(s["battle"][0]);
        if (b != null && pathOf.ContainsKey(b)) c.battle = Stage(b);
        foreach (string g in Guids(s, "rewardCards")) c.rewards.Add(Card(g));
        foreach (string g in Guids(s, "routeFights")) c.fights.Add(Stage(g));
        foreach (string g in Guids(s, "routeElites")) c.elites.Add(Stage(g));
        return c;
    }

    public static Story Story(string guid)
    {
        Story st = new();
        Dictionary<string, List<string>> s = Sections(Lines(guid));
        foreach (string g in Guids(s, "startingDeck")) st.startingDeck.Add(Card(g));
        foreach (string g in Guids(s, "rewardPool")) st.rewardPool.Add(Card(g));
        foreach (string g in Guids(s, "chapters")) st.chapters.Add(Chapter(g));
        return st;
    }
}

class Entry { public Card card = null!; public bool absorbed; public int corruption; public string source = ""; }

class U
{
    public string name = ""; public bool isPlayer; public int hp, maxHp; public int[] res = new int[3];
    public int rust, pendingRust; public int[] status = new int[6], pending = new int[6];
    public List<Card> deck = new(); public int actions = 1;
    public bool Alive => hp > 0;
    public void Damage(int n) { if (!Alive || n <= 0) return; hp = Math.Max(0, hp - n); }
    public int Heal(int n) { if (!Alive || n <= 0) return 0; int b = hp; hp = Math.Min(maxHp, hp + n); return hp - b; }
    public void ClearStatus() { rust = pendingRust = 0; Array.Clear(status); Array.Clear(pending); }
    public void Activate() { rust += pendingRust; pendingRust = 0; for (int i = 0; i < 6; i++) { status[i] = pending[i]; pending[i] = 0; } }
    public void AddStatus(int t, int n, bool next) { if (!Alive || n <= 0) return; if (next) pending[t] += n; else status[t] += n; }
    public void AddRust(int n, bool next) { if (!Alive || n <= 0) return; if (next) pendingRust += n; else rust += n; }
    public static double Mult(int r) => r == 1 ? 1.5 : r == 2 ? 2.0 : 1.0;
    public int Resist(int raw, int detail) => (int)Math.Round(raw * Mult(detail < 3 ? res[detail] : 0), MidpointRounding.ToEven);
}

class Act
{
    public U owner = null!; public Card card = null!; public Entry? entry; public int hour, length; public U? target; public bool resolved;
    public int End => hour + length - 1;
    public bool Covers(int h) => h >= hour && h <= End;
    public Die? DieAt(int h) { if (!Covers(h)) return null; int i = h - hour; return i < card.dice.Count ? card.dice[i] : null; }
    public bool Overlaps(Act o) => hour <= o.End && o.hour <= End;
}

class Run
{
    public Random rng;
    public U player = new() { name = "이안", isPlayer = true, hp = 60, maxHp = 60 };
    public List<Entry> deck = new();
    public int maxKi = 3;
    public HashSet<string> items = new();
    public Story story;
    public static int HandLimit = 7, TensionPerStage = 3, TypeBonus = 2, CorruptionDamage = 2;
    public static double CorruptionBase = 0.20, CorruptionPer = 0.05;
    public static double Skill = 1.0;   // 0 = 무작위에 가까운 플레이, 1 = 계산해서 두는 플레이

    public Run(Story s, int seed)
    {
        story = s; rng = new Random(seed);
        player.hp = player.maxHp = Tune.PlayerHp;
        foreach (Card c in s.startingDeck) deck.Add(new Entry { card = c, source = c.name });
    }

    // ───────────── 전투 ─────────────
    public int lastRounds, lastTension;
    List<U> enemies = new();
    List<Act> eActs = new(), pActs = new();
    Dictionary<U, int> nextBonus = new(); HashSet<U> destroyNext = new(); Dictionary<(U, Die), int> cardPower = new();
    int battleMaxKi, pendingKi, ki, tensionPts;
    List<Entry> draw = new(), discard = new(), hand = new();
    int TensionStage => Math.Min(3, tensionPts / TensionPerStage);

    public bool Battle(Stage stage)
    {
        enemies = stage.enemies.Select(e => new U { name = e.name, hp = e.maxHp, maxHp = e.maxHp, res = (int[])e.res.Clone(), deck = e.deck, actions = e.actions }).ToList();
        battleMaxKi = pendingKi = tensionPts = 0;
        player.ClearStatus();
        List<Entry> src = stage.deckOverride.Count > 0 ? stage.deckOverride.Select(c => new Entry { card = c, source = c.name }).ToList() : deck;
        draw = new List<Entry>(src); Shuffle(draw); discard.Clear(); hand.Clear();
        lastRounds = 0;

        while (true)
        {
            if (!player.Alive) return false;
            if (enemies.All(e => !e.Alive)) { lastTension = TensionStage; return true; }
            if (++lastRounds > 60) return false;   // 끝나지 않는 싸움 = 패배로 친다

            player.Activate();
            foreach (U e in enemies) if (e.Alive) e.Activate();
            ki = maxKi + battleMaxKi + pendingKi; pendingKi = 0;
            pActs.Clear();
            while (hand.Count < HandLimit && DrawOne()) { }
            RollEnemyActions();
            Plan();

            nextBonus.Clear(); destroyNext.Clear(); cardPower.Clear();
            bool ended = false;
            for (int h = 1; h <= 12 && !ended; h++)
            {
                List<Act> starting = eActs.Where(a => a.hour == h).Concat(pActs.Where(a => a.hour == h)).ToList();
                foreach (Act a in starting)
                {
                    if (!a.owner.Alive) continue;
                    if (a.owner.isPlayer && a.entry != null && a.entry.absorbed)
                    {
                        double chance = Math.Min(0.8, CorruptionBase + CorruptionPer * Math.Max(0, a.entry.corruption - 1));
                        if (rng.NextDouble() < chance)
                        {
                            player.Damage(CorruptionDamage + Math.Max(0, a.entry.corruption - 1));
                            if (!player.Alive) { ended = true; break; }
                        }
                    }
                    U? opp = a.owner.isPlayer ? AliveTarget(a) : player;
                    if (opp != null) CardUse(a.owner, opp, a.card);
                }
                if (ended) break;
                ResolveHour(h);
                if (!player.Alive || enemies.All(e => !e.Alive)) { ended = true; break; }
                foreach (Act a in eActs.Concat(pActs)) if (a.End == h) Finish(a);
            }
            foreach (Act a in eActs.Concat(pActs)) Finish(a);
            eActs.Clear(); pActs.Clear();
        }
    }

    void Finish(Act a) { if (a.resolved) return; a.resolved = true; if (a.owner.isPlayer && a.entry != null) discard.Add(a.entry); }

    bool DrawOne()
    {
        if (draw.Count == 0) { if (discard.Count == 0) return false; draw.AddRange(discard); discard.Clear(); Shuffle(draw); }
        hand.Add(draw[0]); draw.RemoveAt(0); return true;
    }

    void Shuffle<X>(List<X> l) { for (int i = l.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (l[i], l[j]) = (l[j], l[i]); } }

    void RollEnemyActions()
    {
        eActs.Clear();
        foreach (U e in enemies)
        {
            if (!e.Alive) continue;
            for (int n = 0; n < Math.Max(1, e.actions); n++)
            {
                if (e.deck.Count == 0) break;
                Card c = e.deck[rng.Next(e.deck.Count)];
                for (int attempt = 0; attempt < 20; attempt++)
                {
                    Act a = new() { owner = e, card = c, hour = rng.Next(1, 12 - c.Len + 2), length = c.Len, target = player };
                    if (eActs.Any(o => o.owner == e && o.Overlaps(a))) continue;
                    eActs.Add(a); break;
                }
            }
        }
        eActs = eActs.OrderBy(a => a.hour).ToList();
    }

    U? AliveTarget(Act a) => a.target != null && a.target.Alive ? a.target : enemies.FirstOrDefault(e => e.Alive);

    void ResolveHour(int h)
    {
        Act? mine = player.Alive ? pActs.FirstOrDefault(a => a.Covers(h)) : null;
        List<Act> foes = eActs.Where(a => a.Covers(h) && a.owner.Alive).ToList();
        Act? partner = null;
        if (mine != null && foes.Count > 0)
        {
            U? aim = AliveTarget(mine);
            partner = foes.FirstOrDefault(f => f.owner == aim) ?? foes[0];
        }
        foreach (Act foe in foes)
        {
            if (!foe.owner.Alive || !player.Alive) continue;
            if (foe == partner)
            {
                if (foe.hour <= mine!.hour) Exchange(foe, mine, h, null); else Exchange(mine, foe, h, null);
            }
            else if (foe.DieAt(h) is Die d && d.type == 0) Exchange(foe, null, h, player);
        }
        if (mine != null && partner == null && player.Alive && mine.DieAt(h) is Die md && md.type == 0)
        {
            U? t = AliveTarget(mine);
            if (t != null) Exchange(mine, null, h, t);
        }
    }

    Die? TryDestroy(U owner, Die? die) => die == null || !destroyNext.Remove(owner) ? die : null;

    void Exchange(Act atk, Act? def, int h, U? lone)
    {
        U a = atk.owner; U? d = def != null ? def.owner : lone;
        if (d == null || !a.Alive || !d.Alive) return;
        Die? ad = atk.DieAt(h), dd = def?.DieAt(h);
        ad = TryDestroy(a, ad); dd = TryDestroy(d, dd);
        if (ad == null && dd == null) return;
        if (ad != null && dd != null && ad.type == 1 && dd.type == 1) return;
        int ar = ad != null ? Roll(a, ad, dd, h) : -1;
        int dr = dd != null ? Roll(d, dd, ad, h) : -1;
        if (ad != null && dd != null) tensionPts = Math.Min(TensionPerStage * 3, tensionPts + 1);
        if (ar == dr) return;
        if (ar > dr) { bool hit = Apply(a, d, ad!, ar); Effects(a, ad, d, dd, hit); }
        else { bool hit = Apply(d, a, dd!, dr); Effects(d, dd, a, ad, hit); }
    }

    static readonly Dictionary<int, int> beats = new() { { 0, 1 }, { 1, 2 }, { 2, 0 } };

    int Roll(U self, Die mine, Die? theirs, int h)
    {
        int max = Math.Max(mine.min, mine.max - self.rust);
        if (self.rust > 0) self.rust--;
        int roll = rng.Next(mine.min, max + 1);
        if (self.status[S.Paralysis] > 0) { self.status[S.Paralysis]--; roll = mine.min; }
        int bonus = mine.type == 0 ? self.status[S.Strength] - self.status[S.Feeble] : self.status[S.Endurance];
        if (nextBonus.TryGetValue(self, out int p) && p > 0) { bonus += p; nextBonus.Remove(self); }
        if (cardPower.TryGetValue((self, mine), out int w) && w > 0) bonus += w;
        if (h % 3 == 0) foreach (Fx f in mine.fx) if (f.effect == E.OnTheHour) bonus += f.amount;
        if (theirs != null)
        {
            if (mine.type == 0 && theirs.type == 0 && beats.TryGetValue(mine.detail, out int b) && theirs.detail == b) bonus += TypeBonus;
            if (mine.detail == 4 && theirs.type == 0) bonus += TypeBonus;
        }
        return Math.Max(roll + bonus, 0);
    }

    bool Apply(U src, U tgt, Die die, int roll)
    {
        if (die.type != 0) return false;
        if (roll <= 0) return false;
        int dmg = tgt.Resist(roll, die.detail) + tgt.status[S.Fragile];
        int plate = tgt.status[S.Plating];
        if (plate > 0) dmg = Math.Max(0, dmg - plate);
        Hurt(src, tgt, dmg);
        return true;
    }

    public long dmgDealt, dmgTaken;
    void Hurt(U? src, U tgt, int dmg) { if (dmg <= 0 || !tgt.Alive) return; int b = tgt.hp; tgt.Damage(dmg); if (tgt.isPlayer) dmgTaken += b - tgt.hp; else dmgDealt += b - tgt.hp; }

    void Effects(U win, Die? wd, U lose, Die? ld, bool hit)
    {
        if (wd != null) { Trigger(win, lose, wd, T.OnClashWin); if (hit) Trigger(win, lose, wd, T.OnHit); }
        if (ld != null) Trigger(lose, win, ld, T.OnClashLose);
    }

    void CardUse(U self, U opp, Card card)
    {
        foreach (Die d in card.dice)
        {
            foreach (Fx f in d.fx)
                if (f.trigger == T.OnUse && f.effect == E.Overwind && self.Alive && f.amount > 0)
                {
                    int lost = Math.Min(f.amount, self.hp - 1);
                    if (lost > 0) Hurt(null, self, lost);
                    foreach (Die x in card.dice) { cardPower.TryGetValue((self, x), out int now); cardPower[(self, x)] = now + f.amount; }
                }
            Trigger(self, opp, d, T.OnUse);
        }
    }

    void Trigger(U self, U opp, Die die, int trig) { foreach (Fx f in die.fx) if (f.trigger == trig) Effect(self, opp, f); }

    int Extra(U t, int amount)
    {
        if (!t.Alive || amount <= 0) return 0;
        int dmg = Math.Max(0, amount + t.status[S.Fragile] - t.status[S.Plating]);
        Hurt(null, t, dmg); return dmg;
    }

    void Effect(U self, U opp, Fx f)
    {
        switch (f.effect)
        {
            case E.Rust: opp.AddRust(f.amount, f.next); break;
            case E.RecoverKi: if (self.isPlayer) pendingKi += f.amount; break;
            case E.DrawCard: if (self.isPlayer) { int n = 0; while (n < f.amount && hand.Count < HandLimit && DrawOne()) n++; } break;
            case E.Heal: self.Heal(f.amount); break;
            case E.Strength: self.AddStatus(S.Strength, f.amount, f.next); break;
            case E.Endurance: self.AddStatus(S.Endurance, f.amount, f.next); break;
            case E.Feeble: opp.AddStatus(S.Feeble, f.amount, f.next); break;
            case E.Fragile: opp.AddStatus(S.Fragile, f.amount, f.next); break;
            case E.Paralysis: opp.AddStatus(S.Paralysis, f.amount, f.next); break;
            case E.PowerNextDie: { nextBonus.TryGetValue(self, out int c); nextBonus[self] = c + f.amount; break; }
            case E.DestroyNextDie: if (opp.Alive) destroyNext.Add(opp); break;
            case E.Overwind:
                if (f.trigger == T.OnUse || !self.Alive) return;
                { int burned = Math.Min(f.amount, self.hp - 1); if (burned > 0) Hurt(null, self, burned); nextBonus.TryGetValue(self, out int w); nextBonus[self] = w + f.amount; }
                break;
            case E.Resonance:
                if (!self.isPlayer) return;
                foreach (U e in enemies.ToList()) if (e != opp && e.Alive) Extra(e, f.amount);
                break;
            case E.Corrode: Extra(opp, (opp.rust + opp.pendingRust) * f.amount); break;
            case E.Plating: self.AddStatus(S.Plating, f.amount, f.next); break;
            case E.MaxKi: if (self.isPlayer) battleMaxKi += f.amount; break;
        }
    }

    // ───────────── 플레이어 AI ─────────────

    /// <summary>주사위 굴림 분포 (값 → 확률). 정적 보정 포함</summary>
    static double[] Dist(Die d, int rust, int bonus)
    {
        int max = Math.Max(d.min, d.max - rust);
        double[] p = new double[40];
        int n = max - d.min + 1;
        for (int r = d.min; r <= max; r++) p[Math.Clamp(r + bonus, 0, 39)] += 1.0 / n;
        return p;
    }

    int StaticBonus(U self, Die mine, Die? theirs, int h, int extra)
    {
        int b = extra + (mine.type == 0 ? self.status[S.Strength] - self.status[S.Feeble] : self.status[S.Endurance]);
        if (h % 3 == 0) foreach (Fx f in mine.fx) if (f.effect == E.OnTheHour) b += f.amount;
        if (theirs != null)
        {
            if (mine.type == 0 && theirs.type == 0 && beats.TryGetValue(mine.detail, out int x) && theirs.detail == x) b += TypeBonus;
            if (mine.detail == 4 && theirs.type == 0) b += TypeBonus;
        }
        return b;
    }

    double DmgTo(U t, int roll, int detail) => roll <= 0 ? 0 : Math.Max(0, t.Resist(roll, detail) + t.status[S.Fragile] - t.status[S.Plating]);

    double FxValue(Fx f, U target, double wD)
    {
        switch (f.effect)
        {
            case E.Rust: return 1.1 * f.amount;
            case E.RecoverKi: return 2.5 * f.amount;
            case E.DrawCard: return 1.0 * f.amount;
            case E.Heal: return Math.Min(f.amount, player.maxHp - player.hp) * wD;
            case E.Strength: return (f.next ? 2.0 : 1.2) * f.amount;
            case E.Endurance: return 0.8 * f.amount;
            case E.Feeble: return 1.3 * f.amount;
            case E.Fragile: return 1.3 * f.amount;
            case E.Paralysis: return 1.8 * f.amount;
            case E.PowerNextDie: return 0.8 * f.amount;
            case E.DestroyNextDie: return 3.0;
            case E.Resonance: return f.amount * enemies.Count(e => e.Alive && e != target);
            case E.Corrode: return (target.rust + target.pendingRust) * f.amount;
            case E.Plating: return 1.5 * f.amount;
            case E.MaxKi: return 5.0 * f.amount;
            default: return 0;
        }
    }

    double Evaluate(Entry en, int s, U target, double wD)
    {
        Card c = en.card;
        double v = 0, dealt = 0;
        int over = 0;
        foreach (Die d in c.dice) foreach (Fx f in d.fx)
        {
            if (f.trigger != T.OnUse) continue;
            if (f.effect == E.Overwind) { over += f.amount; v -= Math.Min(f.amount, player.hp - 1) * wD * 0.8; }
            else v += FxValue(f, target, wD);
        }
        int myRustBase = player.rust - pActs.Sum(a => a.hour < s ? a.length : 0);
        for (int i = 0; i < c.dice.Count; i++)
        {
            int h = s + i; Die md = c.dice[i];
            List<Act> foes = eActs.Where(a => a.Covers(h) && a.owner.Alive).ToList();
            Act? partner = foes.Count > 0 ? (foes.FirstOrDefault(f => f.owner == target) ?? foes[0]) : null;
            int myRust = Math.Max(0, myRustBase - i);
            double pWin, pLose, pHit, eDeal = 0, eTake = 0, baseTake = 0;
            if (partner == null)
            {
                if (md.type != 0) continue;
                double[] mp = Dist(md, myRust, StaticBonus(player, md, null, h, over));
                for (int r = 0; r < 40; r++) eDeal += mp[r] * DmgTo(target, r, md.detail);
                pWin = 1; pLose = 0; pHit = 1 - mp[0];
                dealt += eDeal;
                foreach (Fx f in md.fx)
                    v += f.trigger == T.OnHit ? pHit * FxValue(f, target, wD) : f.trigger == T.OnClashWin ? FxValue(f, target, wD) : 0;
                continue;
            }
            U foe = partner.owner; Die fd = partner.DieAt(h)!;
            if (md.type == 1 && fd.type == 1) continue;
            int foeRustUsed = eActs.Where(a => a.owner == foe).Sum(a => Math.Max(0, Math.Min(a.End, h - 1) - a.hour + 1));
            double[] m = Dist(md, myRust, StaticBonus(player, md, fd, h, over));
            double[] t = Dist(fd, Math.Max(0, foe.rust - foeRustUsed), StaticBonus(foe, fd, md, h, 0));
            pWin = pLose = pHit = 0;
            for (int a = 0; a < 40; a++)
            {
                if (m[a] == 0) continue;
                for (int b = 0; b < 40; b++)
                {
                    if (t[b] == 0) continue;
                    double p = m[a] * t[b];
                    if (a > b) { pWin += p; if (md.type == 0 && a > 0) { pHit += p; eDeal += p * DmgTo(foe, a, md.detail); } }
                    else if (b > a) { pLose += p; if (fd.type == 0) eTake += p * DmgTo(player, b, fd.detail); }
                }
            }
            if (fd.type == 0) for (int b = 0; b < 40; b++) baseTake += t[b] * DmgTo(player, b, fd.detail);
            dealt += eDeal;
            v += (baseTake - eTake) * wD;
            foreach (Fx f in md.fx)
            {
                double p = f.trigger == T.OnHit ? pHit : f.trigger == T.OnClashWin ? pWin : f.trigger == T.OnClashLose ? pLose : 0;
                v += p * FxValue(f, foe, wD);
            }
        }
        // 넘치는 피해는 값이 없다 (마무리는 조금 더 쳐준다)
        double room = target.hp - planned.GetValueOrDefault(target);
        if (dealt >= room && room > 0) v += Math.Min(dealt, room) + 3; else v += Math.Max(0, Math.Min(dealt, room));
        if (en.absorbed) v -= 0.2 * 2 * wD;
        return v;
    }

    Dictionary<U, double> planned = new();

    int FindHourAgainst(U enemy, int length)
    {
        Act? next = eActs.FirstOrDefault(a => a.owner == enemy);
        int best = -1, bestOverlap = 0, bestDist = int.MaxValue;
        for (int h = 1; h + length - 1 <= 12; h++)
        {
            Act probe = new() { hour = h, length = length };
            if (pActs.Any(a => a.Overlaps(probe))) continue;
            if (best < 0) best = h;
            if (next == null) break;
            int overlap = 0;
            for (int t = h; t <= probe.End; t++) if (next.Covers(t)) overlap++;
            int dist = Math.Abs(h - next.hour);
            if (overlap > bestOverlap || (overlap == bestOverlap && overlap > 0 && dist < bestDist)) { best = h; bestOverlap = overlap; bestDist = dist; }
        }
        return best;
    }

    /// <summary>초보: 좋아 보이는 태엽판부터, 체력이 가장 적은 적을 눌러 자동 배치 (게임의 적 클릭 배치 그대로)</summary>
    void PlanCasual()
    {
        foreach (Entry en in hand.OrderByDescending(e => Rating(e.card) + rng.NextDouble()).ToList())
        {
            Card c = en.card;
            if (c.cost > ki || c.reqTension > TensionStage) continue;
            U? t = Skill >= 0.4
                ? enemies.Where(e => e.Alive).OrderByDescending(e => Uncovered(e)).ThenBy(e => e.hp).FirstOrDefault()
                : enemies.Where(e => e.Alive).OrderBy(e => e.hp).FirstOrDefault();
            if (t == null) break;
            int h = FindHourAgainst(t, c.Len);
            if (h < 0) continue;
            hand.Remove(en); ki -= c.cost;
            pActs.Add(new Act { owner = player, card = c, entry = en, hour = h, length = c.Len, target = t });
        }
    }

    /// <summary>이 적이 이번 라운드에 올린 공격 칸 중 내 칸이 아직 안 막은 수</summary>
    int Uncovered(U e)
    {
        int n = 0;
        foreach (Act a in eActs) if (a.owner == e) for (int h = a.hour; h <= a.End; h++) if (a.DieAt(h)!.type == 0 && !pActs.Any(p => p.Covers(h))) n++;
        return n;
    }

    void Plan()
    {
        if (Skill <= 0.4 || (Skill < 1 && rng.NextDouble() > Skill)) { PlanCasual(); return; }
        planned.Clear();
        double wD = 1.0 + (player.hp < player.maxHp * 0.4 ? 0.5 : 0);
        while (true)
        {
            Entry? best = null; int bestH = 0; U? bestT = null; double bestScore = 0.35, bestV = 0;
            foreach (Entry en in hand)
            {
                Card c = en.card;
                if (c.cost > ki || c.reqTension > TensionStage) continue;
                for (int s = 1; s + c.Len - 1 <= 12; s++)
                {
                    Act probe = new() { hour = s, length = c.Len };
                    if (pActs.Any(a => a.Overlaps(probe))) continue;
                    foreach (U t in enemies)
                    {
                        if (!t.Alive) continue;
                        double v = Evaluate(en, s, t, wD);
                        double score = v / (c.cost + 0.6);
                        if (v > 0.35 && score > bestScore) { bestScore = score; best = en; bestH = s; bestT = t; bestV = v; }
                    }
                }
            }
            if (best == null) break;
            hand.Remove(best);
            ki -= best.card.cost;
            pActs.Add(new Act { owner = player, card = best.card, entry = best, hour = bestH, length = best.card.Len, target = bestT });
            planned[bestT!] = planned.GetValueOrDefault(bestT!) + Math.Max(0, bestV) * 0.6;
        }
    }

    // ───────────── 덱 판단 ─────────────

    public static double Rating(Card c)
    {
        double v = 0;
        foreach (Die d in c.dice)
        {
            double mean = (d.min + d.max) / 2.0;
            v += d.type == 0 ? mean : mean * 0.75;
            foreach (Fx f in d.fx)
            {
                double p = f.trigger == T.OnUse ? 1 : 0.55;
                double a = f.amount;
                v += p * (f.effect switch
                {
                    E.Rust => 1.1 * a, E.RecoverKi => 2.5 * a, E.DrawCard => a, E.Heal => 0.9 * a, E.Strength => 1.6 * a, E.Endurance => 0.8 * a,
                    E.Feeble => 1.3 * a, E.Fragile => 1.3 * a, E.Paralysis => 1.8 * a, E.PowerNextDie => 0.8 * a, E.DestroyNextDie => 3,
                    E.Overwind => 0.3 * a * c.dice.Count, E.Resonance => 1.2 * a, E.Corrode => 1.5 * a, E.Plating => 1.5 * a, E.OnTheHour => 0.5 * a / 0.55, E.MaxKi => 5 * a, _ => 0
                });
            }
        }
        return v / (c.cost + 1.2) - 0.25 * c.reqTension;
    }

    double DeckAvg() => deck.Average(e => Rating(e.card));

    public void MaybeAdd(Card c, bool absorbed)
    {
        double r = Rating(c) - (absorbed ? 0.25 : 0);
        if (r < DeckAvg() * 0.95) return;
        Add(c, absorbed);
    }

    public void Add(Card c, bool absorbed)
    {
        if (absorbed)
        {
            Entry? ex = deck.FirstOrDefault(e => e.absorbed && e.source == c.name);
            if (ex != null) { ex.corruption++; return; }
            deck.Add(new Entry { card = c, absorbed = true, corruption = 1, source = c.name });
        }
        else deck.Add(new Entry { card = c, source = c.name });
    }

    public List<Card> CardPool()
    {
        List<Card> pool = new();
        foreach (Card c in story.rewardPool) if (!pool.Contains(c)) pool.Add(c);
        if (pool.Count == 0) foreach (Card c in story.startingDeck) if (!pool.Contains(c)) pool.Add(c);
        foreach (Chapter ch in story.chapters) foreach (Card c in ch.rewards) if (!pool.Contains(c)) pool.Add(c);
        return pool;
    }

    public void PickNewCard()
    {
        List<Card> pool = CardPool(), offer = new();
        while (offer.Count < 3 && pool.Count > 0) { int i = rng.Next(pool.Count); offer.Add(pool[i]); pool.RemoveAt(i); }
        if (offer.Count == 0) return;
        MaybeAdd(offer.OrderByDescending(Rating).First(), false);
    }

    public void RemoveWorst()
    {
        if (deck.Count <= 5) return;
        Entry w = deck.OrderBy(e => Rating(e.card) - (e.absorbed ? 0.25 : 0)).First();
        if (Rating(w.card) < DeckAvg()) deck.Remove(w);
    }

    static readonly string[] itemNames = { "여분의 태엽", "기름 깡통", "황동 톱니", "무거운 태엽통", "덧댄 철판", "시계공의 구급함" };

    public void PickItem()
    {
        List<string> pool = itemNames.Where(n => !items.Contains(n)).ToList(), offer = new();
        if (pool.Count == 0) { PickNewCard(); return; }
        while (offer.Count < 3 && pool.Count > 0) { int i = rng.Next(pool.Count); offer.Add(pool[i]); pool.RemoveAt(i); }
        string pick = offer.Contains("여분의 태엽") ? "여분의 태엽"
                    : offer.Contains("무거운 태엽통") && player.maxHp >= 55 ? "무거운 태엽통"
                    : player.maxHp - player.hp >= 25 && offer.Contains("시계공의 구급함") ? "시계공의 구급함"
                    : offer.Contains("황동 톱니") ? "황동 톱니" : offer.Contains("덧댄 철판") ? "덧댄 철판" : offer.Contains("기름 깡통") ? "기름 깡통" : offer[0];
        items.Add(pick);
        switch (pick)
        {
            case "여분의 태엽": maxKi++; break;
            case "기름 깡통": ChangeMaxHp(Tune.OilMaxHp); player.Heal(Tune.OilHeal); break;
            case "황동 톱니": ChangeMaxHp(Tune.BrassMaxHp); break;
            case "덧댄 철판": ChangeMaxHp(8); break;
            case "시계공의 구급함": player.Heal(30); break;
            case "무거운 태엽통": maxKi++; ChangeMaxHp(-Tune.HeavyHpCost); break;
        }
    }

    void ChangeMaxHp(int d) { player.maxHp = Math.Max(1, player.maxHp + d); player.hp = Math.Clamp(player.hp + Math.Max(0, d), 1, player.maxHp); }
    public void LoseHp(int n) { int l = Math.Min(n, player.hp - 1); if (l > 0) player.Damage(l); }
}

static class Tune
{
    public static double RestHeal = 0.45;
    public static int OilMaxHp = 5, OilHeal = 20, BrassMaxHp = 12, HeavyHpCost = 10;
    public static int Rows = 6;
    public static int PlayerHp = 60;
    public static double ChapterHeal = 0.35;   // 챕터 전투를 이긴 뒤 회복 (최대 체력 비율) — GameManager.chapterClearHealRatio
    public static int[] TypeWeights = { 40, 64, 74, 84, 92 };   // 전투 / 이벤트 / 강적 / 휴식 / 고철상 / (나머지 궤짝)
}

class Node { public int row, col, type; public List<Node> next = new(); }   // type: 0 전 1 강 2 쉼 3 ? 4 철 5 궤 6 보스

class Stats
{
    public int runs, clears;
    public Dictionary<string, int[]> stage = new();        // 이름 → [시도, 승리, 잃은 체력 합, 라운드 합, 장력 합]
    public Dictionary<string, int> deaths = new();
    public Dictionary<string, double[]> checkpoint = new();   // 지점 → [도달 수, 체력 합, 덱 장수 합, 최대기력 합]
    public int[] nodeCount = new int[7];
    public Dictionary<int, int[]> bossByKi = new();
    public Dictionary<string, int[]> cardBoss = new();
    public void Stage(string n, bool win, int lost, int rounds, int tension)
    {
        if (!stage.TryGetValue(n, out int[]? a)) stage[n] = a = new int[5];
        a[0]++; if (win) a[1]++; a[2] += lost; a[3] += rounds; a[4] += tension;
    }
    public void Check(string n, Run r)
    {
        if (!checkpoint.TryGetValue(n, out double[]? a)) checkpoint[n] = a = new double[4];
        a[0]++; a[1] += r.player.hp; a[2] += r.deck.Count; a[3] += r.maxKi;
    }
}

static class Program
{
    const int Columns = 5, Walks = 6;

    static List<Node>[] Generate(Random rng, int rowCount, out Node boss)
    {
        Node?[,] grid = new Node?[rowCount, Columns];
        int first = -1;
        for (int w = 0; w < Walks; w++)
        {
            int c = rng.Next(Columns);
            if (w == 1) while (c == first) c = rng.Next(Columns);
            if (w == 0) first = c;
            for (int r = 0; r < rowCount; r++)
            {
                Node node = grid[r, c] ??= new Node { row = r, col = c };
                if (r == rowCount - 1) break;
                int nc = Math.Clamp(c + rng.Next(-1, 2), 0, Columns - 1);
                int cc = c;
                if (nc != c && grid[r, nc] != null && grid[r, nc]!.next.Exists(x => x.col == cc)) nc = c;
                Node up = grid[r + 1, nc] ??= new Node { row = r + 1, col = nc };
                if (!node.next.Contains(up)) node.next.Add(up);
                c = nc;
            }
        }
        List<Node>[] rows = new List<Node>[rowCount];
        boss = new Node { row = rowCount, type = 6 };
        for (int r = 0; r < rowCount; r++)
        {
            rows[r] = new();
            for (int c = 0; c < Columns; c++)
            {
                if (grid[r, c] == null) continue;
                rows[r].Add(grid[r, c]!);
                if (r == rowCount - 1) grid[r, c]!.next.Add(boss);
            }
        }
        for (int r = 0; r < rowCount; r++)
            foreach (Node n in rows[r])
            {
                List<Node> parents = r == 0 ? new() : rows[r - 1].Where(p => p.next.Contains(n)).ToList();
                n.type = PickType(rng, r, rowCount, parents);
            }
        return rows;
    }

    static int PickType(Random rng, int row, int rowCount, List<Node> parents)
    {
        if (row == 0) return 0;
        if (row == rowCount - 1) return 2;
        int[] w = Tune.TypeWeights;
        for (int tries = 0; tries < 8; tries++)
        {
            int roll = rng.Next(100);
            int t = roll < w[0] ? 0 : roll < w[1] ? 3 : roll < w[2] ? 1 : roll < w[3] ? 2 : roll < w[4] ? 4 : 5;
            if ((t == 1 || t == 2) && row < 2) continue;
            if (t == 2 && row == rowCount - 2) continue;
            if (t != 0 && parents.Exists(p => p.type == t)) continue;
            return t;
        }
        return 0;
    }

    static double NodeUtil(int type, double hpFrac) => type switch
    {
        0 => hpFrac > 0.5 ? 1.0 : -1.5,
        1 => hpFrac > 0.75 ? 1.5 : -4.0,
        2 => hpFrac < 0.65 ? 3.5 : 1.0,
        3 => 1.5,
        4 => 1.0,
        5 => 3.5,
        _ => 0,
    };

    static double BestPath(Node n, double hpFrac)
    {
        double here = NodeUtil(n.type, hpFrac);
        if (n.next.Count == 0) return here;
        double f = n.type == 0 ? hpFrac - 0.15 : n.type == 1 ? hpFrac - 0.3 : n.type == 2 ? Math.Min(1, hpFrac + 0.4) : hpFrac;
        return here + n.next.Max(x => BestPath(x, Math.Max(0.05, f)));
    }

    static bool Route(Run run, Chapter here, Stats st, string tag)
    {
        List<Node>[] rows = Generate(run.rng, Tune.Rows, out Node boss);
        Node? cur = null; Stage? last = null;
        while (true)
        {
            List<Node> options = cur == null ? rows[0] : cur.next;
            double frac = (double)run.player.hp / run.player.maxHp;
            Node chosen = Run.Skill < 0.3 ? options[run.rng.Next(options.Count)] : options.OrderByDescending(n => BestPath(n, frac)).First();
            if (chosen == boss) return true;
            cur = chosen;
            st.nodeCount[chosen.type]++;
            switch (chosen.type)
            {
                case 0:
                case 1:
                {
                    bool elite = chosen.type == 1;
                    List<Stage> groups = (elite ? here.elites : here.fights).Where(g => g.enemies.Count > 0).ToList();
                    if (groups.Count > 1 && last != null) groups.Remove(last);
                    Stage stage;
                    if (groups.Count > 0) { stage = groups[run.rng.Next(groups.Count)]; last = stage; }
                    else
                    {
                        stage = new Stage { name = here.battle!.name + (elite ? " — 강적" : " — 잔당") };
                        if (elite) stage.enemies.AddRange(here.battle.enemies); else stage.enemies.Add(here.battle.enemies[run.rng.Next(here.battle.enemies.Count)]);
                    }
                    if (!Fight(run, stage, st, tag + (elite ? " 강 " : " 전 "))) return false;
                    if (elite) run.PickNewCard();
                    break;
                }
                case 2:
                {
                    int amount = (int)Math.Ceiling(run.player.maxHp * Tune.RestHeal);
                    if (run.player.maxHp - run.player.hp < 8 && run.deck.Count > 5) run.RemoveWorst(); else run.player.Heal(amount);
                    break;
                }
                case 4: run.RemoveWorst(); break;
                case 5: run.PickItem(); break;
                default: Event(run); break;
            }
        }
    }

    static void Event(Run run)
    {
        U p = run.player; int missing = p.maxHp - p.hp;
        List<int> events = new() { 0, 1, 2, 3, 5, 6 };
        if (run.items.Count < 6) events.Add(4);
        switch (events[run.rng.Next(events.Count)])
        {
            case 0: if (missing >= 5) { if (run.rng.NextDouble() < 0.5) p.Heal(10); else run.LoseHp(5); } break;
            case 1: if (missing >= 8) p.Heal(8); else if (p.hp > 4) { run.LoseHp(4); Entry b = run.deck.OrderByDescending(e => Run.Rating(e.card)).First(); run.deck.Add(new Entry { card = b.card, absorbed = b.absorbed, corruption = b.corruption, source = b.source }); } break;
            case 2: { Entry? a = run.deck.FirstOrDefault(e => e.absorbed); if (a != null) { a.absorbed = false; a.corruption = 0; } else run.RemoveWorst(); break; }
            case 3: if (missing >= 10) p.Heal(15); else if (p.hp > 3) { run.LoseHp(3); run.PickNewCard(); } break;
            case 4: if (p.hp > 20) { run.LoseHp(8); run.PickItem(); } break;
            case 5: { Entry? a = run.deck.FirstOrDefault(e => e.absorbed); if (missing >= 10 || a == null) p.Heal(10); else { a.absorbed = false; a.corruption = 0; } break; }
            case 6: if (missing >= 6) p.Heal(6); break;
        }
    }

    static bool Fight(Run run, Stage stage, Stats st, string tag)
    {
        int before = run.player.hp;
        bool win = run.Battle(stage);
        st.Stage(tag + stage.name, win, before - run.player.hp, run.lastRounds, win ? run.lastTension : 0);
        if (!win) { st.deaths[tag + stage.name] = st.deaths.GetValueOrDefault(tag + stage.name) + 1; return false; }
        // 흡수: 장력 단계 + 1장까지, 덱보다 나은 것만
        List<Card> pool = stage.enemies.SelectMany(e => e.deck).Distinct().OrderByDescending(Run.Rating).ToList();
        int picks = Math.Min(1 + run.lastTension, pool.Count);
        for (int i = 0; i < picks; i++) run.MaybeAdd(pool[i], true);
        return true;
    }

    static Stats Simulate(Story story, int n, int seed0)
    {
        Stats st = new();
        for (int i = 0; i < n; i++)
        {
            Run run = new(story, seed0 + i);
            st.runs++;
            bool alive = true;
            for (int ci = 0; ci < story.chapters.Count && alive; ci++)
            {
                Chapter ch = story.chapters[ci];
                if (ch.battle != null)
                {
                    st.Check($"{ci} {ch.title} 시작", run);
                    bool final = !story.chapters.Skip(ci + 1).Any(c => c.battle != null);
                    List<string> names = run.deck.Select(e => e.card.name).Distinct().ToList(); int kiNow = run.maxKi;
                    alive = Fight(run, ch.battle, st, $"[{ci}] 본 ");
                    if (final)
                    {
                        if (!st.bossByKi.TryGetValue(kiNow, out int[]? bk)) st.bossByKi[kiNow] = bk = new int[2];
                        bk[0]++; if (alive) bk[1]++;
                        foreach (string nm in names) { if (!st.cardBoss.TryGetValue(nm, out int[]? cb)) st.cardBoss[nm] = cb = new int[2]; cb[0]++; if (alive) cb[1]++; }
                    }
                    if (!alive) break;
                }
                foreach (Card c in ch.rewards) run.Add(c, false);
                if (ch.battle != null) run.player.Heal((int)Math.Ceiling(run.player.maxHp * Tune.ChapterHeal));
                Chapter? next = story.chapters.Skip(ci + 1).FirstOrDefault(c => c.battle != null);
                if (ch.battle != null && next != null) alive = Route(run, ch, st, $"[{ci}]");
            }
            if (alive) { st.clears++; st.Check("클리어", run); }
        }
        return st;
    }

    static void Report(Stats st, string title)
    {
        Console.WriteLine($"\n===== {title} =====  클리어 {100.0 * st.clears / st.runs:0.0}%  ({st.runs}판)");
        foreach (var kv in st.checkpoint.OrderBy(k => k.Key))
            Console.WriteLine($"  {kv.Key,-28} 도달 {100.0 * kv.Value[0] / st.runs,5:0.0}%  체력 {kv.Value[1] / kv.Value[0],5:0.0}  덱 {kv.Value[2] / kv.Value[0],4:0.0}장  기력 {kv.Value[3] / kv.Value[0]:0.00}");
        Console.WriteLine("  무대                                   시도   승률   잃은체력  라운드  장력   전체 사망 비중");
        foreach (var kv in st.stage.OrderBy(k => k.Key))
        {
            int[] a = kv.Value;
            Console.WriteLine($"  {Pad(kv.Key, 38)} {a[0],5}  {100.0 * a[1] / a[0],5:0.0}%  {(double)a[2] / a[0],6:0.0}  {(double)a[3] / a[0],5:0.0}  {(double)a[4] / Math.Max(1, a[1]),4:0.0}   {100.0 * st.deaths.GetValueOrDefault(kv.Key) / st.runs,5:0.0}%");
        }
        string[] names = { "전", "강", "쉼", "?", "철", "궤" };
        Console.WriteLine("  보스 승률 (최대 기력별): " + string.Join("  ", st.bossByKi.OrderBy(k => k.Key).Select(k => $"기력{k.Key}: {100.0 * k.Value[1] / k.Value[0]:0}% ({k.Value[0]}판)")));
        if (Verbose)
        {
            double baseRate = (double)st.bossByKi.Values.Sum(v => v[1]) / Math.Max(1, st.bossByKi.Values.Sum(v => v[0]));
            Console.WriteLine("  덱에 있을 때 보스 승률 변화 (표본 150판 이상):");
            foreach (var kv in st.cardBoss.Where(k => k.Value[0] >= 150).OrderByDescending(k => (double)k.Value[1] / k.Value[0]))
                Console.WriteLine($"    {Pad(kv.Key, 18)} {100.0 * kv.Value[1] / kv.Value[0] - 100 * baseRate,6:+0.0;-0.0}%p  ({kv.Value[0]}판)");
        }
        Console.WriteLine("  판당 칸: " + string.Join("  ", names.Select((nm, i) => $"{nm} {(double)st.nodeCount[i] / st.runs:0.00}")));
    }

    public static bool Verbose;
    static string Pad(string s, int w)
    {
        int len = s.Sum(ch => ch > 0x2E80 ? 2 : 1);
        return s + new string(' ', Math.Max(0, w - len));
    }

    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        string root = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
        while (!Directory.Exists(Path.Combine(root, "Assets")) && Path.GetDirectoryName(root) != null) root = Path.GetDirectoryName(root)!;
        int n = args.Length > 1 ? int.Parse(args[1]) : 4000;
        string mode = args.Length > 2 ? args[2] : "run";
        Loader.Index(root);
        Story story = Loader.Story("0c4451682b320c9f761d98e970328f7e");
        if (args.Length > 3) Loader.Patch(args[3]);

        if (mode == "cards")
        {
            foreach (Card c in story.startingDeck.Distinct().Concat(story.rewardPool).Concat(story.chapters.SelectMany(c => c.rewards)).Distinct())
                Console.WriteLine($"{Pad(c.name, 18)} 등급{c.rarity} 기력{c.cost} 장력{c.reqTension} 평가 {Run.Rating(c):0.00}  " + Describe(c));
            Console.WriteLine("--- 적");
            foreach (Chapter ch in story.chapters)
            {
                IEnumerable<Stage> stages = (ch.battle != null ? new[] { ch.battle } : Array.Empty<Stage>()).Concat(ch.fights).Concat(ch.elites);
                Console.WriteLine($"## {ch.title}  본:{ch.battle?.name}  전:[{string.Join(", ", ch.fights.Select(s => s.name))}]  강:[{string.Join(", ", ch.elites.Select(s => s.name))}]");
                foreach (Stage s in stages.Distinct())
                {
                    Console.WriteLine($"  무대 {s.name}");
                    foreach (Enemy e in s.enemies)
                    {
                        Console.WriteLine($"    {e.name} HP{e.maxHp} 내성[{string.Join(",", e.res)}] 행동{e.actions}");
                        foreach (Card c in e.deck) Console.WriteLine($"       {Pad(c.name, 16)} 기력{c.cost} 평가 {Run.Rating(c):0.00} " + Describe(c));
                    }
                }
            }
            return;
        }

        Verbose = mode == "v";
        Run.Skill = 1.0; Report(Simulate(story, n, 1000), "숙련 (칸마다 계산해서 두는 플레이)");
        Verbose = false;
        Run.Skill = 0.6; Report(Simulate(story, n, 900000), "보통 (라운드의 60%는 계산해서, 나머지는 적을 눌러 자동 배치)");
        Run.Skill = 0.4; Report(Simulate(story, n, 500000), "초보 (공격이 오는 적을 눌러 자동 배치만)");
    }

    static readonly string[] det = { "참", "관", "타", "방", "회" };
    static readonly string[] trig = { "적중", "승리", "패배", "사용" };
    static readonly string[] eff = { "녹", "기력회복", "뽑기", "회복", "힘", "인내", "허약", "취약", "마비", "다음칸+", "칸파괴", "과감기", "울림", "부식x", "철갑", "정각+", "최대기력+" };
    static string Describe(Card c) => string.Join(" | ", c.dice.Select(d => $"{det[d.detail]}{d.min}-{d.max}" + string.Concat(d.fx.Select(f => $" [{trig[f.trigger]}:{eff[f.effect]}{f.amount}{(f.next ? "(다음)" : "")}]"))));
}
