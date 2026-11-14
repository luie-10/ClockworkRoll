using System.Collections.Generic;
using UnityEngine;

/// <summary>적이 말하는 때</summary>
public enum BarkKind { Idle, Attack, Hurt, Death }

/// <summary>
/// 적의 한마디 — 종류마다 제 말투가 있다 (쥐는 찍찍대고, 인형은 태엽 소리를 섞고, 망령은 시간을 센다).
/// EnemyData의 말풍선 대사 칸에 적어 두면 그 적만의 대사가 우선이고, 비어 있으면 종류에 맞는 기본 대사를 쓴다.
///   Say(유닛, 때, 확률) : 확률로 한마디를 머리 위 말풍선으로 띄운다 (BattleManager가 부른다)
/// </summary>
public static class EnemyBarks
{
    private static readonly Dictionary<Unit, string> lastLine = new Dictionary<Unit, string>();
    private static readonly Dictionary<Unit, float> lastTime = new Dictionary<Unit, float>();

    /// <summary>chance(0~1) 확률로 한마디. 같은 적이 연달아 떠들지 않게 잠깐의 간격을 둔다 (쓰러질 때는 예외)</summary>
    public static bool Say(Unit unit, BarkKind kind, float chance = 1f)
    {
        if (unit == null || unit.isPlayer || Random.value > chance) return false;
        if (kind != BarkKind.Death)
        {
            if (!unit.IsAlive) return false;
            if (lastTime.TryGetValue(unit, out float t) && Time.unscaledTime - t < 1.6f) return false;
        }

        List<string> lines = Lines(unit, kind);
        if (lines == null || lines.Count == 0) return false;

        // 방금 한 말은 되도록 피한다
        lastLine.TryGetValue(unit, out string last);
        string line = lines[Random.Range(0, lines.Count)];
        for (int i = 0; i < 4 && line == last && lines.Count > 1; i++) line = lines[Random.Range(0, lines.Count)];
        if (string.IsNullOrWhiteSpace(line)) return false;

        lastLine[unit] = line;
        lastTime[unit] = Time.unscaledTime;
        SpeechBubble.Show(unit, line, kind == BarkKind.Idle ? 2.8f : 2f);
        return true;
    }

    /// <summary>새 전투가 시작될 때 지난 전투의 기록을 비운다</summary>
    public static void Forget()
    {
        lastLine.Clear();
        lastTime.Clear();
    }

    private static List<string> Lines(Unit unit, BarkKind kind)
    {
        EnemyData d = unit.enemyData;
        if (d != null)
        {
            List<string> own = kind == BarkKind.Attack ? d.attackBarks : kind == BarkKind.Hurt ? d.hurtBarks : kind == BarkKind.Death ? d.deathBarks : d.barks;
            if (own != null && own.Count > 0) return own;
        }
        string name = !string.IsNullOrEmpty(unit.kindName) ? unit.kindName : unit.unitName;
        return Defaults(name ?? "")[(int)kind];
    }

    // ─────────────────────────── 종류별 기본 대사 ───────────────────────────
    // 줄 순서: { 평소, 공격, 맞았을 때, 쓰러질 때 }

    private static List<string>[] Set(string idle, string attack, string hurt, string death)
    {
        return new[] { Split(idle), Split(attack), Split(hurt), Split(death) };
    }

    private static List<string> Split(string joined)
    {
        return new List<string>(joined.Split('|'));
    }

    private static readonly List<string>[] Bat = Set(
        "끼익…|…파닥파닥.|종 소리가 그립다…|어둠이 좋아.|끼끼끼…",
        "끼이이익!|피 냄새…!|물어 주마!",
        "끽?!|날개가…!|끼익…!",
        "끼…이…|…종루로… 돌아가…");

    private static readonly List<string>[] Rat = Set(
        "찍… 배고파.|이 골목은 우리 거다.|찍찍… 냄새가 난다.|물이 또 차오른다…|반짝이는 걸 내놔.|찌익…",
        "찍—!|물어뜯어 주마!|이빨 맛 좀 봐라!",
        "찍?!|아야… 찍!|꼬리는 건드리지 마!",
        "찌…익…|쥐구멍이… 멀다…");

