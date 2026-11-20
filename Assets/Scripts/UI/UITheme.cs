using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 한 벌의 색/글자 규칙 — 메인 컬러는 황동(Brass), 바탕은 푸른 기가 도는 먹색.
///
///   바탕   : Backdrop(화면 전체) · Ink(패널) · InkSolid(카드 · 버튼) · Window(창 · 정보창) · Raised(목록 칸)
///   메인   : Brass 한 색의 밝기 단계 (BrassBright > Brass > BrassButton > BrassFill > BrassDeep) + 테두리 BrassLine
///   글자   : TextMain · TextMuted · TextDim (검정 · 순백은 쓰지 않는다)
///   뜻의 색: Ki/Player(나 · 기력 = 파랑) · Enemy(적 = 주홍) · Good/Bad · Tension(장력 = 주황) · Absorb(흡수 · 침식 = 보라)
///            Gold(꼭 봐야 하는 숫자 · 선택) — 뜻이 있는 곳에만 쓰고 장식에는 쓰지 않는다
///
/// 화면마다 색을 새로 적지 말고 여기 이름을 쓴다. 글 안의 색(<color=...>)은 Hex* 를 쓴다.
/// 주사위 종류 · 등급의 색은 뜻이 따로 있는 분류 색이라 DiceInfo가 갖고 있다.
///
/// 씬에 저장된 UI(무대 배너 · 자원 패널 · 대사창 · 결과 화면 등)는 에디터 자동 세팅으로 만들어진 것이라,
/// 실행 시 ApplyToScene()이 이름으로 찾아 같은 규칙으로 다시 칠하고 자리를 잡는다.
/// 코드로 만드는 UI(시계판 · 툴팁 · 덱 보기 · 승리 화면)는 UIBuilder가 이 색을 그대로 쓴다.
/// </summary>
public static class UITheme
{
    // ── 바탕: 밤물빛 먹색 (검정에 푸른 기를 살짝 — 물에 잠긴 마을의 밤) ──
    public static readonly Color Backdrop = new Color(0.035f, 0.045f, 0.065f, 1f);      // 화면 전체를 덮는 바탕 (시작 · 로딩)
    public static readonly Color Ink = new Color(0.05f, 0.055f, 0.075f, 0.88f);         // 기본 패널 (뒤가 은은하게 비친다)
    public static readonly Color InkSolid = new Color(0.065f, 0.07f, 0.092f, 0.97f);    // 카드 · 버튼 · 이름표처럼 또렷해야 하는 판
    public static readonly Color Window = new Color(0.045f, 0.05f, 0.068f, 0.98f);      // 창 · 정보창
    public static readonly Color Raised = new Color(0.1f, 0.105f, 0.135f, 0.98f);       // 창 안에서 한 단 올라온 칸 (목록 · 시계판 칸)
    public static readonly Color InkDeep = new Color(0.02f, 0.022f, 0.032f, 0.9f);      // 화면 전체 덮개 (결과 · 지도)
    public static readonly Color Scrim = new Color(0f, 0f, 0f, 0.66f);                  // 창 뒤를 어둡게 가리는 막
    public static readonly Color Inset = new Color(0f, 0f, 0f, 0.34f);                  // 판 안에서 움푹 들어간 칸 · 게이지 바닥
    public static readonly Color Hairline = new Color(1f, 1f, 1f, 0.08f);               // 스크롤바 홈 같은 아주 옅은 선
    public static readonly Color Shadow = new Color(0f, 0f, 0f, 0.6f);                  // 글자 · 그림의 그림자

    // ── 메인 컬러: 황동 (한 색의 밝기 단계) ──
    public static readonly Color BrassBright = new Color(1f, 0.9f, 0.64f);              // 빛을 받은 황동 (지금 시각 · 큰 제목의 반짝임)
    public static readonly Color Brass = new Color(0.86f, 0.69f, 0.42f);                // 제목 · 강조 글자 · 문양
    public static readonly Color BrassButton = new Color(0.86f, 0.66f, 0.33f);          // 주 버튼의 면
    public static readonly Color BrassFill = new Color(0.72f, 0.54f, 0.26f);            // 눌린 황동 · 채움
    public static readonly Color BrassDeep = new Color(0.2f, 0.15f, 0.065f, 0.98f);     // 고른 것의 바탕 (어두운 황동 — 그 위의 글자가 4.5:1 이상 나오게 어둡게)
    public static readonly Color BrassLine = new Color(0.86f, 0.69f, 0.42f, 0.42f);     // 패널 테두리
    public static readonly Color Gold = new Color(1f, 0.82f, 0.35f);                    // 꼭 봐야 하는 것: 고른 카드 · 합이 일어나는 칸 · 숫자

