using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전투에서 이긴 뒤 다음 챕터 전투까지 걷는 갈래길 지도.
///
///   맨 위 = 다음 챕터 전투 (종 문양, 목적지) · 그 아래층은 언제나 휴식 · 맨 아래 = 출발점.
///   점선으로 이어진 칸만 갈 수 있다 (한 층에 한 칸).
///   칸은 글자가 아니라 문양으로 그린다 (RouteIcons — 코드로 그린 그림).
///
/// 칸의 종류 (문양)
///   엇갈린 검 전투   : 챕터의 갈래길 적 무리 중 하나와 싸운다 (이기면 흡수 화면)
///   뿔 해골 강적   : 더 센 강적 무리와 싸운다 → 이기면 태엽판 3장 중 1장 추가
///   등불 휴식   : 최대 체력의 30% 회복
///   낯선 문 이벤트 : 무작위 사건 — 선택지마다 결과가 다르다 (Events()에서 추가/수정)
///   금 간 톱니 고철상 : 덱에서 태엽판 1장을 버린다
///   궤짝   : 부품 3개 중 1개를 얻는다 (최대 기력이 늘어난다)
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
        public Image view, icon;
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
    private const float RestHealRatio = 0.45f;   // 휴식: 최대 체력의 이만큼 회복
    private const int Columns = 5;
    private const int Walks = 6;                // 아래에서 위로 그어 보는 길 수 (겹친 곳이 갈림길이 된다)
    private const float NodeSize = 64f;

    private static RouteScreen instance;

    /// <summary>열려 있는 동안 true (테스트 자동 진행이 확인용으로 사용)</summary>
    public static bool IsOpen { get; private set; }

    /// <summary>씬을 다시 불렀을 때 (시작 화면으로 나갔다 올 때) 지난 씬의 '열려 있음'을 잊는다</summary>
    public static void ForgetScene() { IsOpen = false; }

    private RectTransform root, choiceRow, gridArea, gridContent, buttonRow, mapLayer, mapArea;
    private Text titleText, subText, statusText, bodyText;
    private Action autoAction;   // 테스트 자동 진행용: 지금 화면의 기본 선택

    private List<MapNode>[] rows;
    private MapNode current;     // null = 아직 출발점
    private MapNode boss;
    private BattleStageData fightStage;
    private List<BattleStageData> fightGroups = new List<BattleStageData>(), eliteGroups = new List<BattleStageData>();
    private BattleStageData lastGroup;
    private List<BackdropType> backdropPool = new List<BackdropType>();   // 이 챕터 지도의 전투 배경들
    private BackdropType? lastBackdrop;
    private readonly List<MapNode> reachable = new List<MapNode>();

    private static readonly Color fightColor = UITheme.Enemy;
    private static readonly Color eliteColor = new Color(1f, 0.32f, 0.6f);
    private static readonly Color restColor = UITheme.Good;
    private static readonly Color eventColor = UITheme.Absorb;
    private static readonly Color removeColor = UITheme.Ki;
    private static readonly Color treasureColor = UITheme.Brass;
    private static readonly Color pathColor = UITheme.Alpha(UITheme.Brass, 0.28f);
    private static readonly Color walkedColor = UITheme.Alpha(UITheme.Brass, 0.95f);

    // ─────────────────────────── 진입 ───────────────────────────

    /// <summary>
    /// rows층짜리 지도를 만들어 걷게 하고, 꼭대기(bossName)에 닿으면 onDone을 부른다.
    /// fights / elites = '전' / '강' 칸에서 고를 적 무리. 비어 있으면 fightStage(방금 이긴 무대)의 적을 다시 쓴다.
    /// </summary>
    /// resumeSeed / resumePath = 이어하기: 저장해 둔 씨앗으로 같은 지도를 다시 만들고, 지나온 칸까지 걸어 둔다.
    public static void Play(int rows, BattleStageData fightStage, List<BattleStageData> fights, List<BattleStageData> elites,
                            string bossName, Action onDone, int resumeSeed = 0, List<int> resumePath = null, List<BackdropType> backdrops = null)
    {
        if (instance == null) instance = Create();
        instance.fightGroups = fights ?? new List<BattleStageData>();
        instance.eliteGroups = elites ?? new List<BattleStageData>();
        instance.lastGroup = null;
        instance.backdropPool = backdrops != null ? new List<BackdropType>(backdrops) : new List<BackdropType>();
        instance.lastBackdrop = null;
        instance.StartCoroutine(instance.Walk(rows, fightStage, bossName, onDone, resumeSeed, resumePath));
    }

    private int mapSeed;                                     // 이 지도를 만든 씨앗 (저장용)
    private readonly List<int> walked = new List<int>();     // 지나온 칸 (층 * 100 + 열)
    private readonly HashSet<string> seenEvents = new HashSet<string>();   // 이번 지도에서 본 이벤트 (제목)

    /// <summary>테스트 자동 진행용: 지금 화면에서 기본 선택(갈 수 있는 첫 칸 / 첫 선택지 / 계속)을 고른다</summary>
    /// <summary>자동 진행이 지도에서 전투 칸을 먼저 고르게 한다 (홍보영상 촬영)</summary>
    public static bool PreferFight;

    public static void AutoPick()
    {
        if (instance == null || !IsOpen || instance.autoAction == null) return;
        Action a = instance.autoAction;
        instance.autoAction = null;
        a();
    }

    private IEnumerator Walk(int rowCount, BattleStageData stage, string bossName, Action onDone, int resumeSeed, List<int> resumePath)
    {
        IsOpen = true;
        fightStage = stage;
        current = null;
        seenEvents.Clear();
        root.gameObject.SetActive(true);

        // 지도는 씨앗 하나로 정해진다 — 씨앗을 저장해 두면 이어할 때 똑같은 지도가 나온다 (다른 무작위에는 영향 없게 되돌린다)
        mapSeed = resumeSeed != 0 ? resumeSeed : UnityEngine.Random.Range(1, int.MaxValue);
        UnityEngine.Random.State before = UnityEngine.Random.state;
        UnityEngine.Random.InitState(mapSeed);
        Generate(rowCount);
        DrawMap(bossName);
        UnityEngine.Random.state = before;

        // 이어하기: 지나온 칸을 다시 밟아 둔다 (저장과 지도가 어긋나면 거기서 멈춘다)
        walked.Clear();
        if (resumePath != null)
        {
            foreach (int code in resumePath)
            {
                int r = code / 100, col = code % 100;
                MapNode node = r >= 0 && r < rows.Length ? rows[r].Find(n => n.col == col) : null;
                bool connected = node != null && (current == null ? r == 0 : current.next.Contains(node));
                if (!connected) break;
                node.visited = true;
                current = node;
                walked.Add(code);
            }
        }

        while (true)
        {
            // 여기가 이어하기 지점: 지금까지 마친 칸과 그 결과(덱 · 체력 · 부품)를 저장한다
            if (GameManager.Instance != null) GameManager.Instance.SaveRouteCheckpoint(mapSeed, walked);

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
            autoAction = () => chosen = PreferFight
                ? (reachable.Find(r => r.type == NodeType.Fight || r.type == NodeType.Elite) ?? reachable[0])
                : reachable[0];
            while (chosen == null) yield return null;
            foreach (MapNode n in reachable) n.view.GetComponent<Button>().onClick.RemoveAllListeners();

            if (chosen == boss) break;

            chosen.visited = true;
            Sfx.Play(SfxId.MapStep);
            current = chosen;
            walked.Add(chosen.row * 100 + chosen.col);
            RefreshMap();
            yield return new WaitForSecondsRealtime(0.15f);   // 고른 칸에 불이 들어오는 걸 잠깐 보여준다

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
            // 전투 38 · 이벤트 32 · 강적 8 · 휴식 9 · 수거상 7 · 보물 6
            NodeType t = roll < 38 ? NodeType.Fight
                       : roll < 70 ? NodeType.Event
                       : roll < 78 ? NodeType.Elite
                       : roll < 87 ? NodeType.Rest
                       : roll < 94 ? NodeType.Remove
                       : NodeType.Treasure;

            if ((t == NodeType.Elite || t == NodeType.Rest) && row < 2) continue;
            if (t == NodeType.Rest && row == rowCount - 2) continue;   // 바로 위가 휴식이니 연달아 쉬지 않게
            // 전투 · 이벤트는 연달아 나와도 된다 (이벤트는 매번 내용이 다르다)
            if (t != NodeType.Fight && t != NodeType.Event && parents.Exists(p => p.type == t)) continue;
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
        Image startDot = Circle("Start", mapArea, start, 46f, UITheme.InkSolid);
        Ring(startDot.transform, walkedColor);
        Icon(startDot.transform, IconPainter.Start, walkedColor, 0.2f);

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
        Ring(bg.transform, accent);

        n.icon = Icon(bg.transform, TypeIcon(n.type), accent, 0.17f);

        Button b = bg.gameObject.AddComponent<Button>();
        b.targetGraphic = bg;
        HoverColors(b);
        bg.gameObject.AddComponent<RouteNodeHover>().Setup(this, TypeName(n.type), TypeDesc(n.type));
        return bg;
    }

    private Image BossView(string bossName)
    {
        Image bg = Circle("Node Boss", mapArea, boss.pos, 96f, UITheme.Alpha(UITheme.BrassDeep, 1f));
        bg.raycastTarget = true;
        Ring(bg.transform, UITheme.Brass);
        Icon(bg.transform, IconPainter.Boss, UITheme.Brass, 0.2f);

        Text name = UIBuilder.Label("Name", bg.transform, bossName, 22, UITheme.Brass, TextAnchor.MiddleLeft);
        UIBuilder.Place(name.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(320f, 34f));
        name.fontStyle = FontStyle.Bold;

        Button b = bg.gameObject.AddComponent<Button>();
        b.targetGraphic = bg;
        HoverColors(b);
        bool endless = GameManager.Instance != null && GameManager.Instance.IsEndless;
        bg.gameObject.AddComponent<RouteNodeHover>().Setup(this, bossName, endless
            ? "정체를 알 수 없는 보스. 쓰러뜨리면 다음 회차로 넘어가고, 적이 더 강해진다."
            : "다음 챕터 전투. 여기에 들어서면 지도를 떠난다.");
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

    /// <summary>원 칸에 고른 굵기의 테두리를 두른다 (Outline 컴포넌트는 원에서 테가 울퉁불퉁해진다)</summary>
    private static Image Ring(Transform circle, Color color)
    {
        RectTransform rt = UIBuilder.Rect("Ring", circle);
        UIBuilder.Stretch(rt);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = ProceduralSprites.Ring();
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
                if (n.icon != null) n.icon.color = n.visited ? Color.Lerp(accent, Color.white, 0.75f) : accent;
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
    public void ShowHover(string name, string desc, Transform node = null)
    {
        hoverName = name;
        hoverDesc = desc;
        hoveredNode = node;
    }

    public void HideHover(string name, Transform node = null)
    {
        if (hoverName == name) hoverName = null;
        if (node == null || hoveredNode == node) hoveredNode = null;
    }

    private Transform hoveredNode;   // 마우스를 올린 칸 (갈 수 있는 칸이면 또렷하게 커진다)

    private void SetMapMode(bool map)
    {
        Clear();
        mapLayer.gameObject.SetActive(map);
        foreach (Component c in new Component[] { titleText, subText, bodyText, choiceRow, buttonRow })
            c.gameObject.SetActive(!map);
        if (map) gridArea.gameObject.SetActive(false);
        hoverName = null;
        hoveredNode = null;
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

    /// <summary>칸에 그릴 문양 (글자 대신 그림 — IconPainter가 코드로 그린다)</summary>
    private static string TypeIcon(NodeType t)
    {
        switch (t)
        {
            case NodeType.Fight:    return IconPainter.Fight;      // 엇갈린 검
            case NodeType.Elite:    return IconPainter.Elite;      // 뿔 달린 해골
            case NodeType.Rest:     return IconPainter.Rest;       // 등불
            case NodeType.Remove:   return IconPainter.Remove;     // 금이 간 톱니
            case NodeType.Treasure: return IconPainter.Treasure;   // 궤짝
            default:                return IconPainter.Event;      // 열쇠 구멍이 난 문
        }
    }

    /// <summary>동그란 칸 안에 문양 한 장 (inset = 가장자리에서 띄우는 비율)</summary>
    private static Image Icon(Transform parent, string kind, Color color, float inset)
    {
        RectTransform rt = UIBuilder.Rect("Icon", parent);
        rt.anchorMin = new Vector2(inset, inset);
        rt.anchorMax = new Vector2(1f - inset, 1f - inset);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = RouteIcons.Get(kind);
        img.color = color;
        img.preserveAspect = true;
        img.raycastTarget = false;
        return img;
    }

    private static string TypeName(NodeType t)
    {
        switch (t)
        {
            case NodeType.Fight:    return "전투";
            case NodeType.Elite:    return "강적";
            case NodeType.Rest:     return "휴식: 꺼지지 않은 가로등";
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
            case NodeType.Rest:     return $"둘 중 하나: 최대 체력의 {Mathf.RoundToInt(RestHealRatio * 100)}% 회복 / 태엽판 1장 버리기.";
            case NodeType.Remove:   return "덱에서 태엽판 1장을 버린다.";
            case NodeType.Treasure: return "부품 3개 중 1개를 얻는다. 이번 판 내내 효과가 이어진다.";
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
            stage.stageName = fightStage.stageName + (elite ? " (강적)" : " (잔당)");
            stage.backdrop = fightStage.backdrop;
            if (elite) stage.enemies.AddRange(fightStage.enemies);
            else stage.enemies.Add(fightStage.enemies[UnityEngine.Random.Range(0, fightStage.enemies.Count)]);
        }

        bool won = false;
        BattleManager.NextBackdrop = PickBackdrop();   // 챕터마다 다른 배경 (방금 본 배경은 피한다)
        GameManager.Instance.StartRouteBattle(stage, () => won = true);
        while (LoadingScreen.IsOpen && !LoadingScreen.Covered) yield return null;   // 로딩 화면이 다 덮은 뒤에 지도를 내린다 (그 사이 지난 전투 무대가 비치지 않게)
        root.gameObject.SetActive(false);
        while (!won) yield return null;   // 지면 GameManager가 패배 화면을 띄운다 (지도는 숨긴 채로 끝)
        root.gameObject.SetActive(true);
        if (temporary) Destroy(stage);

        if (elite) yield return PickNewCard("강적을 쓰러뜨렸다", "쓰러진 적의 품에서 젖지 않은 태엽판이 나온다. 한 장을 챙긴다.");
    }

    /// <summary>이 챕터 지도의 전투 배경 하나 (방금 본 배경은 되도록 피한다). 목록이 비었으면 null = 무대 배경 그대로</summary>
    private BackdropType? PickBackdrop()
    {
        if (backdropPool == null || backdropPool.Count == 0) return null;
        List<BackdropType> pool = new List<BackdropType>(backdropPool);
        if (pool.Count > 1 && lastBackdrop.HasValue) pool.Remove(lastBackdrop.Value);
        BackdropType pick = pool[UnityEngine.Random.Range(0, pool.Count)];
        lastBackdrop = pick;
        return pick;
    }

    /// <summary>휴식: 체력을 회복하거나, 마른 자리에서 태엽판 하나를 손봐서 버린다 (둘 중 하나)</summary>
    private IEnumerator Rest()
    {
        const string title = "꺼지지 않은 가로등";
        while (true)
        {
            Unit p = Player;
            int amount = p != null ? Mathf.CeilToInt(p.MaxHp * RestHealRatio) : 0;
            bool canRemove = Deck.Count > MinDeckSize;
            int choice = 0;

            Clear();
            titleText.text = title;
            subText.text = "휴식: 하나를 고른다";
            bodyText.text = "비를 피할 처마 밑, 가로등 하나가 아직 켜져 있다. 여기만은 물이 닿지 않아 바닥이 말라 있다.";
            AddButton($"태엽을 쉬게 한다  →  <color={UITheme.HexGood}>체력 +{amount}</color>", false, () => choice = 1, true, 560f);
            AddButton(canRemove ? "마른 자리에서 태엽판을 손본다  →  덱에서 1장 버리기"
                                : $"<color={UITheme.HexMuted}>태엽판 손보기 (덱이 {MinDeckSize}장 이하)</color>",
                      false, () => choice = 2, canRemove, 560f);
            autoAction = () => choice = 1;
            VerticalButtons();
            while (choice == 0) yield return null;

            if (choice == 1)
            {
                int healed = p != null ? p.Heal(amount) : 0;
                yield return Message(title, "젖은 외투를 벗어 말리고, 가로등 불빛 아래서 잠시 눈을 붙인다.\n\n" +
                                            $"<color={UITheme.HexGood}>체력 +{healed}</color>");
                yield break;
            }

            int before = Deck.Count;
            yield return Remove(title, "물기를 닦아 내고 태엽판들을 늘어놓는다. 녹이 너무 깊이 슨 한 장을 골라 버린다.");
            if (Deck.Count < before) yield break;   // 버렸으면 끝, 그만뒀으면 다시 고르기로
        }
    }

    private IEnumerator Remove(string title, string body)
    {
        if (Deck.Count <= MinDeckSize)
        {
            yield return Message(title, $"<color={UITheme.HexMuted}>덱이 {MinDeckSize}장 이하라 더는 버릴 수 없다.</color>");
            yield break;
        }

        DeckEntry picked = null;
        yield return PickDeckEntry(title, body,
            null, "이 태엽판을 버린다", "아무것도 버리지 않는다", e => picked = e);
        if (picked == null) yield break;

        Deck.Remove(picked);
        yield return Message(title, $"[{picked.data.cardName}]을(를) 덱에서 뺐다.\n\n<color={UITheme.HexMuted}>덱 {Deck.Count}장</color>");
    }

    private IEnumerator Treasure()
    {
        yield return PickItem("떠내려온 궤짝", "물살에 밀려온 궤짝 안에 쓸 만한 부품이 들어 있다. 하나를 챙긴다.");
    }

    private IEnumerator Event()
    {
        List<RouteEvent> events = Events().FindAll(e => e.available == null || e.available());
        // 이번 지도에서 이미 본 이벤트는 되도록 다시 나오지 않게 (다 봤으면 처음부터)
        List<RouteEvent> fresh = events.FindAll(e => !seenEvents.Contains(e.title));
        if (fresh.Count == 0) { seenEvents.Clear(); fresh = events; }
        RouteEvent ev = fresh[UnityEngine.Random.Range(0, fresh.Count)];
        seenEvents.Add(ev.title);

        EventOption chosen = null;
        Clear();
        titleText.text = ev.title;
        subText.text = "이벤트";
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
            label = $"태엽을 내어 준다  <color={UITheme.HexBad}>체력 -6</color>  →  무작위 태엽판 1장",
            available = () => Player != null && Player.Hp > 6,
            run = () => GainRandomCard(6, "자판기가 덜컹거리더니 태엽판 한 장을 뱉어 낸다."),
        });
        vending.options.Add(new EventOption
        {
            label = $"걷어찬다  →  반반 확률로 <color={UITheme.HexGood}>체력 +10</color> / <color={UITheme.HexBad}>체력 -5</color>",
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
            label = $"손을 넣는다  <color={UITheme.HexBad}>체력 -4</color>  →  덱의 태엽판 1장 복제",
            available = () => Player != null && Player.Hp > 4,
            run = DuplicateCard,
        });
        mirror.options.Add(new EventOption
        {
            label = $"비친 얼굴을 보며 숨을 고른다  →  <color={UITheme.HexGood}>체력 +8</color>",
            run = () => HealBy(8, "잔물결이 가라앉자 숨도 같이 가라앉는다."),
        });
        list.Add(mirror);

        RouteEvent bench = new RouteEvent
        {
            title = "시계공의 작업대",
            body = "주인 없는 작업대 위에 공구가 가지런하다. 망가진 태엽 하나쯤은 손볼 수 있을 것 같다.",
        };
        bench.options.Add(new EventOption
        {
            label = "❖ 흡수한 태엽판 1장을 정화한다  →  침식이 사라진다",
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
            label = $"기도한다  →  <color={UITheme.HexGood}>체력 +15</color>",
            run = () => HealBy(15, "작은 종이 한 번 울린다. 몸속 태엽이 조금 덜 삐걱거린다."),
        });
        chapel.options.Add(new EventOption
        {
            label = $"헌금함을 비튼다  <color={UITheme.HexBad}>체력 -3</color>  →  태엽판 3장 중 1장",
            available = () => Player != null && Player.Hp > 3,
            run = RobOfferingBox,
        });
        chapel.options.Add(new EventOption { label = "지나간다", run = Nothing });
        list.Add(chapel);

        RouteEvent storehouse = new RouteEvent
        {
            title = "버려진 태엽 창고",
            body = "무너진 선반 아래로 아직 녹슬지 않은 부품 상자가 보인다. 꺼내려면 날카로운 쇠붙이 사이로 손을 넣어야 한다.",
            available = () => UnownedItems().Count > 0,
        };
        storehouse.options.Add(new EventOption
        {
            label = $"손을 넣는다  <color={UITheme.HexBad}>체력 -8</color>  →  부품 3개 중 1개",
            available = () => Player != null && Player.Hp > 8,
            run = RaidStorehouse,
        });
        storehouse.options.Add(new EventOption { label = "지나간다", run = Nothing });
        list.Add(storehouse);

        RouteEvent oilSlick = new RouteEvent
        {
            title = "물 위의 기름띠",
            body = "부서진 기름통에서 새어 나온 기름이 수면 위에 무지개처럼 번져 있다. 물에 섞이지 않고 위에만 떠 있다.",
        };
        oilSlick.options.Add(new EventOption
        {
            label = "❖ 흡수한 태엽판에 기름을 먹인다  →  1장 정화",
            available = () => Deck.Exists(e => e.absorbed),
            run = Purify,
        });
        oilSlick.options.Add(new EventOption
        {
            label = $"관절마다 기름을 바른다  →  <color={UITheme.HexGood}>체력 +10</color>",
            run = () => HealBy(10, "기름이 물기를 밀어낸다. 녹이 슬려던 관절이 다시 부드럽게 돈다."),
        });
        oilSlick.options.Add(new EventOption { label = "지나간다", run = Nothing });
        list.Add(oilSlick);

        RouteEvent rustPool = new RouteEvent
        {
            title = "녹물 웅덩이",
            body = "붉게 물든 웅덩이 바닥에 녹슨 태엽판들이 가라앉아 있다. 손을 넣으면 살갗까지 녹물이 밴다.",
        };
        rustPool.options.Add(new EventOption
        {
            label = $"녹슨 태엽판을 건진다  <color={UITheme.HexBad}>체력 -5</color>  →  녹 태엽판 1장",
            available = () => Player != null && Player.Hp > 5 && RustCards().Count > 0,
            run = FishRustCard,
        });
        rustPool.options.Add(new EventOption
        {
            label = $"빗물로 상처를 씻어 낸다  →  <color={UITheme.HexGood}>체력 +6</color>",
            run = () => HealBy(6, "맑은 빗물로 녹물을 씻어 낸다. 따끔하지만 상처가 덜 번진다."),
        });
        rustPool.options.Add(new EventOption { label = "지나간다", run = Nothing });
        list.Add(rustPool);

        RouteEvent watch = new RouteEvent
        {
            title = "멈춘 회중시계",
            body = "물이 빠진 계단참에 회중시계 하나가 멈춰 있다. 태엽을 감으면 다시 갈 것 같지만, 태엽 끝이 날카롭게 벼려져 있다.",
        };
        watch.options.Add(new EventOption
        {
            label = $"태엽을 끝까지 감는다  <color={UITheme.HexBad}>체력 -7</color>  →  <color={UITheme.HexGood}>최대 체력 +6</color>",
            available = () => Player != null && Player.Hp > 7,
            run = WindWatch,
        });
        watch.options.Add(new EventOption { label = "주머니에 넣지 않고 지나간다", run = Nothing });
        list.Add(watch);

        RouteEvent tinker = new RouteEvent
        {
            title = "떠돌이 수리공",
            body = "삐걱거리는 손수레를 끄는 수리공이 길을 막는다. \"고칠 게 있으면 말하라고. 값은 기름 대신 피 몇 방울이면 돼.\"",
        };
        tinker.options.Add(new EventOption
        {
            label = $"부품을 맞춘다  <color={UITheme.HexBad}>체력 -6</color>  →  부품 3개 중 1개",
            available = () => Player != null && Player.Hp > 6 && UnownedItems().Count > 0,
            run = TinkerItem,
        });
        tinker.options.Add(new EventOption
        {
            label = "태엽판 1장을 맡긴다  →  덱에서 삭제",
            available = () => Deck.Count > MinDeckSize,
            run = () => Remove("떠돌이 수리공", "수리공에게 넘길 태엽판을 고른다. \"이건 내가 쓸모 있게 써 주지.\""),
        });
        tinker.options.Add(new EventOption { label = "고개를 젓는다", run = Nothing });
        list.Add(tinker);

        RouteEvent bell = new RouteEvent
        {
            title = "가라앉은 종",
            body = "무너진 종탑의 종이 물속에 반쯤 잠겨 물살에 낮게 웅웅거린다. 녹슨 망치가 종 옆에 걸려 있다.",
        };
        bell.options.Add(new EventOption
        {
            label = $"종을 친다  →  반반 확률로 태엽판 3장 중 1장 / <color={UITheme.HexBad}>체력 -8</color>",
            available = () => Player != null && Player.Hp > 8,
            run = RingBell,
        });
        bell.options.Add(new EventOption
        {
            label = $"울림에 귀를 기울인다  →  <color={UITheme.HexGood}>체력 +7</color>",
            run = () => HealBy(7, "낮은 울림이 물결을 따라 번진다. 어지럽던 태엽 소리가 그 박자에 맞춰 차분해진다."),
        });
        list.Add(bell);

        RouteEvent puppet = new RouteEvent
        {
            title = "태엽 인형극",
            body = "무너진 광장에서 태엽 인형들이 관객도 없이 극을 이어 간다. 인형 하나가 무대에서 내려와 손을 내민다. 맞바꾸자는 몸짓이다.",
        };
        puppet.options.Add(new EventOption
        {
            label = "태엽판 1장을 내준다  →  태엽판 3장 중 1장",
            available = () => Deck.Count > MinDeckSize,
            run = PuppetTrade,
        });
        puppet.options.Add(new EventOption
        {
            label = $"끝까지 관람한다  →  <color={UITheme.HexGood}>체력 +5</color>",
            run = () => HealBy(5, "막이 내리자 인형들이 일제히 고개를 숙인다. 박수 칠 사람은 나 하나뿐이다."),
        });
        list.Add(puppet);

        RouteEvent diary = new RouteEvent
        {
            title = "떠내려온 일기장",
            body = "물에 불은 일기장이 발치에 걸린다. 마지막 장에는 '물이 차오르던 밤, 종지기는 종을 멈추지 않았다'고 적혀 있다.",
        };
        diary.options.Add(new EventOption
        {
            label = $"끝까지 읽는다  →  <color={UITheme.HexGood}>체력 +6</color>",
            run = () => HealBy(6, "누군가도 이 물속을 버텼다. 그 사실 하나가 이상하게 힘이 된다."),
        });
        diary.options.Add(new EventOption
        {
            label = $"마른 장을 뜯어 관절을 감싼다  →  <color={UITheme.HexGood}>최대 체력 +3</color>",
            run = () => GainMaxHp(3, "종이를 겹겹이 감아 물이 스미던 관절을 막는다. 미안한 마음에 일기장을 품에 넣는다."),
        });
        list.Add(diary);

        RouteEvent scale = new RouteEvent
        {
            title = "기울어진 저울",
            body = "광장 가운데 커다란 저울이 한쪽으로 기울어 있다. 빈 접시에는 '올린 무게만큼 돌려준다'고 새겨져 있다.",
            available = () => UnownedItems().Count > 0,
        };
        scale.options.Add(new EventOption
        {
            label = $"몸의 태엽을 올린다  <color={UITheme.HexBad}>최대 체력 -5</color>  →  부품 3개 중 1개",
            available = () => Player != null && Player.MaxHp > 25,
            run = ScaleTrade,
        });
        scale.options.Add(new EventOption { label = "아무것도 올리지 않는다", run = Nothing });
        list.Add(scale);

        RouteEvent shadow = new RouteEvent
        {
            title = "웅덩이 속 그림자",
            body = "웅덩이에 비친 내 그림자가 혼자 일어선다. 내 태엽판을 그대로 흉내 내며 칼끝을 겨눈다.",
        };
        shadow.options.Add(new EventOption
        {
            label = $"맞선다  →  <color={UITheme.HexEnemy}>강적 전투</color>  (이기면 태엽판 3장 중 1장)",
            run = () => Fight(true),
        });
        shadow.options.Add(new EventOption
        {
            label = $"물을 흐려 그림자를 지운다  →  <color={UITheme.HexBad}>체력 -4</color>",
            run = BlurShadow,
        });
        list.Add(shadow);

        RouteEvent cistern = new RouteEvent
        {
            title = "빗물 저수조",
            body = "지붕 위 저수조에 맑은 빗물이 고여 있다. 녹물이 섞이지 않은 물은 이 마을에서 드물다.",
        };
        int drink = Player != null ? Mathf.Max(1, Player.MaxHp / 4) : 10;
        cistern.options.Add(new EventOption
        {
            label = $"실컷 마신다  →  <color={UITheme.HexGood}>체력 +{drink}</color>",
            run = () => HealBy(drink, "차갑고 맑은 물이 목을 타고 내려간다. 녹 맛이 나지 않는 물이 얼마 만인지."),
        });
        cistern.options.Add(new EventOption
        {
            label = "❖ 흡수한 태엽판을 씻는다  →  1장 정화",
            available = () => Deck.Exists(e => e.absorbed),
            run = Purify,
        });
        list.Add(cistern);

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
        yield return Message(titleText.text, $"{text}\n\n<color={UITheme.HexGood}>체력 +{healed}</color>");
    }

    private IEnumerator GainRandomCard(int hpCost, string text)
    {
        LoseHp(hpCost);
        List<CardData> pool = GameManager.Instance.RouteCardPool();
        if (pool.Count == 0) { yield return Message(titleText.text, text); yield break; }

        CardData card = pool[UnityEngine.Random.Range(0, pool.Count)];
        GameManager.Instance.AddCard(card);
        yield return Message(titleText.text, $"{text}\n\n<color={UITheme.HexBad}>체력 -{hpCost}</color>   ·   <color={UITheme.HexBrass}>[{card.cardName}] 획득</color>");
    }

    private IEnumerator KickVending()
    {
        if (UnityEngine.Random.value < 0.5f)
        {
            yield return HealBy(10, "쿵! 안에서 따뜻한 기름 한 병이 굴러 나온다. 관절에 바르니 삐걱거림이 멎는다.");
        }
        else
        {
            int lost = LoseHp(5);
            yield return Message(titleText.text, $"쿵! 녹슨 문짝이 튀어나와 정강이를 때린다.\n\n<color={UITheme.HexBad}>체력 -{lost}</color>");
        }
    }

    private IEnumerator DuplicateCard()
    {
        DeckEntry picked = null;
        yield return PickDeckEntry(titleText.text, "물속으로 가져갈 태엽판을 고른다. 똑같은 한 장이 따라 올라온다.",
            null, "이 태엽판을 복제한다", null, e => picked = e);

        int lost = LoseHp(4);
        Deck.Add(new DeckEntry(picked.data, picked.absorbed, picked.corruption, picked.sourceName));
        yield return Message("물에 비친 시계", $"[{picked.data.cardName}]이(가) 한 장 더 생겼다.\n\n<color={UITheme.HexBad}>체력 -{lost}</color>   ·   <color={UITheme.HexMuted}>덱 {Deck.Count}장</color>");
    }

    private IEnumerator Purify()
    {
        DeckEntry picked = null;
        yield return PickDeckEntry(titleText.text, "정화할 ❖ 태엽판을 고른다. 다시는 침식이 일어나지 않는다.",
            e => e.absorbed, "이 태엽판을 정화한다", null, e => picked = e);

        picked.absorbed = false;
        picked.corruption = 0;
        yield return Message(titleText.text, $"[{picked.data.cardName}]의 ❖ 표식이 지워졌다. 이제 온전히 내 것이다.");   // 여러 이벤트가 같이 쓴다
    }

    /// <summary>보상 풀에서 녹을 다루는 태엽판 (녹 · 부식 효과)</summary>
    private List<CardData> RustCards()
    {
        return GameManager.Instance.RouteCardPool().FindAll(c => c != null && c.diceList != null &&
            c.diceList.Exists(d => d != null && d.effects != null &&
                d.effects.Exists(fx => fx != null && (fx.effect == DiceEffectType.Rust || fx.effect == DiceEffectType.Corrode))));
    }

    private IEnumerator FishRustCard()
    {
        List<CardData> pool = RustCards();
        int lost = LoseHp(5);
        CardData card = pool[UnityEngine.Random.Range(0, pool.Count)];
        GameManager.Instance.AddCard(card);
        yield return Message(titleText.text, "손끝이 붉게 물든다. 웅덩이 바닥에서 녹이 잔뜩 슨 태엽판 한 장을 건져 낸다.\n\n" +
                                             $"<color={UITheme.HexBad}>체력 -{lost}</color>   ·   <color={UITheme.HexBrass}>[{card.cardName}] 획득</color>");
    }

    private IEnumerator RobOfferingBox()
    {
        LoseHp(3);
        yield return PickNewCard("물에 잠긴 예배당", $"자물쇠가 손가락을 긁으며 열린다. <color={UITheme.HexBad}>체력 -3</color>\n안에 젖은 태엽판들이 들어 있다. 한 장을 챙긴다.");
    }

    private IEnumerator GainMaxHp(int amount, string text)
    {
        if (Player != null) Player.ChangeMaxHp(amount);
        yield return Message(titleText.text, $"{text}\n\n<color={UITheme.HexGood}>최대 체력 +{amount}</color>   ·   " +
                                             $"<color={UITheme.HexMuted}>체력 {Player.Hp}/{Player.MaxHp}</color>");
    }

    private IEnumerator WindWatch()
    {
        int lost = LoseHp(7);
        Player.ChangeMaxHp(6);
        yield return Message(titleText.text, "태엽이 손가락 끝을 물어뜯는다. 끝까지 감자 째깍, 하고 초침이 다시 돈다. 그 박자가 가슴속 태엽에 옮아 붙는다.\n\n" +
                                             $"<color={UITheme.HexBad}>체력 -{lost}</color>   ·   <color={UITheme.HexGood}>최대 체력 +6</color>   ·   " +
                                             $"<color={UITheme.HexMuted}>체력 {Player.Hp}/{Player.MaxHp}</color>");
    }

    private IEnumerator TinkerItem()
    {
        int lost = LoseHp(6);
        yield return PickItem(titleText.text, $"수리공이 바늘로 손등을 찔러 기름 대신 피를 받아 간다. <color={UITheme.HexBad}>체력 -{lost}</color>\n손수레에서 부품 몇 개를 꺼내 보인다. 하나를 고른다.");
    }

    private IEnumerator RingBell()
    {
        if (UnityEngine.Random.value < 0.5f)
        {
            yield return PickNewCard(titleText.text, "데엥. 물속에서 울린 종소리에 바닥의 진흙이 들썩이고, 묻혀 있던 태엽판들이 떠오른다. 한 장을 건진다.");
        }
        else
        {
            int lost = LoseHp(8);
            yield return Message(titleText.text, "데엥. 종소리가 물을 타고 되돌아와 온몸의 태엽을 흔든다. 이가 딱딱 부딪친다.\n\n" +
                                                 $"<color={UITheme.HexBad}>체력 -{lost}</color>");
        }
    }

    private IEnumerator PuppetTrade()
    {
        DeckEntry given = null;
        yield return PickDeckEntry(titleText.text, "인형에게 건넬 태엽판을 고른다. 인형은 받은 만큼 돌려준다고 한다.",
            null, "이 태엽판을 내준다", "그만둔다", e => given = e);
        if (given == null) yield break;

        Deck.Remove(given);
        yield return PickNewCard(titleText.text, $"인형이 [{given.data.cardName}]을(를) 품에 안고 꾸벅 인사한다.\n그리고 무대 뒤에서 태엽판 몇 장을 꺼내 펼쳐 보인다.");
    }

    private IEnumerator ScaleTrade()
    {
        Player.ChangeMaxHp(-5);
        yield return PickItem(titleText.text, $"가슴속 태엽 한 가닥을 풀어 접시에 올리자 저울이 반대쪽으로 기운다. <color={UITheme.HexBad}>최대 체력 -5</color>\n반대쪽 접시에서 부품들이 굴러 나온다. 하나를 고른다.");
    }

    private IEnumerator BlurShadow()
    {
        int lost = LoseHp(4);
        yield return Message(titleText.text, "웅덩이를 마구 휘저었다. 그림자는 흩어졌지만, 물속에 숨어 있던 쇠붙이에 손을 베였다.\n\n" +
                                             $"<color={UITheme.HexBad}>체력 -{lost}</color>");
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
                tile.color = UITheme.BrassDeep;
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

    private static List<RunItem> UnownedItems()
    {
        return RunItems.All.FindAll(i => !GameManager.Instance.OwnedItems.Contains(i));
    }

    private static string ItemLine()
    {
        List<RunItem> items = GameManager.Instance.OwnedItems;
        if (items.Count == 0) return "";
        List<string> names = items.ConvertAll(i => i.name);
        return $"\n<size=18><color={UITheme.HexBrass}>부품</color>  {string.Join(" · ", names)}</size>";
    }

    private IEnumerator RaidStorehouse()
    {
        LoseHp(8);
        yield return PickItem("버려진 태엽 창고", $"<color={UITheme.HexBad}>체력 -8</color>\n상자 안의 부품 가운데 하나를 챙긴다.");
    }

    /// <summary>아직 없는 부품 3개 중 1개 고르기 (건너뛸 수 있음). 다 모았으면 태엽판으로 대신한다</summary>
    private IEnumerator PickItem(string title, string body)
    {
        List<RunItem> pool = UnownedItems();
        if (pool.Count == 0)
        {
            yield return PickNewCard(title, "이미 쓸 만한 부품은 다 챙겼다. 대신 태엽판 한 장을 챙긴다.");
            yield break;
        }

        List<RunItem> offer = new List<RunItem>();
        while (offer.Count < 3 && pool.Count > 0)
        {
            int i = UnityEngine.Random.Range(0, pool.Count);
            offer.Add(pool[i]);
            pool.RemoveAt(i);
        }

        RunItem picked = null;
        bool done = false;
        Clear();
        titleText.text = title;
        bodyText.text = body;
        foreach (RunItem it in offer)
        {
            RunItem item = it;
            ItemTile(item, () => { picked = item; done = true; });
        }
        AddButton("챙기지 않는다", false, () => done = true);
        autoAction = () => { picked = offer[0]; done = true; };
        while (!done) yield return null;

        if (picked != null)
        {
            GameManager.Instance.GainItem(picked);
            Sfx.Play(SfxId.Treasure, 1f, 0f);
            yield return Message(title, $"<color={UITheme.HexBrass}>[{picked.name}]</color>을(를) 몸에 달았다.\n\n{picked.description}\n\n" +
                                        $"<color={UITheme.HexMuted}>체력 {Player.Hp}/{Player.MaxHp}</color>   ·   <color={UITheme.HexKi}>최대 기력 {GameManager.Instance.MaxKi}</color>");
        }
    }

    private void ItemTile(RunItem item, Action onClick)
    {
        Image frame = UIBuilder.Panel("Item " + item.name, choiceRow, UITheme.InkSolid, false);
        frame.rectTransform.sizeDelta = new Vector2(300f, 340f);
        frame.raycastTarget = true;
        UITheme.Border(frame, UITheme.Alpha(UITheme.Ki, 0.75f));

        // 부품 그림 (IconPainter가 코드로 그린 문양에 황동색을 입힌다)
        if (!string.IsNullOrEmpty(item.icon))
        {
            RectTransform art = UIBuilder.Rect("Art", frame.transform);
            UIBuilder.Place(art, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(124f, 124f));
            Image artImg = art.gameObject.AddComponent<Image>();
            artImg.sprite = RouteIcons.Get(item.icon, 256);
            artImg.color = UITheme.Brass;
            artImg.preserveAspect = true;
            artImg.raycastTarget = false;
        }
        Button b = frame.gameObject.AddComponent<Button>();
        b.targetGraphic = frame;
        HoverColors(b);
        b.onClick.AddListener(() => onClick());

        Text n = UIBuilder.Label("Name", frame.transform, item.name, 28, UITheme.Brass, TextAnchor.MiddleCenter);
        n.fontStyle = FontStyle.Bold;
        UIBuilder.Place(n.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, -158f), new Vector2(-24f, 44f));
        Text d = UIBuilder.Label("Desc", frame.transform, item.description, 23, UITheme.TextMain, TextAnchor.UpperCenter);
        d.lineSpacing = 1.25f;
        UIBuilder.Place(d.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, -212f), new Vector2(-30f, 120f));
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
            yield return Message(title, $"<color={UITheme.HexBrass}>[{picked.cardName}]</color>을(를) 덱에 넣었다.\n\n<color={UITheme.HexMuted}>덱 {Deck.Count}장</color>");
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
            ? $"{p.unitName}   <color={UITheme.HexGood}>체력 {p.Hp} / {p.MaxHp}</color>   ·   <color={UITheme.HexKi}>최대 기력 {GameManager.Instance.MaxKi}</color>   ·   태엽판 {deck}장" + ItemLine()
            : $"태엽판 {deck}장";

        if (!mapLayer.gameObject.activeSelf) return;
        mapSub.text = statusTitle;
        hoverText.text = hoverName != null
            ? $"<b><color={UITheme.HexBrass}>{hoverName}</color></b>   {hoverDesc}"
            : $"<color={UITheme.HexDim}>밝게 빛나는 칸으로만 갈 수 있다 · 칸에 마우스를 올리면 설명</color>";

        // 지금 갈 수 있는 칸은 은은하게 숨 쉬고, 마우스를 올린 칸은 또렷하게 커진다
        float pulse = 1f + 0.05f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f));
        float k = 1f - Mathf.Exp(-22f * Time.unscaledDeltaTime);
        foreach (MapNode n in reachable)
        {
            if (n.view == null) continue;
            float target = n.view.transform == hoveredNode ? 1.2f : pulse;
            float s = Mathf.Lerp(n.view.rectTransform.localScale.x, target, k);
            n.view.rectTransform.localScale = new Vector3(s, s, 1f);
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
        Color rarity = DiceInfo.RarityColor(card.rarity);
        UITheme.Border(frame, UITheme.Alpha(rarity, 0.8f));
        Button b = frame.gameObject.AddComponent<Button>();
        b.targetGraphic = frame;
        HoverColors(b);
        b.onClick.AddListener(() => onClick());

        string rarityHex = "#" + ColorUtility.ToHtmlStringRGB(rarity);
        string text =
            $"<size=26><b>{card.cardName}</b></size>\n" +
            $"<color={rarityHex}>{DiceInfo.RarityName(card.rarity)}</color> · <color={UITheme.HexKi}>기력 {card.cost}</color> · <color={UITheme.HexGold}>{DiceInfo.LengthLabel(card)}</color>\n\n" +
            DiceInfo.Detail(card);
        Text label = UIBuilder.Label("Text", frame.transform, text, 19, UITheme.TextMain, TextAnchor.UpperLeft);
        UIBuilder.Stretch(label.rectTransform, 16f);
        label.rectTransform.offsetMax = new Vector2(-16f, -106f);   // 위쪽은 그림 칸
        VictoryScreen.AddCardArt(frame.rectTransform, card);
        frame.gameObject.AddComponent<CardHover>().card = card;
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

        Color stripe = e.absorbed ? UITheme.Absorb : DiceInfo.RarityColor(e.data.rarity);
        Image band = UIBuilder.Panel("Stripe", tile.transform, stripe, false);
        band.raycastTarget = false;
        UIBuilder.Place(band.rectTransform, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(5f, -14f));

        string name = e.absorbed ? $"<color={UITheme.HexAbsorb}>❖ {e.data.cardName}</color>  <size=14><color={UITheme.HexAbsorb}>침식 {e.corruption}</color></size>" : e.data.cardName;
        Text t = UIBuilder.Label("Text", tile.transform,
            $"<b>{name}</b>\n<size=15><color={UITheme.HexKi}>기력 {e.data.cost}</color>  {DiceInfo.Summary(e.data)}</size>",
            19, UITheme.TextMain, TextAnchor.MiddleLeft);
        UIBuilder.Stretch(t.rectTransform);
        t.rectTransform.offsetMin = new Vector2(20f, 6f);
        t.rectTransform.offsetMax = new Vector2(-10f, -6f);
        t.lineSpacing = 1.15f;
        CardHover hover = tile.gameObject.AddComponent<CardHover>();
        hover.card = e.data;
        hover.entry = e;
        hover.hoverScale = 1.03f;
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

        Image dim = UIBuilder.Panel("Dim", go.transform, UITheme.Alpha(UITheme.InkDeep, 0.93f), false);
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
        hit.color = Color.clear;   // 빈 곳에서도 휠 스크롤이 먹도록
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

        // 지도 ↔ 사건 화면이 바뀔 때 툭 튀지 않고 빠르게 스며 나온다
        UIPop.Ensure(screen.mapLayer.gameObject, 1f, 0.15f);
        foreach (Component c in new Component[] { screen.titleText, screen.subText, screen.bodyText, screen.buttonRow })
            UIPop.Ensure(c.gameObject, 1f, 0.15f);
        UIPop.Ensure(screen.choiceRow.gameObject, 0.97f, 0.15f);

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
        UIBuilder.Place(screen.mapSub.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(62f, -96f), new Vector2(820f, 32f));
        screen.mapSub.supportRichText = true;

        // 지도판 (위아래로 긴 먹색 판, 테두리 황동)
        Image board = UIBuilder.Panel("Board", screen.mapLayer, UITheme.Alpha(UITheme.InkSolid, 0.94f));
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
            Image dot = Circle("Key", legend.rectTransform, pos, 42f, UITheme.InkSolid);
            Ring(dot.transform, TypeColor(order[i]));
            Icon(dot.transform, TypeIcon(order[i]), TypeColor(order[i]), 0.17f);
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

    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e) { if (screen != null) screen.ShowHover(title, desc, transform); }

    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) { if (screen != null) screen.HideHover(title, transform); }
}
