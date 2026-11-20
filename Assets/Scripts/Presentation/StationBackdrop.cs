using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 멈춘 기차역 배경 — 마지막 열차가 서 버린 승강장. 에셋 없이 전부 코드로 만든다.
///
///   - 발밑은 돌 승강장, 노란 경계선 너머로 한 단 낮은 선로 (침목 + 철로 두 줄)
///   - 선로 위에 멈춰 선 객차 (창 몇 개에만 불, 타격 때 깜빡), 바퀴 밑으로 새는 김
///   - 머리 위 지붕 기둥과 철골 아치, 매달린 큰 역 시계 (초침이 한 칸씩 끊어 간다)
///   - 흔들리는 매달린 등, 벤치와 행선판
///
/// 변형: 0 막차 끊긴 밤 / 1 안개 낀 새벽 / 2 눈 오는 밤
/// 씬에 없어도 된다 — BattleStageData.backdrop이 Station인 무대에서 코드로 만들어진다.
/// </summary>
public class StationBackdrop : StageBackdrop
{
    public override BackdropType Type { get { return BackdropType.Station; } }
    public override int VariantCount { get { return 3; } }
    public override Color GroundPuffColor { get { return Variant == 2 ? new Color(0.95f, 0.97f, 1f, 0.4f) : base.GroundPuffColor; } }   // 눈이면 눈가루
    public override string VariantName { get { return Variant == 1 ? "안개 낀 새벽" : Variant == 2 ? "눈 오는 밤" : "막차 끊긴 밤"; } }

    private const float TrackZ = 5f, RoofY = 7f;

    private Color fog, ambient, sky, lampColor, stoneColor;
    private float fogDensity, sun;
    private readonly List<Transform> lampPivots = new List<Transform>();
    private readonly List<Light> lamps = new List<Light>();
    private readonly List<Material> carWindows = new List<Material>();
    private Transform second, minute, hour;
    private ParticleSystem steam;
    private float sway, flicker, tempo = 1f;
    private System.Random rng;

    // ─────────────────────────── 배경 이벤트 ───────────────────────────

    protected override void OnShown()
    {
        ApplyAtmosphere(fog, fogDensity, ambient, sky, sun);
    }

    protected override void OnImpact(float strength)
    {
        sway = Mathf.Max(sway, strength);
        flicker = Mathf.Max(flicker, 0.1f + 0.3f * strength);
        if (steam != null) steam.Emit(Mathf.RoundToInt(8 + 20 * strength));
    }

    protected override void OnTempo(int emotionLevel)
    {
        tempo = 1f + 0.35f * emotionLevel;
    }

    void Update()
    {
        if (!IsActive) return;
        float t = Time.time, dt = Time.deltaTime;

        // 역 시계: 초침은 한 칸씩 끊어서 (긴장할수록 빨리), 분침 · 시침은 천천히
        float sec = Mathf.Floor(t * tempo);
        if (second != null) second.localRotation = Quaternion.Euler(0f, 0f, -sec * 6f);
        if (minute != null) minute.localRotation = Quaternion.Euler(0f, 0f, -(sec / 60f) * 6f - 130f);
        if (hour != null) hour.localRotation = Quaternion.Euler(0f, 0f, -(sec / 720f) * 6f - 290f);

        sway = Mathf.MoveTowards(sway, 0f, dt * 0.6f);
        for (int i = 0; i < lampPivots.Count; i++)
            lampPivots[i].localRotation = Quaternion.Euler(Mathf.Sin(t * 1.4f + i * 1.7f) * (2f + 16f * sway), 0f, Mathf.Cos(t * 1.1f + i) * (1.5f + 10f * sway));

        float k = 1f;
        if (flicker > 0f) { flicker -= dt; k = Random.value < 0.4f ? Random.Range(0f, 0.3f) : 1.2f; }
        foreach (Light l in lamps) l.intensity = 2f * k;
        if (flicker > 0f && carWindows.Count > 0)
        {
            Material m = carWindows[Random.Range(0, carWindows.Count)];
            m.SetColor("_EmissionColor", lampColor * (Random.value < 0.5f ? 0.1f : 1.4f));
        }
    }

    // ─────────────────────────── 무대 구성 ───────────────────────────

