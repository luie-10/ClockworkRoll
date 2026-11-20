using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 무한 모드 랭킹 창 — 시작 화면의 [랭킹 보기]로 연다.
///   상위 15명 (순위 · 이름 · 넘긴 회차), 내 순위, 내 이름 바꾸기.
/// 서버(Server/ranking-server.js)가 꺼져 있으면 "서버에 닿지 못했다"만 보인다.
/// </summary>
public class RankingPanel : MonoBehaviour
{
    private static RankingPanel instance;

    public static bool IsOpen { get { return instance != null && instance.gameObject.activeSelf; } }

    private const int Rows = 15;

    private Text status, me;
    private Text[] rankCol = new Text[Rows], nameCol = new Text[Rows], loopCol = new Text[Rows];
    private InputField nameInput;
    private int request;   // 늦게 온 옛 응답은 버린다

    /// <summary>parent(시작 화면 바탕) 위에 창을 띄우고 목록을 새로 받는다</summary>
    public static void Open(Transform parent)
    {
        if (instance == null) instance = Build(parent);
        instance.gameObject.SetActive(true);
        instance.transform.SetAsLastSibling();
        instance.nameInput.text = Ranking.PlayerName;
        instance.Refresh();
    }

    public static void Close()
    {
        if (instance != null) instance.gameObject.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    private void Refresh()
    {
        int id = ++request;
        status.text = "랭킹을 불러오는 중…";
        me.text = "";
        for (int i = 0; i < Rows; i++) rankCol[i].text = nameCol[i].text = loopCol[i].text = "";

        Ranking.FetchTop(Rows, r =>
        {
            if (this == null || id != request) return;
            if (r == null)
            {
                status.text = $"<color={UITheme.HexEnemy}>랭킹 서버에 닿지 못했다</color>\n<size=16>인터넷 연결을 확인하고 잠시 뒤 다시 열어 보자</size>";
                return;
            }
            int n = r.entries != null ? r.entries.Length : 0;
            status.text = n == 0 ? "아직 기록이 없다. 첫 이름을 올려 보자" : $"<color={UITheme.HexMuted}>모두 {r.total}명</color>";
            for (int i = 0; i < Rows; i++)
            {
                if (i >= n) continue;
                Ranking.Entry e = r.entries[i];
                bool mine = r.me != null && r.me.rank == e.rank;
                string color = mine ? UITheme.HexGold : e.rank <= 3 ? UITheme.HexBrass : null;
                rankCol[i].text = Paint($"{e.rank}", color);
                nameCol[i].text = Paint(e.name, color);
                loopCol[i].text = Paint($"{e.loop}회차", color);
            }
            me.text = r.me != null && r.me.rank > 0
                ? $"내 순위  <color={UITheme.HexGold}>{r.me.rank}위</color>  ·  {r.me.loop}회차"
                : $"<color={UITheme.HexMuted}>아직 내 기록이 없다. 무한 모드에서 보스를 넘기면 올라간다</color>";
        });
    }

    private static string Paint(string text, string hex)
    {
        return hex == null ? text : $"<color={hex}>{text}</color>";
    }

    private void SaveName()
    {
        // 이름만 바꾼다 — 서버 기록에도 바로 반영 (회차는 그대로)
        Ranking.Rename(nameInput.text, () => { if (this != null) Refresh(); });
        nameInput.text = Ranking.PlayerName;
    }

    // ─────────────────────────── 생성 ───────────────────────────

    private static RankingPanel Build(Transform parent)
    {
        Vector2 center = new Vector2(0.5f, 0.5f), top = new Vector2(0.5f, 1f);

        Image dim = UIBuilder.Panel("RankingDim", parent, UITheme.Scrim, false);
        dim.sprite = null;
        UIBuilder.Stretch(dim.rectTransform);
        dim.raycastTarget = true;
        RankingPanel panel = dim.gameObject.AddComponent<RankingPanel>();

        Image window = UIBuilder.Panel("Window", dim.transform, UITheme.Window);
        UIBuilder.Place(window.rectTransform, center, center, center, Vector2.zero, new Vector2(720f, 820f));
        UITheme.Elevate(window, 0.7f);
        UIPop.Ensure(window.gameObject, 0.95f, 0.16f);

        Text title = UIBuilder.Label("Title", window.transform, "무한 모드 랭킹", 38, UITheme.Brass, TextAnchor.MiddleCenter);
        title.fontStyle = FontStyle.Bold;
        UIBuilder.Place(title.rectTransform, top, top, top, new Vector2(0f, -26f), new Vector2(600f, 50f));

        panel.status = UIBuilder.Label("Status", window.transform, "", 19, UITheme.TextMain, TextAnchor.MiddleCenter);
        panel.status.supportRichText = true;
        UIBuilder.Place(panel.status.rectTransform, top, top, top, new Vector2(0f, -84f), new Vector2(640f, 52f));

        // 표: 순위 · 이름 · 회차
        float rowH = 34f, y0 = -146f;
        Header(window.transform, "순위", -250f, y0, TextAnchor.MiddleCenter);
        Header(window.transform, "이름", -40f, y0, TextAnchor.MiddleLeft);
        Header(window.transform, "넘긴 회차", 230f, y0, TextAnchor.MiddleCenter);
        Image line = UIBuilder.Panel("Rule", window.transform, UITheme.Alpha(UITheme.Brass, 0.5f), false);
        line.sprite = null;
        UIBuilder.Place(line.rectTransform, top, top, top, new Vector2(0f, y0 - 20f), new Vector2(620f, 2f));
        for (int i = 0; i < Rows; i++)
        {
            float y = y0 - 40f - i * rowH;
            panel.rankCol[i] = Cell(window.transform, -250f, y, 90f, TextAnchor.MiddleCenter);
            panel.nameCol[i] = Cell(window.transform, 40f, y, 360f, TextAnchor.MiddleLeft);
            panel.loopCol[i] = Cell(window.transform, 230f, y, 140f, TextAnchor.MiddleCenter);
        }

        panel.me = UIBuilder.Label("Me", window.transform, "", 20, UITheme.TextMain, TextAnchor.MiddleCenter);
        panel.me.supportRichText = true;
        UIBuilder.Place(panel.me.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 128f), new Vector2(640f, 30f));

        // 내 이름 [입력] [이름 저장] · [새로고침] [닫기]
        Vector2 bottom = new Vector2(0.5f, 0f);
        Text nameLabel = UIBuilder.Label("NameLabel", window.transform, "내 이름", 20, UITheme.TextMuted, TextAnchor.MiddleLeft);
        UIBuilder.Place(nameLabel.rectTransform, bottom, bottom, bottom, new Vector2(-255f, 76f), new Vector2(110f, 44f));
        panel.nameInput = NameField(window.transform, new Vector2(-30f, 76f), new Vector2(300f, 44f));
        Button save = UIBuilder.Button("SaveName", window.transform, "이름 저장", UITheme.InkSolid, 19);
        UITheme.StyleButton(save, false, 19);
        UIBuilder.Place((RectTransform)save.transform, bottom, bottom, bottom, new Vector2(215f, 76f), new Vector2(150f, 44f));
        save.onClick.AddListener(panel.SaveName);

        Button refresh = UIBuilder.Button("Refresh", window.transform, "새로고침", UITheme.InkSolid, 20);
        UITheme.StyleButton(refresh, false, 20);
        UIBuilder.Place((RectTransform)refresh.transform, bottom, bottom, bottom, new Vector2(-100f, 22f), new Vector2(180f, 44f));
        refresh.onClick.AddListener(panel.Refresh);

        Button close = UIBuilder.Button("Close", window.transform, "닫기 (Esc)", UITheme.BrassFill, 20);
        UITheme.StyleButton(close, true, 20);
        UIBuilder.Place((RectTransform)close.transform, bottom, bottom, bottom, new Vector2(100f, 22f), new Vector2(180f, 44f));
        close.onClick.AddListener(Close);

        return panel;
    }

