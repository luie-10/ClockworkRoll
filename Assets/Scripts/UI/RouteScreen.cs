using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전투에서 이긴 뒤 다음 챕터 전투까지 걷는 갈래길 지도.
///
///          (종) ← 다음 챕터 전투 (목적지)
///        쉼   쉼   쉼          맨 위층은 언제나 휴식
///       ⋮ ╲  ⋮  ╱ ⋮
///       전  ?   강   궤         점선으로 이어진 칸만 갈 수 있다 (한 층에 한 칸)
///        ⋮ ╱  ⋮
///         출발
///
/// 칸의 종류
///   전 전투   : 챕터의 갈래길 적 무리 중 하나와 싸운다 (이기면 흡수 화면)
///   강 강적   : 더 센 강적 무리와 싸운다 → 이기면 태엽판 3장 중 1장 추가
///   쉼 휴식   : 최대 체력의 30% 회복
///   ?  이벤트 : 무작위 사건 — 선택지마다 결과가 다르다 (Events()에서 추가/수정)
///   철 고철상 : 덱에서 태엽판 1장을 버린다
///   궤 궤짝   : 태엽판 3장 중 1장을 얻는다
///
/// 직접 붙일 필요 없음 — GameManager가 RouteScreen.Play()로 띄운다 (전용 Canvas를 알아서 만든다).
/// 체력은 전투 사이에 이어지므로 휴식이 의미가 있다.
/// </summary>
public class RouteScreen : MonoBehaviour
{
    private enum NodeType { Fight, Elite, Rest, Event, Remove, Treasure }

    private class MapNode
    {
        public int row, col;
        public NodeType type;
        public Vector2 pos;                                  // 지도 위 위치 (흔들림 포함)
        public readonly List<MapNode> next = new List<MapNode>();
        public Image view;
        public bool visited;
    }

    private class RouteEvent
    {
        public string title, body;
        public readonly List<EventOption> options = new List<EventOption>();
        public Func<bool> available;   // null = 항상 나올 수 있음
    }

    private class EventOption
    {
        public string label;
        public Func<bool> available;   // null = 항상 고를 수 있음
        public Func<IEnumerator> run;
    }

    private const int MinDeckSize = 5;          // 이보다 적게는 버릴 수 없다
    private const float RestHealRatio = 0.3f;
    private const int Columns = 5;
    private const int Walks = 6;                // 아래에서 위로 그어 보는 길 수 (겹친 곳이 갈림길이 된다)
    private const float NodeSize = 58f;

    private static RouteScreen instance;

    /// <summary>열려 있는 동안 true (테스트 자동 진행이 확인용으로 사용)</summary>
    public static bool IsOpen { get; private set; }

    private RectTransform root, choiceRow, gridArea, gridContent, buttonRow, mapLayer, mapArea;
    private Text titleText, subText, statusText, bodyText;
    private Action autoAction;   // 테스트 자동 진행용: 지금 화면의 기본 선택

    private List<MapNode>[] rows;
    private MapNode current;     // null = 아직 출발점
    private MapNode boss;
    private BattleStageData fightStage;
    private List<BattleStageData> fightGroups = new List<BattleStageData>(), eliteGroups = new List<BattleStageData>();
    private BattleStageData lastGroup;
    private readonly List<MapNode> reachable = new List<MapNode>();

    private static readonly Color fightColor = new Color(1f, 0.47f, 0.4f);
    private static readonly Color eliteColor = new Color(1f, 0.32f, 0.6f);
    private static readonly Color restColor = new Color(0.45f, 0.86f, 0.55f);
    private static readonly Color eventColor = new Color(0.74f, 0.58f, 1f);
    private static readonly Color removeColor = new Color(0.55f, 0.76f, 0.95f);
    private static readonly Color treasureColor = UITheme.Brass;
    private static readonly Color pathColor = new Color(0.86f, 0.69f, 0.42f, 0.28f);
    private static readonly Color walkedColor = new Color(0.86f, 0.69f, 0.42f, 0.95f);

    // ─────────────────────────── 진입 ───────────────────────────

    /// <summary>
    /// rows층짜리 지도를 만들어 걷게 하고, 꼭대기(bossName)에 닿으면 onDone을 부른다.
    /// fights / elites = '전' / '강' 칸에서 고를 적 무리. 비어 있으면 fightStage(방금 이긴 무대)의 적을 다시 쓴다.
    /// </summary>
    public static void Play(int rows, BattleStageData fightStage, List<BattleStageData> fights, List<BattleStageData> elites,
                            string bossName, Action onDone)
    {
        if (instance == null) instance = Create();
        instance.fightGroups = fights ?? new List<BattleStageData>();
        instance.eliteGroups = elites ?? new List<BattleStageData>();
        instance.lastGroup = null;
        instance.StartCoroutine(instance.Walk(rows, fightStage, bossName, onDone));
    }

    /// <summary>테스트 자동 진행용: 지금 화면에서 기본 선택(갈 수 있는 첫 칸 / 첫 선택지 / 계속)을 고른다</summary>
    public static void AutoPick()
    {
        if (instance == null || !IsOpen || instance.autoAction == null) return;
        Action a = instance.autoAction;
        instance.autoAction = null;
        a();
    }

    private IEnumerator Walk(int rowCount, BattleStageData stage, string bossName, Action onDone)
    {
        IsOpen = true;
        fightStage = stage;
        current = null;
        root.gameObject.SetActive(true);

        Generate(rowCount);
        DrawMap(bossName);

        while (true)
        {
            // 지도에서 다음 칸 고르기
            MapNode chosen = null;
            SetMapMode(true);
            RefreshMap();
            foreach (MapNode n in reachable)
            {
                MapNode node = n;
                Button b = node.view.GetComponent<Button>();
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(() => chosen = node);
            }
            autoAction = () => chosen = reachable[0];
            while (chosen == null) yield return null;
            foreach (MapNode n in reachable) n.view.GetComponent<Button>().onClick.RemoveAllListeners();

            if (chosen == boss) break;

            chosen.visited = true;
            current = chosen;
            RefreshMap();
            yield return new WaitForSecondsRealtime(0.25f);   // 고른 칸에 불이 들어오는 걸 잠깐 보여준다

            SetMapMode(false);
            switch (chosen.type)
            {
                case NodeType.Fight:    yield return Fight(false); break;
                case NodeType.Elite:    yield return Fight(true); break;
                case NodeType.Rest:     yield return Rest(); break;
                case NodeType.Remove:   yield return Remove("고철 수거상", "녹슨 손수레를 끄는 노인이 쓸모없는 태엽판을 거둬 간다. 한 장을 고른다."); break;
                case NodeType.Treasure: yield return Treasure(); break;
                default:                yield return Event(); break;
            }
        }

        Clear();
        root.gameObject.SetActive(false);
        IsOpen = false;
        if (onDone != null) onDone();
    }

