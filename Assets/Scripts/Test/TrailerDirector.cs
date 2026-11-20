using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using Unity.Collections;
using UnityEditor;
using UnityEditor.Media;
#endif

/// <summary>
/// 홍보영상 자동 촬영 — 메뉴 [ClockworkRoll → 홍보영상 촬영]을 누르면 플레이 모드로 들어가 정해진 장면을 스스로 연기하고,
/// 화면과 소리를 그대로 Recordings/ClockworkRoll_Trailer.mp4 (1초 30장, H.264 + AAC)로 저장한 뒤 플레이 모드를 끝낸다.
///
/// 장면 (약 75초):
///   1. 검은 화면에 제목                     4. 첫 라운드 — [열두 번의 타종] · [톱니 폭풍] · [러시안 태엽]
///   2. 시작 화면                            5. 둘째 라운드 — 즉발 [증기 폭발] + [자정의 일격] · [그믐달 베기] · [마지막 판돈]
///   3. 무한 모드 지도 → 전투 (태엽 딜러 · 룰렛 인형)   6. 셋째 라운드 — [자동 배치]
///                                           7. 승리 → 태엽판 · 적 사전 → 끝 화면
///
/// 시간은 Time.captureFramerate로 1/30초씩만 흐르게 고정한다 — 한 장을 저장하는 데 오래 걸려도 영상은 매끄럽다.
/// 소리는 AudioRenderer로 같은 박자에 맞춰 받아 넣는다. 촬영 동안은 튜토리얼을 끄고 끝나면 되돌린다.
/// 녹화 자체(MP4 쓰기)는 에디터에서만 된다 — 빌드에서는 연출만 돈다.
/// </summary>
public class TrailerDirector : MonoBehaviour
{
    public const string RequestKey = "trailer.request";
    private const int Fps = 30;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        bool requested = false;
        try { requested = PlayerPrefs.GetInt(RequestKey, 0) == 1; PlayerPrefs.DeleteKey(RequestKey); PlayerPrefs.Save(); } catch { }
        if (!requested) return;
        GameObject go = new GameObject("TrailerDirector");
        DontDestroyOnLoad(go);
        go.AddComponent<TrailerDirector>();
    }

    // ─────────────────────────── 자막 ───────────────────────────

    private Canvas canvas;
    private Image black, captionBar;
    private Text bigTitle, bigSub, caption, captionSmall;
    private CanvasGroup captionGroup;
    private Transform panelParent;

    private void BuildOverlay()
    {
        GameObject go = new GameObject("TrailerCanvas");
        go.transform.SetParent(transform, false);
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 990;   // 로딩 화면(950)보다도 위
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // 사전을 띄울 아래층 (자막보다 아래)
        GameObject under = new GameObject("TrailerPanels");
        under.transform.SetParent(transform, false);
        Canvas uc = under.AddComponent<Canvas>();
        uc.renderMode = RenderMode.ScreenSpaceOverlay;
        uc.sortingOrder = 980;
        CanvasScaler us = under.AddComponent<CanvasScaler>();
        us.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        us.referenceResolution = new Vector2(1920f, 1080f);
        us.matchWidthOrHeight = 0.5f;
        under.AddComponent<GraphicRaycaster>();
        RectTransform underRoot = UIBuilder.Rect("Root", under.transform);
        UIBuilder.Stretch(underRoot);
        panelParent = underRoot;

        RectTransform root = UIBuilder.Rect("Root", go.transform);
        UIBuilder.Stretch(root);

        black = root.gameObject.AddComponent<Image>();
        black.color = Color.black;
        black.raycastTarget = false;

        Vector2 mid = new Vector2(0.5f, 0.5f);
        bigTitle = UIBuilder.Label("Title", root, "", 120, UITheme.Brass, TextAnchor.MiddleCenter);
        bigTitle.fontStyle = FontStyle.Bold;
        bigTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
        UIBuilder.Place(bigTitle.rectTransform, mid, mid, mid, new Vector2(0f, 40f), new Vector2(1800f, 160f));
        bigSub = UIBuilder.Label("Sub", root, "", 34, UITheme.TextMain, TextAnchor.MiddleCenter);
        bigSub.horizontalOverflow = HorizontalWrapMode.Overflow;
        UIBuilder.Place(bigSub.rectTransform, mid, mid, mid, new Vector2(0f, -80f), new Vector2(1800f, 60f));

        // 아래 자막 띠
        captionBar = UIBuilder.Panel("CaptionBar", root, new Color(0f, 0f, 0f, 0.62f), false);
        captionBar.sprite = null;
        captionBar.raycastTarget = false;
        UIBuilder.Place(captionBar.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(0f, 112f));
        captionGroup = captionBar.gameObject.AddComponent<CanvasGroup>();
        Image line = UIBuilder.Panel("Line", captionBar.transform, UITheme.Brass, false);
        line.sprite = null;
        UIBuilder.Place(line.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 3f));
        caption = UIBuilder.Label("Caption", captionBar.transform, "", 44, Color.white, TextAnchor.MiddleCenter);
        caption.fontStyle = FontStyle.Bold;
        caption.horizontalOverflow = HorizontalWrapMode.Overflow;
        UIBuilder.Place(caption.rectTransform, mid, mid, mid, new Vector2(0f, 14f), new Vector2(1800f, 60f));
        captionSmall = UIBuilder.Label("CaptionSmall", captionBar.transform, "", 24, UITheme.TextMuted, TextAnchor.MiddleCenter);
        captionSmall.horizontalOverflow = HorizontalWrapMode.Overflow;
        UIBuilder.Place(captionSmall.rectTransform, mid, mid, mid, new Vector2(0f, -32f), new Vector2(1800f, 34f));
        captionGroup.alpha = 0f;
    }

    private Coroutine captionJob;
    private void Caption(string text, string small = "", float hold = 2.6f)
    {
        if (captionJob != null) StopCoroutine(captionJob);
        captionJob = StartCoroutine(CaptionRoutine(text, small, hold));
    }

    private IEnumerator CaptionRoutine(string text, string small, float hold)
    {
        caption.text = text;
        captionSmall.text = small;
        for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime) { captionGroup.alpha = t / 0.25f; yield return null; }
        captionGroup.alpha = 1f;
        for (float t = 0f; t < hold; t += Time.unscaledDeltaTime) yield return null;
        for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime) { captionGroup.alpha = 1f - t / 0.3f; yield return null; }
        captionGroup.alpha = 0f;
        captionJob = null;
    }

    private IEnumerator FadeBlack(float to, float time)
    {
        float from = black.color.a;
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            float a = Mathf.Lerp(from, to, t / time);
            black.color = new Color(0f, 0f, 0f, a);
            bigTitle.color = new Color(bigTitle.color.r, bigTitle.color.g, bigTitle.color.b, a);
            bigSub.color = new Color(bigSub.color.r, bigSub.color.g, bigSub.color.b, a);
            yield return null;
        }
        black.color = new Color(0f, 0f, 0f, to);
    }

    private static IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) yield return null;
    }

    private static IEnumerator Until(System.Func<bool> cond, float timeout = 30f)
    {
        float t = 0f;
        while (!cond() && t < timeout) { t += Time.unscaledDeltaTime; yield return null; }
    }

    // ─────────────────────────── 촬영 ───────────────────────────

    private bool tutorialWas;

    IEnumerator Start()
    {
        BuildOverlay();
        tutorialWas = TutorialCoach.Enabled;
        TutorialCoach.Enabled = false;
        RouteScreen.PreferFight = true;
        Time.captureFramerate = Fps;

        // 첫 장면이 그려질 때까지 (시작 화면이 뜰 때까지)
        yield return Until(() => TitleScreen.IsOpen, 10f);
        yield return null;
        BeginRecording();

        // 1) 검은 화면에 제목
        GameManager gm = GameManager.Instance;
        bigTitle.text = gm != null ? gm.gameTitle : "CLOCKWORK ROLL";
        bigSub.text = "태엽판을 시계판에 놓아라. 같은 시각에 놓인 적과 맞부딪힌다.";
        yield return Wait(3f);
        bigTitle.text = bigSub.text = "";
        yield return FadeBlack(0f, 0.8f);

        // 2) 시작 화면
        Caption("물에 잠긴 시계 마을", "멈춘 종탑으로 향하는 덱 빌딩 로그라이트");
        yield return Wait(3.4f);

        // 3) 무한 모드 → 지도
        TitleScreen.Endless();
        yield return Until(() => RouteScreen.IsOpen && !LoadingScreen.IsOpen, 20f);
        Caption("갈래길을 골라 오른다", "전투, 강적, 이벤트, 휴식, 보물. 꼭대기의 보스는 ???");
        yield return Wait(3.2f);

        // 전투 준비: 보여 줄 태엽판 · 적으로 무대를 꾸민다
        Catalog cat = Catalog.Build(gm);
        BattleStageData stage = ScriptableObject.CreateInstance<BattleStageData>();
        stage.stageName = "딜러의 탁자";
        AddFoe(stage, cat, "태엽 딜러", new Vector3(3f, 0f, 1.2f));
        AddFoe(stage, cat, "룰렛 인형", new Vector3(4.5f, 0f, -1f));
        foreach (string n in new[] { "열두 번의 타종", "톱니 폭풍", "러시안 태엽", "증기 폭발", "자정의 일격", "그믐달 베기", "마지막 판돈", "행운의 태엽" })
            if (cat.cards.TryGetValue(n, out CardData c)) stage.deckOverride.Add(c);
        if (gm != null)
        {
            gm.AddMaxKi(12);
            gm.playerUnit.ChangeMaxHp(300);
            gm.playerUnit.Heal(999);
        }
        GameManager.StageOverride = stage;
        RouteScreen.AutoPick();
        yield return null;
        BattleManager.NextEnemyHpScale = 5f;   // 세 라운드는 버티게

        BattleManager bm = null;
        yield return Until(() => (bm = BattleManager.Instance) != null && bm.Phase == BattleManager.BattlePhase.PlayerTurn && !LoadingScreen.IsOpen, 25f);
        if (bm == null) { yield return Finish(); yield break; }
        if (bm.handManager != null) { bm.handManager.handLimit = 10; bm.handManager.RefillHand(); }
        bm.GrantTension(9);
        yield return Wait(0.6f);

        // 4) 첫 라운드
        Caption("태엽판을 시계판의 시각에 놓는다", "같은 시각에 적 칸과 겹치면 합. 주사위가 높은 쪽만 효과를 낸다");
        yield return PlaceRoutine(bm, ("열두 번의 타종", 1), ("톱니 폭풍", 6), ("러시안 태엽", 10));
        yield return ShowDial(1.8f);
        Caption("[열두 번의 타종]", "한 칸에 종이 세 번. 열두 번째 종이 울리면", 3.5f);
        bm.EndPlayerTurn();
        yield return Until(() => bm.Phase == BattleManager.BattlePhase.PlayerTurn || bm.Phase == BattleManager.BattlePhase.End, 60f);
        yield return Wait(0.5f);

        // 5) 둘째 라운드 — 즉발 + 걸작 태엽판
        if (bm.Phase == BattleManager.BattlePhase.PlayerTurn)
        {
            Caption("즉발 태엽판", "시계판에 놓지 않고 누르는 순간 터진다");
            yield return Wait(0.6f);
            yield return ClickCard(bm, "증기 폭발");
            yield return Wait(1.4f);
            yield return PlaceRoutine(bm, ("자정의 일격", 1), ("그믐달 베기", 7), ("마지막 판돈", 10));
            yield return ShowDial(1.4f);
            Caption("태엽판마다 다른 연출", "자정의 시계판 · 그믐달 · 쏟아지는 동전", 3.5f);
            bm.EndPlayerTurn();
            yield return Until(() => bm.Phase == BattleManager.BattlePhase.PlayerTurn || bm.Phase == BattleManager.BattlePhase.End, 60f);
            yield return Wait(0.5f);
        }

        // 6) 셋째 라운드 — 자동 배치
        if (bm.Phase == BattleManager.BattlePhase.PlayerTurn)
        {
            Caption("[자동 배치]", "급할 땐 A 한 번이면 손패가 알아서 시계판으로");
            yield return Wait(1f);
            bm.AutoPlace();
            yield return ShowDial(1.4f);
            bm.EndPlayerTurn();
            yield return Until(() => bm.Phase == BattleManager.BattlePhase.PlayerTurn || bm.Phase == BattleManager.BattlePhase.End, 60f);
        }

        // 7) 마무리: 남은 적을 쓰러뜨리고 승리 화면
        foreach (Unit e in new List<Unit>(bm.Enemies)) if (e != null && e.IsAlive) bm.DebugKill(e);
        yield return Until(() => VictoryScreen.IsOpen, 10f);
        Caption("쓰러진 적의 기억을 삼킨다", "강하지만 침식이 붙는다. 덱이 커질수록 뒤틀린다");
        yield return Wait(3.4f);

        // 사전
        CompendiumPanel.Open(panelParent);
        Caption("태엽판 140여 장 · 적 30여 종", "특성 17가지: 가시, 분노, 수호, 야행, 매복, 저주…", 3.6f);
        for (float t = 0f; t < 2.2f; t += Time.unscaledDeltaTime) { CompendiumPanel.ScrollList(1f - t / 2.2f * 0.6f); yield return null; }
        CompendiumPanel.ShowEnemies(true);
        for (float t = 0f; t < 2.2f; t += Time.unscaledDeltaTime) { CompendiumPanel.ScrollList(1f - t / 2.2f * 0.5f); yield return null; }

        // 끝 화면
        bigTitle.text = gm != null ? gm.gameTitle : "CLOCKWORK ROLL";
        bigSub.text = "스토리 모드 · 무한 모드 · 온라인 랭킹";
        yield return FadeBlack(1f, 0.7f);
        CompendiumPanel.Close();
        yield return Wait(3f);
        yield return FadeBlack(1f, 0.1f);

        yield return Finish();
    }

    private static void AddFoe(BattleStageData stage, Catalog cat, string name, Vector3 pos)
    {
        if (cat.enemies.TryGetValue(name, out EnemyData e)) stage.enemies.Add(new EnemySpawnInfo { enemy = e, spawnPosition = pos });
    }

    /// <summary>손패에서 이름으로 태엽판을 골라 그 시각에 놓는다 (클릭과 같은 길로)</summary>
    private IEnumerator PlaceRoutine(BattleManager bm, params (string name, int hour)[] plan)
    {
        foreach (var p in plan)
        {
            CardInstance card = FindInHand(bm, p.name);
            if (card == null || card.view == null) continue;
            // 천천히: 카드를 고르고 → 시계판이 열린 채 잠깐 머물고 → 놓은 뒤 결과를 보여 준다
            bm.OnCardClicked(card.view);
            yield return Wait(1.1f);
            bm.OnHourClicked(p.hour);
            yield return Wait(0.9f);
        }
    }

    private IEnumerator ClickCard(BattleManager bm, string name)
    {
        CardInstance card = FindInHand(bm, name);
        if (card != null && card.view != null) bm.OnCardClicked(card.view);
        yield return null;
    }

    private static CardInstance FindInHand(BattleManager bm, string name)
    {
        if (bm.handManager == null) return null;
        foreach (CardInstance c in bm.handManager.Hand)
            if (c != null && c.data != null && c.data.cardName == name) return c;
        return null;
    }

    private static IEnumerator ShowDial(float seconds)
    {
        ClockDial.Open();
        yield return Wait(seconds);
        ClockDial.Close();
        yield return Wait(0.3f);
    }

    private IEnumerator Finish()
    {
        StopRecording();
        Time.captureFramerate = 0;
        RouteScreen.PreferFight = false;
        TutorialCoach.Enabled = tutorialWas;
#if UNITY_EDITOR
        EditorApplication.ExitPlaymode();
#endif
        Destroy(gameObject);
        yield break;
    }

    void OnDestroy()
    {
        StopRecording();
        Time.captureFramerate = 0;
        RouteScreen.PreferFight = false;
    }

    // ─────────────────────────── 자료 찾기 ───────────────────────────

    private class Catalog
    {
        public readonly Dictionary<string, CardData> cards = new Dictionary<string, CardData>();
        public readonly Dictionary<string, EnemyData> enemies = new Dictionary<string, EnemyData>();

        public static Catalog Build(GameManager gm)
        {
            Catalog c = new Catalog();
            StoryData story = gm != null ? gm.story : null;
            if (story == null) return c;
            void Card(CardData d) { if (d != null && !c.cards.ContainsKey(d.cardName)) c.cards[d.cardName] = d; }
            void Stage(BattleStageData s)
            {
                if (s == null) return;
                foreach (EnemySpawnInfo i in s.enemies)
                {
                    if (i == null || i.enemy == null) continue;
                    c.enemies[i.enemy.enemyName] = i.enemy;
                    foreach (CardData d in i.enemy.deck) Card(d);
                }
            }
            foreach (CardData d in story.startingDeck) Card(d);
            foreach (CardData d in story.rewardPool) Card(d);
            foreach (ChapterData ch in story.chapters)
            {
                if (ch == null) continue;
                foreach (CardData d in ch.rewardCards) Card(d);
                Stage(ch.battle);
                foreach (BattleStageData s in ch.routeFights) Stage(s);
                foreach (BattleStageData s in ch.routeElites) Stage(s);
            }
            return c;
        }
    }

    // ─────────────────────────── 녹화 (에디터 전용) ───────────────────────────

