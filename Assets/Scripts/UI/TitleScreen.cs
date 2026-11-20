using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 시작 화면 — 게임을 켜면 가장 먼저 뜬다.
///
///   천천히 도는 톱니와 시계판 위에 제목, [시작하기] / [끝내기] 버튼.
///   시작하기(또는 Enter · Space)를 누르면 onStart를 부르고 걷힌다.
///
/// 직접 붙일 필요 없음 — GameManager가 TitleScreen.Show()로 띄운다 (전용 Canvas를 알아서 만든다).
/// GameManager의 Show Title Screen을 끄면 건너뛴다. 패배 뒤 '다시 시작'도 건너뛴다.
/// </summary>
public class TitleScreen : MonoBehaviour
{
    private static TitleScreen instance;

    /// <summary>떠 있는 동안 true</summary>
    public static bool IsOpen { get; private set; }

    /// <summary>씬을 다시 불렀을 때 (시작 화면으로 나갔다 올 때) 지난 씬의 '열려 있음'을 잊는다</summary>
    public static void ForgetScene() { IsOpen = false; }

    private CanvasGroup group;
    private RectTransform gearA, gearB, gearC, hourHand, minuteHand, secondHand;
    private Text hintText;
    private Button startButton;
    private Action onStart, onContinue, onEndless;
    private bool closing;
    private float shownAt;

    // ─────────────────────────── 진입 ───────────────────────────

    /// <summary>title = 큰 제목, subtitle = 그 아래 이야기 이름, tagline = 한 줄 소개</summary>
    /// continueInfo / onContinue = 저장이 있을 때: '이어하기' 버튼과 그 아래 한 줄 (어디까지 왔는가)
    /// onEndless / endlessInfo = 무한 모드 버튼과 그 아래 한 줄 (최고 기록). 비우면 버튼이 없다
    public static void Show(string title, string subtitle, string tagline, Action onStart, string continueInfo = null, Action onContinue = null,
                            Action onEndless = null, string endlessInfo = null)
    {
        if (instance == null) instance = Create(title, subtitle, tagline, onContinue != null ? continueInfo ?? "" : null, onEndless != null ? endlessInfo ?? "" : null);
        instance.onStart = onStart;
        instance.onContinue = onContinue;
        instance.onEndless = onEndless;
        instance.closing = false;
        instance.shownAt = Time.unscaledTime;
        instance.group.alpha = 0f;
        instance.group.interactable = true;
        instance.gameObject.SetActive(true);
        IsOpen = true;
    }

    /// <summary>시작하기 (버튼 · Enter · Space · 테스트 자동 진행)</summary>
    public static void Begin()
    {
        if (instance == null || !IsOpen || instance.closing) return;
        instance.closing = true;
        instance.group.interactable = false;
        instance.StartCoroutine(instance.Close());
    }

    /// <summary>이어하기 (버튼 · 저장이 있을 때의 Enter · Space)</summary>
    public static void Continue()
    {
        if (instance == null || !IsOpen || instance.closing || instance.onContinue == null) return;
        instance.onStart = instance.onContinue;
        Begin();
    }

    /// <summary>무한 모드로 시작 (버튼)</summary>
    public static void Endless()
    {
        if (instance == null || !IsOpen || instance.closing || instance.onEndless == null) return;
        instance.onStart = instance.onEndless;
        Begin();
    }

    private IEnumerator Close()
    {
        yield return null;   // 누른 Enter · Space가 같은 프레임에 첫 대사까지 넘기지 않게
        if (onStart != null) onStart();

        // 로딩 화면이 위에서 덮는 동안은 그대로 있다가 걷힌다 (둘이 같이 흐려지면 뒤의 장면이 비친다)
        for (float t = 0f; t < 0.2f; t += Time.unscaledDeltaTime) yield return null;
        while (group.alpha > 0f)
        {
            group.alpha = Mathf.MoveTowards(group.alpha, 0f, Time.unscaledDeltaTime / 0.2f);
            yield return null;
        }

        IsOpen = false;
        Destroy(gameObject);
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (!closing)
        {
            group.alpha = Mathf.MoveTowards(group.alpha, 1f, dt / 0.35f);
            // 뜨자마자 눌린 키로 넘어가지 않게 잠깐 기다린 뒤부터 받는다
            if (Time.unscaledTime - shownAt > 0.25f && !RankingPanel.IsOpen && !CompendiumPanel.IsOpen &&
                (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)))
            {
                if (onContinue != null) Continue();
                else Begin();
            }
        }

