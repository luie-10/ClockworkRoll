using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 덱 보기 — 전투 화면 왼쪽 아래 [덱 보기] 버튼 (또는 Tab 키)으로 연다.
///
///   ┌ 보유 덱 14장 ──────────────────────────────────────────────────┐
///   │ 뽑을 더미 (6)     손패 (5)         시계판 위 (1)    버림패 (2)   │
///   │ ┌────────────┐  ┌────────────┐                                 │
///   │ ▌가드 ×2  기력1│  ▌관통   기력1  │   ← 태엽판 하나 = 상자 하나       │
///   │ │ 가3~7       │  │ 관1~6        │      (왼쪽 띠 = 등급 색)          │
///   │ └────────────┘  └────────────┘                                 │
///   └────────────────────────────────────────── 닫기 (Tab / Esc) ───┘
///
/// 뽑을 더미는 순서를 감추려고 이름순으로 묶어서 보여준다. ◈ = 흡수한 태엽판 (침식 수치 함께 표시).
/// 칸마다 마우스 휠로 스크롤된다.
/// 직접 붙일 필요 없음 — BattleManager가 전투 시작 때 DeckViewer.Ensure()로 전투 HUD 안에 버튼을 만든다.
/// </summary>
public class DeckViewer : MonoBehaviour
{
    private const float EntryHeight = 70f;

    private static DeckViewer instance;

    private GameObject panelRoot;
    private Text titleText;
    private readonly Column[] columns = new Column[4];

    private class Column
    {
        public Text title;
        public RectTransform content;   // 상자들이 위에서부터 쌓이는 곳
        public string signature;        // 내용이 바뀌었을 때만 상자를 다시 만든다
    }

    public static bool IsOpen { get { return instance != null && instance.panelRoot.activeSelf; } }

    /// <summary>버튼(전투 HUD 안)과 창(전용 Canvas)을 없으면 만든다.</summary>
    public static void Ensure()
    {
        if (instance != null) return;

        // 창: 모든 전투 UI 위에 뜨는 전용 Canvas
        GameObject canvasGO = new GameObject("DeckViewerCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 550;
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        instance = canvasGO.AddComponent<DeckViewer>();
        instance.Build(canvasGO.transform);

        // 버튼: 전투 HUD 안 (스토리 중엔 같이 숨겨짐). HUD가 없으면 창 Canvas에 둔다
        BattleHUD hud = FindAnyObjectByType<BattleHUD>();
        Transform buttonParent = hud != null ? hud.transform : canvasGO.transform;
        Button open = UIBuilder.Button("DeckButton", buttonParent, "덱 보기 (Tab)", new Color(0.22f, 0.3f, 0.4f), 26);
        UIBuilder.Place((RectTransform)open.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(30f, 305f), new Vector2(230f, 70f));
        open.onClick.AddListener(Toggle);
    }

    public static void Toggle()
    {
        if (instance == null) return;
        if (IsOpen) instance.panelRoot.SetActive(false);
        else instance.Open();
    }

    void Update()
    {
        bool inBattle = GameManager.Instance == null || GameManager.Instance.State == GameManager.GameState.Battle;
        if (!inBattle)
        {
            if (panelRoot.activeSelf) panelRoot.SetActive(false);
            return;
        }

        if (Input.GetKeyDown(KeyCode.Tab)) Toggle();
        else if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) panelRoot.SetActive(false);

        if (IsOpen) Refresh();   // 열어 둔 채로 카드를 뽑거나 써도 바로 반영
    }

    private void Open()
    {
        foreach (Column c in columns) c.signature = null;   // 열 때마다 새로 그린다
        panelRoot.SetActive(true);
        Refresh();
    }

