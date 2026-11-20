using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 밤 장터 배경 — 문 닫은 좌판들이 늘어선 시계 마을의 시장 골목. 에셋 없이 전부 코드로 만든다.
///
///   - 돌바닥 거리, 양옆으로 줄무늬 차양을 친 좌판 (상자 · 항아리 · 걸린 시계 같은 물건들)
///   - 좌판 뒤 2층 가게 건물, 거리 끝 시계가 달린 아치 문
///   - 거리를 가로지르는 등롱 줄 (바람에 흔들리고, 타격 때 크게 출렁)
///
/// 변형: 0 등롱 밤장 / 1 비 오는 파장 / 2 축제 밤 (색색의 등 + 꽃종이)
/// 씬에 없어도 된다 — BattleStageData.backdrop이 Market인 무대에서 코드로 만들어진다.
/// </summary>
public class MarketBackdrop : StageBackdrop
{
    public override BackdropType Type { get { return BackdropType.Market; } }
    public override int VariantCount { get { return 3; } }
    public override string VariantName { get { return Variant == 1 ? "비 오는 파장" : Variant == 2 ? "축제 밤" : "등롱 밤장"; } }

    private const float StallX = 7.5f;

    private Color fog, ambient, sky;
    private float fogDensity, sun;

    private class Lantern
    {
        public Transform pivot;
        public float phase;
        public Light light;
    }

    private readonly List<Lantern> lanterns = new List<Lantern>();
    private ParticleSystem rain;
    private float sway, tempo = 1f;
    private System.Random rng;

    private static readonly Color[] awningColors =
    {
        new Color(0.55f, 0.15f, 0.12f), new Color(0.2f, 0.32f, 0.45f), new Color(0.6f, 0.45f, 0.15f), new Color(0.22f, 0.38f, 0.25f),
    };

    // ─────────────────────────── 배경 이벤트 ───────────────────────────

    protected override void OnShown()
    {
        ApplyAtmosphere(fog, fogDensity, ambient, sky, sun);
    }

    protected override void OnImpact(float strength)
    {
        sway = Mathf.Max(sway, strength);
    }

    protected override void OnTempo(int emotionLevel)
    {
        tempo = 1f + 0.35f * emotionLevel;
        if (rain != null)
        {
            ParticleSystem.EmissionModule e = rain.emission;
            e.rateOverTime = 450f * tempo;
        }
    }

    void Update()
    {
        if (!IsActive) return;
        float t = Time.time, dt = Time.deltaTime;
        sway = Mathf.MoveTowards(sway, 0f, dt * 0.6f);
        float wind = Variant == 1 ? 1.5f : 1f;
        foreach (Lantern l in lanterns)
        {
            l.pivot.localRotation = Quaternion.Euler(Mathf.Sin(t * 1.5f * wind + l.phase) * (4f + 20f * sway), 0f, Mathf.Cos(t * 1.2f + l.phase) * (3f + 14f * sway));
            if (l.light != null) l.light.intensity = 1.3f * (0.9f + 0.1f * Mathf.PerlinNoise(t * 3f, l.phase));   // 촛불처럼 살짝 일렁
        }
    }

    // ─────────────────────────── 무대 구성 ───────────────────────────

    protected override void Build(Transform root)
    {
        rng = VariantRandom(8087);
        lanterns.Clear();

        switch (Variant)
        {
            case 1:
                fog = new Color(0.05f, 0.06f, 0.08f); fogDensity = 0.032f; ambient = new Color(0.1f, 0.11f, 0.14f); sky = new Color(0.03f, 0.04f, 0.06f); sun = 0.3f;
                break;
            case 2:
                fog = new Color(0.12f, 0.06f, 0.1f); fogDensity = 0.02f; ambient = new Color(0.22f, 0.15f, 0.2f); sky = new Color(0.08f, 0.04f, 0.09f); sun = 0.35f;
                break;
            default:
                fog = new Color(0.08f, 0.05f, 0.04f); fogDensity = 0.025f; ambient = new Color(0.17f, 0.12f, 0.1f); sky = new Color(0.05f, 0.03f, 0.03f); sun = 0.3f;
                break;
        }

        Texture2D cobbleTex = PlankTexture(rng, Color.white, 18, 0.6f);
        Material cobble = MakeMaterial(new Color(0.36f, 0.33f, 0.3f), 0.1f, Variant == 1 ? 0.85f : 0.35f, cobbleTex, new Vector2(10f, 14f));
        Material wood = MakeMaterial(new Color(0.3f, 0.19f, 0.11f), 0f, 0.25f);
        Material darkWood = MakeMaterial(new Color(0.16f, 0.1f, 0.06f), 0f, 0.25f);
        Material plaster = MakeMaterial(new Color(0.42f, 0.36f, 0.3f), 0f, 0.1f, PlankTexture(rng, Color.white, 4, 0.3f), new Vector2(2f, 2f));
        Material iron = MakeMaterial(new Color(0.12f, 0.12f, 0.13f), 0.7f, 0.4f);
        Material rope = MakeMaterial(new Color(0.25f, 0.2f, 0.14f), 0f, 0.1f);

        Block("Street", new Vector3(0f, -0.1f, 10f), new Vector3(StallX * 2f + 8f, 0.2f, 44f), cobble, root);
        Block("Gutter", new Vector3(0f, 0.002f, 10f), new Vector3(0.4f, 0.01f, 44f), MakeMaterial(new Color(0.1f, 0.1f, 0.12f), 0.6f, 0.9f), root);

        // 좌판: 양옆으로 줄지어
        for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < 4; i++)
                BuildStall(root, new Vector3(side * StallX, 0f, -1f + i * 5.5f), side, wood, darkWood, iron);