    private static readonly List<string>[] Bug = Set(
        "사각사각…|…녹… 맛있다…|키릭.|쇠 냄새…",
        "키리릭!|갉아 주마!|사각!",
        "끼릭?!|…껍질이…!",
        "…키…릭…|…녹…슬어…");

    private static readonly List<string>[] Crab = Set(
        "딸깍… 딸깍…|옆으로만 간다.|…보글보글.|이 껍질은 못 뚫는다.",
        "딸깍!|집어 주마!|싹둑!",
        "…보글?!|껍질에 금이…!",
        "보글…보…글…|…가라…앉는다…");

    private static readonly List<string>[] Wraith = Set(
        "…똑… 딱…|시간이… 새고 있다…|몇 시였더라…|네 시간은… 얼마 남았나…|…멈춘 종을… 들었나…",
        "…정각이다.|네 시간을… 내놓아라.|똑, 딱, 끝.",
        "…바늘이… 흔들린다…|…아직… 멈출 수 없다…",
        "…드디어… 멈추는구나…|…똑………");

    private static readonly List<string>[] Cuckoo = Set(
        "뻐꾹.|…몇 시지? 뻐꾹.|문 좀 닫아 줘.|태엽이 뻑뻑해… 뻐꾹.",
        "뻐꾹! 뻐꾹!|정각이다, 뻐꾹!|쪼아 주마!",
        "뻐…꾹?!|용수철이…!",
        "뻐……꾹……|…시간… 다 됐다…");

    private static readonly List<string>[] Leech = Set(
        "…꿀럭.|…따뜻한 게… 온다…|츠읍…|하수구는… 포근하다…",
        "츠으읍!|…빨아 주마…!|꿀럭!",
        "…꾸륵?!|…미끄덩…!",
        "…꾸르…륵…|…말라…간다…");

    private static readonly List<string>[] Bell = Set(
        "…뎅…|물속은… 조용하다…|울리고 싶다…|…누가 날… 건져 줘…",
        "뎅——!|울려 주마!|귀를 막아도 소용없다!",
        "…뎅?!|금이… 간다…!",
        "…뎅………|…다시… 가라앉는다…");

    private static readonly List<string>[] Warden = Set(
        "수문은 닫혀 있다.|…통행증.|돌아가라.|이 문 뒤는 종탑이다.",
        "문은 열리지 않는다!|물러서라!|으으음!",
        "…철판이…!|흠…!",
        "…수문이…… 열린다……|…임무… 끝…");

    private static readonly List<string>[] BellRinger = Set(
        "종은 울리지 않는다…|내가 멈췄다… 내가…|올라오지 마라.|…조용히 해. 종이 듣는다.",
        "울려 주마!|종 대신 네가 울어라!|뎅!",
        "…윽!|종을… 지켜야…!",
        "…이제… 누가 종을…|…미안하다… 마을아…");

    private static readonly List<string>[] Soldier = Set(
        "하나, 둘. 하나, 둘.|이상 무.|…태엽을 감아 줄 사람이 없다.|경계. 경계.|녹이 슬어도 초소는 지킨다.",
        "돌격!|침입자 발견!|찔러!",
        "피해… 보고!|끼익… 관절이!|대열 유지!",
        "임무… 실…패…|…태엽이… 풀린다…");

    private static readonly List<string>[] Doll = Set(
        "같이 놀자… 끼릭.|내 태엽 감아 줄래?|주인님은 어디 갔어?|끼릭… 끼릭…|물이 차서 드레스가 젖었어.",
        "놀자! 놀자!|끼리릭!|안아 줄게!",
        "아얏… 끼릭.|금이 갔어…!|왜 때려…?",
        "태엽이… 다 풀렸…어…|…잘 자……");

