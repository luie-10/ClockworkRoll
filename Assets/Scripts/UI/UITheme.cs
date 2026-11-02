using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 한 벌의 색/글자 규칙 (먹색 반투명 패널 + 얇은 황동 테두리 + 절제된 금색 강조).
///
/// 씬에 저장된 UI(무대 배너 · 자원 패널 · 대사창 · 결과 화면 등)는 에디터 자동 세팅으로 만들어진 것이라,
/// 실행 시 ApplyToScene()이 이름으로 찾아 같은 규칙으로 다시 칠하고 자리를 잡는다.
/// 코드로 만드는 UI(시계판 · 툴팁 · 덱 보기 · 승리 화면)는 UIBuilder가 이 색을 그대로 쓴다.
/// </summary>
public static class UITheme
{
    // ── 색 ──
    public static readonly Color Ink = new Color(0.055f, 0.055f, 0.07f, 0.86f);        // 기본 패널
    public static readonly Color InkSolid = new Color(0.07f, 0.07f, 0.085f, 0.97f);    // 카드 · 이름표처럼 또렷해야 하는 판
    public static readonly Color InkDeep = new Color(0.02f, 0.02f, 0.03f, 0.88f);      // 화면 전체 덮개
    public static readonly Color Brass = new Color(0.86f, 0.69f, 0.42f);                // 강조 글자 · 제목
    public static readonly Color BrassLine = new Color(0.86f, 0.69f, 0.42f, 0.42f);     // 패널 테두리
    public static readonly Color BrassFill = new Color(0.72f, 0.54f, 0.26f);            // 주 버튼
    public static readonly Color TextMain = new Color(0.94f, 0.92f, 0.88f);
    public static readonly Color TextMuted = new Color(0.64f, 0.62f, 0.58f);
    public static readonly Color TextOnBrass = new Color(0.09f, 0.07f, 0.05f);
    public static readonly Color Ki = new Color(0.52f, 0.84f, 1f);
    public static readonly Color Tension = new Color(1f, 0.44f, 0.44f);
    public static readonly Color Danger = new Color(0.62f, 0.2f, 0.18f);

    public const float BorderWidth = 1.5f;

    // ─────────────────────────── 부품 칠하기 ───────────────────────────

    /// <summary>둥근 상자 + 얇은 테두리 (border가 null이면 테두리 없음)</summary>
    public static void StylePanel(Image img, Color fill, Color? border)
    {
        if (img == null) return;
        img.sprite = UIBuilder.BoxSpriteOrDefault;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1.6f;
        img.color = fill;

        Outline o = img.GetComponent<Outline>();
        if (border == null)
        {
            if (o != null) o.enabled = false;
            return;
        }
        if (o == null) o = img.gameObject.AddComponent<Outline>();
        o.enabled = true;
        o.effectColor = border.Value;
        o.effectDistance = new Vector2(BorderWidth, -BorderWidth);
    }

    /// <summary>글자: 두꺼운 검은 외곽선 대신 옅은 그림자 (외곽선은 한글을 뭉개 보이게 한다)</summary>
    public static void StyleText(Text t, int size, Color color, FontStyle style = FontStyle.Normal)
    {
        if (t == null) return;
        t.fontSize = size;
        t.color = color;
        t.fontStyle = style;
        SoftShadow(t);
    }

    public static void SoftShadow(Text t)
    {
        if (t == null) return;
        Outline o = t.GetComponent<Outline>();
        if (o != null) Object.Destroy(o);

        Shadow s = null;
        foreach (Shadow c in t.GetComponents<Shadow>())
        {
            if (c.GetType() == typeof(Shadow)) { s = c; break; }
        }
        if (s == null) s = t.gameObject.AddComponent<Shadow>();
        s.effectColor = new Color(0f, 0f, 0f, 0.6f);
        s.effectDistance = new Vector2(1f, -1.5f);
    }