    // ─────────────────────────── 지도 만들기 ───────────────────────────

    /// <summary>
    /// 아래에서 위로 길을 여러 번 그어서 겹치는 곳을 갈림길로 만든다.
    /// 한 칸 위로 갈 때 왼쪽/가운데/오른쪽 칸으로만 갈 수 있고, 이미 그어진 길과 X자로 엇갈리지 않게 한다.
    /// </summary>
    private void Generate(int rowCount)
    {
        MapNode[,] grid = new MapNode[rowCount, Columns];
        int firstStart = -1;
        for (int w = 0; w < Walks; w++)
        {
            int c = UnityEngine.Random.Range(0, Columns);
            if (w == 1) while (c == firstStart) c = UnityEngine.Random.Range(0, Columns);   // 처음부터 적어도 두 갈래
            if (w == 0) firstStart = c;

            for (int r = 0; r < rowCount; r++)
            {
                MapNode node = grid[r, c] ?? (grid[r, c] = new MapNode { row = r, col = c });
                if (r == rowCount - 1) break;

                int nc = Mathf.Clamp(c + UnityEngine.Random.Range(-1, 2), 0, Columns - 1);
                // 옆 칸에서 이쪽으로 비스듬히 넘어오는 길이 이미 있으면 엇갈리므로 곧장 위로
                if (nc != c && grid[r, nc] != null && grid[r, nc].next.Exists(x => x.col == c)) nc = c;

                MapNode up = grid[r + 1, nc] ?? (grid[r + 1, nc] = new MapNode { row = r + 1, col = nc });
                if (!node.next.Contains(up)) node.next.Add(up);
                c = nc;
            }
        }

        rows = new List<MapNode>[rowCount];
        boss = new MapNode { row = rowCount, col = Columns / 2 };
        for (int r = 0; r < rowCount; r++)
        {
            rows[r] = new List<MapNode>();
            for (int c = 0; c < Columns; c++)
            {
                if (grid[r, c] == null) continue;
                rows[r].Add(grid[r, c]);
                if (r == rowCount - 1) grid[r, c].next.Add(boss);
            }
        }

        // 칸 종류 정하기 (아래 → 위)
        for (int r = 0; r < rowCount; r++)
        {
            foreach (MapNode n in rows[r]) n.type = PickType(r, rowCount, Parents(n));
        }
    }

    private List<MapNode> Parents(MapNode n)
    {
        List<MapNode> list = new List<MapNode>();
        if (n.row == 0) return list;
        foreach (MapNode p in rows[n.row - 1]) if (p.next.Contains(n)) list.Add(p);
        return list;
    }

    /// <summary>
    /// 첫 층 = 전투, 맨 위층 = 휴식, 그 사이는 가중치 뽑기.
    /// 강적·휴식은 초반에 안 나오고, 전투가 아닌 특별한 칸은 바로 아래 칸과 겹치지 않게 한다.
    /// </summary>
    private static NodeType PickType(int row, int rowCount, List<MapNode> parents)
    {
        if (row == 0) return NodeType.Fight;
        if (row == rowCount - 1) return NodeType.Rest;

        for (int tries = 0; tries < 8; tries++)
        {
            int roll = UnityEngine.Random.Range(0, 100);
            NodeType t = roll < 40 ? NodeType.Fight
                       : roll < 64 ? NodeType.Event
                       : roll < 74 ? NodeType.Elite
                       : roll < 84 ? NodeType.Rest
                       : roll < 92 ? NodeType.Remove
                       : NodeType.Treasure;

            if ((t == NodeType.Elite || t == NodeType.Rest) && row < 2) continue;
            if (t == NodeType.Rest && row == rowCount - 2) continue;   // 바로 위가 휴식이니 연달아 쉬지 않게
            if (t != NodeType.Fight && parents.Exists(p => p.type == t)) continue;
            return t;
        }
        return NodeType.Fight;
    }

    // ─────────────────────────── 지도 그리기 ───────────────────────────

    private void DrawMap(string bossName)
    {
        for (int i = mapArea.childCount - 1; i >= 0; i--) Destroy(mapArea.GetChild(i).gameObject);

        float height = mapArea.rect.height > 0f ? mapArea.rect.height : 900f;
        float rowGap = (height - 120f) / (rows.Length + 1);
        float bottom = -height * 0.5f + 60f;
        const float colGap = 150f;

        Vector2 start = new Vector2(0f, bottom);
        foreach (List<MapNode> row in rows)
        {
            foreach (MapNode n in row)
            {
                // 격자 위치에 살짝 흔들림을 줘서 손으로 그린 지도처럼
                Vector2 jitter = new Vector2(UnityEngine.Random.Range(-22f, 22f), UnityEngine.Random.Range(-14f, 14f));
                n.pos = new Vector2((n.col - (Columns - 1) * 0.5f) * colGap, bottom + rowGap * (n.row + 1)) + jitter;
            }
        }
        boss.pos = new Vector2(0f, bottom + rowGap * (rows.Length + 1));

        // 길 (점선) 먼저 → 칸이 위에 그려지게
        RectTransform lines = UIBuilder.Rect("Paths", mapArea);
        UIBuilder.Stretch(lines);
        foreach (MapNode n in rows[0]) DottedLine(lines, start, n.pos, "start-" + n.col);
        foreach (List<MapNode> row in rows)
        {
            foreach (MapNode n in row)
            {
                foreach (MapNode up in n.next) DottedLine(lines, n.pos, up.pos, Key(n, up));
            }
        }

        // 출발점
        Image startDot = Circle("Start", mapArea, start, 22f, walkedColor);
        Text startLabel = UIBuilder.Label("StartLabel", startDot.transform, "출발", 18, UITheme.TextMuted, TextAnchor.UpperCenter);
        UIBuilder.Place(startLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(120f, 26f));

        foreach (List<MapNode> row in rows)
        {
            foreach (MapNode n in row) n.view = NodeView(n, mapArea);
        }

        boss.view = BossView(bossName);
    }