        // 좌판 뒤 가게 건물 (2층, 창에 불 조금)
        Color win = Variant == 2 ? new Color(1f, 0.6f, 0.75f) : new Color(1f, 0.72f, 0.4f);
        for (int side = -1; side <= 1; side += 2)
            for (float z = -3f; z < 28f; z += 5f)
            {
                float h = 6f + (float)rng.NextDouble() * 2.5f;
                Block("Shop", new Vector3(side * (StallX + 4f), h * 0.5f, z + 2.5f), new Vector3(4f, h, 4.8f), plaster, root);
                Block("Eave", new Vector3(side * (StallX + 1.9f), h - 0.2f, z + 2.5f), new Vector3(0.6f, 0.2f, 5f), darkWood, root);
                for (float y = 3.6f; y < h - 0.8f; y += 1.8f)
                {
                    bool lit = rng.NextDouble() < 0.4;
                    Block("ShopWindow", new Vector3(side * (StallX + 1.98f), y, z + 2.5f), new Vector3(0.05f, 0.9f, 1.2f),
                        lit ? MakeEmissive(win, 1.4f) : MakeMaterial(new Color(0.05f, 0.05f, 0.06f), 0.3f, 0.8f), root);
                }
            }

        // 거리 끝 아치 문 + 시계
        Vector3 g = new Vector3(0f, 0f, 30f);
        Block("GateL", g + new Vector3(-3.5f, 3.5f, 0f), new Vector3(1.4f, 7f, 1.4f), plaster, root);
        Block("GateR", g + new Vector3(3.5f, 3.5f, 0f), new Vector3(1.4f, 7f, 1.4f), plaster, root);
        Block("GateTop", g + new Vector3(0f, 7.6f, 0f), new Vector3(8.4f, 1.6f, 1.6f), plaster, root);
        Cylinder("GateClock", g + new Vector3(0f, 7.6f, -0.85f), new Vector3(1.4f, 0.05f, 1.4f), new Vector3(90f, 0f, 0f), MakeEmissive(new Color(1f, 0.9f, 0.65f), 1.2f), root);
        Block("GateGlow", g + new Vector3(0f, 3f, 2f), new Vector3(5.6f, 6f, 0.1f), MakeEmissive(Variant == 2 ? new Color(0.9f, 0.4f, 0.5f) : new Color(0.5f, 0.35f, 0.25f), 0.5f), root);

        // 등롱 줄: 거리를 가로질러 몇 줄
        Color[] festive = { new Color(1f, 0.35f, 0.3f), new Color(1f, 0.8f, 0.3f), new Color(0.4f, 0.8f, 1f), new Color(0.6f, 1f, 0.5f), new Color(1f, 0.5f, 0.85f) };
        for (int row = 0; row < 4; row++)
        {
            float z = 2f + row * 6f, y = 4.6f;
            Block("LanternRope", new Vector3(0f, y, z), new Vector3(StallX * 2f + 2f, 0.03f, 0.03f), rope, root);
            int count = 6;
            for (int i = 0; i < count; i++)
            {
                float x = -StallX + 1f + i * (StallX * 2f - 2f) / (count - 1);
                float sag = 0.5f * (1f - Mathf.Pow((x / StallX), 2f));   // 줄이 가운데로 처진다
                bool lit = Variant != 1 || rng.NextDouble() < 0.4;   // 파장: 꺼진 등이 많다
                Color c = Variant == 2 ? festive[(i + row) % festive.Length] : new Color(1f, 0.55f, 0.25f);
                Transform pivot = new GameObject("Lantern").transform;
                pivot.SetParent(root, false);
                pivot.localPosition = new Vector3(x, y - sag, z);
                Block("String", new Vector3(0f, -0.15f, 0f), new Vector3(0.02f, 0.3f, 0.02f), rope, pivot);
                Transform body = Primitive(PrimitiveType.Sphere, "Paper", lit ? MakeEmissive(c, 1.8f) : MakeMaterial(c * 0.35f, 0f, 0.1f), pivot).transform;
                body.localPosition = new Vector3(0f, -0.5f, 0f);
                body.localScale = new Vector3(0.42f, 0.55f, 0.42f);
                body.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Block("Cap", new Vector3(0f, -0.24f, 0f), new Vector3(0.22f, 0.06f, 0.22f), darkWood, pivot);
                Light l = null;
                if (lit && (i == 1 || i == 4))   // 빛은 몇 개만 (조명이 너무 많지 않게)
                {
                    l = AddLight("LanternLight", new Vector3(0f, -0.6f, 0f), c, 1.3f, 7f, pivot);
                    l.shadows = row == 0 ? LightShadows.Soft : LightShadows.None;
                }
                lanterns.Add(new Lantern { pivot = pivot, phase = (float)rng.NextDouble() * 6f, light = l });
            }
        }

