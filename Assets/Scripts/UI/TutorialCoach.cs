using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 튜토리얼 — 처음 하는 사람에게 규칙을 "그 일이 처음 일어나는 순간"에 하나씩 알려 준다.
///
/// 한꺼번에 설명하지 않는다: 시계판 → 시각 고르기와 합 → 기력 → 시계 진행 → 장력 → 아침 · 밤 → 기억 삼키기 → 지도 …
/// 순서로, 그 장면이 처음 나왔을 때만 화면 위쪽에 설명 상자가 뜬다.
/// 상자가 떠 있는 동안은 게임이 멈추고(timeScale 0) 화면이 회색으로 덮인다 — [알겠어](또는 Enter · Space · F1)를 눌러야 이어진다.
/// 한 번 본 설명은 다시 뜨지 않는다 (PlayerPrefs). 상자의 [튜토리얼 끄기] 또는 멈춤 메뉴에서 끄고 켠다.
/// 앞 단계를 보기 전에는 뒤 단계가 뜨지 않는다 — 정말로 한 번에 하나씩.
///
/// GameManager가 Ensure로 만든다 (씬에 둘 필요 없음).
/// </summary>
public class TutorialCoach : MonoBehaviour
{
    private static TutorialCoach instance;

    private const string OffKey = "tut.off", SeenPrefix = "tut.seen.";

    /// <summary>튜토리얼을 켜 두었는가 (기본 켬)</summary>
    public static bool Enabled
    {
        get { try { return PlayerPrefs.GetInt(OffKey, 0) == 0; } catch { return true; } }
        set
        {
            try { PlayerPrefs.SetInt(OffKey, value ? 0 : 1); PlayerPrefs.Save(); } catch { }
            if (!value && instance != null) instance.Hide();
        }
    }

    /// <summary>본 설명을 모두 잊는다 (멈춤 메뉴의 "처음부터 다시 보기")</summary>
    public static void ResetProgress()
    {
        try
        {
            foreach (Step s in Steps) PlayerPrefs.DeleteKey(SeenPrefix + s.id);
            PlayerPrefs.SetInt(OffKey, 0);
            PlayerPrefs.Save();
        }
        catch { }
    }

    private class Step
    {
        public string id, title, body;
        public Func<bool> when;     // 이 장면이 지금 일어나고 있는가
        public bool ordered = true; // 앞 단계를 다 본 뒤에만 (false = 순서와 상관없이 그 장면에서)
    }

    // ─────────────────────────── 단계 ───────────────────────────

    private static BattleManager Bm { get { return BattleManager.Instance; } }
    private static bool InBattle
    {
        get { return GameManager.Instance != null && GameManager.Instance.State == GameManager.GameState.Battle && Bm != null && !VictoryScreen.IsOpen; }
    }
    private static bool PlayerTurn { get { return InBattle && Bm.Phase == BattleManager.BattlePhase.PlayerTurn && !Bm.IsBusy; } }