    protected override void Build(Transform root)
    {
        rng = VariantRandom(3331);
        lampPivots.Clear();
        lamps.Clear();
        carWindows.Clear();

        switch (Variant)
        {
            case 1:
                fog = new Color(0.38f, 0.4f, 0.42f); fogDensity = 0.05f; ambient = new Color(0.34f, 0.36f, 0.38f); sky = fog; sun = 0.6f;
                lampColor = new Color(1f, 0.85f, 0.6f); stoneColor = new Color(0.5f, 0.5f, 0.48f);
                break;
            case 2:
                fog = new Color(0.12f, 0.14f, 0.2f); fogDensity = 0.03f; ambient = new Color(0.18f, 0.2f, 0.27f); sky = new Color(0.08f, 0.1f, 0.15f); sun = 0.45f;
                lampColor = new Color(1f, 0.8f, 0.55f); stoneColor = new Color(0.6f, 0.62f, 0.66f);
                break;
            default:
                fog = new Color(0.04f, 0.05f, 0.07f); fogDensity = 0.028f; ambient = new Color(0.1f, 0.1f, 0.13f); sky = new Color(0.02f, 0.03f, 0.05f); sun = 0.3f;
                lampColor = new Color(1f, 0.72f, 0.4f); stoneColor = new Color(0.42f, 0.4f, 0.38f);
                break;
        }

        Texture2D slab = PlankTexture(rng, Color.white, 6, 0.4f);
        Material stone = MakeMaterial(stoneColor, 0f, Variant == 2 ? 0.2f : 0.45f, slab, new Vector2(8f, 3f));
        Material gravel = MakeMaterial(new Color(0.2f, 0.19f, 0.18f), 0f, 0.1f);
        Material wood = MakeMaterial(new Color(0.22f, 0.14f, 0.09f), 0f, 0.25f);
        Material steel = MakeMaterial(new Color(0.3f, 0.3f, 0.32f), 0.85f, 0.6f);
        Material iron = MakeMaterial(new Color(0.12f, 0.13f, 0.14f), 0.7f, 0.4f);
        Material paint = MakeMaterial(new Color(0.15f, 0.25f, 0.2f), 0.3f, 0.4f);
        Material yellow = MakeMaterial(new Color(0.75f, 0.6f, 0.15f), 0f, 0.3f);

        // 승강장 + 경계선 + 낮은 선로 바닥
        Block("Platform", new Vector3(0f, -0.2f, -1f), new Vector3(30f, 0.4f, 7.6f), stone, root);
        Block("EdgeLine", new Vector3(0f, 0.005f, 2.55f), new Vector3(30f, 0.01f, 0.25f), yellow, root);
        Block("PlatformLip", new Vector3(0f, -0.55f, 2.8f), new Vector3(30f, 0.7f, 0.2f), stone, root);
        Block("TrackBed", new Vector3(0f, -1f, TrackZ + 1f), new Vector3(60f, 0.2f, 6f), gravel, root);
        for (float x = -28f; x < 28f; x += 1.1f)
            Block("Sleeper", new Vector3(x, -0.85f, TrackZ), new Vector3(0.35f, 0.12f, 2.6f), wood, root);
        Block("RailA", new Vector3(0f, -0.72f, TrackZ - 0.75f), new Vector3(60f, 0.12f, 0.1f), steel, root);
        Block("RailB", new Vector3(0f, -0.72f, TrackZ + 0.75f), new Vector3(60f, 0.12f, 0.1f), steel, root);

        BuildCarriage(root, paint, iron);

        // 건너편 벽 (아치 창 줄지어)
        Block("FarWall", new Vector3(0f, 3f, 12f), new Vector3(60f, 8f, 0.5f), MakeMaterial(stoneColor * 0.6f, 0f, 0.15f, slab, new Vector2(12f, 3f)), root);
        for (float x = -18f; x <= 18f; x += 4.5f)
        {
            Color win = Variant == 1 ? new Color(0.7f, 0.72f, 0.7f) : new Color(0.12f, 0.16f, 0.24f);
            Block("ArchWindow", new Vector3(x, 3.6f, 11.62f), new Vector3(1.6f, 3f, 0.05f), MakeEmissive(win, Variant == 1 ? 0.6f : 0.9f), root);
            Cylinder("ArchTop", new Vector3(x, 5.1f, 11.62f), new Vector3(1.6f, 0.03f, 1.6f), new Vector3(90f, 0f, 0f), MakeEmissive(win, Variant == 1 ? 0.6f : 0.9f), root);
        }

        // 지붕 기둥 + 철골 아치 (머리 위)
        foreach (float x in new[] { -12f, -6f, 6f, 12f })   // 가운데는 비워 둔다 (싸우는 자리가 가리지 않게)
        {
            Cylinder("Pillar", new Vector3(x, RoofY * 0.5f, 2.1f), new Vector3(0.35f, RoofY * 0.5f, 0.35f), Vector3.zero, iron, root);
            Block("Bracket", new Vector3(x, RoofY - 0.4f, 0.5f), new Vector3(0.15f, 0.15f, 4.2f), iron, root);
            Transform brace = Block("Brace", new Vector3(x, RoofY - 1.1f, 1.2f), new Vector3(0.1f, 0.1f, 2.2f), iron, root);
            brace.localRotation = Quaternion.Euler(35f, 0f, 0f);
        }
        Block("RoofEdge", new Vector3(0f, RoofY, -1.4f), new Vector3(30f, 0.25f, 0.25f), iron, root);
        Block("Canopy", new Vector3(0f, RoofY + 0.15f, 0.6f), new Vector3(30f, 0.12f, 4.2f), MakeMaterial(new Color(0.16f, 0.15f, 0.15f), 0.4f, 0.3f), root);

        BuildClock(root, iron);

        // 매달린 등
        for (int i = 0; i < 4; i++)
        {
            float x = -9f + i * 6f;
            Transform pivot = new GameObject("LampPivot").transform;
            pivot.SetParent(root, false);
            pivot.localPosition = new Vector3(x, RoofY - 0.1f, 0.3f);
            Block("LampRod", new Vector3(0f, -0.6f, 0f), new Vector3(0.04f, 1.2f, 0.04f), iron, pivot);
            Block("LampShade", new Vector3(0f, -1.25f, 0f), new Vector3(0.6f, 0.15f, 0.6f), paint, pivot);
            Primitive(PrimitiveType.Sphere, "LampBulb", MakeEmissive(lampColor, 2.4f), pivot).transform.localPosition = new Vector3(0f, -1.4f, 0f);
            pivot.GetChild(2).localScale = Vector3.one * 0.25f;
            Light l = AddLight("Lamp", new Vector3(0f, -1.6f, 0f), lampColor, 2f, 8f, pivot);
            if (i == 1 || i == 2) l.shadows = LightShadows.Soft;
            lampPivots.Add(pivot);
            lamps.Add(l);
        }

        // 벤치 + 행선판
        BuildBench(root, new Vector3(-8.6f, 0f, 1.9f), wood, iron);   // 싸우는 자리 뒤쪽 (카메라와 사이에 두지 않는다)
        BuildBench(root, new Vector3(8.8f, 0f, 1.9f), wood, iron);
        Block("SignPost", new Vector3(-11f, 1.3f, 1.5f), new Vector3(0.12f, 2.6f, 0.12f), iron, root);
        Block("Sign", new Vector3(-11f, 2.6f, 1.5f), new Vector3(2.2f, 0.6f, 0.08f), paint, root);
        Block("SignText", new Vector3(-11f, 2.6f, 1.45f), new Vector3(1.8f, 0.12f, 0.02f), MakeEmissive(new Color(0.9f, 0.85f, 0.7f), 0.5f), root);

        steam = MakeMotes(root, "Steam", new Vector3(0f, -0.4f, TrackZ), new Vector3(18f, 0.3f, 1.5f), 10f, new Color(0.8f, 0.82f, 0.85f, 0.18f), 1.4f, -0.02f, 4f);
        if (Variant == 2)
            MakeMotes(root, "Snow", new Vector3(0f, 9f, 5f), new Vector3(30f, 0.5f, 14f), 90f, new Color(1f, 1f, 1f, 0.85f), 0.07f, 0.04f, 9f);
    }

