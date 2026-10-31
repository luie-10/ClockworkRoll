using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// [핵심 시스템 테스트 전용 부트스트래퍼]
///
/// 빈 씬의 빈 오브젝트에 이것만 붙이고 Play 하면,
/// 씬 세팅/프리팹/에셋 없이 아래 것들이 전부 코드로 자동 생성되어 돌아간다.
///
///   - 매니저 4종 (GameManager / DialogueManager / BattleManager / HandManager)
///   - 플레이어 유닛(큐브) + 바닥
///   - 테스트용 카드 / 적 / 무대 / 챕터 (런타임 생성 에셋 — 프로젝트에 파일이 남지 않음)
///   - 대사창 · 기력 · 감정 · 전투 로그 · 턴 종료 버튼 · 손패 UI (코드 생성 Canvas)
///
/// autoPlay 체크: 클릭 없이 시스템이 스스로 대사를 넘기고 카드를 쓴다 (콘솔 관전용).
/// autoPlay 해제: 직접 클릭해서 플레이 (대사 클릭 → 카드 클릭 → 적 클릭 → 턴 종료).
///
/// 주의: 반드시 '빈 씬'에서 단독으로 사용할 것 (수동 씬 세팅과 중복되면 UI가 겹친다).
/// 실제 게임 시스템 스크립트를 그대로 사용하므로, 여기서 확인되면 정식 씬에서도 동일하게 동작한다.
/// </summary>
public class TestBoot : MonoBehaviour
{
    [Header("테스트 모드")]
    [Tooltip("체크: AI가 스스로 진행(관전용) / 해제: 직접 클릭 플레이")]
    public bool autoPlay = false;

    private GameManager gameManager;
    private DialogueManager dialogueManager;
    private BattleManager battleManager;
    private HandManager handManager;
    private Unit playerUnit;

    private GameObject cardTemplate;
    private Button endTurnButton;

    private GameObject dialoguePanel;
    private Text speakerText, bodyText;
    private Text stageText, kiText, emotionText, logText;
    private Transform handPanel;

    private float timer;
    private int actionsLeft = 3;
    private static Font builtinFont;

    void Start()
    {
        CreateEventSystem();
        CreateCanvasAndUI();
        CreateManagers();
        CreatePlayerUnit();
        CreateCardTemplate();
        CreateTestStory();
        SetupCamera();

        Debug.Log("[TestBoot] 준비 완료 — 흐름: 인트로 대사 → 전투(기력/상성/감정) → 승리 시 흡수(◈) → 아웃트로 → 클리어 로그");
    }

    // ─────────────────────────── 자동 진행 (autoPlay) ───────────────────────────

    void Update()
    {
        if (!autoPlay) return;

        timer -= Time.deltaTime;
        if (timer > 0f) return;

        // 1) 스토리 중이면 대사 자동 진행
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsPlaying)
        {
            DialogueManager.Instance.Advance();
            timer = 0.6f;
            return;
        }