    private static readonly List<Step> Steps = new List<Step>
    {
        new Step
        {
            id = "dial", title = "시계판과 태엽판",
            body = "아래 손패의 <b>태엽판</b>(카드)을 골라 가운데 <b>시계판</b>의 시각에 놓는다.\n" +
                   "적이 이번 라운드에 놓은 칸도 시계판에 보인다. 적에게 마우스를 올리면 정보가 나온다.",
            when = () => PlayerTurn,
        },
        new Step
        {
            id = "clash", title = "시각 고르기 · 합",
            body = "태엽판을 골랐다면 시계판의 시각을 눌러 놓는다.\n" +
                   "적의 칸과 <b>같은 시각</b>에 겹치면 <b>합</b>이 된다. 서로 주사위를 굴려 위력이 높은 쪽만 효과를 낸다.\n" +
                   "빈 시각에 놓으면 막히지 않고 <b>일방 공격</b>. 다시 누르면 선택 취소.",
            when = () => PlayerTurn && Bm.PendingCard != null,
        },
        new Step
        {
            id = "ki", title = "기력",
            body = "태엽판을 놓을 때마다 왼쪽 아래 <b>구슬(기력)</b>이 빠진다. 구슬이 모자라면 그 태엽판은 어둡게 잠긴다.\n" +
                   "기력은 라운드마다 다시 차고, 남은 기력은 넘어가지 않으니 아끼지 말고 쓰자.",
            when = () => PlayerTurn && Bm.CurrentKi < Bm.MaxKi,
        },
        new Step
        {
            id = "advance", title = "시계 진행",
            body = "다 놓았으면 <b>시계 진행 ▶</b>(또는 Space). 바늘이 1시부터 돌며 놓인 칸들이 차례로 굴러간다.\n" +
                   "낼 수 있는 태엽판이 없으면 버튼 둘레가 빛난다.",
            when = () => PlayerTurn && Bm.CurrentKi < Bm.MaxKi,
        },
        new Step
        {
            id = "tension", title = "장력",
            body = "서로의 칸이 맞부딪힐 때마다 기력 위의 <b>장력 게이지</b>가 차오른다.\n" +
                   "한 칸을 채울 때마다 단계가 오르고, 이기면 삼킬 수 있는 기억이 늘고 장력이 필요한 태엽판을 낼 수 있다.",
            when = () => InBattle && Bm.TensionPoints > 0,
        },
        new Step
        {
            id = "victory", title = "기억 삼키기 · 침식",
            body = "이긴 뒤엔 쓰러진 적의 태엽판(기억)을 골라 덱에 삼킬 수 있다. 강하지만 <b>침식</b>이 붙는다.\n" +
                   "같은 기억을 또 삼키면 침식이 깊어지고, 침식된 태엽판은 가끔 뜻대로 움직이지 않는다.",
            when = () => VictoryScreen.IsOpen,
        },
        new Step
        {
            id = "route", title = "갈래길 지도",
            body = "큰 싸움 사이엔 지도를 걷는다. 칸마다 <b>전투 · 강적 · 이벤트 · 휴식 · 보물</b>이 있다.\n" +
                   "칸에 마우스를 올리면 무엇이 있는지 보인다. 꼭대기의 보스까지 길을 골라 오르자.",
            when = () => RouteScreen.IsOpen && !LoadingScreen.IsOpen,
        },
        new Step
        {
            id = "time", title = "아침 · 밤", ordered = false,
            body = "<b>[아침] · [밤]</b>이 붙은 태엽판을 골랐다. 이제 시계판이 1~6시는 <b>아침</b>, 7~12시는 <b>밤</b>으로 나뉘어 보인다.\n" +
                   "맞는 시간대에 놓으면 위력이 오른다. 관련 부품도 있다.",
            when = () => PlayerTurn && ClockDial.UsesTime(Bm.PendingCard),
        },
        new Step
        {
            id = "endless", title = "무한 모드", ordered = false,
            body = "스토리 없이 지도와 보스를 끝없이 되풀이한다. <b>회차가 오를수록 적의 체력과 위력이 오른다.</b>\n" +
                   "꼭대기의 보스는 <b>???</b>. 싸워 보기 전엔 정체를 알 수 없다. 보스를 넘길 때마다 체력을 조금 회복한다.",
            when = () => GameManager.Instance != null && GameManager.Instance.IsEndless && RouteScreen.IsOpen && !LoadingScreen.IsOpen,
        },
    };

    // ─────────────────────────── 화면 ───────────────────────────

    private GameObject root, box;
    private Button okButton;
    private bool holding;          // 이 상자가 시간을 멈춰 두고 있는가
    private int closedFrame = -1;

    /// <summary>설명 상자가 떠 있는가 (닫은 그 프레임도 포함 — 알겠어를 누른 클릭이 뒤 화면까지 넘기지 않게)</summary>
    public static bool IsOpen
    {
        get { return instance != null && ((instance.showing != null && instance.root.activeSelf) || Time.frameCount == instance.closedFrame); }
    }
    private Text titleText, bodyText, stepText;
    private Step showing;
    private float shownAt, cooldown;

    public static void Ensure()
    {
        if (instance != null) return;
        GameObject go = new GameObject("TutorialCanvas");
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 640;   // 승리 화면(600) 위, 시작 화면(900) 아래
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        instance = go.AddComponent<TutorialCoach>();
        instance.Build(go.transform);
    }

    private void Build(Transform canvas)
    {
        Vector2 top = new Vector2(0.5f, 1f);
        // 회색 막: 게임 화면을 흐리게 덮고, 뒤를 누르지 못하게 막는다
        Image shade = UIBuilder.Panel("Shade", canvas, new Color(0.16f, 0.16f, 0.18f, 0.62f), false);
        shade.sprite = null;
        UIBuilder.Stretch(shade.rectTransform);
        shade.raycastTarget = true;
        UIPop.Ensure(shade.gameObject, 1f, 0.16f);
        root = shade.gameObject;

        Image bg = UIBuilder.Panel("Box", shade.transform, UITheme.Window);
        UIBuilder.Place(bg.rectTransform, top, top, top, new Vector2(0f, -86f), new Vector2(760f, 196f));
        UITheme.Elevate(bg, 0.7f);
        UIPop.Ensure(bg.gameObject, 0.95f, 0.14f);
        box = bg.gameObject;

        Image edge = UIBuilder.Panel("Edge", bg.transform, UITheme.Gold, false);
        edge.sprite = null;
        edge.raycastTarget = false;
        UIBuilder.Place(edge.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(5f, 0f));

        stepText = UIBuilder.Label("Step", bg.transform, "", 16, UITheme.TextMuted, TextAnchor.UpperRight);
        UIBuilder.Place(stepText.rectTransform, Vector2.one, Vector2.one, Vector2.one, new Vector2(-18f, -14f), new Vector2(200f, 22f));

        titleText = UIBuilder.Label("Title", bg.transform, "", 26, UITheme.Brass, TextAnchor.UpperLeft);
        titleText.fontStyle = FontStyle.Bold;
        UIBuilder.Place(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(26f, -14f), new Vector2(-250f, 34f));

        bodyText = UIBuilder.Label("Body", bg.transform, "", 19, UITheme.TextMain, TextAnchor.UpperLeft);
        bodyText.supportRichText = true;
        bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        bodyText.verticalOverflow = VerticalWrapMode.Overflow;
        bodyText.lineSpacing = 1.1f;
        UIBuilder.Place(bodyText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(26f, -54f), new Vector2(-52f, 90f));

        Button ok = UIBuilder.Button("Ok", bg.transform, "알겠어", UITheme.BrassFill, 20);
        UITheme.StyleButton(ok, true, 20);
        UIBuilder.Place((RectTransform)ok.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-18f, 14f), new Vector2(150f, 40f));
        ok.onClick.AddListener(Dismiss);
        okButton = ok;