    // ── 글자 ──
    public static readonly Color TextMain = new Color(0.94f, 0.92f, 0.88f);
    public static readonly Color TextMuted = new Color(0.64f, 0.62f, 0.58f);
    public static readonly Color TextDim = new Color(0.58f, 0.56f, 0.53f);            // 가장 옅은 글자 — 어느 바탕에서도 명도 대비 4.5:1 이상
    public static readonly Color TextOnBrass = new Color(0.09f, 0.07f, 0.05f);

    // ── 뜻이 있는 색 ──
    public static readonly Color Ki = new Color(0.52f, 0.84f, 1f);                      // 기력
    public static readonly Color Player = new Color(0.42f, 0.76f, 1f);                  // 나의 것
    public static readonly Color Enemy = new Color(1f, 0.45f, 0.36f);                   // 적의 것
    public static readonly Color Good = new Color(0.4f, 0.86f, 0.5f);                   // 가능 · 회복
    public static readonly Color Bad = new Color(1f, 0.38f, 0.32f);                     // 불가 · 손해
    public static readonly Color Danger = new Color(0.62f, 0.2f, 0.18f);                // 위험한 버튼의 면
    public static readonly Color Tension = new Color(1f, 0.6f, 0.32f);                  // 장력
    public static readonly Color Absorb = new Color(0.72f, 0.45f, 0.95f);               // 흡수 · 침식 (테두리 · 띠)
    public static readonly Color AbsorbText = new Color(0.85f, 0.65f, 1f);              // 흡수 · 침식 (글자)
    public static readonly Color AbsorbFill = new Color(0.15f, 0.09f, 0.2f, 1f);        // 흡수한 카드의 바탕
    public static readonly Color Spent = new Color(0.45f, 0.45f, 0.48f, 0.6f);          // 지나간 것 · 못 쓰는 것

    // ── 글 안의 색 (<color=...> 에 넣는 #rrggbb) ──
    public static readonly string HexText = Hex(TextMain);
    public static readonly string HexMuted = Hex(TextMuted);
    public static readonly string HexDim = Hex(TextDim);
    public static readonly string HexBrass = Hex(Brass);
    public static readonly string HexGold = Hex(Gold);
    public static readonly string HexKi = Hex(Ki);
    public static readonly string HexPlayer = Hex(Player);
    public static readonly string HexEnemy = Hex(Enemy);
    public static readonly string HexGood = Hex(Good);
    public static readonly string HexBad = Hex(Bad);
    public static readonly string HexTension = Hex(Tension);
    public static readonly string HexAbsorb = Hex(AbsorbText);
    public static readonly string HexEffect = Hex(new Color(1f, 0.68f, 0.76f));         // 주사위 효과 줄 (분홍)

    /// <summary>같은 색을 다른 투명도로</summary>
    public static Color Alpha(Color c, float a)
    {
        return new Color(c.r, c.g, c.b, a);
    }

