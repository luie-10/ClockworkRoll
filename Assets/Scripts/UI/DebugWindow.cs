#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 왼쪽 위 디버그 창 (에디터 / 개발 빌드 전용, F1로 열고 닫기).
///   - 유닛: 체력 확인 + 즉사 버튼 (플레이어 사망 → 패배, 적 전부 처치 → 승리 테스트)
///   - 카드: 보유 덱 / 손패 / 뽑을 더미 / 버린 더미를 전부 나열
/// 직접 붙일 필요 없음 — 게임 시작 시 알아서 생긴다.
/// </summary>
public class DebugWindow : MonoBehaviour
{
    private const int WindowId = 0x0DEB;

    private static DebugWindow instance;

    private bool visible = true;
    private bool showDetail;   // 카드마다 주사위 요약까지 보기
    private Rect windowRect = new Rect(10f, 10f, 380f, 520f);
    private Vector2 scroll;
    private readonly bool[] foldout = { true, true, false, false };   // 보유 덱 / 손패 / 뽑을 더미 / 버린 더미

    private GUIStyle labelStyle, headerStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (instance != null) return;
        GameObject go = new GameObject("DebugWindow");
        DontDestroyOnLoad(go);   // 재시작(씬 다시 불러오기) 후에도 유지
        instance = go.AddComponent<DebugWindow>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1)) visible = !visible;
    }

    void OnGUI()
    {
        if (!visible) return;

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 13, wordWrap = true };
            headerStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 14, fontStyle = FontStyle.Bold };
        }

        windowRect = GUI.Window(WindowId, windowRect, DrawWindow, "디버그 (F1)");
    }

    private void DrawWindow(int id)
    {
        scroll = GUILayout.BeginScrollView(scroll);

        DrawUnits();
        GUILayout.Space(8f);
        DrawCards();

        GUILayout.EndScrollView();
        GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));   // 제목줄을 잡고 옮기기
    }

    // ─────────────────────────── 유닛 ───────────────────────────

    private void DrawUnits()
    {
        BattleManager bm = BattleManager.Instance;
        GUILayout.Label("<b>유닛</b>", headerStyle);

        if (bm == null || bm.PlayerUnit == null)
        {
            GUILayout.Label("전투 중이 아님", labelStyle);
            return;
        }

        bool ended = bm.Phase == BattleManager.BattlePhase.End;
        GUI.enabled = !ended;

        UnitRow(bm, bm.PlayerUnit, "플레이어 죽기");
        foreach (Unit e in bm.Enemies) UnitRow(bm, e, "처치");

        if (GUILayout.Button("적 전부 처치 (승리 테스트)"))
        {
            // 목록 복사: 처치 도중 승리 처리로 적 목록이 바뀌어도 안전하게
            foreach (Unit e in new List<Unit>(bm.Enemies)) bm.DebugKill(e);
        }

        GUI.enabled = true;
        if (ended) GUILayout.Label("<color=#aaaaaa>전투 종료됨</color>", labelStyle);
    }

    private void UnitRow(BattleManager bm, Unit u, string killLabel)
    {
        if (u == null) return;

        GUILayout.BeginHorizontal();
        string hp = u.IsAlive ? $"{u.Hp}/{u.MaxHp}" : "<color=#ff6666>사망</color>";
        GUILayout.Label($"{u.unitName}  {hp}", labelStyle);
        GUI.enabled = u.IsAlive && bm.Phase != BattleManager.BattlePhase.End;
        if (GUILayout.Button(killLabel, GUILayout.Width(110f))) bm.DebugKill(u);
        GUI.enabled = bm.Phase != BattleManager.BattlePhase.End;
        GUILayout.EndHorizontal();
    }

    // ─────────────────────────── 카드 ───────────────────────────

    private void DrawCards()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("<b>카드</b>", headerStyle);
        showDetail = GUILayout.Toggle(showDetail, "주사위 보기");
        GUILayout.EndHorizontal();

        GameManager gm = GameManager.Instance;
        if (gm != null && gm.OwnedDeck != null) Section(0, "보유 덱", gm.OwnedDeck);

        HandManager hand = BattleManager.Instance != null ? BattleManager.Instance.handManager : null;
        if (hand == null) return;

        List<DeckEntry> handEntries = new List<DeckEntry>();
        foreach (CardInstance c in hand.Hand) handEntries.Add(c.entry != null ? c.entry : new DeckEntry(c.data, false, 0));

        Section(1, "손패", handEntries);
        Section(2, "뽑을 더미", hand.DrawPile);
        Section(3, "버린 더미", hand.DiscardPile);
    }

    private void Section(int index, string title, IReadOnlyList<DeckEntry> cards)
    {
        foldout[index] = GUILayout.Toggle(foldout[index], $"{(foldout[index] ? "▼" : "▶")} {title} ({cards.Count}장)", headerStyle);
        if (!foldout[index]) return;

        if (cards.Count == 0)
        {
            GUILayout.Label("   (없음)", labelStyle);
            return;
        }

        // 같은 카드는 한 줄로 묶어서 ×N
        List<string> order = new List<string>();
        Dictionary<string, int> counts = new Dictionary<string, int>();
        Dictionary<string, DeckEntry> sample = new Dictionary<string, DeckEntry>();
        foreach (DeckEntry e in cards)
        {
            if (e == null || e.data == null) continue;
            string key = e.data.cardName + (e.absorbed ? "◈" + e.corruption : "");
            if (!counts.ContainsKey(key)) { order.Add(key); counts[key] = 0; sample[key] = e; }
            counts[key]++;
        }

        StringBuilder sb = new StringBuilder();
        foreach (string key in order)
        {
            DeckEntry e = sample[key];
            sb.Length = 0;
            sb.Append("   ").Append(e.data.cardName);
            if (e.absorbed) sb.Append($" <color=#d9a6ff>◈ 침식{e.corruption}</color>");
            if (counts[key] > 1) sb.Append($" ×{counts[key]}");
            sb.Append($"  <color=#8cd9ff>기력{e.data.cost}</color>");
            if (showDetail) sb.Append("  ").Append(DiceInfo.Summary(e.data));
            GUILayout.Label(sb.ToString(), labelStyle);
        }
    }
}
#endif
