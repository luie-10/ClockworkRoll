using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 사전 — 시작 화면의 [태엽판 · 적 사전]으로 연다. 게임에 들어 있는 모든 태엽판과 적을 본다.
///   [태엽판] 탭: 전체 / 내 태엽판(시작 덱 · 보상 · 챕터 보상) / 적의 태엽판 — 등급 · 기력 순
///   [적] 탭:     스토리 챕터 순서대로, 강적 · 보스 표시
/// 왼쪽 목록에서 하나를 누르면 오른쪽에 그림과 자세한 설명 (주사위 · 효과 용어 · 얻는 곳 / 체력 · 특성 · 내성 · 덱 · 나오는 곳).
/// 목록은 GameManager의 스토리 에셋에서 따라가며 모은다 (빌드에서도 그대로 동작).
/// </summary>
public class CompendiumPanel : MonoBehaviour
{
    private static CompendiumPanel instance;
    public static bool IsOpen { get { return instance != null && instance.gameObject.activeSelf; } }

    private enum Tab { Cards, Enemies }
    private enum CardFilter { All, Player, Enemy }

    // ─────────────────────────── 모은 자료 ───────────────────────────

    private class CardInfo
    {
        public CardData card;
        public bool player, enemyOwned;
        public readonly List<string> sources = new List<string>();     // 얻는 곳
        public readonly List<string> users = new List<string>();       // 쓰는 적
    }

    private class EnemyInfo
    {
        public EnemyData enemy;
        public int order;               // 처음 나오는 챕터 순서
        public bool elite, boss;
        public readonly List<string> places = new List<string>();
    }

    private readonly List<CardInfo> cards = new List<CardInfo>();
    private readonly List<EnemyInfo> enemies = new List<EnemyInfo>();

    private void Collect()
    {
        cards.Clear();
        enemies.Clear();
        StoryData story = GameManager.Instance != null ? GameManager.Instance.story : null;
        if (story == null) return;

        Dictionary<CardData, CardInfo> cardMap = new Dictionary<CardData, CardInfo>();
        Dictionary<EnemyData, EnemyInfo> enemyMap = new Dictionary<EnemyData, EnemyInfo>();

        CardInfo CardOf(CardData c)
        {
            if (!cardMap.TryGetValue(c, out CardInfo info)) { info = new CardInfo { card = c }; cardMap[c] = info; cards.Add(info); }
            return info;
        }
        void AddSource(CardData c, string where)
        {
            if (c == null) return;
            CardInfo info = CardOf(c);
            info.player = true;
            if (!info.sources.Contains(where)) info.sources.Add(where);
        }
        void AddStage(BattleStageData stage, int order, string place, bool elite, bool boss)
        {
            if (stage == null || stage.enemies == null) return;
            foreach (EnemySpawnInfo spawn in stage.enemies)
            {
                EnemyData e = spawn != null ? spawn.enemy : null;
                if (e == null) continue;
                if (!enemyMap.TryGetValue(e, out EnemyInfo info))
                {
                    info = new EnemyInfo { enemy = e, order = order };
                    enemyMap[e] = info;
                    enemies.Add(info);
                }
                info.elite |= elite;
                info.boss |= boss;
                if (!info.places.Contains(place)) info.places.Add(place);
                if (e.deck == null) continue;
                foreach (CardData c in e.deck)
                {
                    if (c == null) continue;
                    CardInfo ci = CardOf(c);
                    ci.enemyOwned = true;
                    if (!ci.users.Contains(e.enemyName)) ci.users.Add(e.enemyName);
                }
            }
        }

        foreach (CardData c in story.startingDeck) AddSource(c, "시작 덱");
        foreach (CardData c in story.rewardPool) AddSource(c, "갈래길 보상 (궤짝 · 강적 · 이벤트)");
        for (int i = 0; i < story.chapters.Count; i++)
        {
            ChapterData ch = story.chapters[i];
            if (ch == null) continue;
            string title = string.IsNullOrEmpty(ch.chapterTitle) ? $"{i + 1}장" : ch.chapterTitle;
            foreach (CardData c in ch.rewardCards) AddSource(c, $"{title} 클리어 보상");
            AddStage(ch.battle, i, $"{title} (본 전투)", false, true);
            foreach (BattleStageData s in ch.routeFights) AddStage(s, i, $"{title} (갈래길 전투)", false, false);
            foreach (BattleStageData s in ch.routeElites) AddStage(s, i, $"{title} (강적)", true, false);
        }

        cards.Sort((a, b) =>
        {
            if (a.player != b.player) return a.player ? -1 : 1;
            int r = a.card.rarity.CompareTo(b.card.rarity);
            if (r != 0) return r;
            r = a.card.cost.CompareTo(b.card.cost);
            return r != 0 ? r : string.CompareOrdinal(a.card.cardName, b.card.cardName);
        });
        enemies.Sort((a, b) =>
        {
            int r = a.order.CompareTo(b.order);
            if (r != 0) return r;
            int rank(EnemyInfo x) { return x.boss ? 2 : x.elite ? 1 : 0; }
            r = rank(a).CompareTo(rank(b));
            return r != 0 ? r : string.CompareOrdinal(a.enemy.enemyName, b.enemy.enemyName);
        });
    }

