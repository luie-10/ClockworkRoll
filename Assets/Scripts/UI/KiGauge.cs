using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 기력 게이지 — 숫자("기력 2/3") 대신 구슬을 늘어놓는다. 기력 패널(kiText의 부모) 안에 들어간다.
///   ● 채워진 구슬 = 지금 쓸 수 있는 기력
///   ○ 빈 구슬     = 이번 라운드에 쓴 기력 (최대치까지의 빈자리)
///   ◌ 흐린 구슬   = 다음 라운드에 더 받을 기력 ([기력 회복])
/// 태엽판을 골라 둔 동안에는 그 태엽판이 쓸 구슬이 깜빡인다. 구슬이 차거나 빌 때 한 번 튄다.
/// BattleManager가 전투 시작 때 Ensure로 만든다 (씬에 따로 둘 필요 없음).
/// </summary>
public class KiGauge : MonoBehaviour
{
    private static KiGauge instance;

    private RectTransform row;
    private readonly List<Pip> pips = new List<Pip>();

    private const float MaxPip = 30f, Gap = 6f;
    private static readonly Color kiColor = UITheme.Ki;
    private static readonly Color ringIdle = UITheme.Alpha(UITheme.Ki, 0.4f);
    private static readonly Color hollow = UITheme.Alpha(UITheme.Backdrop, 0.9f);

    private enum State { Full, Empty, Ghost }

    private class Pip
    {
        public RectTransform rt;
        public Image ring, hole, fill, glint;
        public State state = State.Empty;
        public float pop;
    }

    public static void Ensure(Text kiText)
    {
        if (instance != null || kiText == null) return;
        Transform panel = kiText.transform.parent != null ? kiText.transform.parent : kiText.transform;

        RectTransform root = UIBuilder.Rect("KiGauge", panel);
        UIBuilder.Stretch(root, 6f);

        instance = root.gameObject.AddComponent<KiGauge>();
        instance.row = UIBuilder.Rect("Pips", root);
        instance.row.anchorMin = Vector2.zero;
        instance.row.anchorMax = Vector2.one;
        instance.row.offsetMin = new Vector2(6f, 0f);
        instance.row.offsetMax = new Vector2(-6f, 0f);

        kiText.enabled = false;   // 숫자는 감춘다 (BattleManager는 계속 써 넣지만 보이지 않는다)

        // 글자 이름 대신: 패널에 마우스를 올리면 설명이 뜬다
        HoverHint.Attach(panel.gameObject, Describe);
    }

