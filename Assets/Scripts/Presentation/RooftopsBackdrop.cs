using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 지붕 위 배경 — 시계 마을 지붕 꼭대기의 평평한 옥상. 에셋 없이 전부 코드로 만든다.
///
///   - 발밑은 타르 칠한 옥상 판, 앞쪽 낮은 난간 너머로 들쭉날쭉한 지붕들이 멀리까지 이어진다
///   - 지붕마다 뾰족한 박공 · 굴뚝 (연기), 창 몇 개에만 불, 멀리 시계탑 하나
///   - 옥상 굴뚝 위 바람개비 (바람 따라 돈다), 빨랫줄에 널린 천 (타격 때 크게 펄럭)
///   - 하늘의 달
///
/// 변형: 0 달밤의 비 / 1 별 맑은 밤 / 2 폭풍 (번개)
/// 씬에 없어도 된다 — BattleStageData.backdrop이 Rooftops인 무대에서 코드로 만들어진다.
/// </summary>
public class RooftopsBackdrop : StageBackdrop
{
    public override BackdropType Type { get { return BackdropType.Rooftops; } }
    public override int VariantCount { get { return 3; } }
    public override string VariantName { get { return Variant == 1 ? "별 맑은 밤" : Variant == 2 ? "폭풍" : "달밤의 비"; } }

    private struct Mood
    {
        public Color fog, ambient, sky, window, moon, roof;
        public float fogDensity, sun, rain;
    }

    private class Cloth
    {
        public Transform t;
        public float phase;
    }

    private Mood mood;
    private readonly List<Cloth> cloths = new List<Cloth>();
    private readonly List<Material> windows = new List<Material>();
    private Transform vane;
    private ParticleSystem rain;
    private float gust, flash, nextBolt = 4f, tempo = 1f;
    private System.Random rng;

