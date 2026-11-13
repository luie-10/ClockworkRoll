using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로딩 화면 — 화면을 덮고(맞물려 도는 톱니 + 진행 막대 + 도움말 한 줄) 그 뒤에서 장면을 바꾼다.
///
///   LoadingScreen.Play("뒷골목 무대", "1장. 뒷골목의 이빨", null, () => 전투 시작);
///
///   1) 화면이 덮인다
///   2) work(있으면)를 한 걸음씩 돌린다 — 그림 미리 그리기 같은 실제로 시간이 걸리는 일
///   3) 다 덮인 상태에서 onCovered를 부른다 → 배경·적이 바뀌는 순간이 보이지 않는다
///   4) 걷힌다
///
/// 덮여 있는 동안 또 Play를 부르면 걷히지 않고 이어서 보여준다 (게임 시작 → 곧바로 첫 전투).
/// 직접 붙일 필요 없음 — 전용 Canvas를 알아서 만든다. GameManager의 Show Loading Screen으로 끌 수 있다.
/// </summary>
public class LoadingScreen : MonoBehaviour
{
    private class Request
    {
        public string title, sub;
        public IEnumerator work;
        public Action onCovered;
        public float minTime;
    }

    private const float FadeIn = 0.22f, FadeOut = 0.32f;
    private const float WorkBudget = 0.008f;   // 한 프레임에 work에 쓰는 시간(초) — 톱니가 끊기지 않게

    private static readonly string[] tips =
    {
        "같은 시각에 놓인 칸끼리만 합을 한다. 적의 공격 칸 위에 내 칸을 겹쳐 막자.",
        "막지 않은 적의 공격 칸은 그대로 맞는다. 시계판을 먼저 읽자.",
        "참격은 관통에, 관통은 타격에, 타격은 참격에 강하다. 앞서면 굴림 +2.",
        "회피는 공격 칸을 상대로 굴림 +2.",
        "합이 이어질수록 장력이 오른다. 장력 단계만큼 더 많은 기억을 삼킬 수 있다.",
        "◈ 흡수한 태엽판은 쓸 때마다 침식이 일어날 수 있다.",
        "체력은 전투가 끝나도 돌아오지 않는다. 가로등 아래에서 쉬어 가자.",
        "녹이 슬면 칸의 최댓값이 깎인다. 굴릴 때마다 1씩 벗겨진다.",
        "적을 클릭하면 그 적의 다음 행동에 맞부딪히는 시각에 태엽판이 놓인다.",
        "약점을 찌르면 피해가 1.5배, 취약이면 2배. 적에게 마우스를 올려 내성을 보자.",
    };

    private static LoadingScreen instance;

    /// <summary>화면에 떠 있는 동안 true</summary>
    public static bool IsOpen { get; private set; }

    /// <summary>화면을 완전히 덮었는가 (뒤에서 무엇을 바꿔도 보이지 않는다)</summary>
    public static bool Covered { get { return instance != null && IsOpen && instance.group.alpha >= 0.999f; } }

    private readonly Queue<Request> queue = new Queue<Request>();
    private CanvasGroup group;
    private Text titleText, subText, tipText;
    private Image fill;
    private RectTransform bigGear, smallGear;
    private bool running;
    private int lastTip = -1;

    // ─────────────────────────── 진입 ───────────────────────────

    /// <summary>
    /// title/sub = 가운데에 보여 줄 글 (어디로 가는가), work = 덮인 동안 돌릴 일 (없으면 null),
    /// onCovered = 다 덮였을 때 할 일, minTime = 최소로 보여 주는 시간(초), instant = 서서히 덮지 않고 바로 덮는다.
    /// </summary>
    public static void Play(string title, string sub, IEnumerator work, Action onCovered, float minTime = 0.9f, bool instant = false)
    {
        if (instance == null) instance = Create();
        instance.queue.Enqueue(new Request { title = title, sub = sub, work = work, onCovered = onCovered, minTime = minTime });
        if (instance.running) return;   // 이미 덮여 있다 — 지금 것이 끝나면 이어서

        instance.running = true;
        IsOpen = true;
        instance.gameObject.SetActive(true);
        instance.group.alpha = instant ? 1f : 0f;
        instance.StartCoroutine(instance.Run());
    }