    private static readonly List<string>[] Clockmaker = Set(
        "어디 보자, 어디가 고장이지?|요즘 시계는 다 물을 먹었어.|네 태엽, 소리가 안 좋군.|톱니 하나만 빌려 주게.",
        "분해해 주지!|가만있어, 금방 끝나!|조여 주마!",
        "어이쿠!|내 고글!|허리가…!",
        "수리는… 다음에…|…시간이… 없군…");

    private static readonly List<string>[] Hero = Set(
        "…나는 누구였지.|물속이 더 편해.|올라가 봐야… 똑같아.",
        "그만 가라앉아!|너도 잊게 될 거야!",
        "…아파. 아직도 아프네.|…그래, 그게 나였지.",
        "…이제… 올라가…|…기억해 줘…");

    private static readonly List<string>[] Stranger = Set(
        "……|여긴 지나갈 수 없다.|물이 차오른다…",
        "비켜라!|받아라!",
        "큭…!|제법이군.",
        "…여기까지인가…");

    /// <summary>이름으로 말투를 고른다 (그림을 고르는 순서와 같다: '박쥐'가 '쥐'보다, '종지기' · '인형'이 '종'보다 먼저)</summary>
    private static List<string>[] Defaults(string name)
    {
        if (name.Contains("박쥐")) return Bat;
        if (name.Contains("쥐")) return Rat;
        if (name.Contains("벌레")) return Bug;
        if (name.EndsWith("게")) return Crab;
        if (name.Contains("망령")) return Wraith;
        if (name.Contains("뻐꾸기")) return Cuckoo;
        if (name.Contains("수문장")) return Warden;
        if (name.Contains("거머리")) return Leech;
        if (name.Contains("종지기")) return BellRinger;
        if (name.Contains("경비") || name.Contains("병정")) return Soldier;
        if (name.Contains("인형") || name == "틱") return Doll;
        if (name.Contains("종")) return Bell;
        if (name.Contains("시계공")) return Clockmaker;
        if (name.Contains("이안")) return Hero;
        return Stranger;
    }
}

/// <summary>
/// 머리 위 말풍선 — 유닛을 따라다니며 잠깐 떴다 사라진다. 한 유닛에 하나만 (새 말이 나오면 앞의 것은 지운다).
/// 월드 공간에 스프라이트 상자 + 3D 글자로 만든다 (Canvas 없음, 항상 카메라를 본다).
/// 직접 붙일 필요 없음 — SpeechBubble.Show()가 만든다.
/// </summary>
public class SpeechBubble : MonoBehaviour
{
    private const int MaxLineChars = 13;        // 이보다 길면 띄어쓰기에서 줄을 바꾼다
    private const float CharSize = 0.042f;
    private static readonly Color Paper = new Color(0.97f, 0.95f, 0.88f, 0.97f);
    private static readonly Color Edge = new Color(0.1f, 0.08f, 0.09f, 0.95f);
    private static readonly Color InkColor = new Color(0.12f, 0.1f, 0.11f, 1f);

    private static readonly Dictionary<Unit, SpeechBubble> active = new Dictionary<Unit, SpeechBubble>();
    private static Sprite tailSprite;

    private Unit unit;
    private TextMesh text;
    private MeshRenderer textRenderer;
    private SpriteRenderer box, edge, tail, tailEdge;
    private float life, age;
    private bool measured;
    private Vector3 lastPos;

    public static void Show(Unit unit, string line, float life = 2.4f)
    {
        if (unit == null || string.IsNullOrEmpty(line)) return;
        if (active.TryGetValue(unit, out SpeechBubble old) && old != null) Destroy(old.gameObject);

        GameObject go = new GameObject("SpeechBubble");
        SpeechBubble b = go.AddComponent<SpeechBubble>();
        b.unit = unit;
        b.life = life + line.Length * 0.03f;   // 긴 말은 조금 더 오래
        b.Build(Wrap(line));
        b.lastPos = b.Anchor();
        go.transform.position = b.lastPos;
        active[unit] = b;
    }