    private void BuildCarriage(Transform root, Material paint, Material iron)
    {
        Transform car = new GameObject("Carriage").transform;
        car.SetParent(root, false);
        car.localPosition = new Vector3(-2f, 0f, TrackZ);
        Material body = MakeMaterial(new Color(0.32f, 0.12f, 0.1f), 0.4f, 0.45f);
        Material trim = MakeMaterial(new Color(0.68f, 0.52f, 0.25f), 0.8f, 0.5f);

        Block("Body", new Vector3(0f, 1.3f, 0f), new Vector3(16f, 2.8f, 2.4f), body, car);
        Block("Roof", new Vector3(0f, 2.85f, 0f), new Vector3(16.3f, 0.3f, 2.7f), iron, car);
        Block("RoofHump", new Vector3(0f, 3.1f, 0f), new Vector3(15f, 0.25f, 1.6f), iron, car);
        Block("TrimLine", new Vector3(0f, 0.55f, -1.22f), new Vector3(16f, 0.08f, 0.02f), trim, car);
        Block("Underframe", new Vector3(0f, -0.25f, 0f), new Vector3(15f, 0.4f, 2f), iron, car);
        for (int i = 0; i < 4; i++)
        {
            float x = -6f + i * 4f;
            Cylinder("Wheel", new Vector3(x, -0.4f, -1f), new Vector3(0.8f, 0.06f, 0.8f), new Vector3(90f, 0f, 0f), iron, car);
        }
        for (float x = -7f; x <= 7f; x += 1.6f)
        {
            bool lit = rng.NextDouble() < 0.5;
            Material m = lit ? MakeEmissive(lampColor, 1.4f) : MakeMaterial(new Color(0.04f, 0.05f, 0.06f), 0.4f, 0.85f);
            Block("CarWindow", new Vector3(x, 1.7f, -1.21f), new Vector3(1f, 0.9f, 0.03f), m, car);
            if (lit) carWindows.Add(m);
        }
        Block("Door", new Vector3(7.4f, 1.2f, -1.21f), new Vector3(0.9f, 2.2f, 0.03f), MakeMaterial(new Color(0.2f, 0.08f, 0.07f), 0.3f, 0.4f), car);
    }