    private static string Key(MapNode a, MapNode b)
    {
        return $"{a.row}:{a.col}-{b.row}:{b.col}";
    }

    private readonly Dictionary<string, List<Image>> lineDots = new Dictionary<string, List<Image>>();

    private void DottedLine(RectTransform parent, Vector2 from, Vector2 to, string key)
    {
        List<Image> dots = new List<Image>();
        Vector2 dir = to - from;
        float len = dir.magnitude;
        const float margin = 36f;   // 칸 테두리에서 조금 떨어져서 시작/끝
        const float step = 15f;
        for (float d = margin; d < len - margin; d += step)
        {
            Image dot = Circle("Dot", parent, from + dir * (d / len), 6f, pathColor);
            dot.raycastTarget = false;
            dots.Add(dot);
        }
        lineDots[key] = dots;
    }

    private Image NodeView(MapNode n, RectTransform parent)
    {
        Color accent = TypeColor(n.type);
        Image bg = Circle($"Node {n.row} {n.col} {n.type}", parent, n.pos, NodeSize, UITheme.InkSolid);
        bg.raycastTarget = true;
        Outline ring = bg.gameObject.AddComponent<Outline>();
        ring.effectColor = accent;
        ring.effectDistance = new Vector2(2f, -2f);

        Text glyph = UIBuilder.Label("Glyph", bg.transform, TypeGlyph(n.type), 26, accent, TextAnchor.MiddleCenter);
        glyph.fontStyle = FontStyle.Bold;
        UIBuilder.Stretch(glyph.rectTransform);

        Button b = bg.gameObject.AddComponent<Button>();
        b.targetGraphic = bg;
        HoverColors(b);
        bg.gameObject.AddComponent<RouteNodeHover>().Setup(this, TypeName(n.type), TypeDesc(n.type));
        return bg;
    }

    private Image BossView(string bossName)
    {
        Image bg = Circle("Node Boss", mapArea, boss.pos, 96f, new Color(0.16f, 0.12f, 0.06f, 1f));
        bg.raycastTarget = true;
        Outline ring = bg.gameObject.AddComponent<Outline>();
        ring.effectColor = UITheme.Brass;
        ring.effectDistance = new Vector2(3f, -3f);
        Text glyph = UIBuilder.Label("Glyph", bg.transform, "종", 44, UITheme.Brass, TextAnchor.MiddleCenter);
        glyph.fontStyle = FontStyle.Bold;
        UIBuilder.Stretch(glyph.rectTransform);

        Text name = UIBuilder.Label("Name", bg.transform, bossName, 22, UITheme.Brass, TextAnchor.MiddleLeft);
        UIBuilder.Place(name.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(320f, 34f));
        name.fontStyle = FontStyle.Bold;

        Button b = bg.gameObject.AddComponent<Button>();
        b.targetGraphic = bg;
        HoverColors(b);
        bg.gameObject.AddComponent<RouteNodeHover>().Setup(this, bossName, "다음 챕터 전투. 여기에 들어서면 지도를 떠난다.");
        return bg;
    }