        // 톱니는 맞물린 듯 서로 반대로, 시곗바늘은 초침만 똑딱거리며 돈다
        gearA.Rotate(0f, 0f, -6f * dt);
        gearB.Rotate(0f, 0f, 9f * dt);
        gearC.Rotate(0f, 0f, -12f * dt);

        float now = Time.unscaledTime;
        float tick = Mathf.Floor(now) + Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((now % 1f) / 0.18f));   // 초마다 한 칸씩 툭
        secondHand.localRotation = Quaternion.Euler(0f, 0f, -6f * tick);
        minuteHand.localRotation = Quaternion.Euler(0f, 0f, -52f - 0.5f * now);
        hourHand.localRotation = Quaternion.Euler(0f, 0f, 62f - 0.04f * now);

        if (hintText != null)
        {
            Color c = hintText.color;
            c.a = 0.45f + 0.4f * (0.5f + 0.5f * Mathf.Sin(now * 2.4f));
            hintText.color = c;
        }
    }

    // ─────────────────────────── 생성 ───────────────────────────

    private static TitleScreen Create(string title, string subtitle, string tagline, string continueInfo, string endlessInfo)
    {
        GameObject go = new GameObject("TitleScreenCanvas");
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;   // 게임 화면 전부의 위, 로딩 화면(950) 아래
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();

        TitleScreen screen = go.AddComponent<TitleScreen>();
        screen.group = go.AddComponent<CanvasGroup>();
        Vector2 center = new Vector2(0.5f, 0.5f);

        // 바탕: 깊은 먹색 + 아래쪽이 더 어두운 그늘 (물에 잠긴 마을의 밤)
        Image bg = UIBuilder.Panel("Background", go.transform, UITheme.Backdrop, false);
        bg.sprite = null;
        UIBuilder.Stretch(bg.rectTransform);
        bg.raycastTarget = true;   // 뒤의 게임 화면을 누르지 못하게

        // 구석의 큰 톱니들 (화면 밖으로 반쯤 걸친다)
        Color dim = UITheme.Alpha(UITheme.Brass, 0.09f);
        screen.gearA = Emblem(bg.transform, IconPainter.Gear, new Vector2(0f, 0f), new Vector2(120f, 90f), 760f, dim, 256);
        screen.gearB = Emblem(bg.transform, IconPainter.GearSmall, new Vector2(0f, 0f), new Vector2(610f, 300f), 420f, dim, 256);
        screen.gearC = Emblem(bg.transform, IconPainter.GearSmall, new Vector2(1f, 1f), new Vector2(-150f, -110f), 520f,
                              UITheme.Alpha(UITheme.Brass, 0.07f), 256);

        // 가운데 뒤의 큰 시계판 + 바늘 (위아래는 화면 밖으로 넘친다 — 제목과 버튼이 시계 안에 들어앉는다)
        Color faint = UITheme.Alpha(UITheme.Brass, 0.16f);
        RectTransform clock = Emblem(bg.transform, IconPainter.ClockFace, center, Vector2.zero, 1320f, faint, 512);
        screen.hourHand = Hand(clock, 270f, 14f, faint);
        screen.minuteHand = Hand(clock, 420f, 9f, faint);
        screen.secondHand = Hand(clock, 500f, 3f, UITheme.Alpha(UITheme.Enemy, 0.35f));
        RectTransform pin = UIBuilder.Rect("Pin", clock);
        pin.anchorMin = pin.anchorMax = pin.pivot = center;
        pin.sizeDelta = new Vector2(26f, 26f);
        Image pinImg = pin.gameObject.AddComponent<Image>();
        pinImg.sprite = ProceduralSprites.Circle();
        pinImg.color = UITheme.Alpha(UITheme.Brass, 0.45f);
        pinImg.raycastTarget = false;

        Image shade = UIBuilder.Panel("Shade", bg.transform, UITheme.Alpha(Color.black, 0.75f), false);
        shade.sprite = ProceduralSprites.VerticalFade();
        shade.type = Image.Type.Simple;
        shade.raycastTarget = false;
        UIBuilder.Place(shade.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 620f));

        // 제목
        Text big = UIBuilder.Label("Title", bg.transform, title, 112, UITheme.Brass, TextAnchor.MiddleCenter);
        big.fontStyle = FontStyle.Bold;
        big.horizontalOverflow = HorizontalWrapMode.Overflow;
        UIBuilder.Place(big.rectTransform, center, center, center, new Vector2(0f, 230f), new Vector2(1700f, 150f));

        Image rule = UIBuilder.Panel("Rule", bg.transform, UITheme.Alpha(UITheme.Brass, 0.7f), false);
        rule.sprite = null;
        rule.raycastTarget = false;
        UIBuilder.Place(rule.rectTransform, center, center, center, new Vector2(0f, 144f), new Vector2(520f, 2f));
        RectTransform bell = Emblem(bg.transform, IconPainter.Boss, center, new Vector2(0f, 144f), 46f, UITheme.Brass, 128);
        Image bellBack = UIBuilder.Panel("BellBack", bg.transform, UITheme.Backdrop, false);   // 줄이 종 뒤로 지나가지 않게
        bellBack.sprite = ProceduralSprites.Circle();
        bellBack.type = Image.Type.Simple;
        bellBack.raycastTarget = false;
        UIBuilder.Place(bellBack.rectTransform, center, center, center, new Vector2(0f, 144f), new Vector2(76f, 76f));
        bell.SetAsLastSibling();

        Text sub = UIBuilder.Label("Subtitle", bg.transform, subtitle, 44, UITheme.TextMain, TextAnchor.MiddleCenter);
        UIBuilder.Place(sub.rectTransform, center, center, center, new Vector2(0f, 70f), new Vector2(1400f, 60f));

        Text line = UIBuilder.Label("Tagline", bg.transform, tagline, 24, UITheme.TextMuted, TextAnchor.MiddleCenter);
        UIBuilder.Place(line.rectTransform, center, center, center, new Vector2(0f, 12f), new Vector2(1400f, 36f));

        // 버튼 배치: [이어하기]는 위 가운데 (저장이 있을 때만),
        //           그 아래 왼쪽 [스토리 모드] · 오른쪽 [무한 모드] 나란히 — 무한 모드 밑에 [랭킹], 맨 아래 가운데 [끝내기]
        bool hasSave = continueInfo != null;
        bool hasEndless = endlessInfo != null;
        float y = hasSave ? -88f : -120f;
        if (hasSave)
        {
            Button resume = UIBuilder.Button("ContinueButton", bg.transform, "이어하기", UITheme.BrassFill, 30);
            UITheme.StyleButton(resume, true, 30);
            UIBuilder.Place((RectTransform)resume.transform, center, center, center, new Vector2(0f, y), new Vector2(360f, 76f));
            resume.onClick.AddListener(Continue);

            Text info = UIBuilder.Label("ContinueInfo", bg.transform, continueInfo, 19, UITheme.TextMuted, TextAnchor.MiddleCenter);
            UIBuilder.Place(info.rectTransform, center, center, center, new Vector2(0f, y - 54f), new Vector2(900f, 28f));
            y -= 140f;
        }

        const float colX = 220f;
        float storyX = hasEndless ? -colX : 0f;
        screen.startButton = UIBuilder.Button("StartButton", bg.transform, hasSave ? "스토리 새로 시작" : "스토리 모드", UITheme.BrassFill, 28);
        UITheme.StyleButton(screen.startButton, !hasSave, 28);
        UIBuilder.Place((RectTransform)screen.startButton.transform, center, center, center, new Vector2(storyX, y), new Vector2(380f, 76f));
        screen.startButton.onClick.AddListener(Begin);
        Text storyInfo = UIBuilder.Label("StoryInfo", bg.transform, hasSave ? "저장해 둔 판은 지워진다" : "물에 잠긴 시계 마을의 이야기를 따라간다", 18, UITheme.TextMuted, TextAnchor.MiddleCenter);
        UIBuilder.Place(storyInfo.rectTransform, center, center, center, new Vector2(storyX, y - 54f), new Vector2(420f, 26f));

        // 사전: 모든 태엽판과 적 (스토리 모드 밑 — 무한 모드 밑의 [랭킹 보기]와 나란히)
        Button dex = UIBuilder.Button("CompendiumButton", bg.transform, "태엽판 · 적 사전", UITheme.InkSolid, 20);
        UITheme.StyleButton(dex, false, 20);
        UIBuilder.Place((RectTransform)dex.transform, center, center, center, new Vector2(storyX, y - 104f), new Vector2(240f, 46f));
        dex.onClick.AddListener(() => CompendiumPanel.Open(bg.transform));

        // 무한 모드: 스토리 없이 지도와 ??? 보스를 끝없이 (회차마다 적이 강해진다)
        if (hasEndless)
        {
            Button endless = UIBuilder.Button("EndlessButton", bg.transform, "무한 모드", UITheme.BrassFill, 28);
            UITheme.StyleButton(endless, !hasSave, 28);
            UIBuilder.Place((RectTransform)endless.transform, center, center, center, new Vector2(colX, y), new Vector2(380f, 76f));
            endless.onClick.AddListener(Endless);
            Text info = UIBuilder.Label("EndlessInfo", bg.transform, endlessInfo, 18, UITheme.TextMuted, TextAnchor.MiddleCenter);
            UIBuilder.Place(info.rectTransform, center, center, center, new Vector2(colX, y - 54f), new Vector2(420f, 26f));

            Button rank = UIBuilder.Button("RankingButton", bg.transform, "랭킹 보기", UITheme.InkSolid, 20);
            UITheme.StyleButton(rank, false, 20);
            UIBuilder.Place((RectTransform)rank.transform, center, center, center, new Vector2(colX, y - 104f), new Vector2(220f, 46f));
            rank.onClick.AddListener(() => RankingPanel.Open(bg.transform));
        }
        y -= 196f;

        Button quit = UIBuilder.Button("QuitButton", bg.transform, "끝내기", UITheme.InkSolid, 22);
        UITheme.StyleButton(quit, false, 22);
        UIBuilder.Place((RectTransform)quit.transform, center, center, center, new Vector2(0f, y), new Vector2(240f, 54f));
        quit.onClick.AddListener(Quit);

        screen.hintText = UIBuilder.Label("Hint", bg.transform, hasSave ? "Enter · Space 로 이어하기" : "Enter · Space 로 스토리 모드 시작", 20, UITheme.TextMuted, TextAnchor.MiddleCenter);
        UIBuilder.Place(screen.hintText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 56f), new Vector2(800f, 30f));

        Text version = UIBuilder.Label("Version", bg.transform, "v" + Application.version, 18, UITheme.TextDim, TextAnchor.LowerRight);
        UIBuilder.Place(version.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-28f, 20f), new Vector2(300f, 26f));

        go.SetActive(false);
        return screen;
    }

    /// <summary>문양 스프라이트 한 장 (anchor 한 점 기준 배치)</summary>
    private static RectTransform Emblem(Transform parent, string kind, Vector2 anchor, Vector2 pos, float size, Color color, int texture)
    {
        RectTransform rt = UIBuilder.Rect(kind, parent);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(size, size);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = RouteIcons.Get(kind, texture);
        img.color = color;
        img.raycastTarget = false;
        return rt;
    }

    /// <summary>시곗바늘: 가운데에서 위로 뻗은 막대 (부모 가운데를 축으로 돈다)</summary>
    private static RectTransform Hand(RectTransform clock, float length, float width, Color color)
    {
        RectTransform pivot = UIBuilder.Rect("Hand", clock);
        pivot.anchorMin = pivot.anchorMax = pivot.pivot = new Vector2(0.5f, 0.5f);
        pivot.sizeDelta = Vector2.zero;

        RectTransform bar = UIBuilder.Rect("Bar", pivot);
        bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0.5f);
        bar.pivot = new Vector2(0.5f, 0.12f);   // 축 뒤로 조금 튀어나온 꼬리
        bar.anchoredPosition = Vector2.zero;
        bar.sizeDelta = new Vector2(width, length);
        Image img = bar.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return pivot;
    }

    private static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void OnDestroy()
    {
        if (instance == this) { instance = null; IsOpen = false; }
    }
}