    private static Mood MoodFor(int v)
    {
        switch (v)
        {
            case 1:   // 별 맑은 밤: 비 없음, 맑은 남색, 밝은 달
                return new Mood
                {
                    fog = new Color(0.05f, 0.08f, 0.15f), fogDensity = 0.012f, ambient = new Color(0.14f, 0.17f, 0.26f),
                    sky = new Color(0.04f, 0.07f, 0.15f), window = new Color(1f, 0.78f, 0.45f), moon = new Color(0.95f, 0.95f, 1f),
                    roof = new Color(0.3f, 0.3f, 0.36f), sun = 0.55f, rain = 0f,
                };
            case 2:   // 폭풍: 짙은 회색, 굵은 비, 가끔 번개
                return new Mood
                {
                    fog = new Color(0.06f, 0.07f, 0.08f), fogDensity = 0.035f, ambient = new Color(0.09f, 0.1f, 0.12f),
                    sky = new Color(0.05f, 0.06f, 0.07f), window = new Color(1f, 0.7f, 0.4f), moon = new Color(0.6f, 0.65f, 0.7f),
                    roof = new Color(0.24f, 0.25f, 0.27f), sun = 0.25f, rain = 1000f,
                };
            default:  // 달밤의 비
                return new Mood
                {
                    fog = new Color(0.04f, 0.05f, 0.09f), fogDensity = 0.022f, ambient = new Color(0.1f, 0.12f, 0.19f),
                    sky = new Color(0.03f, 0.04f, 0.08f), window = new Color(1f, 0.74f, 0.42f), moon = new Color(0.78f, 0.86f, 1f),
                    roof = new Color(0.28f, 0.27f, 0.3f), sun = 0.38f, rain = 420f,
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
        gust = Mathf.Max(gust, strength);
        if (Variant == 2 && strength >= 0.8f) flash = 1f;   // 폭풍: 큰 타격엔 번개가 같이 친다
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

        gust = Mathf.MoveTowards(gust, 0f, dt * 0.7f);
        float wind = (Variant == 2 ? 1.6f : 1f) * tempo;
        foreach (Cloth c in cloths)
        {
            float swing = (6f + 10f * (wind - 1f)) * Mathf.Sin(t * 2.1f * wind + c.phase) + 40f * gust * Mathf.Sin(t * 9f + c.phase);
            c.t.localRotation = Quaternion.Euler(swing, 0f, 0f);
        }
        if (vane != null) vane.Rotate(0f, (60f * wind + 500f * gust) * dt, 0f, Space.Self);

        // 폭풍: 가끔 저절로 번개
        if (Variant == 2)
        {
            nextBolt -= dt;
            if (nextBolt <= 0f) { flash = 1f; nextBolt = Random.Range(3.5f, 9f) / tempo; }
        }
        if (flash > 0f)
        {
            flash = Mathf.MoveTowards(flash, 0f, dt * 3f);
            float k = flash * (Random.value < 0.3f ? 0.4f : 1f);   // 번쩍이며 두어 번 떨린다
            RenderSettings.ambientLight = Color.Lerp(mood.ambient, new Color(0.7f, 0.75f, 0.9f), k);
            if (Camera.main != null) Camera.main.backgroundColor = Color.Lerp(mood.sky, new Color(0.55f, 0.6f, 0.75f), k);
            if (flash <= 0f) OnShown();
        }

        // 창불은 가끔 꺼졌다 켜진다
        if (windows.Count > 0 && Random.value < 0.02f * tempo)
        {
            Material m = windows[Random.Range(0, windows.Count)];
            bool lit = m.GetColor("_EmissionColor").maxColorComponent > 0.1f;
            m.SetColor("_EmissionColor", lit ? Color.black : mood.window * 1.6f);
        }
    }

    // ─────────────────────────── 무대 구성 ───────────────────────────

    protected override void Build(Transform root)
    {
        mood = MoodFor(Variant);
        rng = VariantRandom(6203);
        cloths.Clear();
        windows.Clear();
        vane = null;
        flash = 0f;

        Material tar = MakeMaterial(new Color(0.16f, 0.16f, 0.18f), 0.2f, Variant == 1 ? 0.3f : 0.8f, PlankTexture(rng, Color.white, 6, 0.3f), new Vector2(3f, 2f));
        Material brick = MakeMaterial(new Color(0.42f, 0.24f, 0.18f), 0f, 0.2f, PlankTexture(rng, Color.white, 12, 0.4f), new Vector2(1f, 1f));
        Material tile = MakeMaterial(mood.roof, 0.1f, 0.5f, PlankTexture(rng, Color.white, 10, 0.5f), new Vector2(2f, 2f));
        Material wall = MakeMaterial(mood.roof * 0.75f + new Color(0.06f, 0.05f, 0.04f), 0f, 0.15f);
        Material iron = MakeMaterial(new Color(0.13f, 0.13f, 0.15f), 0.7f, 0.4f);

        // 옥상 판 + 앞쪽 낮은 난간 + 옆 난간
        Block("Roof", new Vector3(0f, -0.15f, 0f), new Vector3(22f, 0.3f, 14f), tar, root);
        Block("Parapet", new Vector3(0f, 0.3f, 6.8f), new Vector3(22f, 0.6f, 0.4f), brick, root);
        Block("ParapetL", new Vector3(-10.8f, 0.3f, 0f), new Vector3(0.4f, 0.6f, 14f), brick, root);
        Block("ParapetR", new Vector3(10.8f, 0.3f, 0f), new Vector3(0.4f, 0.6f, 14f), brick, root);

        // 옥상 위 굴뚝 두 개 (하나엔 바람개비)
        BuildChimney(root, new Vector3(-7.5f, 0f, 4.5f), 2.4f, brick, iron, true);
        BuildChimney(root, new Vector3(8.2f, 0f, 5.2f), 1.8f, brick, iron, false);

        // 빨랫줄: 기둥 두 개 사이에 줄, 천 몇 장
        Vector3 a = new Vector3(-9.5f, 0f, -2f), b = new Vector3(-9.5f, 0f, 3.5f);
        Block("PoleA", a + Vector3.up * 1.3f, new Vector3(0.12f, 2.6f, 0.12f), iron, root);
        Block("PoleB", b + Vector3.up * 1.3f, new Vector3(0.12f, 2.6f, 0.12f), iron, root);
        Block("Line", new Vector3(a.x, 2.5f, (a.z + b.z) * 0.5f), new Vector3(0.03f, 0.03f, b.z - a.z), iron, root);
        Color[] clothColors = { new Color(0.7f, 0.68f, 0.6f), new Color(0.5f, 0.2f, 0.18f), new Color(0.25f, 0.33f, 0.45f) };
        for (int i = 0; i < 4; i++)
        {
            Transform pivot = new GameObject("Cloth").transform;
            pivot.SetParent(root, false);
            pivot.localPosition = new Vector3(a.x, 2.5f, a.z + 0.8f + i * 1.25f);
            Block("Sheet", new Vector3(0f, -0.45f, 0f), new Vector3(0.02f, 0.9f, 0.9f), MakeMaterial(clothColors[i % clothColors.Length], 0f, 0.1f), pivot);
            cloths.Add(new Cloth { t = pivot, phase = i * 1.3f });
        }

        // 물탱크 (옥상 안쪽 왼편 뒤 — 난간 너머에 두면 아래 지붕들과 겹친다)
        Transform tank = Cylinder("WaterTank", new Vector3(-4.2f, 2.6f, 5.4f), new Vector3(2.2f, 1.2f, 2.2f), Vector3.zero, MakeMaterial(new Color(0.3f, 0.22f, 0.15f), 0f, 0.3f), root);
        Cylinder("TankCap", tank.localPosition + Vector3.up * 1.4f, new Vector3(2.4f, 0.15f, 2.4f), Vector3.zero, iron, root);
        for (int i = 0; i < 4; i++)
        {
            float ang = i * 90f * Mathf.Deg2Rad + 0.78f;
            Block("TankLeg", new Vector3(-4.2f + Mathf.Cos(ang) * 0.9f, 0.7f, 5.4f + Mathf.Sin(ang) * 0.9f), new Vector3(0.12f, 1.4f, 0.12f), iron, root);
        }

        BuildSkyline(root, tile, wall);

        // 달
        Transform moon = Primitive(PrimitiveType.Sphere, "Moon", MakeEmissive(mood.moon, Variant == 2 ? 0.5f : 1.6f), root).transform;
        moon.localPosition = new Vector3(-11f, 15f, 55f);
        moon.localScale = Vector3.one * 6f;
        moon.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        if (Variant == 1)
            MakeMotes(root, "Stars", new Vector3(0f, 22f, 60f), new Vector3(90f, 18f, 2f), 30f, new Color(0.9f, 0.95f, 1f, 0.9f), 0.25f, 0f, 6f);
        if (mood.rain > 0f)
            rain = MakeRain(root, new Vector3(0f, 9f, 5f), new Vector3(26f, 0.5f, 22f), mood.rain, new Color(0.7f, 0.78f, 0.9f, 0.4f), Variant == 2 ? -4f : -1.5f);
        else rain = null;

        // 옥상 가장자리 작은 등 하나 (따뜻한 빛)
        Block("LampPost", new Vector3(4f, 1.2f, 6.4f), new Vector3(0.1f, 2.4f, 0.1f), iron, root);
        Block("LampHead", new Vector3(4f, 2.45f, 6.4f), new Vector3(0.3f, 0.3f, 0.3f), MakeEmissive(mood.window, 2f), root);
        AddLight("RoofLamp", new Vector3(4f, 2.3f, 6f), mood.window, 1.8f, 9f, root).shadows = LightShadows.Soft;
    }

    private void BuildChimney(Transform root, Vector3 at, float height, Material brick, Material iron, bool withVane)
    {
        Block("Chimney", at + Vector3.up * height * 0.5f, new Vector3(1.1f, height, 1.1f), brick, root);
        Block("ChimneyCap", at + Vector3.up * (height + 0.08f), new Vector3(1.3f, 0.16f, 1.3f), brick, root);
        MakeMotes(root, "Smoke", at + Vector3.up * (height + 0.5f), new Vector3(0.4f, 0.2f, 0.4f), 5f, new Color(0.5f, 0.5f, 0.52f, 0.25f), 1.1f, -0.03f, 5f);
        if (!withVane) return;

        Block("VaneRod", at + Vector3.up * (height + 0.8f), new Vector3(0.05f, 1.4f, 0.05f), iron, root);
        vane = new GameObject("Vane").transform;
        vane.SetParent(root, false);
        vane.localPosition = at + Vector3.up * (height + 1.4f);
        Block("Arrow", new Vector3(0f, 0f, 0f), new Vector3(1.1f, 0.06f, 0.06f), iron, vane);
        Block("Tail", new Vector3(-0.5f, 0.12f, 0f), new Vector3(0.3f, 0.3f, 0.03f), iron, vane);
        Block("Cog", new Vector3(0f, -0.25f, 0f), new Vector3(0.25f, 0.06f, 0.25f), MakeMaterial(new Color(0.7f, 0.55f, 0.25f), 0.85f, 0.5f), vane);
    }

    /// <summary>난간 너머 지붕들: 줄마다 높이가 다른 건물 + 박공 지붕 + 굴뚝 + 창, 멀리 시계탑</summary>
    private void BuildSkyline(Transform root, Material tile, Material wall)
    {
        for (int row = 0; row < 4; row++)
        {
            float z = 12f + row * 9f;
            float drop = 2.5f + row * 0.8f;   // 우리 옥상보다 아래에서 시작 (멀수록 더 아래)
            for (float x = -26f; x < 26f; )
            {
                float w = 3f + (float)rng.NextDouble() * 4f;
                float h = 2f + (float)rng.NextDouble() * (3f + row);
                float cx = x + w * 0.5f;
                float baseY = -drop;
                Block("House", new Vector3(cx, baseY + h * 0.5f, z), new Vector3(w - 0.2f, h, 5f), wall, root);

                // 박공: 기울인 판 두 장
                float pitch = 30f + (float)rng.NextDouble() * 15f;
                float half = (w - 0.2f) * 0.5f;
                float slab = half / Mathf.Cos(pitch * Mathf.Deg2Rad);
                float rise = Mathf.Tan(pitch * Mathf.Deg2Rad) * half;
                Transform l = Block("RoofL", new Vector3(cx - half * 0.5f, baseY + h + rise * 0.5f, z), new Vector3(slab + 0.2f, 0.18f, 5.3f), tile, root);
                l.localRotation = Quaternion.Euler(0f, 0f, pitch);
                Transform r = Block("RoofR", new Vector3(cx + half * 0.5f, baseY + h + rise * 0.5f, z), new Vector3(slab + 0.2f, 0.18f, 5.3f), tile, root);
                r.localRotation = Quaternion.Euler(0f, 0f, -pitch);

                if (rng.NextDouble() < 0.45)
                {
                    float chx = cx + ((float)rng.NextDouble() - 0.5f) * w * 0.5f;
                    Block("FarChimney", new Vector3(chx, baseY + h + rise * 0.6f, z + 0.8f), new Vector3(0.5f, rise + 0.8f, 0.5f), wall, root);
                }

                // 창: 앞면에 한두 줄
                for (float wy = baseY + 0.9f; wy < baseY + h - 0.5f; wy += 1.4f)
                    for (float wx = cx - half + 0.6f; wx < cx + half - 0.4f; wx += 1.2f)
                    {
                        bool lit = rng.NextDouble() < 0.3;
                        Material m = lit ? MakeEmissive(mood.window, 1.6f) : MakeMaterial(new Color(0.05f, 0.06f, 0.08f), 0.3f, 0.8f);
                        Block("Window", new Vector3(wx, wy, z - 2.52f), new Vector3(0.5f, 0.7f, 0.05f), m, root);
                        if (lit) windows.Add(m);
                    }
                x += w;
            }
        }

        // 멀리 시계탑
        Vector3 tw = new Vector3(9f, -2f, 44f);
        Block("Tower", tw + Vector3.up * 8f, new Vector3(4f, 16f, 4f), wall, root);
        Transform spire = Block("Spire", tw + Vector3.up * 17.5f, new Vector3(2.9f, 2.9f, 2.9f), tile, root);
        spire.localRotation = Quaternion.Euler(45f, 45f, 0f);
        Cylinder("TowerClock", tw + new Vector3(0f, 13.5f, -2.05f), new Vector3(3f, 0.05f, 3f), new Vector3(90f, 0f, 0f), MakeEmissive(new Color(1f, 0.9f, 0.65f), 1.3f), root);
    }
}
