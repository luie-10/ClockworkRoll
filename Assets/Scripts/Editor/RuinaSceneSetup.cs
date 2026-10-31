using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 메뉴: Ruina → 현재 씬 자동 세팅
///
/// 지금 열려 있는 씬에 게임에 필요한 것 중 '없는 것'을 만들고, 인스펙터 연결까지 전부 해준다.
///   - 매니저 3개(Game/Dialogue/Battle+Hand) — 있으면 재사용
///   - 플레이어(2.5D 스프라이트), 바닥, 카메라(BattleCamera), 조명(StageLighting)
///   - UI 전체 (대사창 · HUD · 턴 종료 버튼 · 손패 · 결과 화면) + CardView 프리팹
///   - GameManager에 '시계 마을 이야기' 스토리 연결, 씬을 Build Settings에 등록
///
/// 테스트 부트스트래퍼(TestBoot / TestBootStandalone)와, 연결되지 않은 기존 UI는 지우지 않고 '비활성화'만 한다.
/// 모든 작업은 Ctrl+Z 한 번으로 되돌릴 수 있다. 끝나면 Ctrl+S로 씬을 저장할 것.
/// 다시 실행하면 RuinaUI를 새로 만든다 (여러 번 눌러도 중복 생성되지 않음).
/// </summary>
public static class RuinaSceneSetup
{
    private const string StoryPath = "Assets/Data/Story/시계 마을 이야기.asset";
    private const string CardPrefabPath = "Assets/Prefabs/UI/CardView.prefab";
    private const string UIRootName = "RuinaUI";

    private static readonly List<string> report = new List<string>();