    /// <summary>떠 있는 말풍선을 모두 지운다 (전투가 끝났을 때)</summary>
    public static void ClearAll()
    {
        foreach (SpeechBubble b in active.Values) if (b != null) Destroy(b.gameObject);
        active.Clear();
    }

    private void Build(string line)
    {
        // 상자: 어두운 테두리 판 위에 종이색 판 (둥근 9-slice), 아래에 꼬리
        Vector2 guess = new Vector2(0.5f + LongestLine(line) * CharSize * 5.6f, 0.34f + (CountLines(line) - 1) * 0.3f);
        edge = ProceduralSprites.WorldBox(transform, "Edge", guess + Vector2.one * 0.07f, Edge, 40);
        box = ProceduralSprites.WorldBox(transform, "Box", guess, Paper, 41);
        tailEdge = Tail("TailEdge", Edge, 40, 0.3f);
        tail = Tail("Tail", Paper, 42, 0.22f);

        GameObject t = new GameObject("Text");
        t.transform.SetParent(transform, false);
        t.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        text = t.AddComponent<TextMesh>();
        text.text = line;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.fontSize = 64;
        text.characterSize = CharSize;
        text.lineSpacing = 0.95f;
        text.color = InkColor;
        textRenderer = t.GetComponent<MeshRenderer>();
        Font font = DamagePopup.GetBuiltinFont();
        if (font != null)
        {
            text.font = font;
            textRenderer.sharedMaterial = font.material;
        }
        textRenderer.sortingOrder = 43;

        Layout(guess);
        transform.localScale = Vector3.one * 0.6f;
    }

    private SpriteRenderer Tail(string name, Color color, int order, float size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localScale = Vector3.one * size;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = TailSprite();
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    /// <summary>글자 크기에 맞춰 상자 · 꼬리를 놓는다</summary>
    private void Layout(Vector2 size)
    {
        box.size = size;
        edge.size = size + Vector2.one * 0.07f;
        float bottom = -size.y * 0.5f;
        tail.transform.localPosition = new Vector3(-size.x * 0.18f, bottom + 0.02f, 0f);
        tailEdge.transform.localPosition = new Vector3(-size.x * 0.18f, bottom - 0.005f, 0f);
    }

    /// <summary>말풍선이 뜰 자리: 머리 위 (이름표 · 체력바보다 위)</summary>
    private Vector3 Anchor()
    {
        if (unit == null) return lastPos;
        if (unit.bodyRenderer != null)
        {
            Bounds b = unit.bodyRenderer.bounds;
            return new Vector3(b.center.x, b.max.y + 1.25f, b.center.z);
        }
        return unit.transform.position + Vector3.up * 3.2f;
    }

    void LateUpdate()
    {
        age += Time.deltaTime;
        bool inBattle = GameManager.Instance == null || GameManager.Instance.State == GameManager.GameState.Battle;
        if (age >= life || !inBattle) { Destroy(gameObject); return; }

        // 글자가 실제로 차지한 크기를 한 번 재서 상자를 맞춘다
        if (!measured && textRenderer != null)
        {
            Vector3 s = textRenderer.localBounds.size;
            if (s.x > 0.01f)
            {
                Layout(new Vector2(s.x + 0.36f, s.y + 0.24f));
                measured = true;
            }
        }

        lastPos = Anchor();
        transform.position = lastPos + Vector3.up * Mathf.Sin(age * 2.2f) * 0.03f;
        Camera cam = Camera.main;
        if (cam != null) transform.rotation = cam.transform.rotation;

        // 통 하고 튀어나왔다가, 끝에서 옅어진다
        float pop = Mathf.Clamp01(age / 0.14f);
        transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, 1f - (1f - pop) * (1f - pop)) * (1f + 0.08f * Mathf.Sin(pop * Mathf.PI));
        float a = Mathf.Clamp01((life - age) / 0.35f);
        SetAlpha(box, Paper, a);
        SetAlpha(tail, Paper, a);
        SetAlpha(edge, Edge, a);
        SetAlpha(tailEdge, Edge, a);
        if (text != null) text.color = new Color(InkColor.r, InkColor.g, InkColor.b, a);
    }