    /// <summary>primary = 황동 버튼(어두운 글자) / 아니면 먹색 버튼(황동 테두리)</summary>
    public static void StyleButton(Button b, bool primary, int fontSize)
    {
        if (b == null) return;
        Image img = b.targetGraphic as Image;
        StylePanel(img, primary ? BrassFill : InkSolid, primary ? (Color?)new Color(1f, 0.85f, 0.55f, 0.6f) : BrassLine);

        ColorBlock cb = b.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.18f, 1.18f, 1.18f);
        cb.selectedColor = Color.white;
        cb.pressedColor = new Color(0.82f, 0.82f, 0.82f);
        cb.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        cb.fadeDuration = 0.08f;
        b.colors = cb;

        Text label = b.GetComponentInChildren<Text>(true);
        StyleText(label, fontSize, primary ? TextOnBrass : TextMain, FontStyle.Bold);
        if (primary && label != null)
        {
            Shadow s = label.GetComponent<Shadow>();
            if (s != null) s.effectColor = new Color(1f, 0.9f, 0.7f, 0.35f);   // 밝은 바탕 위에선 검은 그림자가 지저분하다
        }
    }

    // ─────────────────────────── 씬 UI 다시 칠하기 ───────────────────────────

    /// <summary>에디터 자동 세팅으로 만든 씬 UI 묶음(RuinaUI)을 새 규칙으로 정리한다. BattleHUD가 시작할 때 부른다.</summary>
    public static void ApplyToScene(Transform root)
    {
        if (root == null) return;

        // 0) 모든 글자의 두꺼운 외곽선 → 옅은 그림자
        foreach (Text t in root.GetComponentsInChildren<Text>(true)) SoftShadow(t);

        // 1) 상단 가운데: 무대 이름 + 단계 표시
        Image banner = FindImage(root, "StageBanner");
        if (banner != null)
        {
            StylePanel(banner, Ink, BrassLine);
            Place(banner.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(460f, 54f));
            StyleText(FindText(banner.transform, "StageNameText"), 28, Brass, FontStyle.Bold);
        }
        Text phase = FindText(root, "PhaseText");
        if (phase != null)
        {
            StyleText(phase, 22, TextMain);
            Place(phase.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(600f, 34f));
        }

        // 2) 우상단: 기력 / 장력
        Image res = FindImage(root, "ResourcePanel");
        if (res != null)
        {
            StylePanel(res, Ink, BrassLine);
            Place(res.rectTransform, Vector2.one, new Vector2(-20f, -18f), new Vector2(340f, 124f));

            Text ki = FindText(res.transform, "KiText");
            StyleText(ki, 36, Ki, FontStyle.Bold);
            if (ki != null) Inset(ki.rectTransform, new Vector2(20f, -10f), new Vector2(-40f, 46f));

            Text emo = FindText(res.transform, "EmotionText");
            StyleText(emo, 19, TextMain);
            if (emo != null) Inset(emo.rectTransform, new Vector2(20f, -60f), new Vector2(-40f, 26f));

            Image gauge = FindImage(res.transform, "EmotionGauge");
            if (gauge != null)
            {
                StylePanel(gauge, new Color(0f, 0f, 0f, 0.55f), null);
                Inset(gauge.rectTransform, new Vector2(20f, -94f), new Vector2(-40f, 12f));
                Image fill = FindImage(gauge.transform, "Fill");
                if (fill != null) { fill.color = Tension; UIBuilder.Stretch(fill.rectTransform, 2f); }
            }
        }

        // 3) 하단: 손패 받침대 + 시계 진행 버튼
        Image tray = FindImage(root, "HandTray");
        if (tray != null)
        {
            tray.sprite = ProceduralSprites.VerticalFade();   // 위로 갈수록 투명해지는 그늘 (딱딱한 경계선 없이)
            tray.type = Image.Type.Simple;
            tray.color = new Color(0f, 0f, 0f, 0.7f);
            Outline o = tray.GetComponent<Outline>();
            if (o != null) o.enabled = false;
        }
        Transform endTurn = FindDeep(root, "EndTurnButton");
        if (endTurn != null)
        {
            StyleButton(endTurn.GetComponent<Button>(), true, 28);
            Place((RectTransform)endTurn, new Vector2(1f, 0f), new Vector2(-28f, 300f), new Vector2(240f, 72f));
        }

        // 4) 대사창
        Image dlg = FindImage(root, "DialoguePanel");
        if (dlg != null)
        {
            StylePanel(dlg, new Color(0.035f, 0.035f, 0.045f, 0.9f), BrassLine);
            RectTransform rt = dlg.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = new Vector2(1f, 0f); rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 24f);
            rt.sizeDelta = new Vector2(-160f, 232f);

            // 위치는 '초상화가 있을 때' 기준으로 잡는다 — 초상화가 없는 줄이면 DialogueManager가
            // 이름표와 대사를 초상화 폭만큼 왼쪽으로 당긴다 (여기서 미리 당겨 두면 두 번 당겨져 창 밖으로 나간다)
            float portraitRoom = PortraitWidth(dlg.transform) + 20f;

            Image plate = FindImage(dlg.transform, "SpeakerPlate");
            if (plate != null)
            {
                StylePanel(plate, InkSolid, new Color(0.86f, 0.69f, 0.42f, 0.7f));
                RectTransform p = plate.rectTransform;
                p.anchorMin = p.anchorMax = new Vector2(0f, 1f);
                p.pivot = new Vector2(0f, 0.5f);
                p.anchoredPosition = new Vector2(36f + portraitRoom, 0f);
                p.sizeDelta = new Vector2(220f, 48f);
                StyleText(FindText(plate.transform, "SpeakerNameText"), 26, Brass, FontStyle.Bold);
            }
            Text body = FindText(dlg.transform, "BodyText");
            if (body != null)
            {
                StyleText(body, 30, TextMain);
                body.lineSpacing = 1.15f;
                RectTransform b = body.rectTransform;
                float left = 56f + portraitRoom;
                b.anchorMin = Vector2.zero; b.anchorMax = Vector2.one; b.pivot = new Vector2(0f, 1f);
                b.offsetMin = new Vector2(left, 44f);
                b.offsetMax = new Vector2(-56f, -46f);
            }
            Text hint = FindText(dlg.transform, "ClickHint");
            if (hint != null) StyleText(hint, 17, TextMuted);
        }

        // 5) 결과 화면 (패배 / 승리)
        Image result = FindImage(root, "ResultPanel");
        if (result != null)
        {
            result.sprite = null;
            result.color = InkDeep;
            Outline o = result.GetComponent<Outline>();
            if (o != null) o.enabled = false;

            Text title = FindText(result.transform, "TitleText");
            StyleText(title, 88, Brass, FontStyle.Bold);
            if (title != null) title.rectTransform.anchoredPosition = new Vector2(0f, 110f);
            Text msg = FindText(result.transform, "MessageText");
            StyleText(msg, 24, TextMuted);
            if (msg != null) msg.rectTransform.anchoredPosition = new Vector2(0f, 26f);

            Transform restart = FindDeep(result.transform, "RestartButton");
            if (restart != null)
            {
                StyleButton(restart.GetComponent<Button>(), true, 28);
                ((RectTransform)restart).anchoredPosition = new Vector2(0f, -70f);
                ((RectTransform)restart).sizeDelta = new Vector2(280f, 72f);
            }
        }
    }

    // ─────────────────────────── 도우미 ───────────────────────────

    /// <summary>대사창 초상화 칸의 폭 (DialogueManager가 초상화 없는 줄에서 당기는 거리 - 20)</summary>
    private static float PortraitWidth(Transform dialogue)
    {
        Transform p = FindDeep(dialogue, "PortraitImage");
        return p != null ? ((RectTransform)p).sizeDelta.x : 0f;
    }

    /// <summary>anchor 한 점 기준 배치 (pivot = anchor)</summary>
    private static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    /// <summary>부모 위쪽에 가로로 꽉 찬 줄 (좌우 여백 = -size.x / 2)</summary>
    private static void Inset(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name) return t;
        }
        return null;
    }

    private static Image FindImage(Transform parent, string name)
    {
        Transform t = FindDeep(parent, name);
        return t != null ? t.GetComponent<Image>() : null;
    }

    private static Text FindText(Transform parent, string name)
    {
        Transform t = FindDeep(parent, name);
        return t != null ? t.GetComponent<Text>() : null;
    }
}