    private static Image Circle(string name, RectTransform parent, Vector2 pos, float size, Color color)
    {
        RectTransform rt = UIBuilder.Rect(name, parent);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(size, size);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = ProceduralSprites.Circle();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    /// <summary>지나온 칸 · 지금 갈 수 있는 칸 · 못 가는 칸을 다시 칠한다</summary>
    private void RefreshMap()
    {
        reachable.Clear();
        if (current == null) reachable.AddRange(rows[0]);
        else reachable.AddRange(current.next);

        int curRow = current != null ? current.row : -1;
        foreach (List<MapNode> row in rows)
        {
            foreach (MapNode n in row)
            {
                bool canGo = reachable.Contains(n);
                bool past = n.row <= curRow && !n.visited;
                Color accent = TypeColor(n.type);
                n.view.color = n.visited ? new Color(accent.r * 0.45f, accent.g * 0.45f, accent.b * 0.45f, 1f) : UITheme.InkSolid;
                CanvasGroup g = n.view.GetComponent<CanvasGroup>();
                if (g == null) g = n.view.gameObject.AddComponent<CanvasGroup>();
                g.alpha = past ? 0.25f : canGo || n.visited ? 1f : 0.6f;
                n.view.GetComponent<Button>().interactable = canGo;
                if (!canGo) n.view.rectTransform.localScale = Vector3.one;
            }
        }
        boss.view.GetComponent<Button>().interactable = reachable.Contains(boss);

        // 걸어온 길만 밝게
        foreach (KeyValuePair<string, List<Image>> line in lineDots)
        {
            foreach (Image dot in line.Value) dot.color = pathColor;
        }
        MapNode prev = null;
        foreach (List<MapNode> row in rows)
        {
            MapNode v = row.Find(n => n.visited);
            if (v == null) break;
            string key = prev == null ? "start-" + v.col : Key(prev, v);
            if (lineDots.TryGetValue(key, out List<Image> dots)) foreach (Image d in dots) d.color = walkedColor;
            prev = v;
        }

        statusTitle = current == null ? "출발점에서 첫 칸을 고른다" : $"{current.row + 1}층 / {rows.Length}층";
    }

    private string statusTitle = "";
    private string hoverName, hoverDesc;
    private Text mapTitle, mapSub, hoverText;

    /// <summary>칸에 마우스를 올리면 아래쪽에 이름/설명 (RouteNodeHover가 부름)</summary>
    public void ShowHover(string name, string desc)
    {
        hoverName = name;
        hoverDesc = desc;
    }

    public void HideHover(string name)
    {
        if (hoverName == name) hoverName = null;
    }

    private void SetMapMode(bool map)
    {
        Clear();
        mapLayer.gameObject.SetActive(map);
        foreach (Component c in new Component[] { titleText, subText, bodyText, choiceRow, buttonRow })
            c.gameObject.SetActive(!map);
        if (map) gridArea.gameObject.SetActive(false);
        hoverName = null;
    }

    // ─────────────────────────── 칸 종류 ───────────────────────────

    private static Color TypeColor(NodeType t)
    {
        switch (t)
        {
            case NodeType.Fight:    return fightColor;
            case NodeType.Elite:    return eliteColor;
            case NodeType.Rest:     return restColor;
            case NodeType.Remove:   return removeColor;
            case NodeType.Treasure: return treasureColor;
            default:                return eventColor;
        }
    }

    private static string TypeGlyph(NodeType t)
    {
        switch (t)
        {
            case NodeType.Fight:    return "전";
            case NodeType.Elite:    return "강";
            case NodeType.Rest:     return "쉼";
            case NodeType.Remove:   return "철";
            case NodeType.Treasure: return "궤";
            default:                return "?";
        }
    }

    private static string TypeName(NodeType t)
    {
        switch (t)
        {
            case NodeType.Fight:    return "전투";
            case NodeType.Elite:    return "강적";
            case NodeType.Rest:     return "휴식 — 꺼지지 않은 가로등";
            case NodeType.Remove:   return "고철 수거상";
            case NodeType.Treasure: return "떠내려온 궤짝";
            default:                return "알 수 없는 골목";
        }
    }

    private static string TypeDesc(NodeType t)
    {
        switch (t)
        {
            case NodeType.Fight:    return "물에 잠긴 거리의 적 무리와 싸운다.";
            case NodeType.Elite:    return "강한 적과 싸운다. 이기면 태엽판 3장 중 1장.";
            case NodeType.Rest:     return $"최대 체력의 {Mathf.RoundToInt(RestHealRatio * 100)}%를 회복한다.";
            case NodeType.Remove:   return "덱에서 태엽판 1장을 버린다.";
            case NodeType.Treasure: return "태엽판 3장 중 1장을 얻는다.";
            default:                return "무슨 일이 생길지 모른다.";
        }
    }

    // ─────────────────────────── 길마다 일어나는 일 ───────────────────────────

    /// <summary>
    /// 지도 위 전투: 적 무리 목록(전 = fights / 강 = elites)에서 하나를 고른다 — 방금 싸운 무리는 되도록 피한다.
    /// 목록이 비어 있으면 방금 이긴 무대에서 적 1마리(강적이면 전부)를 데려온다.
    /// </summary>
    private IEnumerator Fight(bool elite)
    {
        if (GameManager.Instance == null) yield break;

        List<BattleStageData> groups = (elite ? eliteGroups : fightGroups).FindAll(g => g != null && g.enemies.Count > 0);
        if (groups.Count > 1) groups.Remove(lastGroup);

        BattleStageData stage;
        bool temporary = false;
        if (groups.Count > 0)
        {
            stage = groups[UnityEngine.Random.Range(0, groups.Count)];
            lastGroup = stage;
        }
        else
        {
            if (fightStage == null || fightStage.enemies.Count == 0) yield break;
            stage = ScriptableObject.CreateInstance<BattleStageData>();
            temporary = true;
            stage.stageName = fightStage.stageName + (elite ? " — 강적" : " — 잔당");
            stage.backdrop = fightStage.backdrop;
            stage.startKi = fightStage.startKi;
            stage.kiPerTurn = fightStage.kiPerTurn;
            if (elite) stage.enemies.AddRange(fightStage.enemies);
            else stage.enemies.Add(fightStage.enemies[UnityEngine.Random.Range(0, fightStage.enemies.Count)]);
        }

        bool won = false;
        root.gameObject.SetActive(false);
        GameManager.Instance.StartRouteBattle(stage, () => won = true);
        while (!won) yield return null;   // 지면 GameManager가 패배 화면을 띄운다 (지도는 숨긴 채로 끝)
        root.gameObject.SetActive(true);
        if (temporary) Destroy(stage);

        if (elite) yield return PickNewCard("강적을 쓰러뜨렸다", "쓰러진 적의 품에서 젖지 않은 태엽판이 나온다. 한 장을 챙긴다.");
    }

    private IEnumerator Rest()
    {
        Unit p = Player;
        int healed = p != null ? p.Heal(Mathf.CeilToInt(p.MaxHp * RestHealRatio)) : 0;
        yield return Message("꺼지지 않은 가로등",
            "비를 피할 처마 밑, 가로등 하나가 아직 켜져 있다. 잠시 태엽을 쉬게 한다.\n\n" +
            $"<color=#7ddc8c>체력 +{healed}</color>");
    }

    private IEnumerator Remove(string title, string body)
    {
        if (Deck.Count <= MinDeckSize)
        {
            yield return Message(title, $"<color=#a8a29a>덱이 {MinDeckSize}장 이하라 더는 버릴 수 없다.</color>");
            yield break;
        }

        DeckEntry picked = null;
        yield return PickDeckEntry(title, body,
            null, "이 태엽판을 버린다", "아무것도 버리지 않는다", e => picked = e);
        if (picked == null) yield break;

        Deck.Remove(picked);
        yield return Message(title, $"[{picked.data.cardName}]을(를) 덱에서 뺐다.\n\n<color=#a8a29a>덱 {Deck.Count}장</color>");
    }

    private IEnumerator Treasure()
    {
        yield return PickNewCard("떠내려온 궤짝", "물살에 밀려온 궤짝 안에 태엽판이 젖지 않은 채 들어 있다. 한 장을 챙긴다.");
    }

    private IEnumerator Event()
    {
        List<RouteEvent> events = Events().FindAll(e => e.available == null || e.available());
        RouteEvent ev = events[UnityEngine.Random.Range(0, events.Count)];

        EventOption chosen = null;
        Clear();
        titleText.text = ev.title;
        subText.text = "? 이벤트";
        bodyText.text = ev.body;
        foreach (EventOption o in ev.options)
        {
            EventOption option = o;
            bool ok = o.available == null || o.available();
            AddButton(o.label, false, () => chosen = option, ok, 560f);
            if (ok && autoAction == null) autoAction = () => chosen = option;
        }
        // 선택지는 세로로 쌓는다 (문장이 길다)
        VerticalButtons();
        while (chosen == null) yield return null;

        yield return chosen.run();
    }

    // ─────────────────────────── 이벤트 목록 (여기에 추가) ───────────────────────────

    private List<RouteEvent> Events()
    {
        List<RouteEvent> list = new List<RouteEvent>();

        RouteEvent vending = new RouteEvent
        {
            title = "녹슨 자판기",
            body = "물에 반쯤 잠긴 자판기가 깜빡인다. 동전 투입구 위에 '태엽 한 바퀴'라고 적혀 있다.",
        };
        vending.options.Add(new EventOption
        {
            label = "태엽을 내어 준다  <color=#ff8a7a>체력 -6</color>  →  무작위 태엽판 1장",
            available = () => Player != null && Player.Hp > 6,
            run = () => GainRandomCard(6, "자판기가 덜컹거리더니 태엽판 한 장을 뱉어 낸다."),
        });
        vending.options.Add(new EventOption
        {
            label = "걷어찬다  →  반반 확률로 <color=#7ddc8c>체력 +8</color> / <color=#ff8a7a>체력 -5</color>",
            run = KickVending,
        });
        vending.options.Add(new EventOption { label = "지나간다", run = Nothing });
        list.Add(vending);

        RouteEvent mirror = new RouteEvent
        {
            title = "물에 비친 시계",
            body = "고인 웅덩이 아래, 거꾸로 선 또 하나의 시계 마을이 비친다. 그곳의 태엽판은 이쪽 것과 꼭 닮았다.",
        };
        mirror.options.Add(new EventOption
        {
            label = "손을 넣는다  <color=#ff8a7a>체력 -4</color>  →  덱의 태엽판 1장 복제",
            available = () => Player != null && Player.Hp > 4,
            run = DuplicateCard,
        });
        mirror.options.Add(new EventOption
        {
            label = "비친 얼굴을 보며 숨을 고른다  →  <color=#7ddc8c>체력 +5</color>",
            run = () => HealBy(5, "잔물결이 가라앉자 숨도 같이 가라앉는다."),
        });
        list.Add(mirror);

        RouteEvent bench = new RouteEvent
        {
            title = "시계공의 작업대",
            body = "주인 없는 작업대 위에 공구가 가지런하다. 망가진 태엽 하나쯤은 손볼 수 있을 것 같다.",
        };
        bench.options.Add(new EventOption
        {
            label = "◈ 흡수한 태엽판 1장을 정화한다  →  침식이 사라진다",
            available = () => Deck.Exists(e => e.absorbed),
            run = Purify,
        });
        bench.options.Add(new EventOption
        {
            label = "태엽판 1장을 분해한다  →  덱에서 삭제",
            available = () => Deck.Count > MinDeckSize,
            run = () => Remove("시계공의 작업대", "분해할 태엽판을 고른다. 부품은 다시 물살에 흘려보낸다."),
        });
        bench.options.Add(new EventOption { label = "공구를 내려놓고 간다", run = Nothing });
        list.Add(bench);

        RouteEvent chapel = new RouteEvent
        {
            title = "물에 잠긴 예배당",
            body = "종탑의 종소리를 흉내 내는 작은 종이 물결에 흔들린다. 제단 옆 헌금함은 아직 잠겨 있다.",
        };
        chapel.options.Add(new EventOption
        {
            label = "기도한다  →  <color=#7ddc8c>체력 +12</color>",
            run = () => HealBy(12, "작은 종이 한 번 울린다. 몸속 태엽이 조금 덜 삐걱거린다."),
        });
        chapel.options.Add(new EventOption
        {
            label = "헌금함을 비튼다  <color=#ff8a7a>체력 -3</color>  →  태엽판 3장 중 1장",
            available = () => Player != null && Player.Hp > 3,
            run = RobOfferingBox,
        });
        chapel.options.Add(new EventOption { label = "지나간다", run = Nothing });
        list.Add(chapel);

        return list;
    }

    // ─────────────────────────── 이벤트 결과 ───────────────────────────

    private IEnumerator Nothing()
    {
        yield break;
    }

    private IEnumerator HealBy(int amount, string text)
    {
        int healed = Player != null ? Player.Heal(amount) : 0;
        yield return Message(titleText.text, $"{text}\n\n<color=#7ddc8c>체력 +{healed}</color>");
    }

    private IEnumerator GainRandomCard(int hpCost, string text)
    {
        LoseHp(hpCost);
        List<CardData> pool = GameManager.Instance.RouteCardPool();
        if (pool.Count == 0) { yield return Message(titleText.text, text); yield break; }

        CardData card = pool[UnityEngine.Random.Range(0, pool.Count)];
        GameManager.Instance.AddCard(card);
        yield return Message(titleText.text, $"{text}\n\n<color=#ff8a7a>체력 -{hpCost}</color>   ·   <color=#ffd98a>[{card.cardName}] 획득</color>");
    }

    private IEnumerator KickVending()
    {
        if (UnityEngine.Random.value < 0.5f)
        {
            yield return HealBy(8, "쿵! 안에서 따뜻한 기름 한 병이 굴러 나온다.");
        }
        else
        {
            int lost = LoseHp(5);
            yield return Message(titleText.text, $"쿵! 녹슨 문짝이 튀어나와 정강이를 때린다.\n\n<color=#ff8a7a>체력 -{lost}</color>");
        }
    }

    private IEnumerator DuplicateCard()
    {
        DeckEntry picked = null;
        yield return PickDeckEntry(titleText.text, "물속으로 가져갈 태엽판을 고른다. 똑같은 한 장이 따라 올라온다.",
            null, "이 태엽판을 복제한다", null, e => picked = e);

        int lost = LoseHp(4);
        Deck.Add(new DeckEntry(picked.data, picked.absorbed, picked.corruption, picked.sourceName));
        yield return Message("물에 비친 시계", $"[{picked.data.cardName}]이(가) 한 장 더 생겼다.\n\n<color=#ff8a7a>체력 -{lost}</color>   ·   <color=#a8a29a>덱 {Deck.Count}장</color>");
    }

    private IEnumerator Purify()
    {
        DeckEntry picked = null;
        yield return PickDeckEntry(titleText.text, "정화할 ◈ 태엽판을 고른다. 다시는 침식이 일어나지 않는다.",
            e => e.absorbed, "이 태엽판을 정화한다", null, e => picked = e);

        picked.absorbed = false;
        picked.corruption = 0;
        yield return Message("시계공의 작업대", $"[{picked.data.cardName}]의 ◈ 표식이 지워졌다. 이제 온전히 내 것이다.");
    }

    private IEnumerator RobOfferingBox()
    {
        LoseHp(3);
        yield return PickNewCard("물에 잠긴 예배당", "자물쇠가 손가락을 긁으며 열린다. <color=#ff8a7a>체력 -3</color>\n안에 젖은 태엽판들이 들어 있다. 한 장을 챙긴다.");
    }

    // ─────────────────────────── 고르기 화면 ───────────────────────────

    /// <summary>글 + [계속] 버튼</summary>
    private IEnumerator Message(string title, string body)
    {
        bool done = false;
        Clear();
        titleText.text = title;
        bodyText.text = body;
        bodyText.rectTransform.anchoredPosition = new Vector2(0f, 40f);
        bodyText.rectTransform.sizeDelta = new Vector2(1200f, 300f);
        AddButton("계속", true, () => done = true);
        autoAction = () => done = true;
        while (!done) yield return null;
    }

    /// <summary>덱에서 한 장 고르기. skipLabel이 null이면 건너뛸 수 없다.</summary>
    private IEnumerator PickDeckEntry(string title, string body, Predicate<DeckEntry> filter, string confirmLabel, string skipLabel,
                                      Action<DeckEntry> result)
    {
        DeckEntry selected = null;
        bool done = false;

        Clear();
        titleText.text = title;
        bodyText.text = body;
        gridArea.gameObject.SetActive(true);

        Button confirm = null;
        List<Image> tiles = new List<Image>();
        List<DeckEntry> shown = new List<DeckEntry>();
        foreach (DeckEntry e in Deck)
        {
            if (e == null || e.data == null || (filter != null && !filter(e))) continue;
            DeckEntry entry = e;
            Image tile = null;
            tile = EntryTile(entry, () =>
            {
                selected = entry;
                foreach (Image t in tiles) t.color = UITheme.InkSolid;
                tile.color = new Color(0.32f, 0.24f, 0.08f, 1f);
                confirm.interactable = true;
            });
            tiles.Add(tile);
            shown.Add(entry);
        }

        if (skipLabel != null) AddButton(skipLabel, false, () => { selected = null; done = true; });
        confirm = AddButton(confirmLabel, true, () => done = selected != null);
        confirm.interactable = false;
        autoAction = () => { selected = skipLabel != null ? null : shown[0]; done = true; };

        while (!done) yield return null;
        result(selected);
    }

    /// <summary>새 태엽판 3장 중 1장 (건너뛸 수 있음)</summary>
    private IEnumerator PickNewCard(string title, string body)
    {
        List<CardData> pool = GameManager.Instance.RouteCardPool();
        List<CardData> offer = new List<CardData>();
        while (offer.Count < 3 && pool.Count > 0)
        {
            int i = UnityEngine.Random.Range(0, pool.Count);
            offer.Add(pool[i]);
            pool.RemoveAt(i);
        }

        CardData picked = null;
        bool done = false;
        Clear();
        titleText.text = title;
        bodyText.text = body;
        foreach (CardData c in offer)
        {
            CardData card = c;
            CardTile(card, () => { picked = card; done = true; });
        }
        AddButton("챙기지 않는다", false, () => done = true);
        autoAction = () => { picked = offer.Count > 0 ? offer[0] : null; done = true; };
        while (!done) yield return null;

        if (picked != null)
        {
            GameManager.Instance.AddCard(picked);
            yield return Message(title, $"<color=#ffd98a>[{picked.cardName}]</color>을(를) 덱에 넣었다.\n\n<color=#a8a29a>덱 {Deck.Count}장</color>");
        }
    }

    // ─────────────────────────── 체력 / 덱 ───────────────────────────

    private static Unit Player { get { return GameManager.Instance != null ? GameManager.Instance.playerUnit : null; } }

    private static List<DeckEntry> Deck { get { return GameManager.Instance.OwnedDeck; } }

    /// <summary>체력을 잃는다 (갈림길에서는 죽지 않는다 — 최소 1은 남는다). 실제로 잃은 양을 돌려준다</summary>
    private static int LoseHp(int amount)
    {
        Unit p = Player;
        if (p == null) return 0;
        int lost = Mathf.Min(amount, p.Hp - 1);
        if (lost > 0) p.TakeDamage(lost);
        return Mathf.Max(0, lost);
    }

    void Update()
    {
        if (!IsOpen) return;
        Unit p = Player;
        int deck = GameManager.Instance != null && GameManager.Instance.OwnedDeck != null ? Deck.Count : 0;
        statusText.text = p != null
            ? $"{p.unitName}   <color=#7ddc8c>체력 {p.Hp} / {p.MaxHp}</color>   ·   태엽판 {deck}장"
            : $"태엽판 {deck}장";

        if (!mapLayer.gameObject.activeSelf) return;
        mapSub.text = statusTitle;
        hoverText.text = hoverName != null
            ? $"<b><color=#dcb06b>{hoverName}</color></b>   {hoverDesc}"
            : "<color=#9a958d>밝게 빛나는 칸으로만 갈 수 있다 · 칸에 마우스를 올리면 설명</color>";

        // 지금 갈 수 있는 칸은 숨 쉬듯 커졌다 작아진다
        float pulse = 1f + 0.08f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f));
        foreach (MapNode n in reachable)
        {
            if (n.view != null) n.view.rectTransform.localScale = Vector3.one * pulse;
        }
    }

    // ─────────────────────────── 화면 조각 ───────────────────────────

    private void Clear()
    {
        autoAction = null;
        foreach (RectTransform parent in new[] { choiceRow, gridContent, buttonRow })
        {
            for (int i = parent.childCount - 1; i >= 0; i--) Destroy(parent.GetChild(i).gameObject);
        }
        gridArea.gameObject.SetActive(false);
        subText.text = "";
        bodyText.text = "";
        bodyText.rectTransform.anchoredPosition = new Vector2(0f, 250f);
        bodyText.rectTransform.sizeDelta = new Vector2(1300f, 110f);

        VerticalLayoutGroup v = buttonRow.GetComponent<VerticalLayoutGroup>();
        if (v != null) DestroyImmediate(v);
        if (buttonRow.GetComponent<HorizontalLayoutGroup>() == null) RowLayout(buttonRow, 24f);
        buttonRow.anchoredPosition = new Vector2(0f, -400f);
    }

    /// <summary>이벤트 선택지: 버튼을 가운데에 세로로 쌓는다</summary>
    private void VerticalButtons()
    {
        DestroyImmediate(buttonRow.GetComponent<HorizontalLayoutGroup>());
        VerticalLayoutGroup v = buttonRow.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = 14f;
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = false;
        v.childControlHeight = false;
        v.childForceExpandWidth = false;
        v.childForceExpandHeight = false;
        buttonRow.anchoredPosition = new Vector2(0f, 80f);
    }

    private void CardTile(CardData card, Action onClick)
    {
        Image frame = UIBuilder.Panel("Card " + card.cardName, choiceRow, UITheme.InkSolid, false);
        frame.rectTransform.sizeDelta = new Vector2(260f, 340f);
        frame.raycastTarget = true;
        Outline edge = frame.gameObject.AddComponent<Outline>();
        Color rarity = DiceInfo.RarityColor(card.rarity);
        edge.effectColor = new Color(rarity.r, rarity.g, rarity.b, 0.8f);
        edge.effectDistance = new Vector2(2f, -2f);
        Button b = frame.gameObject.AddComponent<Button>();
        b.targetGraphic = frame;
        HoverColors(b);
        b.onClick.AddListener(() => onClick());

        string rarityHex = "#" + ColorUtility.ToHtmlStringRGB(rarity);
        string text =
            $"<size=26><b>{card.cardName}</b></size>\n" +
            $"<color={rarityHex}>{DiceInfo.RarityName(card.rarity)}</color> · <color=#8cd9ff>기력 {card.cost}</color> · <color=#ffd159>{TimedAction.LengthOf(card)}칸</color>\n\n" +
            DiceInfo.Detail(card);
        Text label = UIBuilder.Label("Text", frame.transform, text, 19, UITheme.TextMain, TextAnchor.UpperLeft);
        UIBuilder.Stretch(label.rectTransform, 16f);
        label.rectTransform.offsetMax = new Vector2(-16f, -106f);   // 위쪽은 그림 칸
        VictoryScreen.AddCardArt(frame.rectTransform, card);
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 12;
        label.resizeTextMaxSize = 19;
        label.verticalOverflow = VerticalWrapMode.Truncate;
    }

    private Image EntryTile(DeckEntry e, Action onClick)
    {
        Image tile = UIBuilder.Panel("Entry " + e.data.cardName, gridContent, UITheme.InkSolid, false);
        tile.raycastTarget = true;
        Button b = tile.gameObject.AddComponent<Button>();
        b.targetGraphic = tile;
        HoverColors(b);
        b.onClick.AddListener(() => onClick());

        Color stripe = e.absorbed ? new Color(0.72f, 0.45f, 0.95f) : DiceInfo.RarityColor(e.data.rarity);
        Image band = UIBuilder.Panel("Stripe", tile.transform, stripe, false);
        band.raycastTarget = false;
        UIBuilder.Place(band.rectTransform, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(5f, -14f));

        string name = e.absorbed ? $"<color=#e0b0ff>◈ {e.data.cardName}</color>  <size=14><color=#c9a0ee>침식 {e.corruption}</color></size>" : e.data.cardName;
        Text t = UIBuilder.Label("Text", tile.transform,
            $"<b>{name}</b>\n<size=15><color=#8cd9ff>기력 {e.data.cost}</color>  {DiceInfo.Summary(e.data)}</size>",
            19, UITheme.TextMain, TextAnchor.MiddleLeft);
        UIBuilder.Stretch(t.rectTransform);
        t.rectTransform.offsetMin = new Vector2(20f, 6f);
        t.rectTransform.offsetMax = new Vector2(-10f, -6f);
        t.lineSpacing = 1.15f;
        return tile;
    }

    private Button AddButton(string label, bool primary, Action onClick, bool interactable = true, float width = 300f)
    {
        Button b = UIBuilder.Button("Button", buttonRow, label, UITheme.InkSolid, 24);
        UITheme.StyleButton(b, primary, 22);
        Text t = b.GetComponentInChildren<Text>();
        if (t != null) t.supportRichText = true;
        ((RectTransform)b.transform).sizeDelta = new Vector2(width, 60f);
        b.interactable = interactable;
        b.onClick.AddListener(() => onClick());
        return b;
    }

    private static void HoverColors(Button b)
    {
        ColorBlock cb = b.colors;
        cb.highlightedColor = new Color(1.25f, 1.25f, 1.25f);
        cb.pressedColor = new Color(0.85f, 0.85f, 0.85f);
        cb.fadeDuration = 0.08f;
        b.colors = cb;
    }

    private static void RowLayout(RectTransform rt, float spacing)
    {
        HorizontalLayoutGroup row = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = spacing;
        row.childAlignment = TextAnchor.MiddleCenter;
        row.childControlWidth = false;
        row.childControlHeight = false;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = false;
    }

    // ─────────────────────────── 생성 ───────────────────────────

    private static RouteScreen Create()
    {
        GameObject go = new GameObject("RouteScreenCanvas");
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 600;   // 전투 UI 위, 결과 화면(700) 아래
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();

        RouteScreen screen = go.AddComponent<RouteScreen>();
        Vector2 center = new Vector2(0.5f, 0.5f);

        Image dim = UIBuilder.Panel("Dim", go.transform, new Color(0.02f, 0.025f, 0.035f, 0.93f), false);
        dim.sprite = null;
        UIBuilder.Stretch(dim.rectTransform);
        dim.raycastTarget = true;   // 뒤의 화면 클릭 막기
        screen.root = dim.rectTransform;

        screen.titleText = UIBuilder.Label("Title", dim.transform, "", 56, UITheme.Brass, TextAnchor.MiddleCenter);
        screen.titleText.fontStyle = FontStyle.Bold;
        UIBuilder.Place(screen.titleText.rectTransform, center, center, center, new Vector2(0f, 400f), new Vector2(1200f, 80f));

        screen.subText = UIBuilder.Label("Sub", dim.transform, "", 22, UITheme.TextMuted, TextAnchor.MiddleCenter);
        UIBuilder.Place(screen.subText.rectTransform, center, center, center, new Vector2(0f, 345f), new Vector2(1200f, 34f));

        screen.statusText = UIBuilder.Label("Status", dim.transform, "", 22, UITheme.TextMain, TextAnchor.MiddleRight);
        UIBuilder.Place(screen.statusText.rectTransform, Vector2.one, Vector2.one, Vector2.one, new Vector2(-40f, -28f), new Vector2(700f, 36f));

        screen.bodyText = UIBuilder.Label("Body", dim.transform, "", 25, UITheme.TextMain, TextAnchor.MiddleCenter);
        screen.bodyText.lineSpacing = 1.2f;
        UIBuilder.Place(screen.bodyText.rectTransform, center, center, center, new Vector2(0f, 250f), new Vector2(1300f, 110f));

        screen.choiceRow = UIBuilder.Rect("Choices", dim.transform);
        UIBuilder.Place(screen.choiceRow, center, center, center, new Vector2(0f, -40f), new Vector2(1700f, 400f));
        RowLayout(screen.choiceRow, 40f);

        // 덱 고르기: 스크롤되는 격자
        screen.gridArea = UIBuilder.Rect("Grid", dim.transform);
        UIBuilder.Place(screen.gridArea, center, center, center, new Vector2(0f, -80f), new Vector2(1560f, 500f));   // 위 설명 글과 겹치지 않게
        screen.gridArea.gameObject.AddComponent<RectMask2D>();
        Image hit = screen.gridArea.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);   // 빈 곳에서도 휠 스크롤이 먹도록
        screen.gridContent = UIBuilder.Rect("Content", screen.gridArea);
        screen.gridContent.anchorMin = new Vector2(0f, 1f);
        screen.gridContent.anchorMax = Vector2.one;
        screen.gridContent.pivot = new Vector2(0.5f, 1f);
        screen.gridContent.sizeDelta = Vector2.zero;
        GridLayoutGroup grid = screen.gridContent.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(292f, 84f);
        grid.spacing = new Vector2(14f, 14f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 5;
        grid.childAlignment = TextAnchor.UpperCenter;
        ContentSizeFitter fit = screen.gridContent.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        ScrollRect scroll = screen.gridArea.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = screen.gridArea;
        scroll.content = screen.gridContent;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        screen.buttonRow = UIBuilder.Rect("Buttons", dim.transform);
        UIBuilder.Place(screen.buttonRow, center, center, new Vector2(0.5f, 1f), new Vector2(0f, -400f), new Vector2(1400f, 70f));
        RowLayout(screen.buttonRow, 24f);

        BuildMapLayer(screen, dim.rectTransform);

        dim.gameObject.SetActive(false);
        return screen;
    }

    /// <summary>지도 화면: 가운데 세로로 긴 지도판 + 오른쪽 범례 + 아래 설명 줄</summary>
    private static void BuildMapLayer(RouteScreen screen, RectTransform dim)
    {
        Vector2 center = new Vector2(0.5f, 0.5f);
        screen.mapLayer = UIBuilder.Rect("MapLayer", dim);
        UIBuilder.Stretch(screen.mapLayer);

        screen.mapTitle = UIBuilder.Label("Title", screen.mapLayer, "갈래길", 44, UITheme.Brass, TextAnchor.MiddleLeft);
        screen.mapTitle.fontStyle = FontStyle.Bold;
        UIBuilder.Place(screen.mapTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -36f), new Vector2(500f, 60f));
        screen.mapSub = UIBuilder.Label("Sub", screen.mapLayer, "", 22, UITheme.TextMuted, TextAnchor.MiddleLeft);
        UIBuilder.Place(screen.mapSub.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(62f, -96f), new Vector2(500f, 32f));

        // 지도판 (위아래로 긴 먹색 판, 테두리 황동)
        Image board = UIBuilder.Panel("Board", screen.mapLayer, new Color(0.06f, 0.06f, 0.075f, 0.94f));
        UIBuilder.Place(board.rectTransform, center, center, center, new Vector2(0f, 6f), new Vector2(900f, 960f));
        board.raycastTarget = false;
        screen.mapArea = UIBuilder.Rect("Map", board.transform);
        UIBuilder.Stretch(screen.mapArea, 20f);

        // 범례
        Image legend = UIBuilder.Panel("Legend", screen.mapLayer, UITheme.Ink);
        UIBuilder.Place(legend.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-70f, 60f), new Vector2(250f, 400f));
        Text head = UIBuilder.Label("Head", legend.transform, "범례", 24, UITheme.Brass, TextAnchor.MiddleLeft);
        head.fontStyle = FontStyle.Bold;
        UIBuilder.Place(head.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(-44f, 34f));
        NodeType[] order = { NodeType.Fight, NodeType.Elite, NodeType.Event, NodeType.Rest, NodeType.Remove, NodeType.Treasure };
        for (int i = 0; i < order.Length; i++)
        {
            Vector2 pos = new Vector2(-70f, 120f - i * 50f);
            Image dot = Circle("Key", legend.rectTransform, pos, 38f, UITheme.InkSolid);
            Outline ring = dot.gameObject.AddComponent<Outline>();
            ring.effectColor = TypeColor(order[i]);
            ring.effectDistance = new Vector2(1.5f, -1.5f);
            Text g = UIBuilder.Label("Glyph", dot.transform, TypeGlyph(order[i]), 18, TypeColor(order[i]), TextAnchor.MiddleCenter);
            g.fontStyle = FontStyle.Bold;
            UIBuilder.Stretch(g.rectTransform);
            string label = order[i] == NodeType.Rest ? "휴식" : order[i] == NodeType.Event ? "이벤트" : TypeName(order[i]);
            Text t = UIBuilder.Label("Label", legend.transform, label, 20, UITheme.TextMain, TextAnchor.MiddleLeft);
            UIBuilder.Place(t.rectTransform, center, center, new Vector2(0f, 0.5f), new Vector2(-38f, pos.y), new Vector2(160f, 30f));
        }

        // 아래 설명 줄 (마우스를 올린 칸)
        screen.hoverText = UIBuilder.Label("Hover", screen.mapLayer, "", 22, UITheme.TextMain, TextAnchor.MiddleCenter);
        UIBuilder.Place(screen.hoverText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(1400f, 34f));
        screen.hoverText.supportRichText = true;

        screen.mapLayer.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (instance == this) { instance = null; IsOpen = false; }
    }
}

/// <summary>지도 칸에 마우스를 올리면 RouteScreen 아래 설명 줄에 이름/설명을 띄운다</summary>
public class RouteNodeHover : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
{
    private RouteScreen screen;
    private string title, desc;

    public void Setup(RouteScreen screen, string title, string desc)
    {
        this.screen = screen;
        this.title = title;
        this.desc = desc;
    }

    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e) { if (screen != null) screen.ShowHover(title, desc); }

    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) { if (screen != null) screen.HideHover(title); }
}