    private IEnumerator Run()
    {
        while (group.alpha < 1f)
        {
            group.alpha = Mathf.MoveTowards(group.alpha, 1f, Time.unscaledDeltaTime / FadeIn);
            yield return null;
        }

        while (queue.Count > 0)
        {
            Request r = queue.Dequeue();
            titleText.text = r.title ?? "";
            subText.text = r.sub ?? "";
            tipText.text = NextTip();
            fill.fillAmount = 0f;

            bool done = r.work == null;
            float t = 0f;
            while (!done || t < r.minTime)
            {
                float started = Time.realtimeSinceStartup;
                while (!done && Time.realtimeSinceStartup - started < WorkBudget) done = !Step(r.work);

                t += Time.unscaledDeltaTime;
                float shown = r.minTime > 0f ? Mathf.Clamp01(t / r.minTime) : 1f;
                if (!done) shown = Mathf.Min(shown, 0.88f);   // 일이 남았으면 끝까지 차지 않는다
                fill.fillAmount = Mathf.MoveTowards(fill.fillAmount, shown, Time.unscaledDeltaTime * 2.5f);
                yield return null;
            }
            fill.fillAmount = 1f;
            yield return null;

            if (r.onCovered != null)
            {
                try { r.onCovered(); }
                catch (Exception e) { Debug.LogException(e); }   // 여기서 멈추면 화면이 덮인 채로 남는다
            }
            yield return null;   // 바뀐 장면이 한 번 그려질 틈
            yield return null;
        }

        while (group.alpha > 0f)
        {
            if (queue.Count > 0) { StartCoroutine(Run()); yield break; }   // 걷히는 중에 또 불렸으면 다시 덮는다
            group.alpha = Mathf.MoveTowards(group.alpha, 0f, Time.unscaledDeltaTime / FadeOut);
            yield return null;
        }

        running = false;
        IsOpen = false;
        gameObject.SetActive(false);
    }

    private static bool Step(IEnumerator work)
    {
        try { return work.MoveNext(); }
        catch (Exception e) { Debug.LogException(e); return false; }
    }

    private string NextTip()
    {
        int i = UnityEngine.Random.Range(0, tips.Length);
        if (i == lastTip) i = (i + 1) % tips.Length;
        lastTip = i;
        return "<color=#dcb06b>도움말</color>   " + tips[i];
    }

    void Update()
    {
        // 맞물린 두 톱니: 이가 12개 · 8개라 작은 쪽이 1.5배 빠르게 반대로 돈다
        float dt = Time.unscaledDeltaTime;
        bigGear.Rotate(0f, 0f, -42f * dt);
        smallGear.Rotate(0f, 0f, 63f * dt);
    }

    // ─────────────────────────── 생성 ───────────────────────────

    private static LoadingScreen Create()
    {
        GameObject go = new GameObject("LoadingScreenCanvas");
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 950;   // 무엇보다 위 (결과 화면 700, 시작 화면 900)
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();

        LoadingScreen screen = go.AddComponent<LoadingScreen>();
        screen.group = go.AddComponent<CanvasGroup>();
        Vector2 center = new Vector2(0.5f, 0.5f);

        Image bg = UIBuilder.Panel("Cover", go.transform, new Color(0.022f, 0.025f, 0.035f, 1f), false);
        bg.sprite = null;
        UIBuilder.Stretch(bg.rectTransform);
        bg.raycastTarget = true;   // 덮여 있는 동안 뒤의 화면을 누르지 못하게

        // 맞물린 톱니 두 개
        screen.bigGear = Gear(bg.transform, IconPainter.Gear, new Vector2(-46f, 96f), 230f, UITheme.Brass);
        screen.smallGear = Gear(bg.transform, IconPainter.GearSmall, new Vector2(94f, 24f), 150f, new Color(0.62f, 0.5f, 0.3f));
        screen.smallGear.localRotation = Quaternion.Euler(0f, 0f, 22.5f);

        screen.titleText = UIBuilder.Label("Title", bg.transform, "", 46, UITheme.Brass, TextAnchor.MiddleCenter);
        screen.titleText.fontStyle = FontStyle.Bold;
        UIBuilder.Place(screen.titleText.rectTransform, center, center, center, new Vector2(0f, -110f), new Vector2(1400f, 64f));

        screen.subText = UIBuilder.Label("Sub", bg.transform, "", 24, UITheme.TextMuted, TextAnchor.MiddleCenter);
        UIBuilder.Place(screen.subText.rectTransform, center, center, center, new Vector2(0f, -160f), new Vector2(1400f, 36f));

        screen.fill = UIBuilder.Gauge("Progress", bg.transform, UITheme.BrassFill);
        RectTransform bar = (RectTransform)screen.fill.transform.parent;
        UIBuilder.Place(bar, center, center, center, new Vector2(0f, -214f), new Vector2(520f, 16f));

        screen.tipText = UIBuilder.Label("Tip", bg.transform, "", 23, UITheme.TextMain, TextAnchor.MiddleCenter);
        screen.tipText.supportRichText = true;
        UIBuilder.Place(screen.tipText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(1500f, 40f));

        go.SetActive(false);
        return screen;
    }

    private static RectTransform Gear(Transform parent, string kind, Vector2 pos, float size, Color color)
    {
        RectTransform rt = UIBuilder.Rect(kind, parent);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(size, size);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = RouteIcons.Get(kind, 256);
        img.color = color;
        img.raycastTarget = false;
        return rt;
    }

    void OnDestroy()
    {
        if (instance == this) { instance = null; IsOpen = false; }
    }
}