#if UNITY_EDITOR
    private MediaEncoder encoder;
    private Texture2D frame;
    private int width, height, channels;
    private NativeArray<float> audioBuffer;
    private Coroutine captureJob;
    private string outPath;
#endif

    private void BeginRecording()
    {
#if UNITY_EDITOR
        width = Screen.width & ~1;
        height = Screen.height & ~1;
        channels = AudioSettings.speakerMode == AudioSpeakerMode.Mono ? 1 : 2;
        string dir = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, "Recordings");
        System.IO.Directory.CreateDirectory(dir);
        outPath = System.IO.Path.Combine(dir, "ClockworkRoll_Trailer.mp4");
        if (System.IO.File.Exists(outPath)) System.IO.File.Delete(outPath);

        VideoTrackAttributes video = new VideoTrackAttributes
        {
            frameRate = new MediaRational(Fps),
            width = (uint)width,
            height = (uint)height,
            includeAlpha = false,
            bitRateMode = VideoBitrateMode.High,
        };
        AudioTrackAttributes audio = new AudioTrackAttributes
        {
            sampleRate = new MediaRational(AudioSettings.outputSampleRate),
            channelCount = (ushort)channels,
            language = "ko",
        };
        encoder = new MediaEncoder(outPath, video, audio);
        frame = new Texture2D(width, height, TextureFormat.RGBA32, false);
        AudioRenderer.Start();
        captureJob = StartCoroutine(CaptureLoop());
        Debug.Log($"홍보영상 녹화 시작 — {width}x{height} {Fps}fps → {outPath}");