    // ─────────────────────────── 열기 ───────────────────────────

    public static void Open(Transform parent)
    {
        if (instance == null) instance = Build(parent);
        instance.gameObject.SetActive(true);
        instance.transform.SetAsLastSibling();
        instance.Collect();
        instance.ShowTab(instance.tab);
    }

    /// <summary>탭 바꾸기 (홍보영상 촬영)</summary>
    public static void ShowEnemies(bool enemies)
    {
        if (instance != null) instance.ShowTab(enemies ? Tab.Enemies : Tab.Cards);
    }

    /// <summary>목록 스크롤 (1 = 맨 위, 0 = 맨 아래)</summary>
    public static void ScrollList(float normalized)
    {
        if (instance != null && instance.listScroll != null) instance.listScroll.verticalNormalizedPosition = Mathf.Clamp01(normalized);
    }

    public static void Close()
    {
        if (instance != null) instance.gameObject.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) Close();
        else if (Input.GetKeyDown(KeyCode.Tab)) ShowTab(tab == Tab.Cards ? Tab.Enemies : Tab.Cards);
    }

    // ─────────────────────────── 목록 ───────────────────────────

    private Tab tab = Tab.Cards;
    private CardFilter filter = CardFilter.All;
    private RectTransform listContent;
    private ScrollRect listScroll;
    private Text countText, detailTitle, detailBody;
    private Image detailArt, detailFrame;
    private Button tabCards, tabEnemies;
    private readonly List<Button> filterButtons = new List<Button>();
    private GameObject filterRow;
    private Image selectedRow;
    private Coroutine artJob;

    private void ShowTab(Tab t)
    {
        tab = t;
        StyleToggle(tabCards, t == Tab.Cards);
        StyleToggle(tabEnemies, t == Tab.Enemies);
        filterRow.SetActive(t == Tab.Cards);
        for (int i = 0; i < filterButtons.Count; i++) StyleToggle(filterButtons[i], (int)filter == i);
        Rebuild();
    }

    private static void StyleToggle(Button b, bool on)
    {
        if (b == null) return;
        Image img = b.GetComponent<Image>();
        if (img != null) img.color = on ? UITheme.BrassFill : UITheme.InkSolid;
        Text label = b.GetComponentInChildren<Text>();
        if (label != null) label.color = on ? UITheme.TextOnBrass : UITheme.TextMain;
    }

    private void Rebuild()
    {
        if (artJob != null) StopCoroutine(artJob);
        for (int i = listContent.childCount - 1; i >= 0; i--) Destroy(listContent.GetChild(i).gameObject);
        selectedRow = null;

        List<(Image icon, System.Func<Sprite> art)> pending = new List<(Image, System.Func<Sprite>)>();
        int shown = 0;
        bool first = true;
        if (tab == Tab.Cards)
        {
            foreach (CardInfo info in cards)
            {
                if (filter == CardFilter.Player && !info.player) continue;
                if (filter == CardFilter.Enemy && !info.enemyOwned) continue;
                CardData c = info.card;
                string rarityHex = "#" + ColorUtility.ToHtmlStringRGB(DiceInfo.RarityColor(c.rarity));
                string owner = info.player ? "" : $"  <color={UITheme.HexEnemy}>적</color>";
                string head = $"<color={rarityHex}><b>{c.cardName}</b></color>{owner}";
                string sub = $"<color={UITheme.HexKi}>기력 {c.cost}</color>  {DiceInfo.Summary(c)}";
                CardInfo captured = info;
                Image icon = Row(head, sub, DiceInfo.RarityColor(c.rarity), row => SelectCard(captured, row), first);
                pending.Add((icon, () => c.cardArt != null ? c.cardArt : Illustrator.CardArt(c)));
                if (first) SelectCard(info, null);
                first = false;
                shown++;
            }
            countText.text = $"태엽판 {shown}장";
        }
        else
        {
            foreach (EnemyInfo info in enemies)
            {
                EnemyData e = info.enemy;
                string tag = info.boss ? $"  <color={UITheme.HexEnemy}>보스</color>" : info.elite ? $"  <color={UITheme.HexTension}>강적</color>" : "";
                string head = $"<b>{e.enemyName}</b>{tag}";
                StringBuilder traits = new StringBuilder();
                foreach (EnemyTraitEntry t in Traits(e)) traits.Append($" <color={UITheme.HexGold}>{EnemyTypeInfo.Name(t.trait)}</color>");
                string sub = $"<color={UITheme.HexGood}>HP {e.maxHp}</color>{(e.actionsPerRound > 1 ? $"  행동 {e.actionsPerRound}" : "")}{traits}";
                EnemyInfo captured = info;
                Image icon = Row(head, sub, e.tintColor, row => SelectEnemy(captured, row), first);
                pending.Add((icon, () => e.sprite != null ? e.sprite : Illustrator.Character(e.enemyName, e.tintColor).idle));
                if (first) SelectEnemy(info, null);
                first = false;
                shown++;
            }
            countText.text = $"적 {shown}종";
        }
        if (shown == 0) { detailTitle.text = ""; detailBody.text = "스토리 에셋을 찾지 못했다."; detailArt.enabled = false; }
        listScroll.verticalNormalizedPosition = 1f;
        artJob = StartCoroutine(PaintIcons(pending));
    }

    /// <summary>그림은 몇 개씩 나눠서 그린다 (한 번에 다 그리면 창이 늦게 뜬다)</summary>
    private IEnumerator PaintIcons(List<(Image icon, System.Func<Sprite> art)> pending)
    {
        int n = 0;
        foreach (var p in pending)
        {
            if (p.icon == null) continue;
            Sprite s = null;
            try { s = p.art(); } catch { }
            if (s != null)
            {
                p.icon.sprite = s;
                p.icon.color = Color.white;
                p.icon.rectTransform.sizeDelta = Fit(s, IconMaxW, IconH);   // 그림 비율 그대로
            }
            if (++n % 6 == 0) yield return null;
        }
        artJob = null;
    }

    private const float IconH = 60f, IconMaxW = 80f, TextX = 108f;

    /// <summary>그림 비율을 지키면서 maxW × maxH 안에 들어가는 크기</summary>
    private static Vector2 Fit(Sprite s, float maxW, float maxH)
    {
        if (s == null || s.rect.height <= 0f) return new Vector2(maxH, maxH);
        float aspect = s.rect.width / s.rect.height;
        float w = maxH * aspect, h = maxH;
        if (w > maxW) { w = maxW; h = maxW / aspect; }
        return new Vector2(w, h);
    }

    private Image Row(string head, string sub, Color stripe, System.Action<Image> onClick, bool selected)
    {
        Image bg = UIBuilder.Panel("Row", listContent, UITheme.InkSolid, false);
        LayoutElement le = bg.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = 72f;
        bg.raycastTarget = true;

        Image bar = UIBuilder.Panel("Stripe", bg.transform, stripe, false);
        bar.sprite = null;
        bar.raycastTarget = false;
        UIBuilder.Place(bar.rectTransform, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(5f, 0f));

        Image icon = UIBuilder.Panel("Icon", bg.transform, UITheme.Alpha(stripe, 0.25f), false);
        icon.type = Image.Type.Simple;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        UIBuilder.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(60f, 60f));

        Text h = UIBuilder.Label("Head", bg.transform, head, 22, UITheme.TextMain, TextAnchor.LowerLeft);
        h.supportRichText = true;
        h.raycastTarget = false;
        UIBuilder.Place(h.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(TextX, 0f), new Vector2(-TextX - 12f, 0f));
        Text s = UIBuilder.Label("Sub", bg.transform, sub, 17, UITheme.TextMuted, TextAnchor.UpperLeft);
        s.supportRichText = true;
        s.raycastTarget = false;
        s.horizontalOverflow = HorizontalWrapMode.Overflow;
        UIBuilder.Place(s.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(0f, 1f), new Vector2(TextX, 0f), new Vector2(-TextX - 12f, 0f));

        Button b = bg.gameObject.AddComponent<Button>();
        b.targetGraphic = bg;
        b.onClick.AddListener(() => { onClick(bg); Sfx.Play(SfxId.Click); });
        if (selected) Highlight(bg);
        return icon;
    }

    private void Highlight(Image row)
    {
        if (selectedRow != null) selectedRow.color = UITheme.InkSolid;
        selectedRow = row;
        if (row != null) row.color = UITheme.Alpha(UITheme.Brass, 0.28f);
    }

    // ─────────────────────────── 자세히 ───────────────────────────

    private void SelectCard(CardInfo info, Image row)
    {
        if (row != null) Highlight(row);
        CardData c = info.card;
        Color rarity = DiceInfo.RarityColor(c.rarity);
        string rarityHex = "#" + ColorUtility.ToHtmlStringRGB(rarity);
        SetArt(c.cardArt != null ? c.cardArt : Illustrator.CardArt(c), rarity);
        detailTitle.text = $"<color={rarityHex}>{c.cardName}</color>";

        StringBuilder sb = new StringBuilder();
        string keywords = DiceInfo.CardKeywords(c);
        sb.Append($"<color={rarityHex}>{DiceInfo.RarityName(c.rarity)}</color>  ·  <color={UITheme.HexKi}>기력 {c.cost}</color>  ·  <color={UITheme.HexGold}>{DiceInfo.LengthLabel(c)}</color>");
        if (c.requireTension > 0) sb.Append($"  ·  <color={UITheme.HexTension}>장력 {c.requireTension}단계</color>");
        if (keywords.Length > 0) sb.Append($"  ·  {keywords}");
        if (!string.IsNullOrEmpty(c.cardDescription)) sb.Append($"\n\n<i><color={UITheme.HexMuted}>{c.cardDescription}</color></i>");
        sb.Append($"\n\n<color={UITheme.HexBrass}>― 칸 ―</color>\n{DiceInfo.Detail(c)}");
        string glossary = DiceInfo.Keywords(c);
        if (glossary.Length > 0) sb.Append($"\n\n<color={UITheme.HexBrass}>― 용어 ―</color>\n<size=18>{glossary}</size>");
        if (info.sources.Count > 0) sb.Append($"\n\n<color={UITheme.HexBrass}>― 얻는 곳 ―</color>\n  {string.Join("\n  ", info.sources)}");
        if (info.users.Count > 0) sb.Append($"\n\n<color={UITheme.HexBrass}>― 쓰는 적 ―</color>\n  <color={UITheme.HexEnemy}>{string.Join(", ", info.users)}</color>\n  <color={UITheme.HexDim}>(이기면 기억으로 삼킬 수 있다)</color>");
        SetBody(sb.ToString());
    }

    private void SelectEnemy(EnemyInfo info, Image row)
    {
        if (row != null) Highlight(row);
        EnemyData e = info.enemy;
        SetArt(e.sprite != null ? e.sprite : Illustrator.Character(e.enemyName, e.tintColor).idle, e.tintColor);
        string tag = info.boss ? $"  <size=22><color={UITheme.HexEnemy}>보스</color></size>" : info.elite ? $"  <size=22><color={UITheme.HexTension}>강적</color></size>" : "";
        detailTitle.text = $"<color=#ff9e73>{e.enemyName}</color>{tag}";

        StringBuilder sb = new StringBuilder();
        sb.Append($"<color={UITheme.HexGood}>체력 {e.maxHp}</color>  ·  라운드마다 행동 {e.actionsPerRound}번");
        if (e.nicknames != null && e.nicknames.Count > 0) sb.Append($"\n<color={UITheme.HexDim}>별명: {string.Join(", ", e.nicknames)}</color>");

        sb.Append($"\n\n<color={UITheme.HexBrass}>― 행동 · 특성 ―</color>");
        sb.Append(e.behavior != EnemyBehavior.Random
            ? $"\n  <color={UITheme.HexTension}>{EnemyTypeInfo.Name(e.behavior)}</color>  <color={UITheme.HexMuted}>{EnemyTypeInfo.Describe(e.behavior)}</color>"
            : $"\n  <color={UITheme.HexTension}>무작위</color>  <color={UITheme.HexMuted}>아무 시각에나 놓는다</color>");
        bool anyTrait = false;
        foreach (EnemyTraitEntry t in Traits(e))
        {
            sb.Append($"\n  <color={UITheme.HexGold}>{EnemyTypeInfo.Name(t.trait)}</color>  <color={UITheme.HexMuted}>{EnemyTypeInfo.Describe(t.trait, t.amount)}</color>");
            anyTrait = true;
        }
        if (!anyTrait) sb.Append($"\n  <color={UITheme.HexDim}>특성 없음</color>");

        sb.Append($"\n\n<color={UITheme.HexBrass}>― 내성 (받는 피해) ―</color>");
        ResistanceSet r = e.resistance ?? new ResistanceSet();
        sb.Append($"\n  {ResistText(DiceDetail.Slash, r.slash)}   {ResistText(DiceDetail.Thrust, r.thrust)}   {ResistText(DiceDetail.Blunt, r.blunt)}");

        if (e.deck != null && e.deck.Count > 0)
        {
            sb.Append($"\n\n<color={UITheme.HexBrass}>― 쓰는 태엽판 ―</color>");
            HashSet<CardData> seen = new HashSet<CardData>();
            foreach (CardData c in e.deck)
            {
                if (c == null || !seen.Add(c)) continue;
                sb.Append($"\n  <b>{c.cardName}</b>  {DiceInfo.Summary(c)}");
                foreach (DiceData die in c.diceList)
                    if (die != null && die.effects != null)
                        foreach (DiceEffect fx in die.effects)
                            if (fx != null) sb.Append($"  <size=16><color=#ff8fa6>{fx.Describe()}</color></size>");
            }
        }
        if (info.places.Count > 0) sb.Append($"\n\n<color={UITheme.HexBrass}>― 나오는 곳 ―</color>\n  {string.Join("\n  ", info.places)}\n  <color={UITheme.HexDim}>무한 모드에서도 나온다</color>");
        string bark = e.barks != null && e.barks.Count > 0 ? e.barks[0] : null;
        if (!string.IsNullOrEmpty(bark)) sb.Append($"\n\n<i><color={UITheme.HexMuted}>“{bark}”</color></i>");
        SetBody(sb.ToString());
    }

    private static IEnumerable<EnemyTraitEntry> Traits(EnemyData e)
    {
        if (e.trait != EnemyTrait.None && e.traitAmount > 0) yield return new EnemyTraitEntry { trait = e.trait, amount = e.traitAmount };
        if (e.extraTraits == null) yield break;
        foreach (EnemyTraitEntry x in e.extraTraits)
            if (x != null && x.trait != EnemyTrait.None && x.amount > 0) yield return x;
    }

    private static string ResistText(DiceDetail d, Resistance r)
    {
        string name = $"<color={DiceInfo.ColorHex(d)}>{DiceInfo.Name(d)}</color>";
        switch (r)
        {
            case Resistance.Weak:  return $"{name} <color={UITheme.HexTension}>약점 x1.5</color>";
            case Resistance.Fatal: return $"{name} <color={UITheme.HexEnemy}>취약 x2.0</color>";
            default:               return $"{name} <color={UITheme.HexMuted}>보통</color>";
        }
    }

    private const float ArtH = 220f, ArtMaxW = 300f, ArtPad = 10f;

    private void SetArt(Sprite s, Color frame)
    {
        detailArt.enabled = s != null;
        detailArt.sprite = s;
        detailFrame.color = UITheme.Alpha(frame, 0.18f);
        Vector2 art = Fit(s, ArtMaxW - ArtPad * 2f, ArtH - ArtPad * 2f);
        Vector2 box = art + Vector2.one * ArtPad * 2f;
        detailFrame.rectTransform.sizeDelta = box;
        // 이름: 그림 틀 오른쪽 남은 폭 (틀 높이 가운데에)
        RectTransform t = detailTitle.rectTransform;
        t.anchoredPosition = new Vector2(20f + box.x + 24f, -20f);
        t.sizeDelta = new Vector2(758f - (20f + box.x + 24f) - 20f, box.y);
    }

    private ScrollRect detailScroll;
    private void SetBody(string text)
    {
        detailBody.text = text;
        if (detailScroll != null) detailScroll.verticalNormalizedPosition = 1f;
    }

    // ─────────────────────────── 생성 ───────────────────────────

    private static CompendiumPanel Build(Transform parent)
    {
        Vector2 center = new Vector2(0.5f, 0.5f), top = new Vector2(0.5f, 1f), topLeft = new Vector2(0f, 1f);

        Image dim = UIBuilder.Panel("CompendiumDim", parent, UITheme.Scrim, false);
        dim.sprite = null;
        UIBuilder.Stretch(dim.rectTransform);
        dim.raycastTarget = true;
        CompendiumPanel panel = dim.gameObject.AddComponent<CompendiumPanel>();

        Image window = UIBuilder.Panel("Window", dim.transform, UITheme.Window);
        UIBuilder.Place(window.rectTransform, center, center, center, Vector2.zero, new Vector2(1640f, 940f));
        UITheme.Elevate(window, 0.7f);
        UIPop.Ensure(window.gameObject, 0.95f, 0.16f);

        Text title = UIBuilder.Label("Title", window.transform, "태엽판 · 적 사전", 38, UITheme.Brass, TextAnchor.MiddleLeft);
        title.fontStyle = FontStyle.Bold;
        UIBuilder.Place(title.rectTransform, topLeft, topLeft, topLeft, new Vector2(36f, -22f), new Vector2(600f, 54f));

        // 탭
        panel.tabCards = UIBuilder.Button("TabCards", window.transform, "태엽판", UITheme.InkSolid, 22);
        UIBuilder.Place((RectTransform)panel.tabCards.transform, topLeft, topLeft, topLeft, new Vector2(36f, -90f), new Vector2(170f, 48f));
        panel.tabCards.onClick.AddListener(() => panel.ShowTab(Tab.Cards));
        panel.tabEnemies = UIBuilder.Button("TabEnemies", window.transform, "적", UITheme.InkSolid, 22);
        UIBuilder.Place((RectTransform)panel.tabEnemies.transform, topLeft, topLeft, topLeft, new Vector2(216f, -90f), new Vector2(170f, 48f));
        panel.tabEnemies.onClick.AddListener(() => panel.ShowTab(Tab.Enemies));

        // 태엽판 거르기
        RectTransform filters = UIBuilder.Rect("Filters", window.transform);
        UIBuilder.Place(filters, topLeft, topLeft, topLeft, new Vector2(420f, -96f), new Vector2(420f, 40f));
        panel.filterRow = filters.gameObject;
        string[] names = { "전체", "내 태엽판", "적의 태엽판" };
        for (int i = 0; i < names.Length; i++)
        {
            int idx = i;
            Button f = UIBuilder.Button("Filter" + i, filters, names[i], UITheme.InkSolid, 17);
            UIBuilder.Place((RectTransform)f.transform, topLeft, topLeft, topLeft, new Vector2(i * 138f, 0f), new Vector2(130f, 38f));
            f.onClick.AddListener(() => { panel.filter = (CardFilter)idx; panel.ShowTab(Tab.Cards); });
            panel.filterButtons.Add(f);
        }

        panel.countText = UIBuilder.Label("Count", window.transform, "", 18, UITheme.TextMuted, TextAnchor.MiddleRight);
        UIBuilder.Place(panel.countText.rectTransform, topLeft, topLeft, topLeft, new Vector2(520f, -30f), new Vector2(306f, 40f));

        // 왼쪽 목록
        Image listFrame = UIBuilder.Panel("List", window.transform, UITheme.Alpha(Color.black, 0.25f));
        UIBuilder.Place(listFrame.rectTransform, topLeft, topLeft, topLeft, new Vector2(36f, -150f), new Vector2(790f, 700f));
        listFrame.raycastTarget = true;
        RectTransform viewport = UIBuilder.Rect("Viewport", listFrame.transform);
        UIBuilder.Stretch(viewport, 8f);
        viewport.gameObject.AddComponent<RectMask2D>();
        panel.listContent = UIBuilder.Rect("Content", viewport);
        panel.listContent.anchorMin = new Vector2(0f, 1f);
        panel.listContent.anchorMax = Vector2.one;
        panel.listContent.pivot = new Vector2(0.5f, 1f);
        panel.listContent.anchoredPosition = Vector2.zero;
        panel.listContent.sizeDelta = Vector2.zero;
        VerticalLayoutGroup v = panel.listContent.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = 6f;
        v.padding = new RectOffset(0, 10, 0, 0);
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        ContentSizeFitter fit = panel.listContent.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        panel.listScroll = listFrame.gameObject.AddComponent<ScrollRect>();
        panel.listScroll.viewport = viewport;
        panel.listScroll.content = panel.listContent;
        panel.listScroll.horizontal = false;
        panel.listScroll.movementType = ScrollRect.MovementType.Clamped;
        panel.listScroll.scrollSensitivity = 40f;

        // 오른쪽 자세히: 그림 + 이름 + 설명(스크롤)
        Image detail = UIBuilder.Panel("Detail", window.transform, UITheme.Alpha(Color.black, 0.25f));
        UIBuilder.Place(detail.rectTransform, topLeft, topLeft, topLeft, new Vector2(846f, -96f), new Vector2(758f, 754f));

        panel.detailFrame = UIBuilder.Panel("ArtFrame", detail.transform, UITheme.Alpha(Color.white, 0.1f));
        UIBuilder.Place(panel.detailFrame.rectTransform, topLeft, topLeft, topLeft, new Vector2(20f, -20f), new Vector2(220f, 220f));
        panel.detailArt = UIBuilder.Panel("Art", panel.detailFrame.transform, Color.white, false);
        panel.detailArt.type = Image.Type.Simple;
        panel.detailArt.preserveAspect = true;
        panel.detailArt.raycastTarget = false;
        UIBuilder.Stretch(panel.detailArt.rectTransform, 10f);

        panel.detailTitle = UIBuilder.Label("Name", detail.transform, "", 34, UITheme.TextMain, TextAnchor.MiddleLeft);
        panel.detailTitle.supportRichText = true;
        panel.detailTitle.fontStyle = FontStyle.Bold;
        panel.detailTitle.horizontalOverflow = HorizontalWrapMode.Wrap;
        UIBuilder.Place(panel.detailTitle.rectTransform, topLeft, topLeft, topLeft, new Vector2(260f, -20f), new Vector2(478f, 220f));

        RectTransform dView = UIBuilder.Rect("BodyView", detail.transform);
        dView.anchorMin = Vector2.zero;
        dView.anchorMax = Vector2.one;
        dView.offsetMin = new Vector2(22f, 16f);
        dView.offsetMax = new Vector2(-18f, -256f);
        dView.gameObject.AddComponent<RectMask2D>();
        Image dHit = dView.gameObject.AddComponent<Image>();
        dHit.color = Color.clear;   // 빈 곳에서도 휠이 먹게
        RectTransform dContent = UIBuilder.Rect("Body", dView);
        dContent.anchorMin = new Vector2(0f, 1f);
        dContent.anchorMax = Vector2.one;
        dContent.pivot = new Vector2(0.5f, 1f);
        dContent.anchoredPosition = Vector2.zero;
        dContent.sizeDelta = Vector2.zero;
        panel.detailBody = dContent.gameObject.AddComponent<Text>();
        panel.detailBody.font = UIBuilder.DefaultFont;
        panel.detailBody.fontSize = 20;
        panel.detailBody.lineSpacing = 1.15f;
        panel.detailBody.color = UITheme.TextMain;
        panel.detailBody.supportRichText = true;
        panel.detailBody.alignment = TextAnchor.UpperLeft;
        panel.detailBody.horizontalOverflow = HorizontalWrapMode.Wrap;
        panel.detailBody.verticalOverflow = VerticalWrapMode.Overflow;
        panel.detailBody.raycastTarget = false;
        dContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        panel.detailScroll = dView.gameObject.AddComponent<ScrollRect>();
        panel.detailScroll.viewport = dView;
        panel.detailScroll.content = dContent;
        panel.detailScroll.horizontal = false;
        panel.detailScroll.movementType = ScrollRect.MovementType.Clamped;
        panel.detailScroll.scrollSensitivity = 40f;

        // 아래: 안내 + 닫기
        Vector2 bottom = new Vector2(0.5f, 0f);
        Text hint = UIBuilder.Label("Hint", window.transform, "Tab: 태엽판 ↔ 적   ·   목록에서 하나를 누르면 오른쪽에 자세히", 17, UITheme.TextDim, TextAnchor.MiddleLeft);
        UIBuilder.Place(hint.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 28f), new Vector2(800f, 30f));
        Button close = UIBuilder.Button("Close", window.transform, "닫기 (Esc)", UITheme.BrassFill, 20);
        UITheme.StyleButton(close, true, 20);
        UIBuilder.Place((RectTransform)close.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-36f, 22f), new Vector2(200f, 48f));
        close.onClick.AddListener(Close);

        return panel;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
