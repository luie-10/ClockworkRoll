using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 하수도 배경 — 둥근 천장 터널이 안쪽으로 뻗고, 오른쪽에 수로가 흐른다. 에셋 없이 전부 코드로 만든다.
///
///   - 벽돌 바닥과 벽, 여러 조각으로 이어 붙인 둥근 천장 + 일정 간격의 아치 갈빗대
///   - 오른쪽 수로 (빛나는 탁한 물), 벽을 따라 달리는 굵은 배관과 밸브
///   - 천장 배수구 철망에서 내려오는 빛줄기, 벽에 달린 철망 전등 (타격 때 지직거림)
///   - 천장에서 떨어지는 물방울, 수로 위로 피어오르는 김
///
/// 변형: 0 독한 초록 / 1 녹물 주황 / 2 넘친 물 (바닥 전체가 물에 잠김)
/// 씬에 없어도 된다 — BattleStageData.backdrop이 Sewer인 무대에서 코드로 만들어진다.
/// </summary>
public class SewerBackdrop : StageBackdrop
{
    public override BackdropType Type { get { return BackdropType.Sewer; } }
    public override int VariantCount { get { return 3; } }
    public override string VariantName { get { return Variant == 1 ? "녹물 주황" : Variant == 2 ? "넘친 물" : "독한 초록"; } }

    private const float Width = 7f;        // 벽까지 거리 (좌우)
    private const float WallH = 3.6f;      // 벽 높이 (그 위로 둥근 천장)
    private const float Near = -10f, Far = 46f;

    private Color fog, ambient, lampColor, sludge, brick;
    private float fogDensity;
    private readonly List<Light> lamps = new List<Light>();
    private readonly List<Material> lampMats = new List<Material>();
    private float flicker, tempo = 1f;
    private ParticleSystem drips;
    private System.Random rng;

    // ─────────────────────────── 배경 이벤트 ───────────────────────────

    protected override void OnShown()
    {
        ApplyAtmosphere(fog, fogDensity, ambient, fog, 0.15f);
    }

    protected override void OnImpact(float strength)
    {
        flicker = Mathf.Max(flicker, 0.12f + 0.4f * strength);
        if (drips != null) drips.Emit(Mathf.RoundToInt(10 + 30 * strength));   // 천장에서 물이 후드득
    }

    protected override void OnTempo(int emotionLevel)
    {
        tempo = 1f + 0.35f * emotionLevel;
    }

    void Update()
    {
        if (!IsActive) return;
        float dt = Time.deltaTime;

        for (int i = 0; i < lamps.Count; i++)
        {
            float k = 1f;
            if (flicker > 0f) k = Random.value < 0.5f ? Random.Range(0f, 0.2f) : 1.3f;
            else if (Random.value < 0.004f * tempo) k = 0.3f;   // 평소에도 가끔 끊긴다 (긴장할수록 자주)
            lamps[i].intensity = 1.8f * k;
            lampMats[i].SetColor("_EmissionColor", lampColor * 2.2f * k);
        }
        if (flicker > 0f) flicker -= dt;
    }

    // ─────────────────────────── 무대 구성 ───────────────────────────