#endif
    }

#if UNITY_EDITOR
    private IEnumerator CaptureLoop()
    {
        WaitForEndOfFrame eof = new WaitForEndOfFrame();
        while (encoder != null)
        {
            yield return eof;
            if (encoder == null) break;
            Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
            if (shot != null)
            {
                if (shot.width == width && shot.height == height)
                {
                    frame.SetPixels32(shot.GetPixels32());
                    frame.Apply(false);
                }
                Destroy(shot);
                encoder.AddFrame(frame);   // 크기가 바뀐 프레임은 앞 장면을 한 번 더 (영상 길이가 어긋나지 않게)
            }

            int samples = AudioRenderer.GetSampleCountForCaptureFrame();
            if (samples > 0)
            {
                int need = samples * channels;
                if (!audioBuffer.IsCreated || audioBuffer.Length != need)
                {
                    if (audioBuffer.IsCreated) audioBuffer.Dispose();
                    audioBuffer = new NativeArray<float>(need, Allocator.Persistent);
                }
                AudioRenderer.Render(audioBuffer);
                encoder.AddSamples(audioBuffer);
            }
        }
    }
#endif

    private void StopRecording()
    {
#if UNITY_EDITOR
        if (encoder == null) return;
        if (captureJob != null) StopCoroutine(captureJob);
        AudioRenderer.Stop();
        encoder.Dispose();
        encoder = null;
        if (audioBuffer.IsCreated) audioBuffer.Dispose();
        if (frame != null) Destroy(frame);
        Debug.Log($"홍보영상 저장 완료 → {outPath}");
        EditorUtility.RevealInFinder(outPath);
#endif
    }
}