    /// <summary>마우스를 올렸을 때의 설명 (지금 숫자도 함께)</summary>
    private static string Describe()
    {
        BattleManager bm = BattleManager.Instance;
        string now = bm != null ? $"  <color={UITheme.HexKi}>{bm.CurrentKi} / {bm.MaxKi}</color>" +
                                  (bm.PendingKi > 0 ? $"  <color={UITheme.HexMuted}>(다음 라운드 +{bm.PendingKi})</color>" : "") : "";
        return $"<b><color={UITheme.HexKi}>기력</color></b>{now}\n" +
               $"<color={UITheme.HexMuted}>태엽판을 낼 때 쓴다. 라운드가 시작될 때마다 최대치까지 다시 차고, 남은 기력은 넘어가지 않는다.</color>\n" +
               $"<color={UITheme.HexKi}>●</color> 쓸 수 있음   <color={UITheme.HexMuted}>○</color> 이번 라운드에 씀   <color={UITheme.HexKi}>흐린 구슬</color> 다음 라운드에 더 받음\n" +
               $"<color={UITheme.HexMuted}>태엽판을 골라 두면 쓰게 될 구슬이 깜빡인다.</color>";
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Update()
    {
        BattleManager bm = BattleManager.Instance;
        if (bm == null || row == null) return;

        int cur = Mathf.Max(0, bm.CurrentKi), max = Mathf.Max(1, bm.MaxKi), pend = Mathf.Max(0, bm.PendingKi);
        int solid = Mathf.Max(max, cur);   // 최대치보다 더 받은 라운드에는 그만큼 칸이 늘어난다
        int count = solid + pend;
        while (pips.Count < count) pips.Add(NewPip(pips.Count));
        for (int i = 0; i < pips.Count; i++) pips[i].rt.gameObject.SetActive(i < count);

        // 크기: 칸이 많으면 줄인다
        float width = row.rect.width > 1f ? row.rect.width : 150f;
        float size = Mathf.Min(MaxPip, (width - Gap * (count - 1)) / Mathf.Max(1, count));
        size = Mathf.Min(size, row.rect.height > 1f ? row.rect.height - 6f : MaxPip);

        // 골라 둔 태엽판이 쓸 구슬 (오른쪽 끝부터)
        int spend = bm.PendingCard != null ? Mathf.Min(cur, bm.PendingCard.cost) : 0;
        float blink = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 9f);
        float start = Mathf.Max(0f, (width - (count * size + (count - 1) * Gap)) * 0.5f);   // 가운데 정렬

        for (int i = 0; i < count; i++)
        {
            Pip p = pips[i];
            State s = i < cur ? State.Full : i < solid ? State.Empty : State.Ghost;
            if (s != p.state) { p.pop = 1f; p.state = s; }
            p.pop = Mathf.MoveTowards(p.pop, 0f, Time.unscaledDeltaTime * 5f);

            p.rt.sizeDelta = new Vector2(size, size);
            p.rt.anchoredPosition = new Vector2(start + i * (size + Gap) + size * 0.5f, 0f);
            p.rt.localScale = Vector3.one * (1f + 0.3f * p.pop);

            bool spending = s == State.Full && i >= cur - spend;
            p.fill.enabled = s != State.Empty;
            p.glint.enabled = s == State.Full;
            p.fill.color = s == State.Ghost ? UITheme.Alpha(kiColor, 0.22f + 0.12f * blink)
                         : spending ? UITheme.Alpha(Color.Lerp(kiColor, Color.white, 0.5f), 0.45f + 0.55f * blink)
                         : kiColor;
            p.ring.color = s == State.Full ? UITheme.Alpha(Color.Lerp(kiColor, Color.white, 0.35f), 0.95f)
                         : s == State.Ghost ? UITheme.Alpha(kiColor, 0.5f)
                         : ringIdle;
        }
    }

    private Pip NewPip(int index)
    {
        Pip p = new Pip();
        p.rt = UIBuilder.Rect("Pip" + index, row);
        p.rt.anchorMin = p.rt.anchorMax = new Vector2(0f, 0.5f);
        p.rt.pivot = new Vector2(0.5f, 0.5f);

        p.hole = Layer(p.rt, "Hole", ProceduralSprites.Circle(), hollow, 2f);          // 빈 구슬의 움푹한 속
        p.fill = Layer(p.rt, "Fill", ProceduralSprites.Circle(), kiColor, 4f);          // 차 있는 기력
        p.ring = Layer(p.rt, "Ring", ProceduralSprites.Ring(), ringIdle, 0f);           // 테
        p.glint = Layer(p.rt, "Glint", ProceduralSprites.Circle(), UITheme.Alpha(Color.white, 0.55f), 0f);   // 반짝임
        RectTransform g = p.glint.rectTransform;
        g.anchorMin = g.anchorMax = new Vector2(0.34f, 0.68f);
        g.sizeDelta = Vector2.zero;
        g.offsetMin = new Vector2(-3f, -3f);
        g.offsetMax = new Vector2(3f, 3f);
        return p;
    }

    private static Image Layer(RectTransform parent, string name, Sprite sprite, Color color, float inset)
    {
        RectTransform rt = UIBuilder.Rect(name, parent);
        UIBuilder.Stretch(rt, inset);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }
}