    private void Refresh()
    {
        BattleManager bm = BattleManager.Instance;
        HandManager hm = bm != null ? bm.handManager : null;
        if (hm == null) return;

        List<DeckEntry> hand = new List<DeckEntry>();
        foreach (CardInstance c in hm.Hand) hand.Add(c.entry);
        List<DeckEntry> onClock = new List<DeckEntry>();
        foreach (TimedAction a in bm.PlayerActions) onClock.Add(a.card.entry);

        int owned = GameManager.Instance != null && GameManager.Instance.OwnedDeck != null ? GameManager.Instance.OwnedDeck.Count : 0;
        titleText.text = $"보유 덱 {owned}장   <size=20><color=#a8a8a8>◈ = 흡수한 태엽판 · 뽑을 더미는 순서를 감추고 이름순</color></size>";

        Fill(columns[0], "뽑을 더미", hm.DrawPile);
        Fill(columns[1], "손패", hand);
        Fill(columns[2], "시계판 위", onClock);
        Fill(columns[3], "버림패", hm.DiscardPile);
    }

    /// <summary>같은 태엽판은 "이름 ×장수" 상자 하나로 묶어 이름순으로 쌓는다.</summary>
    private static void Fill(Column column, string label, IReadOnlyList<DeckEntry> entries)
    {
        column.title.text = $"{label} <color=#e8c27a>({entries.Count})</color>";

        SortedDictionary<string, int> counts = new SortedDictionary<string, int>();
        Dictionary<string, DeckEntry> sample = new Dictionary<string, DeckEntry>();
        foreach (DeckEntry e in entries)
        {
            if (e == null || e.data == null) continue;
            string key = (e.absorbed ? "◈ " : "") + e.data.cardName;
            counts.TryGetValue(key, out int n);
            counts[key] = n + 1;
            sample[key] = e;
        }

        // 바뀐 게 없으면 그대로 (매 프레임 상자를 다시 만들지 않게)
        StringBuilder sig = new StringBuilder();
        foreach (KeyValuePair<string, int> kv in counts) sig.Append(kv.Key).Append(kv.Value).Append(sample[kv.Key].corruption).Append('|');
        if (sig.ToString() == column.signature) return;
        column.signature = sig.ToString();

        for (int i = column.content.childCount - 1; i >= 0; i--) Destroy(column.content.GetChild(i).gameObject);

        if (counts.Count == 0)
        {
            Text empty = UIBuilder.Label("Empty", column.content, "(없음)", 20, new Color(0.5f, 0.5f, 0.5f), TextAnchor.MiddleCenter, false);
            empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
            return;
        }

        foreach (KeyValuePair<string, int> kv in counts) BuildEntry(column.content, kv.Key, kv.Value, sample[kv.Key]);
    }

    /// <summary>태엽판 상자 하나: 왼쪽 등급 색 띠 + 1줄 이름·장수·기력(·침식) + 2줄 주사위</summary>
    private static void BuildEntry(RectTransform parent, string name, int count, DeckEntry e)
    {
        Color stripe = e.absorbed ? new Color(0.85f, 0.6f, 1f) : DiceInfo.RarityColor(e.data.rarity);

        Image box = UIBuilder.Panel("Entry " + name, parent, new Color(0.11f, 0.1f, 0.12f, 0.98f), false);
        box.raycastTarget = false;
        box.gameObject.AddComponent<LayoutElement>().preferredHeight = EntryHeight;

        Image band = UIBuilder.Panel("Stripe", box.transform, stripe, false);
        band.raycastTarget = false;
        band.sprite = null;   // 가는 띠는 둥근 상자 대신 단색 사각형
        band.type = Image.Type.Simple;
        UIBuilder.Place(band.rectTransform, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(6f, -10f));

        string nameHex = "#" + ColorUtility.ToHtmlStringRGB(stripe);
        string line1 = $"<color={nameHex}><b>{name}</b></color>" + (count > 1 ? $" <color=#ffffff>×{count}</color>" : "") +
                       $"   <color=#8cd9ff>기력 {e.data.cost}</color>" +
                       (e.absorbed ? $"  <color=#d9a6ff>침식 {e.corruption}</color>" : "");

        Text top = UIBuilder.Label("Name", box.transform, line1, 21, Color.white, TextAnchor.MiddleLeft, false);
        UIBuilder.Place(top.rectTransform, new Vector2(0f, 0.5f), Vector2.one, new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(-24f, 0f));
        top.horizontalOverflow = HorizontalWrapMode.Overflow;

        Text dice = UIBuilder.Label("Dice", box.transform, DiceInfo.Summary(e.data), 19, Color.white, TextAnchor.MiddleLeft, false);
        UIBuilder.Place(dice.rectTransform, Vector2.zero, new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(-24f, 0f));
        dice.horizontalOverflow = HorizontalWrapMode.Overflow;
    }