    private static void Header(Transform parent, string text, float x, float y, TextAnchor anchor)
    {
        Vector2 top = new Vector2(0.5f, 1f);
        Text t = UIBuilder.Label("Head " + text, parent, text, 18, UITheme.TextMuted, anchor);
        UIBuilder.Place(t.rectTransform, top, top, top, new Vector2(x, y), new Vector2(anchor == TextAnchor.MiddleLeft ? 360f : 140f, 28f));
    }

    private static Text Cell(Transform parent, float x, float y, float w, TextAnchor anchor)
    {
        Vector2 top = new Vector2(0.5f, 1f);
        Text t = UIBuilder.Label("Cell", parent, "", 21, UITheme.TextMain, anchor);
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        UIBuilder.Place(t.rectTransform, top, top, top, new Vector2(x, y), new Vector2(w, 32f));
        return t;
    }

    /// <summary>한 줄 입력칸 (UIBuilder에 입력칸이 없어 직접 만든다)</summary>
    private static InputField NameField(Transform parent, Vector2 pos, Vector2 size)
    {
        Vector2 bottom = new Vector2(0.5f, 0f);
        Image bg = UIBuilder.Panel("NameField", parent, UITheme.InkSolid);
        UIBuilder.Place(bg.rectTransform, bottom, bottom, bottom, pos, size);
        bg.raycastTarget = true;

        Text text = UIBuilder.Label("Text", bg.transform, "", 21, UITheme.TextMain, TextAnchor.MiddleLeft);
        text.supportRichText = false;
        UIBuilder.Stretch(text.rectTransform, 10f);
        Text placeholder = UIBuilder.Label("Placeholder", bg.transform, "이름 (16자까지)", 21, UITheme.TextDim, TextAnchor.MiddleLeft);
        UIBuilder.Stretch(placeholder.rectTransform, 10f);

        InputField field = bg.gameObject.AddComponent<InputField>();
        field.textComponent = text;
        field.placeholder = placeholder;
        field.characterLimit = 16;
        field.lineType = InputField.LineType.SingleLine;
        field.targetGraphic = bg;
        return field;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