        if (Variant == 1)
            rain = MakeRain(root, new Vector3(0f, 9f, 8f), new Vector3(20f, 0.5f, 22f), 450f, new Color(0.7f, 0.75f, 0.85f, 0.4f));
        else rain = null;
        if (Variant == 2)
            MakeMotes(root, "Confetti", new Vector3(0f, 6f, 6f), new Vector3(14f, 0.5f, 14f), 20f, new Color(1f, 0.85f, 0.7f, 0.9f), 0.08f, 0.05f, 7f);
        else
            MakeMotes(root, "Dust", new Vector3(0f, 2f, 4f), new Vector3(14f, 3f, 10f), 5f, new Color(1f, 0.85f, 0.65f, 0.25f), 0.04f, 0f, 6f);
    }

    /// <summary>좌판 하나: 판매대 + 기둥 두 개 + 기운 줄무늬 차양 + 물건</summary>
    private void BuildStall(Transform root, Vector3 at, int side, Material wood, Material darkWood, Material iron)
    {
        Transform s = new GameObject("Stall").transform;
        s.SetParent(root, false);
        s.localPosition = at;

        Block("Counter", new Vector3(0f, 0.5f, 0f), new Vector3(2f, 1f, 4f), wood, s);
        Block("CounterTop", new Vector3(-side * 0.1f, 1.03f, 0f), new Vector3(2.3f, 0.06f, 4.2f), darkWood, s);
        Block("PostFront", new Vector3(-side * 1.1f, 1.4f, -1.9f), new Vector3(0.12f, 2.8f, 0.12f), darkWood, s);
        Block("PostBack", new Vector3(-side * 1.1f, 1.4f, 1.9f), new Vector3(0.12f, 2.8f, 0.12f), darkWood, s);

        // 차양: 줄무늬 두 색을 번갈아, 거리 쪽으로 기울게
        Color a = awningColors[rng.Next(awningColors.Length)];
        Material stripeA = MakeMaterial(a, 0f, 0.15f), stripeB = MakeMaterial(new Color(0.75f, 0.7f, 0.6f), 0f, 0.15f);
        Transform awning = new GameObject("Awning").transform;
        awning.SetParent(s, false);
        awning.localPosition = new Vector3(0f, 2.9f, 0f);
        awning.localRotation = Quaternion.Euler(0f, 0f, side * 18f);
        for (int i = 0; i < 6; i++)
            Block("Stripe", new Vector3(0f, 0f, -2f + 0.35f + i * 0.66f), new Vector3(2.8f, 0.05f, 0.66f), i % 2 == 0 ? stripeA : stripeB, awning);

        // 물건: 상자 · 항아리 · 작은 시계
        for (int i = 0; i < 5; i++)
        {
            float z = -1.6f + i * 0.8f + (float)rng.NextDouble() * 0.2f;
            double kind = rng.NextDouble();
            if (kind < 0.4)
                Block("Crate", new Vector3(-side * 0.2f, 1.25f, z), new Vector3(0.45f, 0.4f, 0.5f), wood, s);
            else if (kind < 0.75)
                Cylinder("Jar", new Vector3(-side * 0.3f, 1.3f, z), new Vector3(0.35f, 0.25f, 0.35f), Vector3.zero, MakeMaterial(new Color(0.45f, 0.3f, 0.2f), 0f, 0.4f), s);
            else
                Cylinder("Clock", new Vector3(-side * 0.5f, 1.35f, z), new Vector3(0.4f, 0.04f, 0.4f), new Vector3(0f, 0f, 90f), MakeMaterial(new Color(0.75f, 0.6f, 0.3f), 0.8f, 0.5f), s);
        }
        // 좌판 앞 바닥의 통 하나
        if (rng.NextDouble() < 0.6)
            Cylinder("Barrel", new Vector3(-side * 1.6f, 0.45f, -1.2f + (float)rng.NextDouble() * 2.4f), new Vector3(0.7f, 0.45f, 0.7f), Vector3.zero, darkWood, s);
    }
}