    [MenuItem("Ruina/현재 씬 자동 세팅")]
    public static void SetupCurrentScene()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Ruina", "Play 모드를 끄고 실행하세요.", "확인");
            return;
        }

        report.Clear();

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Ruina 씬 자동 세팅");
        int undoGroup = Undo.GetCurrentGroup();

        DisableTestBoots();

        GameManager gm = FindOrCreate<GameManager>("GameManager");
        DialogueManager dm = FindOrCreate<DialogueManager>("DialogueManager");
        BattleManager bm = FindOrCreate<BattleManager>("BattleManager");
        HandManager hm = Find<HandManager>();
        if (hm == null)
        {
            hm = Undo.AddComponent<HandManager>(bm.gameObject);
            report.Add("+ HandManager (BattleManager 오브젝트에 추가)");
        }
        if (bm.gameObject.name.Trim() != bm.gameObject.name)   // 이름 앞의 탭 문자 정리
        {
            Undo.RecordObject(bm.gameObject, "rename");
            bm.gameObject.name = bm.gameObject.name.Trim();
        }

        // 배경 2종 — 무대(Battle Stage)의 Backdrop 값에 따라 하나만 켜진다
        EnsureBackdrop<AlleyBackdrop>("AlleyBackdrop", "뒷골목 배경");
        EnsureBackdrop<ClockworkBackdrop>("ClockworkBackdrop", "태엽 탑 배경");

        EnsureEventSystem();
        DisableOldGround();
        Unit player = EnsurePlayer();
        SetupCamera();
        SetupLight();

        Canvas canvas = EnsureCanvas();
        DisableOldCanvasChildren(canvas);
        UIRefs ui = BuildUI(canvas);
        CardView cardPrefab = BuildCardViewPrefab();

        // ── 연결 ──
        Undo.RecordObject(gm, "wire");
        gm.playerUnit = player;
        gm.dialogueManager = dm;
        gm.battleManager = bm;
        StoryData story = AssetDatabase.LoadAssetAtPath<StoryData>(StoryPath);
        if (story != null) { gm.story = story; report.Add("= GameManager.Story ← 시계 마을 이야기"); }
        else report.Add("! 스토리 에셋을 찾지 못함: " + StoryPath + " (GameManager의 Chapters를 직접 채우세요)");

        Undo.RecordObject(dm, "wire");
        dm.dialoguePanel = ui.dialoguePanel;
        dm.speakerNameText = ui.speakerText;
        dm.bodyText = ui.bodyText;
        dm.portraitImage = ui.portrait;
        if (dm.speakerPortraits.Count == 0)
        {
            // 스토리에 나오는 화자 이름을 미리 채워 둔다 → Portrait 칸에 그림만 끌어다 놓으면 됨
            foreach (string speaker in new[] { "이안", "틱", "???", "종지기" })
                dm.speakerPortraits.Add(new DialogueManager.SpeakerPortrait { speaker = speaker });
            report.Add("+ DialogueManager > Speaker Portraits: 이안 / 틱 / ??? / 종지기 (그림 칸만 채우면 됨)");
        }

        Undo.RecordObject(bm, "wire");
        bm.handManager = hm;
        bm.stageNameText = ui.stageText;
        bm.kiText = ui.kiText;
        bm.emotionText = ui.emotionText;
        bm.emotionGauge = ui.emotionGauge;
        bm.phaseText = ui.phaseText;
        bm.endTurnButton = ui.endTurnButton;
        bm.battleLogText = ui.logText;

        Undo.RecordObject(hm, "wire");
        hm.cardViewPrefab = cardPrefab;
        hm.handPanel = ui.handPanel;

        UnityEventTools.AddPersistentListener(ui.endTurnButton.onClick, bm.EndPlayerTurn);
        UnityEventTools.AddPersistentListener(ui.restartButton.onClick, gm.RestartScene);
        report.Add("= 모든 인스펙터 연결 완료 (턴 종료 / 다시 시작 버튼 포함)");

        AddSceneToBuildSettings();

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = gm.gameObject;

        string summary = string.Join("\n", report);
        Debug.Log("[Ruina 씬 자동 세팅]\n" + summary);
        EditorUtility.DisplayDialog("Ruina 씬 자동 세팅 완료",
            summary + "\n\nCtrl+S로 씬을 저장한 뒤 Play 하세요.\n(되돌리기: Ctrl+Z)", "확인");
    }

    // ─────────────────────────── 오브젝트 ───────────────────────────

    private static T Find<T>() where T : Object
    {
        return Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
    }

    private static T FindOrCreate<T>(string name) where T : Component
    {
        T found = Find<T>();
        if (found != null) return found;

        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "create " + name);
        report.Add("+ " + name);
        return go.AddComponent<T>();
    }

    private static void DisableTestBoots()
    {
        List<GameObject> targets = new List<GameObject>();
        targets.AddRange(Object.FindObjectsByType<TestBoot>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Select(t => t.gameObject));
        targets.AddRange(Object.FindObjectsByType<RuinaTest.TestBootStandalone>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Select(t => t.gameObject));

        foreach (GameObject go in targets.Distinct())
        {
            Undo.RecordObject(go, "disable test boot");
            go.SetActive(false);
            report.Add("- 테스트 부트스트래퍼 비활성화: " + go.name + " (정식 씬과 같이 켜두면 UI/유닛이 겹침)");
        }
    }

    private static void EnsureEventSystem()
    {
        if (Find<EventSystem>() != null) return;

        GameObject go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        Undo.RegisterCreatedObjectUndo(go, "create EventSystem");
        report.Add("+ EventSystem");
    }

    private static void EnsureBackdrop<T>(string name, string label) where T : StageBackdrop
    {
        if (Find<T>() != null) return;

        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "create backdrop");
        go.AddComponent<T>();
        report.Add("+ " + name + " (" + label + " — Play 할 때 생성되어 보임)");
    }

    /// <summary>배경이 자체 바닥을 갖고 있으므로 예전 흰 Plane/Ground는 꺼둔다 (어두운 골목에서 튀어 보임).</summary>
    private static void DisableOldGround()
    {
        foreach (string n in new[] { "Plane", "Ground" })
        {
            GameObject g = GameObject.Find(n);
            if (g == null || g.GetComponent<MeshRenderer>() == null) continue;
            Undo.RecordObject(g, "disable ground");
            g.SetActive(false);
            report.Add("- 기존 바닥 비활성화: " + n + " (배경에 바닥이 포함됨)");
        }
    }

    private static Unit EnsurePlayer()
    {
        Unit existing = Object.FindObjectsByType<Unit>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(u => u.isPlayer);
        if (existing != null)
        {
            report.Add("= Player 재사용: " + existing.name);
            return existing;
        }

        GameObject go = new GameObject("Player");
        Undo.RegisterCreatedObjectUndo(go, "create Player");
        go.transform.position = new Vector3(-3.5f, 0f, 0f);

        // SpriteCharacter를 먼저 붙여야 실행 시 Unit이 몸(Body)을 찾는다. 몸은 Play 할 때 생성됨
        SpriteCharacter sc = Undo.AddComponent<SpriteCharacter>(go);
        sc.height = 2f;
        sc.tint = new Color(0.35f, 0.6f, 1f);   // 그림을 넣으면 흰색으로 바꿀 것

        Unit unit = Undo.AddComponent<Unit>(go);
        unit.isPlayer = true;
        unit.unitName = "이안";
        SerializedObject so = new SerializedObject(unit);
        so.FindProperty("maxHp").intValue = 60;
        so.ApplyModifiedPropertiesWithoutUndo();

        report.Add("+ Player '이안' (2.5D 스프라이트, HP 60) — 그림은 Player의 SpriteCharacter > Sprite에 넣기");
        return unit;
    }

    private static void SetupCamera()
    {
        Camera cam = Camera.main != null ? Camera.main : Find<Camera>();
        if (cam == null)
        {
            GameObject go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";
            Undo.RegisterCreatedObjectUndo(go, "create camera");
            cam = go.GetComponent<Camera>();
            report.Add("+ Main Camera");
        }

        Undo.RecordObject(cam.transform, "camera");
        cam.transform.position = new Vector3(0f, 3f, -9f);       // 2.5D: 옆에서 살짝 내려다보기
        cam.transform.rotation = Quaternion.Euler(12f, 0f, 0f);
        report.Add("= 카메라 2.5D 시점으로 이동 (0, 3, -9), 회전 X 12");

        BattleCamera bc = cam.GetComponent<BattleCamera>();
        if (bc == null)
        {
            bc = Undo.AddComponent<BattleCamera>(cam.gameObject);
            report.Add("+ BattleCamera");
        }

        // 클로즈업 때 머리 위 주사위 표시까지 화면에 들어오도록 조금 멀리서, 조금 위를 본다
        Undo.RecordObject(bc, "battle camera");
        bc.focusOffset = new Vector3(0f, 1.6f, -4.5f);
        bc.lookHeight = 1.8f;
    }

    private static void SetupLight()
    {
        Light sun = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .FirstOrDefault(l => l.type == LightType.Directional);
        if (sun == null || sun.GetComponent<StageLighting>() != null) return;

        Undo.AddComponent<StageLighting>(sun.gameObject);
        report.Add("+ StageLighting (" + sun.name + ")");
    }

    private static void AddSceneToBuildSettings()
    {
        // RestartScene()이 buildIndex로 씬을 다시 불러오므로 Build Settings에 있어야 한다
        Scene scene = SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(scene.path))
        {
            report.Add("! 씬이 아직 저장되지 않아 Build Settings에 등록 못 함 — 저장 후 다시 실행하세요");
            return;
        }
        if (EditorBuildSettings.scenes.Any(s => s.path == scene.path)) return;

        List<EditorBuildSettingsScene> list = EditorBuildSettings.scenes.ToList();
        list.Insert(0, new EditorBuildSettingsScene(scene.path, true));
        EditorBuildSettings.scenes = list.ToArray();
        report.Add("+ Build Settings에 씬 등록: " + scene.path + " (다시 시작 버튼용)");
    }

    // ─────────────────────────── UI ───────────────────────────

    private class UIRefs
    {
        public GameObject dialoguePanel;
        public Text speakerText, bodyText;
        public Image portrait;
        public Text stageText, kiText, emotionText, phaseText, logText;
        public Image emotionGauge;
        public Button endTurnButton, restartButton;
        public Transform handPanel;
    }

    private static Canvas EnsureCanvas()
    {
        Canvas canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .FirstOrDefault(c => c.isRootCanvas && c.renderMode != RenderMode.WorldSpace);

        if (canvas == null)
        {
            GameObject go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(go, "create Canvas");
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            report.Add("+ Canvas");
        }

        // 1920x1080 기준으로 해상도에 맞춰 늘고 줄게
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = Undo.AddComponent<CanvasScaler>(canvas.gameObject);
        Undo.RecordObject(scaler, "scaler");
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (canvas.GetComponent<GraphicRaycaster>() == null) Undo.AddComponent<GraphicRaycaster>(canvas.gameObject);
        return canvas;
    }

    /// <summary>예전에 만든(코드와 연결되지 않은 TMP) UI는 겹치지 않게 꺼둔다. 지우지는 않는다.</summary>
    private static void DisableOldCanvasChildren(Canvas canvas)
    {
        Transform old = canvas.transform.Find(UIRootName);
        if (old != null)
        {
            Undo.DestroyObjectImmediate(old.gameObject);   // 다시 실행 시 새로 만든다
        }

        foreach (Transform child in canvas.transform.Cast<Transform>().ToList())
        {
            if (!child.gameObject.activeSelf) continue;
            Undo.RecordObject(child.gameObject, "disable old UI");
            child.gameObject.SetActive(false);
            report.Add("- 기존 UI 비활성화: " + child.name + " (필요하면 다시 켜세요)");
        }
    }

    private const string BoxSpritePath = "Assets/Art/UI/RoundedBox.png";

    /// <summary>
    /// 패널용 둥근 상자 스프라이트를 PNG 에셋으로 저장해 둔다.
    /// (코드로 만든 스프라이트는 씬에 저장되지 않으므로, 에디터에서 만든 UI는 에셋을 참조해야 한다)
    /// </summary>
    private static Sprite EnsureBoxSpriteAsset()
    {
        if (!System.IO.File.Exists(BoxSpritePath))
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(BoxSpritePath));
            System.IO.File.WriteAllBytes(BoxSpritePath, ProceduralSprites.RoundedBoxTexture().EncodeToPNG());
            AssetDatabase.ImportAsset(BoxSpritePath);

            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(BoxSpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            int b = ProceduralSprites.BoxCorner + 2;
            importer.spriteBorder = new Vector4(b, b, b, b);   // 9-slice
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            report.Add("+ UI 상자 스프라이트: " + BoxSpritePath);
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(BoxSpritePath);
    }

    private static UIRefs BuildUI(Canvas canvas)
    {
        UIRefs ui = new UIRefs();
        UIBuilder.BoxSprite = EnsureBoxSpriteAsset();

        RectTransform root = UIBuilder.Rect(UIRootName, canvas.transform);
        UIBuilder.Stretch(root);
        Undo.RegisterCreatedObjectUndo(root.gameObject, "create UI");

        // 전투 UI 묶음: 스토리 중에는 숨겨지고, 전투 중에는 반투명 (BattleHUD)
        RectTransform hud = UIBuilder.Rect("BattleHUD", root);
        UIBuilder.Stretch(hud);
        hud.gameObject.AddComponent<BattleHUD>();

        Color gold = new Color(1f, 0.84f, 0.55f);

        // ── 전투 기록 (좌상단, 마우스 휠로 스크롤) ──
        ui.logText = UIBuilder.ScrollLog("BattleLogPanel", hud, "전투 기록   (마우스 휠로 스크롤)");
        RectTransform logPanel = (RectTransform)ui.logText.transform.parent.parent;   // Content → Viewport → Panel
        UIBuilder.Place(logPanel, new Vector2(0f, 0.47f), new Vector2(0.3f, 0.98f), new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(-20f, -20f));

        // ── 무대 이름 배너 + 턴 표시 (상단 가운데) ──
        Image banner = UIBuilder.Panel("StageBanner", hud);
        UIBuilder.Place(banner.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(560f, 64f));
        ui.stageText = UIBuilder.Label("StageNameText", banner.transform, "", 34, gold, TextAnchor.MiddleCenter);
        UIBuilder.Stretch(ui.stageText.rectTransform);

        ui.phaseText = UIBuilder.Label("PhaseText", hud, "", 32, Color.white, TextAnchor.MiddleCenter);
        UIBuilder.Place(ui.phaseText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(400f, 46f));

        // ── 자원 패널 (우상단): 기력 / 감정 + 게이지 ──
        Image res = UIBuilder.Panel("ResourcePanel", hud);
        UIBuilder.Place(res.rectTransform, Vector2.one, Vector2.one, Vector2.one, new Vector2(-20f, -20f), new Vector2(380f, 170f));

        ui.kiText = UIBuilder.Label("KiText", res.transform, "기력 0", 46, new Color(0.55f, 0.88f, 1f), TextAnchor.MiddleLeft);
        UIBuilder.Place(ui.kiText.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(-48f, 64f));

        ui.emotionText = UIBuilder.Label("EmotionText", res.transform, "감정 LV.0", 28, new Color(1f, 0.72f, 0.72f), TextAnchor.MiddleLeft);
        UIBuilder.Place(ui.emotionText.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(-48f, 40f));

        ui.emotionGauge = UIBuilder.Gauge("EmotionGauge", res.transform, new Color(1f, 0.38f, 0.42f));
        UIBuilder.Place((RectTransform)ui.emotionGauge.transform.parent, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, -128f), new Vector2(-48f, 22f));

        // ── 손패 (하단) — 카드 뒤에 어두운 받침대 ──
        Image tray = UIBuilder.Panel("HandTray", hud, new Color(0f, 0f, 0f, 0.28f), false);
        tray.raycastTarget = false;
        UIBuilder.Place(tray.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 290f));

        RectTransform hand = UIBuilder.Rect("HandPanel", hud);   // HandManager가 자식을 비우므로 받침대와 따로 둔다
        UIBuilder.Place(hand, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(-300f, 270f));
        HorizontalLayoutGroup layout = hand.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.LowerCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        ui.handPanel = hand;

        // ── 턴 종료 버튼 (손패 받침대 위, 오른쪽) ──
        ui.endTurnButton = UIBuilder.Button("EndTurnButton", hud, "턴 종료", new Color(0.62f, 0.42f, 0.15f), 34);
        UIBuilder.Place((RectTransform)ui.endTurnButton.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 305f), new Vector2(260f, 84f));

        // ── 대사창 (하단, 스토리 중에만 켜짐) ──
        Image dlg = UIBuilder.Panel("DialoguePanel", root, new Color(0.02f, 0.02f, 0.03f, 0.6f));
        UIBuilder.Place(dlg.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(-40f, 300f));
        ui.dialoguePanel = dlg.gameObject;

        RectTransform portraitRt = UIBuilder.Rect("PortraitImage", dlg.transform);
        UIBuilder.Place(portraitRt, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(30f, 30f), new Vector2(240f, 240f));
        ui.portrait = portraitRt.gameObject.AddComponent<Image>();
        ui.portrait.preserveAspect = true;
        ui.portrait.raycastTarget = false;

        Image plate = UIBuilder.Panel("SpeakerPlate", dlg.transform, new Color(0.22f, 0.15f, 0.07f, 0.8f));   // 대사창 위에 걸친 이름표
        UIBuilder.Place(plate.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(290f, 0f), new Vector2(320f, 58f));
        ui.speakerText = UIBuilder.Label("SpeakerNameText", plate.transform, "화자", 32, gold, TextAnchor.MiddleCenter);
        UIBuilder.Stretch(ui.speakerText.rectTransform);

        ui.bodyText = UIBuilder.Label("BodyText", dlg.transform, "대사", 30, Color.white, TextAnchor.UpperLeft);
        UIBuilder.Place(ui.bodyText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 1f), new Vector2(300f, -52f), new Vector2(-340f, -92f));

        Text hint = UIBuilder.Label("ClickHint", dlg.transform, "클릭 / Space ▶", 20, new Color(1f, 1f, 1f, 0.5f), TextAnchor.LowerRight);
        UIBuilder.Stretch(hint.rectTransform, 18f);

        // ── 결과 화면 (패배 / 클리어) ──
        Image result = UIBuilder.Panel("ResultPanel", root, new Color(0f, 0f, 0f, 0.8f), false);
        UIBuilder.Stretch(result.rectTransform);

        Text title = UIBuilder.Label("TitleText", result.transform, "패배...", 76, gold, TextAnchor.MiddleCenter);
        UIBuilder.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(900f, 110f));
        Text message = UIBuilder.Label("MessageText", result.transform, "", 30, new Color(0.88f, 0.88f, 0.88f), TextAnchor.MiddleCenter);
        UIBuilder.Place(message.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(1200f, 60f));

        ui.restartButton = UIBuilder.Button("RestartButton", result.transform, "다시 시작", new Color(0.55f, 0.22f, 0.2f));
        UIBuilder.Place((RectTransform)ui.restartButton.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -90f), new Vector2(300f, 84f));

        GameStatePanel statePanel = root.gameObject.AddComponent<GameStatePanel>();
        statePanel.panel = result.gameObject;
        statePanel.titleText = title;
        statePanel.messageText = message;
        result.gameObject.SetActive(false);

        report.Add("+ UI: 스크롤 전투 기록 · 무대 배너 · 턴 표시 · 기력/감정 게이지 · 손패 받침대 · 턴 종료 · 대사창 · 결과 화면 (" + canvas.name + "/" + UIRootName + ")");
        return ui;
    }

    private static CardView BuildCardViewPrefab()
    {
        GameObject root = new GameObject("CardView", typeof(RectTransform));
        ((RectTransform)root.transform).sizeDelta = new Vector2(180f, 250f);
        Image bg = root.AddComponent<Image>();
        bg.sprite = UIBuilder.BoxSprite;
        bg.type = Image.Type.Sliced;
        Outline edge = root.AddComponent<Outline>();
        edge.effectColor = new Color(0f, 0f, 0f, 0.9f);
        edge.effectDistance = new Vector2(3f, -3f);

        Text nameText = UIBuilder.Label("NameText", root.transform, "카드 이름", 24, Color.black, TextAnchor.UpperCenter, false);
        UIBuilder.Place(nameText.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(-16f, 60f));

        // 기력: 어두운 배지 위 하늘색 글자
        Image costPlate = UIBuilder.Panel("CostPlate", root.transform, new Color(0.05f, 0.07f, 0.12f, 0.9f), false);
        costPlate.raycastTarget = false;
        UIBuilder.Place(costPlate.rectTransform, Vector2.one, Vector2.one, Vector2.one, new Vector2(-8f, -70f), new Vector2(104f, 32f));
        Text costText = UIBuilder.Label("CostText", costPlate.transform, "기력 0", 20, new Color(0.55f, 0.88f, 1f), TextAnchor.MiddleCenter, false);
        UIBuilder.Stretch(costText.rectTransform);

        // 주사위/효과: 어두운 판 위 흰 글자 → 어떤 카드 색(흡수 보라 포함)에서도 읽힌다
        Image dicePlate = UIBuilder.Panel("DicePlate", root.transform, new Color(0f, 0f, 0f, 0.6f), false);
        dicePlate.raycastTarget = false;
        UIBuilder.Place(dicePlate.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(-14f, 150f));
        Text diceText = UIBuilder.Label("DiceText", dicePlate.transform, "주사위", 17, new Color(0.95f, 0.95f, 0.95f), TextAnchor.LowerLeft, false);
        UIBuilder.Stretch(diceText.rectTransform, 6f);

        CardView view = root.AddComponent<CardView>();
        view.Configure(nameText, costText, diceText, bg);   // [SerializeField] 필드라 프리팹에 저장된다

        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(CardPrefabPath));
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, CardPrefabPath);
        Object.DestroyImmediate(root);

        report.Add("+ CardView 프리팹: " + CardPrefabPath);
        return prefab.GetComponent<CardView>();
    }
}