    /// <summary>글 안에 넣는 색 코드 (#rrggbb)</summary>
    public static string Hex(Color c)
    {
        return "#" + Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255f).ToString("x2") + Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255f).ToString("x2")
                   + Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255f).ToString("x2");
    }

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

        if (border == null) NoBorder(img);
        else Border(img, border.Value);
    }

    private const string FrameName = "Frame", ShadowName = "Shadow";

    /// <summary>
    /// 둥근 상자에 또렷한 테두리 선을 두른다 (선만 그린 그림을 자식으로 겹친다 — 굵기가 고르고 모서리가 뭉개지지 않는다).
    /// 이미 있으면 색 · 굵기만 바꾼다. 예전 방식의 Outline이 붙어 있으면 끈다. 돌려주는 Image의 color로 나중에 색을 바꿀 수 있다.
    /// </summary>
    public static Image Border(Image img, Color color, bool bold = false)
    {
        if (img == null) return null;
        Outline old = img.GetComponent<Outline>();
        if (old != null) old.enabled = false;

        Transform t = img.transform.Find(FrameName);
        Image frame = t != null ? t.GetComponent<Image>() : null;
        if (frame == null)
        {
            frame = Overlay(img.transform, FrameName, 0f, 0f);
            frame.type = Image.Type.Sliced;
        }
        frame.sprite = ProceduralSprites.Frame(bold);
        frame.pixelsPerUnitMultiplier = img.pixelsPerUnitMultiplier;
        frame.color = color;
        frame.enabled = true;
        return frame;
    }

    public static void NoBorder(Image img)
    {
        if (img == null) return;
        Outline old = img.GetComponent<Outline>();
        if (old != null) old.enabled = false;
        Transform t = img.transform.Find(FrameName);
        if (t != null) t.gameObject.SetActive(false);
    }

    /// <summary>떠 있는 것(창 · 정보창 · 카드)에 부드러운 그림자를 깐다 — 뒤의 화면에서 한 단 떠 보인다. strength = 짙기(0~1)</summary>
    public static void Elevate(Image img, float strength = 0.55f)
    {
        if (img == null || !Application.isPlaying) return;
        Transform t = img.transform.Find(ShadowName);
        Image shadow = t != null ? t.GetComponent<Image>() : null;
        if (shadow == null)
        {
            float pad = ProceduralSprites.ShadowPad;
            shadow = Overlay(img.transform, ShadowName, pad, 5f);   // 사방으로 번지고, 아래로 조금 처진다
            shadow.sprite = ProceduralSprites.SoftShadow();
            shadow.type = Image.Type.Sliced;
            shadow.pixelsPerUnitMultiplier = 1f;
            shadow.transform.SetAsFirstSibling();
        }
        shadow.color = new Color(1f, 1f, 1f, strength);
    }

    /// <summary>부모를 꽉 채우는(또는 grow만큼 더 큰) 장식 그림. 클릭도 레이아웃도 건드리지 않는다</summary>
    private static Image Overlay(Transform parent, string name, float grow, float drop)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-grow, -grow - drop);
        rt.offsetMax = new Vector2(grow, grow - drop);
        rt.SetAsFirstSibling();
        go.AddComponent<LayoutElement>().ignoreLayout = true;   // 레이아웃 그룹 안에서도 자리를 차지하지 않게
        Image img = go.AddComponent<Image>();
        img.raycastTarget = false;
        return img;
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
        s.effectColor = Shadow;
        s.effectDistance = new Vector2(1f, -1.5f);
    }

    /// <summary>primary = 황동 버튼(어두운 글자) / 아니면 먹색 버튼(황동 테두리)</summary>
    public static void StyleButton(Button b, bool primary, int fontSize)
    {
        if (b == null) return;
        Image img = b.targetGraphic as Image;
        StylePanel(img, primary ? BrassFill : InkSolid, primary ? (Color?)Alpha(BrassBright, 0.6f) : BrassLine);
        if (primary && img != null)
        {
            img.sprite = ProceduralSprites.Bevel();   // 주 버튼은 살짝 도톰하게 (위가 밝고 아래가 그늘진 판)
            img.color = BrassButton;
        }
        UIButtonFeel.Ensure(b);

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
            if (s != null) s.effectColor = Alpha(BrassBright, 0.35f);   // 밝은 바탕 위에선 검은 그림자가 지저분하다
        }
    }

    // ─────────────────────────── 씬 UI 다시 칠하기 ───────────────────────────

    /// <summary>에디터 자동 세팅으로 만든 씬 UI 묶음(RuinaUI)을 새 규칙으로 정리한다. BattleHUD가 시작할 때 부른다.</summary>
    public static void ApplyToScene(Transform root)
    {
        if (root == null) return;

        // 0) 씬에 저장된 글자도 게임 글꼴로 + 두꺼운 외곽선 → 옅은 그림자
        foreach (Text t in root.GetComponentsInChildren<Text>(true))
        {
            if (UIBuilder.DefaultFont != null) t.font = UIBuilder.DefaultFont;
            SoftShadow(t);
        }

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

        // 2) 왼쪽 아래 바닥: 기력
        Image res = FindImage(root, "ResourcePanel");
        if (res != null)
        {
            StylePanel(res, Ink, BrassLine);
            Place(res.rectTransform, Vector2.zero, new Vector2(24f, 20f), new Vector2(250f, 64f));   // 기력 구슬 · 장력 게이지가 들어갈 폭
            res.rectTransform.SetAsLastSibling();   // 손패 받침대의 그늘에 덮이지 않게

            Text ki = FindText(res.transform, "KiText");
            StyleText(ki, 38, Ki, FontStyle.Bold);
            UITextPunch.Ensure(ki);   // 기력이 바뀌는 순간 숫자가 한 번 튄다
            if (ki != null)
            {
                UIBuilder.Stretch(ki.rectTransform, 6f);
                ki.alignment = TextAnchor.MiddleCenter;
            }

            // 예전 씬에 남아 있는 장력 표시는 숨긴다
            foreach (string old in new[] { "EmotionText", "EmotionGauge" })
            {
                Transform t = FindDeep(res.transform, old);
                if (t != null) t.gameObject.SetActive(false);
            }
        }

        // 3) 하단: 손패 받침대 + 시계 진행 버튼
        Image tray = FindImage(root, "HandTray");
        if (tray != null)
        {
            tray.sprite = ProceduralSprites.VerticalFade();   // 위로 갈수록 투명해지는 그늘 (딱딱한 경계선 없이)
            tray.type = Image.Type.Simple;
            tray.color = Alpha(Color.black, 0.7f);
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
            StylePanel(dlg, Alpha(Window, 0.92f), BrassLine);
            Elevate(dlg, 0.6f);
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
                StylePanel(plate, InkSolid, Alpha(Brass, 0.7f));
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