    protected override void Build(Transform root)
    {
        rng = VariantRandom(9091);
        lamps.Clear();
        lampMats.Clear();

        Color glow;
        switch (Variant)
        {
            case 1:   // 녹물: 주황 전등, 갈색 물
                fog = new Color(0.08f, 0.05f, 0.03f); fogDensity = 0.04f; ambient = new Color(0.16f, 0.11f, 0.08f);
                lampColor = new Color(1f, 0.55f, 0.25f); sludge = new Color(0.22f, 0.12f, 0.05f); glow = new Color(0.6f, 0.3f, 0.1f);
                brick = new Color(0.55f, 0.38f, 0.3f);
                break;
            case 2:   // 넘친 물: 차가운 남색, 바닥까지 잠김
                fog = new Color(0.02f, 0.04f, 0.06f); fogDensity = 0.035f; ambient = new Color(0.08f, 0.12f, 0.17f);
                lampColor = new Color(0.7f, 0.85f, 1f); sludge = new Color(0.03f, 0.07f, 0.1f); glow = new Color(0.2f, 0.45f, 0.6f);
                brick = new Color(0.4f, 0.42f, 0.45f);
                break;
            default:  // 독한 초록: 초록 안개, 빛나는 수로
                fog = new Color(0.03f, 0.06f, 0.04f); fogDensity = 0.04f; ambient = new Color(0.1f, 0.15f, 0.1f);
                lampColor = new Color(0.85f, 1f, 0.6f); sludge = new Color(0.08f, 0.2f, 0.06f); glow = new Color(0.35f, 0.9f, 0.3f);
                brick = new Color(0.45f, 0.42f, 0.36f);
                break;
        }

        Texture2D brickTex = PlankTexture(rng, Color.white, 16, 0.5f);
        Material wall = MakeMaterial(brick, 0f, 0.35f, brickTex, new Vector2(8f, 3f));
        Material vault = MakeMaterial(brick * 0.75f, 0f, 0.3f, brickTex, new Vector2(1f, 10f));
        Material rib = MakeMaterial(brick * 0.55f, 0f, 0.2f);
        Material iron = MakeMaterial(new Color(0.16f, 0.15f, 0.14f), 0.7f, 0.35f);
        Material rust = MakeMaterial(new Color(0.32f, 0.16f, 0.08f), 0.5f, 0.3f);

        float len = Far - Near, midZ = (Far + Near) * 0.5f;

        // 바닥 + 벽
        Block("Floor", new Vector3(0f, -0.11f, midZ), new Vector3(Width * 2f, 0.2f, len), MakeMaterial(brick * 0.8f, 0.1f, 0.55f, brickTex, new Vector2(5f, 18f)), root);
        Block("WallL", new Vector3(-Width - 0.5f, WallH * 0.5f, midZ), new Vector3(1f, WallH, len), wall, root);
        Block("WallR", new Vector3(Width + 0.5f, WallH * 0.5f, midZ), new Vector3(1f, WallH, len), wall, root);

        // 둥근 천장: 반원을 9조각 판으로 이어 붙인다
        const int pieces = 9;
        for (int i = 0; i < pieces; i++)
        {
            float a = Mathf.PI * (i + 0.5f) / pieces;
            Vector3 p = new Vector3(Mathf.Cos(a) * (Width + 0.5f), WallH + Mathf.Sin(a) * (Width + 0.5f), midZ);
            float chord = 2f * (Width + 0.5f) * Mathf.Sin(Mathf.PI / pieces / 2f) + 0.15f;
            Transform v = Block("Vault", p, new Vector3(chord, 1f, len), vault, root);
            v.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg - 90f);
        }

