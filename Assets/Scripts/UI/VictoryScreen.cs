using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전투 승리 연출 + 흡수할 태엽판 고르기.
///
///   1) 화면 가운데 "VICTORY"가 크게 떴다가
///   2) 쓰러뜨린 적들의 태엽판이 카드로 펼쳐진다 → 장력 단계만큼 골라서 흡수 (1단계 = 1장, 2단계 = 2장 ...)
///      장력 0이면 아무것도 삼키지 못하고 넘어간다.
///   3) [흡수] 버튼 → 고른 태엽판이 덱에 ◈로 들어간다 (이미 가진 것이면 침식 +1)
///
/// 직접 붙일 필요 없음 — BattleManager가 승리 시 VictoryScreen.Play()로 띄운다 (전용 Canvas를 알아서 만든다).
/// </summary>
public class VictoryScreen : MonoBehaviour
{
    private static VictoryScreen instance;

    /// <summary>열려 있는 동안 true (테스트 자동 진행이 확인용으로 사용)</summary>
    public static bool IsOpen { get; private set; }

    private RectTransform root;
    private Text bannerText, subText, hintText;
    private RectTransform cardRow;
    private Button confirmButton;
    private Text confirmLabel;

    private readonly List<CardData> pool = new List<CardData>();
    private readonly List<CardData> picked = new List<CardData>();
    private readonly List<Image> cardFrames = new List<Image>();
    private int pickCount;
    private bool confirmed;

    private static readonly Color frameNormal = new Color(0.08f, 0.07f, 0.06f, 0.95f);
    private static readonly Color framePicked = new Color(0.42f, 0.3f, 0.08f, 0.98f);

    /// <summary>
    /// 승리 연출 → 고르기. 끝나면 onDone(고른 태엽판 목록)을 부른다.
    /// tension = 전투가 끝났을 때의 장력 단계 (= 고를 수 있는 장수). ownedSources = 이미 흡수한 태엽판 이름 (침식 경고용)
    /// </summary>
    public static Coroutine Play(MonoBehaviour host, int tension, List<CardData> spoils, ICollection<string> ownedSources,
                                 Action<List<CardData>> onDone)
    {
        if (instance == null) instance = Create();
        return host.StartCoroutine(instance.PlayRoutine(tension, spoils, ownedSources, onDone));
    }

    /// <summary>테스트 자동 진행용: 앞에서부터 고를 수 있는 만큼 골라 바로 흡수</summary>
    public static void AutoPick()
    {
        if (instance == null || !IsOpen) return;
        for (int i = 0; i < instance.pool.Count && instance.picked.Count < instance.pickCount; i++)
            instance.Toggle(i);
        instance.confirmed = true;
    }

    private IEnumerator PlayRoutine(int tension, List<CardData> spoils, ICollection<string> ownedSources,
                                    Action<List<CardData>> onDone)
    {
        IsOpen = true;
        confirmed = false;
        picked.Clear();
        pool.Clear();
        foreach (CardData c in spoils)
        {
            if (c != null && !pool.Contains(c)) pool.Add(c);   // 같은 태엽판은 한 번만 보여준다
        }
        pickCount = Mathf.Min(Mathf.Max(0, tension), pool.Count);

        root.gameObject.SetActive(true);
        cardRow.gameObject.SetActive(false);
        confirmButton.gameObject.SetActive(false);
        subText.text = "";
        hintText.text = "";

        // 1) VICTORY — 크게 튀어나왔다가 자리 잡는다
        bannerText.text = "VICTORY";
        RectTransform banner = bannerText.rectTransform;
        banner.anchoredPosition = new Vector2(0f, 0f);
        for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.5f;
            banner.localScale = Vector3.one * Mathf.Lerp(2.2f, 1f, 1f - Mathf.Pow(1f - k, 3f));
            bannerText.color = new Color(1f, 0.85f, 0.4f, k);
            yield return null;
        }
        banner.localScale = Vector3.one;
        yield return new WaitForSecondsRealtime(0.9f);

        // 2) 위로 올라가며 흡수 고르기 화면으로
        for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
        {
            banner.anchoredPosition = new Vector2(0f, Mathf.Lerp(0f, 300f, t / 0.3f));
            yield return null;
        }
        banner.anchoredPosition = new Vector2(0f, 300f);

        if (pickCount <= 0)
        {
            subText.text = pool.Count == 0
                ? "삼킬 기억이 없다."
                : "<color=#ff8a6b>장력이 0이라 아무 기억도 삼키지 못했다.</color>\n<size=22>(장력 1단계 = 1장, 2단계 = 2장 ... 싸우는 동안 적중시켜 장력을 올리자)</size>";
            yield return new WaitForSecondsRealtime(2.2f);
        }
        else
        {
            subText.text = $"쓰러뜨린 적의 기억을 삼킨다 — <color=#ffd159>장력 {tension}단계 → {pickCount}장</color> 고르기";
            BuildCards(ownedSources);
            cardRow.gameObject.SetActive(true);
            confirmButton.gameObject.SetActive(true);
            RefreshPicks();
            while (!confirmed) yield return null;
        }