        // 2) 내 턴이면: 카드를 최대 3장 사용하고 턴 종료 (반복)
        if (BattleManager.Instance != null &&
            BattleManager.Instance.Phase == BattleManager.BattlePhase.PlayerTurn &&
            !BattleManager.Instance.IsBusy)
        {
            CardInstance card = battleManager.handManager.GetRandomHandCard();

            if (card == null || card.view == null || actionsLeft <= 0)
            {
                actionsLeft = 3;
                battleManager.EndPlayerTurn();
                timer = 1.0f;
                return;
            }

            battleManager.OnCardClicked(card.view);

            Unit target = FindRandomAliveEnemy();
            if (target != null) battleManager.OnUnitClicked(target);

            actionsLeft--;
            timer = 1.2f;
        }
    }

    // ─────────────────────────── 생성: 기반 ───────────────────────────

    private void CreateEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private void SetupCamera()
    {
        // 2.5D 시점: 옆에서 살짝 내려다보는 카메라 (플레이어 왼쪽 ↔ 적 오른쪽) + 합 클로즈업/흔들림 연출
        Camera cam = Camera.main;
        if (cam == null) return;

        cam.transform.position = new Vector3(0f, 3f, -9f);
        cam.transform.rotation = Quaternion.Euler(12f, 0f, 0f);

        // BattleCamera는 Awake에서 현재 위치를 '기본 시점'으로 기억하므로 위치를 정한 뒤에 붙인다
        if (cam.GetComponent<BattleCamera>() == null) cam.gameObject.AddComponent<BattleCamera>();
    }

    private void CreateManagers()
    {
        gameManager = new GameObject("GameManager").AddComponent<GameManager>();
        dialogueManager = new GameObject("DialogueManager").AddComponent<DialogueManager>();
        battleManager = new GameObject("BattleManager").AddComponent<BattleManager>();
        handManager = battleManager.gameObject.AddComponent<HandManager>();
    }

    private void CreatePlayerUnit()
    {
        // 3D 배경 2종 (바닥 포함). 테스트 무대는 뒷골목 — 무대의 Backdrop 값으로 바뀐다
        if (FindAnyObjectByType<AlleyBackdrop>() == null)
            new GameObject("AlleyBackdrop").AddComponent<AlleyBackdrop>();
        if (FindAnyObjectByType<ClockworkBackdrop>() == null)
            new GameObject("ClockworkBackdrop").AddComponent<ClockworkBackdrop>();

        // 플레이어: 2D 스프라이트 캐릭터 (그림이 없으니 파란 실루엣). 발이 바닥(y=0)에 닿는다
        GameObject go = new GameObject("Player");
        go.transform.position = new Vector3(-3.5f, 0f, 0f);

        SpriteCharacter sc = go.AddComponent<SpriteCharacter>();
        sc.Setup(null, 2f, new Color(0.35f, 0.6f, 1f));   // Unit보다 먼저 외형을 만든다

        playerUnit = go.AddComponent<Unit>();
        playerUnit.isPlayer = true;
        playerUnit.unitName = "테스트 수헬자";
    }

    // ─────────────────────────── 생성: UI ───────────────────────────

    private void CreateCanvasAndUI()
    {
        GameObject canvasGO = new GameObject("TestCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // ── 대사창 (하단) ──
        dialoguePanel = new GameObject("DialoguePanel");
        dialoguePanel.transform.SetParent(canvas.transform, false);

        RectTransform dpr = dialoguePanel.AddComponent<RectTransform>();
        dpr.anchorMin = new Vector2(0f, 0f);
        dpr.anchorMax = new Vector2(1f, 0f);
        dpr.pivot = new Vector2(0.5f, 0f);
        dpr.anchoredPosition = Vector2.zero;
        dpr.sizeDelta = new Vector2(0f, 190f);

        Image dpImg = dialoguePanel.AddComponent<Image>();
        dpImg.color = new Color(0f, 0f, 0f, 0.78f);
        dpImg.raycastTarget = false;

        speakerText = CreateText(dialoguePanel.transform, "SpeakerText", "화자",
            TextAnchor.MiddleLeft, 18, new Color(1f, 0.9f, 0.6f),
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(20f, -14f), new Vector2(-40f, 28f));

        bodyText = CreateText(dialoguePanel.transform, "BodyText", "대사",
            TextAnchor.UpperLeft, 16, Color.white,
            new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 8f), new Vector2(-40f, -70f));

        dialoguePanel.SetActive(false);   // DialogueManager가 재생 중에만 켠다

        // ── 전투 UI 묶음: 스토리 중에는 숨김, 전투 중에는 반투명 (BattleHUD) ──
        RectTransform hud = UIBuilder.Rect("BattleHUD", canvas.transform);
        UIBuilder.Stretch(hud);
        hud.gameObject.AddComponent<BattleHUD>();

        // ── HUD (상단) ──
        stageText = CreateText(hud, "StageText", "",
            TextAnchor.UpperCenter, 20, Color.white,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -10f), new Vector2(0f, 32f));

        kiText = CreateText(hud, "KiText", "기력 0",
            TextAnchor.UpperRight, 18, new Color(0.55f, 0.85f, 1f),
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-12f, -8f), new Vector2(200f, 28f));

        emotionText = CreateText(hud, "EmotionText", "감정 LV.0",
            TextAnchor.UpperRight, 18, new Color(1f, 0.7f, 0.7f),
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-12f, -38f), new Vector2(240f, 28f));

        // ── 전투 로그 (좌측, 마우스 휠로 스크롤) ──
        logText = UIBuilder.ScrollLog("LogPanel", hud, "전투 기록 (휠 스크롤)");
        logText.fontSize = 13;
        UIBuilder.Place((RectTransform)logText.transform.parent.parent,
            new Vector2(0f, 0.55f), new Vector2(0.34f, 0.95f), new Vector2(0f, 1f), new Vector2(10f, -10f), new Vector2(-10f, -10f));

        // ── 턴 종료 버튼 (우하단, 손패 위) ──
        GameObject btnGO = new GameObject("EndTurnButton");
        btnGO.transform.SetParent(hud, false);

        RectTransform br = btnGO.AddComponent<RectTransform>();
        br.anchorMin = new Vector2(1f, 0f);
        br.anchorMax = new Vector2(1f, 0f);
        br.pivot = new Vector2(1f, 0f);
        br.anchoredPosition = new Vector2(-230f, 205f);
        br.sizeDelta = new Vector2(180f, 46f);

        Image bImg = btnGO.AddComponent<Image>();
        bImg.color = new Color(0.25f, 0.35f, 0.55f);

        endTurnButton = btnGO.AddComponent<Button>();

        CreateText(btnGO.transform, "Label", "턴 종료",
            TextAnchor.MiddleCenter, 16, Color.white,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);

        // ── 손패 패널 (하단, 대사창 위) ──
        GameObject hpGO = new GameObject("HandPanel");
        hpGO.transform.SetParent(hud, false);

        RectTransform hpr = hpGO.AddComponent<RectTransform>();
        hpr.anchorMin = new Vector2(0f, 0f);
        hpr.anchorMax = new Vector2(1f, 0f);
        hpr.pivot = new Vector2(0.5f, 0f);
        hpr.anchoredPosition = new Vector2(0f, 200f);
        hpr.sizeDelta = new Vector2(0f, 240f);

        HorizontalLayoutGroup layout = hpGO.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.padding = new RectOffset(20, 20, 0, 0);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = false;
        layout.childControlHeight = false;

        handPanel = hpGO.transform;

        // 자동 진행 모드 표시
        if (autoPlay)
        {
            CreateText(canvas.transform, "AutoPlayTag", "[AUTO PLAY]",
                TextAnchor.UpperLeft, 16, new Color(1f, 0.6f, 0.3f),
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(12f, -10f), new Vector2(160f, 30f));
        }
    }

    private void CreateCardTemplate()
    {
        // 카드 UI 1장을 코드로 만들어 템플릿으로 사용 (HandManager가 이를 복제)
        GameObject root = new GameObject("CardTemplate");
        root.transform.SetParent(transform, false);
        root.SetActive(false);   // 템플릿은 화면에 안 보임 (복제본은 HandManager가 활성화)

        RectTransform rect = root.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(170f, 230f);

        Image bg = root.AddComponent<Image>();
        bg.color = Color.white;

        // 카드 이름 (상단)
        Text nameText = CreateText(root.transform, "NameText", "카드 이름",
            TextAnchor.UpperCenter, 15, Color.black,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -8f), new Vector2(-16f, 26f));

        // 기력 코스트 (우측 상단)
        Text costText = CreateText(root.transform, "CostText", "기력 0",
            TextAnchor.MiddleRight, 13, new Color(0.15f, 0.3f, 0.7f),
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-8f, -38f), new Vector2(100f, 24f));

        // 주사위 요약 + 침식 수치 (하단) — 어두운 판 위 흰 글자 (흡수 카드의 보라 배경에서도 읽히게)
        Image dicePlate = UIBuilder.Panel("DicePlate", root.transform, new Color(0f, 0f, 0f, 0.6f), false);
        dicePlate.raycastTarget = false;
        UIBuilder.Place(dicePlate.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(-12f, 96f));
        Text diceText = CreateText(dicePlate.transform, "DiceText", "주사위",
            TextAnchor.LowerLeft, 13, new Color(0.95f, 0.95f, 0.95f),
            new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(-10f, -8f));

        CardView view = root.AddComponent<CardView>();
        view.Configure(nameText, costText, diceText, bg);

        cardTemplate = root;
    }

    // ─────────────────────────── 생성: 테스트 데이터 + 연결 ───────────────────────────

    private void CreateTestStory()
    {
        // 1) 플레이어 시작 덱 카드
        CardData slash = MakeCard("기본 참격", 1, new[] { MakeDice("참격", DiceType.Attack, DiceDetail.Slash, 2, 6) });
        CardData guard = MakeCard("기본 가드", 1, new[] { MakeDice("가드", DiceType.Defense, DiceDetail.Guard, 3, 7) });
        CardData evade = MakeCard("기본 회피", 1, new[] { MakeDice("회피", DiceType.Defense, DiceDetail.Evade, 2, 6) });
        CardData thrust = MakeCard("기본 관통", 1, new[] { MakeDice("관통", DiceType.Attack, DiceDetail.Thrust, 1, 5) });
        CardData blunt = MakeCard("기본 타격", 2, new[] { MakeDice("타격", DiceType.Attack, DiceDetail.Blunt, 3, 8) });
        CardData emotionBlade = MakeCard("감정의 칼날", 3, new[] { MakeDice("참격", DiceType.Attack, DiceDetail.Slash, 6, 12) }, 2);

        // 2) 적 — 쥐 책장 6장 (적이 쓰는 카드 = 승리 시 흡수되는 카드). 출혈 스킬 테스트용
        CardData bite = MakeCard("물어뜯기", 1, new[] {
            Bleed(MakeDice("참격", DiceType.Attack, DiceDetail.Slash, 1, 4), DiceTrigger.OnHit, 1, true),
            MakeDice("타격", DiceType.Attack, DiceDetail.Blunt, 1, 4) }, 0, CardRarity.Paper);
        CardData alleyRush = MakeCard("골목 돌파", 1, new[] {
            Bleed(MakeDice("관통", DiceType.Attack, DiceDetail.Thrust, 1, 8), DiceTrigger.OnHit, 1, true) }, 0, CardRarity.Stone);
        CardData scram = MakeCard("튀어", 1, new[] {
            MakeDice("회피", DiceType.Defense, DiceDetail.Evade, 1, 6),
            MakeDice("회피", DiceType.Defense, DiceDetail.Evade, 1, 6),
            MakeDice("참격", DiceType.Attack, DiceDetail.Slash, 1, 4) }, 0, CardRarity.Stone);
        CardData survival = MakeCard("쥐의 생존법", 2, new[] {
            MakeDice("가드", DiceType.Defense, DiceDetail.Guard, 1, 6),
            Bleed(MakeDice("참격", DiceType.Attack, DiceDetail.Slash, 3, 4), DiceTrigger.OnHit, 1, true),
            MakeDice("회피", DiceType.Defense, DiceDetail.Evade, 1, 7) }, 0, CardRarity.Paper);
        CardData dirtyHit = MakeCard("더러운 타격", 2, new[] {
            Bleed(MakeDice("타격", DiceType.Attack, DiceDetail.Blunt, 3, 7), DiceTrigger.OnClashWin, 2, false),
            MakeDice("가드", DiceType.Defense, DiceDetail.Guard, 1, 3) }, 0, CardRarity.Iron);
        CardData gut = MakeCard("내장담기", 3, new[] {
            Bleed(MakeDice("참격", DiceType.Attack, DiceDetail.Slash, 3, 6), DiceTrigger.OnHit, 1, true),
            Bleed(MakeDice("참격", DiceType.Attack, DiceDetail.Slash, 3, 5), DiceTrigger.OnHit, 1, true),
            MakeDice("가드", DiceType.Defense, DiceDetail.Guard, 1, 4) }, 0, CardRarity.Iron);

        EnemyData enemyA = ScriptableObject.CreateInstance<EnemyData>();
        enemyA.enemyName = "쥐 A";
        enemyA.maxHp = 30;
        enemyA.resistance = new ResistanceSet(Resistance.Normal, Resistance.Weak, Resistance.Fatal);   // 참1.0 관1.5 타2.0
        enemyA.tintColor = new Color(0.8f, 0.35f, 0.3f);
        enemyA.deck = new List<CardData> { bite, alleyRush, scram };

        EnemyData enemyB = ScriptableObject.CreateInstance<EnemyData>();
        enemyB.enemyName = "쥐 B";
        enemyB.maxHp = 45;
        enemyB.resistance = new ResistanceSet(Resistance.Weak, Resistance.Normal, Resistance.Weak);    // 참1.5 관1.0 타1.5
        enemyB.tintColor = new Color(0.4f, 0.5f, 0.85f);
        enemyB.deck = new List<CardData> { survival, dirtyHit, gut };

        // 3) 무대
        BattleStageData stage = ScriptableObject.CreateInstance<BattleStageData>();
        stage.stageName = "테스트 무대";
        stage.startKi = 3;
        stage.kiPerTurn = 1;
        stage.enemies = new List<EnemySpawnInfo>
        {
            new EnemySpawnInfo { enemy = enemyA, spawnPosition = new Vector3(3f, 0f, 1.2f) },
            new EnemySpawnInfo { enemy = enemyB, spawnPosition = new Vector3(4.5f, 0f, -1f) },
        };

        // 4) 챕터: 인트로 대사 → 무대 → 아웃트로 대사 → 보상
        ChapterData chapter = ScriptableObject.CreateInstance<ChapterData>();
        chapter.chapterTitle = "테스트 챕터";
        chapter.introLines = new List<DialogueLine>
        {
            new DialogueLine { speaker = "안내 (테스트)", text = "핵심 시스템 테스트 무대다. 클릭 또는 Space로 대사를 넘긴다." },
            new DialogueLine { speaker = "안내 (테스트)", text = "전투에서 이기면 적의 책장이 ◈ 흡수된다. 사용할 때 침식을 조심하자." },
        };
        chapter.battle = stage;
        chapter.outroLines = new List<DialogueLine>
        {
            new DialogueLine { speaker = "안내 (테스트)", text = "무대 클리어. 적의 책장이 덱에 흡수되었다." },
            new DialogueLine { speaker = "안내 (테스트)", text = "◈ 카드를 사용해 침식 발동을 확인해 보자. 이 대사가 끝나면 클리어 로그가 찍힌다." },
        };
        chapter.rewardCards = new List<CardData> { emotionBlade };

        // 5) 매니저 상호 연결
        gameManager.chapters = new List<ChapterData> { chapter };
        gameManager.playerUnit = playerUnit;
        gameManager.dialogueManager = dialogueManager;
        gameManager.battleManager = battleManager;
        gameManager.startingDeck = new List<CardData>
        {
            slash, slash, slash, guard, guard, evade, thrust, blunt,
        };

        dialogueManager.dialoguePanel = dialoguePanel;
        dialogueManager.speakerNameText = speakerText;
        dialogueManager.bodyText = bodyText;

        battleManager.handManager = handManager;
        battleManager.stageNameText = stageText;
        battleManager.kiText = kiText;
        battleManager.emotionText = emotionText;
        battleManager.battleLogText = logText;
        battleManager.endTurnButton = endTurnButton;

        handManager.cardViewPrefab = cardTemplate.GetComponent<CardView>();
        handManager.handPanel = handPanel;

        endTurnButton.onClick.AddListener(battleManager.EndPlayerTurn);
    }

    // ─────────────────────────── 유틸 ───────────────────────────

    private Unit FindRandomAliveEnemy()
    {
        Unit[] units = FindObjectsByType<Unit>(FindObjectsSortMode.None);
        List<Unit> alive = new List<Unit>();
        foreach (Unit u in units)
        {
            if (!u.isPlayer && u.IsAlive) alive.Add(u);
        }
        return alive.Count > 0 ? alive[Random.Range(0, alive.Count)] : null;
    }

    private static CardData MakeCard(string cardName, int cost, DiceData[] dice, int requireEmotion = 0,
                                     CardRarity rarity = CardRarity.Paper)
    {
        CardData card = ScriptableObject.CreateInstance<CardData>();
        card.cardName = cardName;
        card.rarity = rarity;
        card.cost = cost;
        card.requireEmotionLevel = requireEmotion;
        card.cardDescription = "테스트용 런타임 생성 카드";
        card.diceList = new List<DiceData>(dice);
        return card;
    }

    private static DiceData MakeDice(string diceName, DiceType type, DiceDetail detail, int minRoll, int maxRoll)
    {
        DiceData dice = new DiceData();
        dice.diceName = diceName;
        dice.diceType = type;
        dice.diceDetail = detail;
        dice.minRoll = minRoll;
        dice.maxRoll = maxRoll;
        dice.diceEffectDescription = "";
        return dice;
    }

    /// <summary>주사위에 출혈 효과를 붙여서 그대로 돌려준다.</summary>
    private static DiceData Bleed(DiceData dice, DiceTrigger trigger, int amount, bool nextTurn)
    {
        dice.effects.Add(new DiceEffect { trigger = trigger, effect = DiceEffectType.Bleed, amount = amount, nextTurn = nextTurn });
        return dice;
    }

    private Text CreateText(Transform parent, string objName, string initial,
        TextAnchor alignment, int fontSize, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 sizeDelta)
    {
        GameObject go = new GameObject(objName);
        go.transform.SetParent(parent, false);

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = sizeDelta;

        Text text = go.AddComponent<Text>();
        text.text = initial;
        text.alignment = alignment;
        text.fontSize = fontSize;
        text.color = color;
        text.font = GetBuiltinFont();
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;   // 텍스트가 클릭을 가로채지 않도록

        return text;
    }

    private static Font GetBuiltinFont()
    {
        if (builtinFont != null) return builtinFont;

        // 유니티 버전에 따라 내장 폰트 이름이 다르다 (2022+ : LegacyRuntime.ttf)
        try { builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (builtinFont == null)
        {
            try { builtinFont = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
        }
        return builtinFont;
    }
}
