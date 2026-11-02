using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 물에 잠긴 거리 배경 — 발목까지 물이 찬 넓은 큰길. 에셋 없이 전부 코드로 만든다.
///
///   - 거울처럼 번들거리는 물바닥 + 빗방울 파문, 반쯤 잠긴 보도 턱
///   - 양옆 석조 건물 (창 몇 개에만 불), 반쯤 잠긴 가로등 2개
///   - 둥둥 떠다니는 나무상자 · 통 (천천히 출렁임, 타격 때 크게 흔들림)
///   - 길 끝 멀리 종탑 실루엣과 빛나는 시계판, 하늘의 달(해)
///
/// 변형: 0 달밤의 비 / 1 새벽 안개 / 2 붉은 노을
/// 씬에 없어도 된다 — BattleStageData.backdrop이 FloodedStreet인 무대에서 코드로 만들어진다.
/// </summary>
public class FloodedStreetBackdrop : StageBackdrop
{
    public override BackdropType Type { get { return BackdropType.FloodedStreet; } }
    public override int VariantCount { get { return 3; } }
    public override string VariantName { get { return Variant == 1 ? "새벽 안개" : Variant == 2 ? "붉은 노을" : "달밤의 비"; } }

    private struct Mood
    {
        public Color fog, ambient, sky, lamp, celestial, water, stone;
        public float fogDensity, sun, rain;
    }

    private class Floater
    {
        public Transform t;
        public Vector3 basePos;
        public float phase, sway;
    }

    private Mood mood;
    private readonly List<Floater> floaters = new List<Floater>();
    private readonly List<Light> lamps = new List<Light>();
    private Transform towerMinute, towerHour;
    private ParticleSystem rain, ripples;
    private float flicker, jolt, tempo = 1f;
    private System.Random rng;

    private Mood MoodFor(int v)
    {
        switch (v)
        {
            case 1:   // 새벽 안개: 희뿌연 청록, 비 없음, 등은 꺼져 가는 중
                return new Mood
                {
                    fog = new Color(0.36f, 0.42f, 0.44f), fogDensity = 0.045f, ambient = new Color(0.36f, 0.4f, 0.42f),
                    sky = new Color(0.42f, 0.48f, 0.5f), lamp = new Color(1f, 0.85f, 0.6f), celestial = new Color(1f, 0.95f, 0.85f),
                    water = new Color(0.12f, 0.17f, 0.18f), stone = new Color(0.5f, 0.52f, 0.52f), sun = 0.7f, rain = 0f,
                };
            case 2:   // 붉은 노을: 주황 하늘, 긴 그림자, 가랑비
                return new Mood
                {
                    fog = new Color(0.45f, 0.2f, 0.12f), fogDensity = 0.022f, ambient = new Color(0.32f, 0.18f, 0.14f),
                    sky = new Color(0.55f, 0.24f, 0.14f), lamp = new Color(1f, 0.6f, 0.3f), celestial = new Color(1f, 0.55f, 0.25f),
                    water = new Color(0.16f, 0.08f, 0.06f), stone = new Color(0.46f, 0.36f, 0.32f), sun = 0.9f, rain = 60f,
                };
            default:  // 달밤의 비: 짙은 남색, 차가운 달빛, 비
                return new Mood
                {
                    fog = new Color(0.04f, 0.06f, 0.1f), fogDensity = 0.03f, ambient = new Color(0.1f, 0.13f, 0.2f),
                    sky = new Color(0.03f, 0.05f, 0.09f), lamp = new Color(1f, 0.75f, 0.45f), celestial = new Color(0.75f, 0.85f, 1f),
                    water = new Color(0.03f, 0.06f, 0.09f), stone = new Color(0.36f, 0.38f, 0.42f), sun = 0.35f, rain = 520f,
                };
        }
    }

    // ─────────────────────────── 배경 이벤트 ───────────────────────────