        Button off = UIBuilder.Button("Off", bg.transform, "튜토리얼 끄기", UITheme.InkSolid, 17);
        UITheme.StyleButton(off, false, 17);
        UIBuilder.Place((RectTransform)off.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-180f, 14f), new Vector2(150f, 40f));
        off.onClick.AddListener(() => Enabled = false);

        root.SetActive(false);
    }

    private void Hide()
    {
        showing = null;
        if (root != null) root.SetActive(false);
        Release();
    }

    /// <summary>멈춰 둔 시간을 돌려준다 (멈춤 메뉴가 열려 있으면 그쪽이 계속 멈춰 둔다)</summary>
    private void Release()
    {
        if (!holding) return;
        holding = false;
        if (!PauseMenu.IsOpen) Time.timeScale = 1f;
    }

    private void Hold()
    {
        holding = true;
        Time.timeScale = 0f;   // 히트스톱 등이 1로 돌려놓아도 매 프레임 다시 멈춘다
    }

    private void Dismiss()
    {
        if (showing != null) MarkSeen(showing);
        closedFrame = Time.frameCount;
        Hide();
        cooldown = 0.6f;   // 다음 설명이 곧바로 겹쳐 뜨지 않게 숨 돌릴 틈
        Sfx.Play(SfxId.Tock);
    }

    private static bool Seen(Step s) { try { return PlayerPrefs.GetInt(SeenPrefix + s.id, 0) == 1; } catch { return true; } }
    private static void MarkSeen(Step s) { try { PlayerPrefs.SetInt(SeenPrefix + s.id, 1); PlayerPrefs.Save(); } catch { } }

    /// <summary>다른 창이 덮고 있을 때는 설명을 띄우지 않는다</summary>
    private static bool Blocked
    {
        get
        {
            return TitleScreen.IsOpen || LoadingScreen.IsOpen || PauseMenu.IsOpen || DeckViewer.IsOpen ||
                   (GameManager.Instance != null && GameManager.Instance.dialogueManager != null && GameManager.Instance.dialogueManager.IsPlaying);
        }
    }

    void Update()
    {
        if (!Enabled || Blocked)
        {
            if (root.activeSelf) root.SetActive(false);   // 잠깐 가린다 (본 것으로 치지 않는다)
            Release();
            return;
        }

        if (showing != null)
        {
            if (!root.activeSelf) root.SetActive(true);
            Hold();
            // 키보드로도 넘긴다 (게임이 멈춰 있으니 Enter · Space가 뒤로 새지 않는다)
            if (Time.unscaledTime - shownAt > 0.6f &&
                (Input.GetKeyDown(KeyCode.F1) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)))
                Dismiss();
            // 뒤 화면의 버튼이 키보드 선택을 쥐고 있으면 Space · Enter가 그 버튼을 누른다 — 알겠어로 옮겨 둔다
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (showing != null && es != null && okButton != null && es.currentSelectedGameObject != okButton.gameObject)
                es.SetSelectedGameObject(okButton.gameObject);
            return;
        }

        if (cooldown > 0f) { cooldown -= Time.unscaledDeltaTime; return; }

        bool earlierUnseen = false;
        for (int i = 0; i < Steps.Count; i++)
        {
            Step s = Steps[i];
            bool seen = Seen(s);
            if (!seen && (!s.ordered || !earlierUnseen) && SafeWhen(s)) { Show(s, i); return; }
            if (!seen && s.ordered) earlierUnseen = true;
        }
    }

    private static bool SafeWhen(Step s)
    {
        try { return s.when(); } catch { return false; }
    }

    private void Show(Step s, int index)
    {
        showing = s;
        shownAt = Time.unscaledTime;
        titleText.text = s.title;
        bodyText.text = s.body;
        int total = Steps.FindAll(x => x.ordered).Count;
        stepText.text = s.ordered ? $"배우기 {Steps.FindAll(x => x.ordered).IndexOf(s) + 1} / {total}  ·  F1 넘기기" : "알아 두기  ·  F1 넘기기";
        root.SetActive(true);
        Hold();
        Sfx.Play(SfxId.Tock);
    }

    void OnDestroy()
    {
        if (instance == this) { Release(); instance = null; }
    }
}