    private static void SetAlpha(SpriteRenderer sr, Color c, float a)
    {
        if (sr != null) sr.color = new Color(c.r, c.g, c.b, c.a * a);
    }

    void OnDestroy()
    {
        if (unit != null && active.TryGetValue(unit, out SpeechBubble b) && b == this) active.Remove(unit);
    }

    // ─────────────────────────── 도우미 ───────────────────────────

    /// <summary>긴 말은 띄어쓰기에서 줄을 바꾼다</summary>
    private static string Wrap(string line)
    {
        if (line.Contains("\n") || line.Length <= MaxLineChars) return line;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        int run = 0;
        foreach (string word in line.Split(' '))
        {
            if (run > 0 && run + word.Length + 1 > MaxLineChars)
            {
                sb.Append('\n');
                run = 0;
            }
            else if (run > 0)
            {
                sb.Append(' ');
                run++;
            }
            sb.Append(word);
            run += word.Length;
        }
        return sb.ToString();
    }

    private static int LongestLine(string text)
    {
        int best = 0;
        foreach (string l in text.Split('\n')) best = Mathf.Max(best, l.Length);
        return best;
    }

    private static int CountLines(string text)
    {
        return text.Split('\n').Length;
    }

    /// <summary>아래로 뾰족한 삼각형 (말풍선 꼬리). 피벗은 윗변 가운데</summary>
    private static Sprite TailSprite()
    {
        if (tailSprite != null) return tailSprite;

        const int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.hideFlags = HideFlags.HideAndDontSave;
        for (int y = 0; y < size; y++)
        {
            float half = (y + 0.5f) / size * size * 0.5f;   // 위로 갈수록 넓어진다
            for (int x = 0; x < size; x++)
            {
                float d = half - Mathf.Abs(x + 0.5f - size * 0.5f + (size - y) * 0.25f);   // 살짝 왼쪽으로 기운 꼬리
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(d + 0.5f)));
            }
        }
        tex.Apply();
        tailSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 1f), size);
        tailSprite.name = "BubbleTail";
        return tailSprite;
    }
}

/// <summary>
/// 계획 단계 동안 적들이 이따금 한마디씩 떠든다 (전투가 시작되면 먼저 누군가 한마디).
/// 직접 붙일 필요 없음 — BattleManager가 전투를 시작할 때 EnemyChatter.BattleStarted()를 부른다.
/// </summary>
public class EnemyChatter : MonoBehaviour
{
    [Tooltip("평소 한마디 사이의 간격 (초, 최소~최대)")]
    public Vector2 interval = new Vector2(7f, 13f);

    private static EnemyChatter instance;
    private float timer;

    public static void BattleStarted()
    {
        if (!Application.isPlaying) return;
        if (instance == null) instance = new GameObject("EnemyChatter").AddComponent<EnemyChatter>();
        EnemyBarks.Forget();
        SpeechBubble.ClearAll();
        instance.timer = 1.1f;   // 계획 단계가 열리고 조금 뒤에 첫마디
    }

    void Update()
    {
        BattleManager bm = BattleManager.Instance;
        bool inBattle = GameManager.Instance == null || GameManager.Instance.State == GameManager.GameState.Battle;
        if (bm == null || !inBattle || bm.Phase != BattleManager.BattlePhase.PlayerTurn || VictoryScreen.IsOpen || LoadingScreen.IsOpen) return;

        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = Random.Range(interval.x, interval.y);

        List<Unit> alive = new List<Unit>();
        foreach (Unit e in bm.Enemies) if (e != null && e.IsAlive) alive.Add(e);
        if (alive.Count > 0) EnemyBarks.Say(alive[Random.Range(0, alive.Count)], BarkKind.Idle);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