        // 아치 갈빗대 (일정 간격)
        for (float z = Near + 2f; z < Far; z += 6f)
        {
            Block("RibL", new Vector3(-Width + 0.1f, WallH * 0.5f, z), new Vector3(0.5f, WallH, 0.7f), rib, root);
            Block("RibR", new Vector3(Width - 0.1f, WallH * 0.5f, z), new Vector3(0.5f, WallH, 0.7f), rib, root);
            for (int i = 0; i < pieces; i++)
            {
                float a = Mathf.PI * (i + 0.5f) / pieces;
                Vector3 p = new Vector3(Mathf.Cos(a) * (Width - 0.1f), WallH + Mathf.Sin(a) * (Width - 0.1f), z);
                Transform r = Block("Rib", p, new Vector3(2.5f, 0.5f, 0.7f), rib, root);
                r.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg - 90f);
            }
        }

        // 수로 (오른쪽 벽을 따라) 또는 넘친 물
        Material water = MakeMaterial(sludge, 0.6f, 0.95f);
        water.EnableKeyword("_EMISSION");
        water.SetColor("_EmissionColor", glow * 0.35f);
        if (Variant == 2)
        {
            Transform flood = Block("Flood", new Vector3(0f, 0.04f, midZ), new Vector3(Width * 2f, 0.04f, len), water, root);
            flood.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        else
        {
            Block("ChannelEdge", new Vector3(5.1f, 0.05f, midZ), new Vector3(0.3f, 0.18f, len), rib, root);
            Block("Channel", new Vector3(6.1f, -0.25f, midZ), new Vector3(1.8f, 0.05f, len), water, root);
            AddLight("ChannelGlow", new Vector3(6.1f, 0.4f, 4f), glow, 0.9f, 7f, root).shadows = LightShadows.None;
            AddLight("ChannelGlow", new Vector3(6.1f, 0.4f, 16f), glow, 0.9f, 7f, root).shadows = LightShadows.None;
            MakeMotes(root, "Steam", new Vector3(6.1f, 0.3f, 8f), new Vector3(1.4f, 0.2f, 24f), 8f, new Color(glow.r, glow.g, glow.b, 0.15f), 1.6f, -0.01f, 5f);
        }

        // 배관 (왼쪽 벽을 따라 길게) + 밸브
        Cylinder("PipeA", new Vector3(-Width + 0.45f, 2.4f, midZ), new Vector3(0.5f, len * 0.5f, 0.5f), new Vector3(90f, 0f, 0f), rust, root);
        Cylinder("PipeB", new Vector3(-Width + 0.4f, 3.1f, midZ), new Vector3(0.28f, len * 0.5f, 0.28f), new Vector3(90f, 0f, 0f), iron, root);
        for (float z = 2f; z < 30f; z += 9f + (float)rng.NextDouble() * 4f)
        {
            Cylinder("Valve", new Vector3(-Width + 0.95f, 2.4f, z), new Vector3(0.7f, 0.05f, 0.7f), new Vector3(0f, 0f, 90f), rust, root);
            Cylinder("ValveStem", new Vector3(-Width + 0.8f, 2.4f, z), new Vector3(0.1f, 0.15f, 0.1f), new Vector3(0f, 0f, 90f), iron, root);
        }
        // 벽에서 튀어나와 꺾이는 배수관 몇 개
        for (int i = 0; i < 3; i++)
        {
            float z = 6f + i * 9f + (float)rng.NextDouble() * 3f;
            Cylinder("Drain", new Vector3(Width - 0.3f, 1.4f, z), new Vector3(0.45f, 0.5f, 0.45f), new Vector3(0f, 0f, 90f), iron, root);
        }

        // 천장 배수구에서 내려오는 빛 + 벽 전등
        for (float z = 3f; z < 30f; z += 12f)
        {
            Block("Grate", new Vector3(-1.5f, WallH + Width + 0.05f, z), new Vector3(1.6f, 0.1f, 1.6f), MakeEmissive(new Color(0.8f, 0.85f, 0.9f), 1.2f), root);
            GameObject shaft = new GameObject("GrateLight");
            shaft.transform.SetParent(root, false);
            shaft.transform.localPosition = new Vector3(-1.5f, WallH + Width - 0.3f, z);
            shaft.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Light s = shaft.AddComponent<Light>();
            s.type = LightType.Spot;
            s.color = new Color(0.75f, 0.82f, 0.9f);
            s.spotAngle = 35f;
            s.range = 14f;
            s.intensity = Variant == 2 ? 2.6f : 1.8f;
            MakeMotes(root, "ShaftDust", new Vector3(-1.5f, 5f, z), new Vector3(1.4f, 6f, 1.4f), 6f, new Color(0.9f, 0.9f, 0.85f, 0.35f), 0.05f, 0f, 5f);
        }
        for (int i = 0; i < 4; i++)
        {
            int side = i % 2 == 0 ? -1 : 1;
            float z = 1f + i * 7f;
            Material bulb = MakeEmissive(lampColor, 2.2f);
            Block("LampCage", new Vector3(side * (Width - 0.25f), 3.1f, z), new Vector3(0.4f, 0.5f, 0.4f), iron, root);
            Block("LampBulb", new Vector3(side * (Width - 0.45f), 3.05f, z), new Vector3(0.25f, 0.3f, 0.25f), bulb, root);
            Light l = AddLight("WallLamp", new Vector3(side * (Width - 1f), 3f, z), lampColor, 1.8f, 9f, root);
            if (i < 2) l.shadows = LightShadows.Soft;
            lamps.Add(l);
            lampMats.Add(bulb);
        }

        // 터널 끝: 빛나는 둥근 출구
        Cylinder("Exit", new Vector3(0f, 3f, Far - 0.5f), new Vector3(4.5f, 0.05f, 4.5f), new Vector3(90f, 0f, 0f), MakeEmissive(glow * 0.8f + Color.white * 0.2f, 0.8f), root);

        drips = MakeMotes(root, "Drips", new Vector3(0f, WallH + 4f, 8f), new Vector3(10f, 0.3f, 22f), 12f, new Color(0.7f, 0.85f, 0.8f, 0.8f), 0.05f, 0.7f, 2f);
    }
}