    // ─────────────────────────── 생성 ───────────────────────────

    private void Build(Transform canvas)
    {
        Image dim = UIBuilder.Panel("Dim", canvas, new Color(0f, 0f, 0f, 0.6f), false);
        UIBuilder.Stretch(dim.rectTransform);
        dim.raycastTarget = true;   // 창이 열려 있는 동안 뒤 클릭 막기
        Button dimClose = dim.gameObject.AddComponent<Button>();
        dimClose.transition = Selectable.Transition.None;
        dimClose.onClick.AddListener(() => panelRoot.SetActive(false));   // 바깥 클릭 = 닫기
        panelRoot = dim.gameObject;

        Image window = UIBuilder.Panel("Window", dim.transform, new Color(0.05f, 0.05f, 0.06f, 0.97f));
        UIBuilder.Place(window.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1640f, 860f));
        window.raycastTarget = true;
        window.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;   // 창 안 클릭이 바깥(닫기)으로 번지지 않게 여기서 받는다

        titleText = UIBuilder.Label("Title", window.transform, "", 34, new Color(1f, 0.84f, 0.55f), TextAnchor.MiddleLeft);
        UIBuilder.Place(titleText.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(-60f, 56f));

        // 네 칸: 뽑을 더미 / 손패 / 시계판 위 / 버림패
        for (int i = 0; i < 4; i++)
        {
            float x0 = 0.015f + i * 0.2425f;
            columns[i] = BuildColumn(window.transform, new Vector2(x0, 0.08f), new Vector2(x0 + 0.23f, 0.9f));
        }

        Button close = UIBuilder.Button("Close", window.transform, "닫기 (Tab / Esc)", new Color(0.35f, 0.25f, 0.12f), 24);
        UIBuilder.Place((RectTransform)close.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 14f), new Vector2(260f, 48f));
        close.onClick.AddListener(() => panelRoot.SetActive(false));

        panelRoot.SetActive(false);
    }

    /// <summary>칸 하나: 위쪽 제목 줄 + 그 아래 스크롤되는 상자 목록 (제목과 목록이 겹치지 않게 영역을 나눈다)</summary>
    private static Column BuildColumn(Transform window, Vector2 anchorMin, Vector2 anchorMax)
    {
        Column col = new Column();

        Image frame = UIBuilder.Panel("Column", window, new Color(0.08f, 0.08f, 0.09f, 0.95f));
        UIBuilder.Place(frame.rectTransform, anchorMin, anchorMax, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        // 제목 줄 (위 46px)
        col.title = UIBuilder.Label("Title", frame.transform, "", 24, new Color(1f, 0.9f, 0.7f), TextAnchor.MiddleLeft);
        UIBuilder.Place(col.title.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(-28f, 42f));

        // 목록 영역 (제목 아래 ~ 바닥): 이 밖으로 나간 상자는 잘린다
        RectTransform viewport = UIBuilder.Rect("Viewport", frame.transform);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(10f, 10f);
        viewport.offsetMax = new Vector2(-10f, -50f);
        viewport.gameObject.AddComponent<RectMask2D>();
        Image hit = viewport.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);   // 빈 곳에서도 휠 스크롤이 먹도록

        col.content = UIBuilder.Rect("Content", viewport);
        col.content.anchorMin = new Vector2(0f, 1f);
        col.content.anchorMax = Vector2.one;
        col.content.pivot = new Vector2(0.5f, 1f);
        col.content.anchoredPosition = Vector2.zero;
        col.content.sizeDelta = Vector2.zero;

        VerticalLayoutGroup list = col.content.gameObject.AddComponent<VerticalLayoutGroup>();
        list.spacing = 8f;
        list.childControlWidth = true;
        list.childControlHeight = true;
        list.childForceExpandWidth = true;
        list.childForceExpandHeight = false;
        ContentSizeFitter fit = col.content.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = frame.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = col.content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        return col;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