    protected override void OnShown()
    {
        ApplyAtmosphere(mood.fog, mood.fogDensity, mood.ambient, mood.sky, mood.sun);
    }

    protected override void OnImpact(float strength)
    {
        flicker = Mathf.Max(flicker, 0.1f + 0.3f * strength);
        jolt = Mathf.Max(jolt, strength);
        if (ripples != null) ripples.Emit(Mathf.RoundToInt(6 + 14 * strength));   // 물이 크게 출렁인다
    }

    protected override void OnTempo(int emotionLevel)
    {
        tempo = 1f + 0.35f * emotionLevel;
        if (rain != null)
        {
            ParticleSystem.EmissionModule e = rain.emission;
            e.rateOverTime = mood.rain * tempo;
        }
    }

    void Update()
    {
        if (!IsActive) return;
        float t = Time.time, dt = Time.deltaTime;

        jolt = Mathf.MoveTowards(jolt, 0f, dt * 0.8f);
        foreach (Floater f in floaters)
        {
            float amp = 0.05f + 0.25f * jolt;
            f.t.localPosition = f.basePos + new Vector3(Mathf.Sin(t * 0.3f + f.phase) * 0.15f, Mathf.Sin(t * 1.3f + f.phase) * amp, 0f);
            f.t.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.9f + f.phase) * (4f + 18f * jolt), f.sway, Mathf.Cos(t * 1.1f + f.phase) * (3f + 14f * jolt));
        }

        float k = 1f;
        if (flicker > 0f) { flicker -= dt; k = Random.value < 0.4f ? Random.Range(0f, 0.3f) : 1.2f; }
        foreach (Light l in lamps) l.intensity = 2.4f * k;

        // 멀리 종탑 시계는 아주 천천히 (감정이 오르면 조금 빨리)
        if (towerMinute != null) towerMinute.localRotation = Quaternion.Euler(0f, 0f, -t * 6f * tempo);
        if (towerHour != null) towerHour.localRotation = Quaternion.Euler(0f, 0f, -t * 0.5f * tempo);
    }

    // ─────────────────────────── 무대 구성 ───────────────────────────

    protected override void Build(Transform root)
    {
        mood = MoodFor(Variant);
        rng = VariantRandom(4417);
        floaters.Clear();
        lamps.Clear();

        // 물바닥 (거울처럼) + 그 아래 돌바닥이 비치는 느낌은 색으로만
        Transform water = Block("Water", new Vector3(0f, -0.01f, 20f), new Vector3(30f, 0.02f, 80f), MakeMaterial(mood.water, 0.9f, 0.97f), root);
        water.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        Texture2D stoneTex = PlankTexture(rng, Color.white, 8, 0.4f);
        Material stone = MakeMaterial(mood.stone, 0f, 0.15f, stoneTex, new Vector2(3f, 4f));
        Material stoneDark = MakeMaterial(mood.stone * 0.7f, 0f, 0.15f, stoneTex, new Vector2(3f, 5f));
        Material iron = MakeMaterial(new Color(0.12f, 0.13f, 0.14f), 0.7f, 0.4f);

        // 보도 턱 (물 위로 살짝)
        Block("CurbL", new Vector3(-7.6f, 0.06f, 20f), new Vector3(1.6f, 0.14f, 80f), stoneDark, root);
        Block("CurbR", new Vector3(7.6f, 0.06f, 20f), new Vector3(1.6f, 0.14f, 80f), stoneDark, root);

        BuildBuildings(root, stone, stoneDark);
        BuildTower(root, stoneDark);
        BuildLamps(root, iron);
        BuildFloaters(root);

        // 하늘의 달 / 해
        Transform orb = Primitive(PrimitiveType.Sphere, "Celestial", MakeEmissive(mood.celestial, 2.5f), root).transform;
        orb.localPosition = new Vector3(-14f, 26f, 90f);
        orb.localScale = Vector3.one * 7f;

        if (mood.rain > 0f) rain = MakeRain(root, new Vector3(0f, 14f, 10f), new Vector3(18f, 0.1f, 30f), mood.rain, new Color(0.75f, 0.8f, 0.9f, 0.32f));
        else rain = null;
        ripples = BuildRipples(root);
        if (Variant == 1) MakeMotes(root, "Mist", new Vector3(0f, 0.6f, 6f), new Vector3(22f, 1f, 16f), 10f, new Color(0.8f, 0.85f, 0.88f, 0.18f), 3f, 0f, 8f);
    }

    private void BuildBuildings(Transform root, Material stone, Material stoneDark)
    {
        Material dark = MakeMaterial(new Color(0.03f, 0.035f, 0.04f), 0.3f, 0.9f);
        Material lit = MakeEmissive(mood.lamp, 1.2f);
        Material frame = MakeMaterial(new Color(0.1f, 0.09f, 0.08f), 0f, 0.2f);

        for (int side = -1; side <= 1; side += 2)
        {
            float z = -8f;
            while (z < 60f)
            {
                float len = 6f + (float)rng.NextDouble() * 6f;
                float h = 8f + (float)rng.NextDouble() * 9f;
                float x = side * (9.5f + (float)rng.NextDouble() * 0.8f);
                Material m = rng.NextDouble() < 0.5 ? stone : stoneDark;
                Block("Building", new Vector3(x, h * 0.5f - 0.5f, z + len * 0.5f), new Vector3(3f, h, len - 0.3f), m, root);
                Block("Cornice", new Vector3(x - side * 0.2f, h - 0.4f, z + len * 0.5f), new Vector3(3.5f, 0.35f, len), stoneDark, root);

                // 창: 층마다 두세 개, 몇 개에만 불
                float face = x - side * 1.52f;
                for (float wy = 2.2f; wy < h - 1.5f; wy += 2.6f)
                {
                    for (float wz = z + 1.2f; wz < z + len - 1f; wz += 2f)
                    {
                        Material glass = rng.NextDouble() < 0.16 ? lit : dark;
                        Block("WinFrame", new Vector3(face, wy, wz), new Vector3(0.06f, 1.5f, 1f), frame, root);
                        Block("Window", new Vector3(face - side * 0.02f, wy, wz), new Vector3(0.05f, 1.3f, 0.8f), glass, root);
                    }
                }
                // 1층 문은 물에 반쯤 잠겼다
                Block("Door", new Vector3(face, 0.7f, z + len * 0.5f), new Vector3(0.08f, 1.9f, 1.2f), frame, root);
                z += len;
            }
        }
    }

    private void BuildTower(Transform root, Material stoneDark)
    {
        // 길 끝, 안개 너머의 종탑 실루엣 + 빛나는 시계판
        Material silhouette = MakeMaterial(mood.fog * 0.6f, 0f, 0.1f);
        Block("Tower", new Vector3(2f, 16f, 75f), new Vector3(7f, 32f, 7f), silhouette, root);
        Block("TowerTop", new Vector3(2f, 33.5f, 75f), new Vector3(5f, 3f, 5f), silhouette, root);
        Transform spire = Primitive(PrimitiveType.Cylinder, "Spire", silhouette, root).transform;
        spire.localPosition = new Vector3(2f, 37f, 75f);
        spire.localScale = new Vector3(2.5f, 2.5f, 2.5f);

        Transform clock = new GameObject("TowerClock").transform;
        clock.SetParent(root, false);
        clock.localPosition = new Vector3(2f, 26f, 71.4f);
        Transform face = Cylinder("Face", Vector3.zero, new Vector3(4.6f, 0.05f, 4.6f), new Vector3(90f, 0f, 0f), MakeEmissive(new Color(1f, 0.85f, 0.55f), 1.4f), clock);
        face.localPosition = Vector3.zero;
        Material handMat = MakeMaterial(new Color(0.05f, 0.04f, 0.03f), 0f, 0.2f);
        towerMinute = Hand(clock, 1.9f, 0.14f, handMat, -0.08f);
        towerHour = Hand(clock, 1.2f, 0.22f, handMat, -0.1f);
    }

    private Transform Hand(Transform clock, float length, float width, Material mat, float z)
    {
        Transform pivot = new GameObject("Hand").transform;
        pivot.SetParent(clock, false);
        pivot.localPosition = new Vector3(0f, 0f, z);
        Block("Bar", new Vector3(0f, length * 0.5f, 0f), new Vector3(width, length, 0.04f), mat, pivot);
        return pivot;
    }

    private void BuildLamps(Transform root, Material iron)
    {
        Vector3[] spots = { new Vector3(-6.4f, 0f, 5f), new Vector3(6.3f, 0f, 14f) };
        foreach (Vector3 p in spots)
        {
            float lean = (float)rng.NextDouble() * 6f - 3f;   // 물살에 살짝 기울었다
            Transform pole = Cylinder("LampPole", p + Vector3.up * 2.6f, new Vector3(0.16f, 2.6f, 0.16f), new Vector3(0f, 0f, lean), iron, root);
            Transform head = Block("LampHead", p + new Vector3(0f, 5.3f, 0f), new Vector3(0.55f, 0.6f, 0.55f), MakeEmissive(mood.lamp, 2f), root);
            head.localRotation = pole.localRotation;
            Light l = AddLight("Lamp", p + new Vector3(0f, 5f, 0f), mood.lamp, 2.4f, 12f, root);
            l.shadows = LightShadows.Soft;
            lamps.Add(l);
        }
    }

    private void BuildFloaters(Transform root)
    {
        Material wood = MakeMaterial(new Color(0.3f, 0.21f, 0.13f), 0f, 0.25f);
        Material barrel = MakeMaterial(new Color(0.28f, 0.16f, 0.1f), 0.5f, 0.35f);
        Vector3[] spots = { new Vector3(-4.5f, 0.1f, 6f), new Vector3(-2f, 0.1f, 12f), new Vector3(5.6f, 0.1f, 4f), new Vector3(3f, 0.1f, 18f), new Vector3(-5.5f, 0.1f, 20f) };
        for (int i = 0; i < spots.Length; i++)
        {
            bool isBarrel = rng.NextDouble() < 0.4;
            Transform t = isBarrel
                ? Cylinder("Barrel", spots[i], new Vector3(0.7f, 0.35f, 0.7f), new Vector3(90f, 0f, 0f), barrel, root)
                : Block("Crate", spots[i], Vector3.one * (0.6f + (float)rng.NextDouble() * 0.4f), wood, root);
            floaters.Add(new Floater { t = t, basePos = spots[i], phase = (float)rng.NextDouble() * 6f, sway = (float)rng.NextDouble() * 360f });
        }
    }

    /// <summary>물 위 파문: 납작하게 누운 고리가 퍼지며 사라진다 (빗방울 / 타격)</summary>
    private ParticleSystem BuildRipples(Transform root)
    {
        GameObject go = new GameObject("Ripples");
        go.transform.SetParent(root, false);
        go.transform.localPosition = new Vector3(0f, 0.02f, 6f);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.startLifetime = 1.1f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        main.startColor = new Color(0.85f, 0.9f, 1f, 0.35f);
        main.maxParticles = 400;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = mood.rain > 0f ? 25f + mood.rain * 0.03f : 4f;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(16f, 0f, 14f);

        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.2f, 1f, 1.6f));

        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
        pr.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        pr.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = RingTexture() };

        ps.Play();
        return ps;
    }

    private static Texture2D ringTex;

    private static Texture2D RingTexture()
    {
        if (ringTex != null) return ringTex;
        const int size = 64;
        ringTex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float r = size * 0.42f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f));
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - r) / 2.2f);
                ringTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        ringTex.Apply();
        return ringTex;
    }
}