        root.gameObject.SetActive(false);
        IsOpen = false;
        if (onDone != null) onDone(new List<CardData>(picked));
    }

    // ─────────────────────────── 카드 고르기 ───────────────────────────

    private void BuildCards(ICollection<string> ownedSources)
    {
        for (int i = cardRow.childCount - 1; i >= 0; i--) Destroy(cardRow.GetChild(i).gameObject);
        cardFrames.Clear();

        for (int i = 0; i < pool.Count; i++)
        {
            CardData card = pool[i];
            int index = i;

            Image frame = UIBuilder.Panel("Card " + card.cardName, cardRow, frameNormal);
            frame.rectTransform.sizeDelta = new Vector2(250f, 330f);
            frame.raycastTarget = true;
            Button button = frame.gameObject.AddComponent<Button>();
            button.targetGraphic = frame;
            button.onClick.AddListener(() => Toggle(index));
            cardFrames.Add(frame);

            string rarityHex = "#" + ColorUtility.ToHtmlStringRGB(DiceInfo.RarityColor(card.rarity));
            bool again = ownedSources != null && ownedSources.Contains(card.cardName);
            string text =
                $"<size=26><b>{card.cardName}</b></size>\n" +
                $"<color={rarityHex}>{DiceInfo.RarityName(card.rarity)}</color> · <color=#8cd9ff>기력 {card.cost}</color> · <color=#ffd159>{TimedAction.LengthOf(card)}칸</color>\n\n" +
                DiceInfo.Detail(card) +
                (again ? "\n\n<color=#d9a6ff>이미 삼킨 기억 — 침식 +1</color>" : "\n\n<color=#d9a6ff>◈ 흡수 · 침식 1</color>");

            Text label = UIBuilder.Label("Text", frame.transform, text, 19, Color.white, TextAnchor.UpperLeft);
            UIBuilder.Stretch(label.rectTransform, 14f);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 12;
            label.resizeTextMaxSize = 19;
            label.verticalOverflow = VerticalWrapMode.Truncate;
        }
    }

    private void Toggle(int index)
    {
        CardData card = pool[index];
        if (picked.Contains(card)) picked.Remove(card);
        else if (picked.Count < pickCount) picked.Add(card);
        else if (pickCount == 1) { picked.Clear(); picked.Add(card); }   // 1장만 고를 땐 바로 바꿔 고르기
        RefreshPicks();
    }

    private void RefreshPicks()
    {
        for (int i = 0; i < cardFrames.Count; i++)
        {
            bool on = picked.Contains(pool[i]);
            cardFrames[i].color = on ? framePicked : frameNormal;
            cardFrames[i].rectTransform.localScale = Vector3.one * (on ? 1.06f : 1f);
        }

        bool ready = picked.Count == pickCount;
        confirmButton.interactable = ready;
        confirmLabel.text = ready ? $"흡수 ({picked.Count}장)" : $"{picked.Count} / {pickCount}장 선택";
        hintText.text = "카드를 클릭해서 고르기 · 다시 클릭하면 취소";
    }

    // ─────────────────────────── 생성 ───────────────────────────

    private static VictoryScreen Create()
    {
        GameObject go = new GameObject("VictoryScreenCanvas");
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 600;   // 모든 전투 UI 위
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();

        VictoryScreen screen = go.AddComponent<VictoryScreen>();

        Image dim = UIBuilder.Panel("Dim", go.transform, new Color(0f, 0f, 0f, 0.72f), false);
        UIBuilder.Stretch(dim.rectTransform);
        dim.raycastTarget = true;   // 뒤의 전투 화면 클릭 막기
        screen.root = dim.rectTransform;

        screen.bannerText = UIBuilder.Label("Banner", dim.transform, "VICTORY", 140, new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter);
        UIBuilder.Place(screen.bannerText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200f, 200f));
        screen.bannerText.fontStyle = FontStyle.Bold;

        screen.subText = UIBuilder.Label("Sub", dim.transform, "", 30, Color.white, TextAnchor.MiddleCenter);
        UIBuilder.Place(screen.subText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 190f), new Vector2(1400f, 110f));

        // 카드 줄 (가운데 정렬, 장수에 따라 늘어남)
        screen.cardRow = UIBuilder.Rect("Cards", dim.transform);
        UIBuilder.Place(screen.cardRow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(1700f, 340f));
        HorizontalLayoutGroup row = screen.cardRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 22f;
        row.childAlignment = TextAnchor.MiddleCenter;
        row.childControlWidth = false;
        row.childControlHeight = false;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = false;

        screen.hintText = UIBuilder.Label("Hint", dim.transform, "", 22, new Color(0.75f, 0.75f, 0.75f), TextAnchor.MiddleCenter);
        UIBuilder.Place(screen.hintText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -220f), new Vector2(900f, 40f));

        screen.confirmButton = UIBuilder.Button("Confirm", dim.transform, "흡수", new Color(0.55f, 0.38f, 0.12f), 32);
        UIBuilder.Place((RectTransform)screen.confirmButton.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -300f), new Vector2(320f, 80f));
        screen.confirmLabel = screen.confirmButton.GetComponentInChildren<Text>();
        screen.confirmButton.onClick.AddListener(() => screen.confirmed = true);

        dim.gameObject.SetActive(false);
        return screen;
    }

    void OnDestroy()
    {
        if (instance == this) { instance = null; IsOpen = false; }
    }
}