    private void BuildClock(Transform root, Material iron)
    {
        Transform clock = new GameObject("StationClock").transform;
        clock.SetParent(root, false);
        clock.localPosition = new Vector3(1.5f, RoofY - 1.5f, 0.6f);
        Block("ClockRod", new Vector3(0f, 0.9f, 0f), new Vector3(0.08f, 0.9f, 0.08f), iron, clock);
        Cylinder("Case", Vector3.zero, new Vector3(1.5f, 0.08f, 1.5f), new Vector3(90f, 0f, 0f), iron, clock);
        Cylinder("Face", new Vector3(0f, 0f, -0.1f), new Vector3(1.3f, 0.02f, 1.3f), new Vector3(90f, 0f, 0f), MakeEmissive(new Color(0.95f, 0.9f, 0.75f), 0.9f), clock);
        Material ink = MakeMaterial(new Color(0.05f, 0.05f, 0.05f), 0f, 0.2f);
        for (int i = 0; i < 12; i++)
        {
            float a = i * 30f * Mathf.Deg2Rad;
            Block("Tick", new Vector3(Mathf.Sin(a) * 0.55f, Mathf.Cos(a) * 0.55f, -0.13f), new Vector3(0.04f, 0.1f, 0.01f), ink, clock).localRotation = Quaternion.Euler(0f, 0f, -i * 30f);
        }
        hour = Hand(clock, "Hour", 0.32f, 0.06f, ink, -0.14f);
        minute = Hand(clock, "Minute", 0.5f, 0.04f, ink, -0.15f);
        second = Hand(clock, "Second", 0.55f, 0.015f, MakeMaterial(new Color(0.7f, 0.1f, 0.08f), 0f, 0.3f), -0.16f);
    }

    private Transform Hand(Transform clock, string name, float length, float width, Material mat, float z)
    {
        Transform pivot = new GameObject(name).transform;
        pivot.SetParent(clock, false);
        pivot.localPosition = new Vector3(0f, 0f, z);
        Block("Blade", new Vector3(0f, length * 0.45f, 0f), new Vector3(width, length, 0.01f), mat, pivot);
        return pivot;
    }

    private void BuildBench(Transform root, Vector3 at, Material wood, Material iron)
    {
        Block("Seat", at + new Vector3(0f, 0.45f, 0f), new Vector3(2.2f, 0.08f, 0.5f), wood, root);
        Block("Back", at + new Vector3(0f, 0.8f, -0.25f), new Vector3(2.2f, 0.5f, 0.06f), wood, root);
        Block("LegL", at + new Vector3(-0.95f, 0.22f, 0f), new Vector3(0.08f, 0.45f, 0.45f), iron, root);
        Block("LegR", at + new Vector3(0.95f, 0.22f, 0f), new Vector3(0.08f, 0.45f, 0.45f), iron, root);
    }
}
