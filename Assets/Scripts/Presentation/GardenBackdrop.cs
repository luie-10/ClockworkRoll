using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 태엽 정원 배경 — 무쇠 뼈대만 남은 온실 속, 태엽으로 피고 지는 꽃밭. 에셋 없이 전부 코드로 만든다.
///
///   - 잔디 바닥과 자갈길, 양옆의 다듬은 생울타리
///   - 뒤쪽을 두른 온실 뼈대 (세로 살 + 아치 지붕살), 가운데 분수 (물이 빛난다)
///   - 놋쇠 꽃잎이 도는 태엽 꽃들 (긴장할수록 빨리, 타격 때 확 돈다), 생울타리 뒤 커다란 장식 톱니
///   - 반딧불 (가을 변형이면 지는 잎)
///
/// 변형: 0 황혼 정원 / 1 달빛 정원 / 2 시든 가을
/// 씬에 없어도 된다 — BattleStageData.backdrop이 Garden인 무대에서 코드로 만들어진다.
/// </summary>
public class GardenBackdrop : StageBackdrop
{
    public override BackdropType Type { get { return BackdropType.Garden; } }
    public override int VariantCount { get { return 3; } }
    public override Color GroundPuffColor { get { return Variant == 2 ? new Color(0.6f, 0.4f, 0.2f, 0.35f) : new Color(0.45f, 0.55f, 0.35f, 0.3f); } }
    public override string VariantName { get { return Variant == 1 ? "달빛 정원" : Variant == 2 ? "시든 가을" : "황혼 정원"; } }

    private const float FrameZ = 10f;

    private Color fog, ambient, sky, grass, leaf, petal, glow;
    private float fogDensity, sun;

    private class Flower
    {
        public Transform head;
        public float speed;
    }

    private readonly List<Flower> flowers = new List<Flower>();
    private Transform bigGear;
    private ParticleSystem bugs;
    private float spin, tempo = 1f;
    private System.Random rng;

    // ─────────────────────────── 배경 이벤트 ───────────────────────────

    protected override void OnShown()
    {
        ApplyAtmosphere(fog, fogDensity, ambient, sky, sun);
    }

    protected override void OnImpact(float strength)
    {
        spin = Mathf.Max(spin, strength);
        if (bugs != null) bugs.Emit(Mathf.RoundToInt(6 + 18 * strength));
    }

    protected override void OnTempo(int emotionLevel)
    {
        tempo = 1f + 0.35f * emotionLevel;
    }

    void Update()
    {
        if (!IsActive) return;
        float dt = Time.deltaTime;
        spin = Mathf.MoveTowards(spin, 0f, dt * 0.8f);
        float k = tempo + 8f * spin;
        foreach (Flower f in flowers) f.head.Rotate(0f, f.speed * k * dt, 0f, Space.Self);
        if (bigGear != null) bigGear.Rotate(0f, 8f * k * dt, 0f, Space.Self);
    }

    // ─────────────────────────── 무대 구성 ───────────────────────────

    protected override void Build(Transform root)
    {
        rng = VariantRandom(5557);
        flowers.Clear();

        switch (Variant)
        {
            case 1:
                fog = new Color(0.05f, 0.08f, 0.12f); fogDensity = 0.025f; ambient = new Color(0.14f, 0.18f, 0.26f); sky = new Color(0.04f, 0.07f, 0.12f); sun = 0.5f;
                grass = new Color(0.12f, 0.2f, 0.18f); leaf = new Color(0.08f, 0.16f, 0.14f); petal = new Color(0.75f, 0.78f, 0.85f); glow = new Color(0.6f, 0.85f, 1f);
                break;
            case 2:
                fog = new Color(0.3f, 0.18f, 0.1f); fogDensity = 0.022f; ambient = new Color(0.3f, 0.2f, 0.14f); sky = new Color(0.4f, 0.24f, 0.14f); sun = 0.8f;
                grass = new Color(0.36f, 0.27f, 0.14f); leaf = new Color(0.42f, 0.22f, 0.08f); petal = new Color(0.7f, 0.45f, 0.2f); glow = new Color(1f, 0.6f, 0.25f);
                break;
            default:
                fog = new Color(0.32f, 0.22f, 0.24f); fogDensity = 0.02f; ambient = new Color(0.3f, 0.24f, 0.26f); sky = new Color(0.42f, 0.28f, 0.3f); sun = 0.75f;
                grass = new Color(0.2f, 0.32f, 0.16f); leaf = new Color(0.13f, 0.24f, 0.1f); petal = new Color(0.85f, 0.65f, 0.3f); glow = new Color(1f, 0.85f, 0.45f);
                break;
        }

        Material lawn = MakeMaterial(grass, 0f, 0.1f, PlankTexture(rng, Color.white, 20, 0.7f), new Vector2(6f, 6f));
        Material gravel = MakeMaterial(new Color(0.5f, 0.47f, 0.42f), 0f, 0.15f, PlankTexture(rng, Color.white, 30, 0.6f), new Vector2(2f, 8f));
        Material hedge = MakeMaterial(leaf, 0f, 0.05f, PlankTexture(rng, Color.white, 24, 0.9f), new Vector2(3f, 1f));
        Material iron = MakeMaterial(new Color(0.12f, 0.14f, 0.13f), 0.7f, 0.4f);
        Material brass = MakeMaterial(petal, 0.85f, 0.55f);
        Material stone = MakeMaterial(new Color(0.55f, 0.53f, 0.5f), 0f, 0.2f);

        // 바닥: 잔디 + 가운데 자갈길 (뒤로 분수까지)
        Block("Lawn", new Vector3(0f, -0.1f, 4f), new Vector3(30f, 0.2f, 24f), lawn, root);
        Block("Path", new Vector3(0f, 0.01f, 4.4f), new Vector3(2.6f, 0.02f, 5.2f), gravel, root);   // 마당 · 잔디와 높이를 달리해 면이 겹치지 않게
        Block("Plaza", new Vector3(0f, 0.02f, -0.2f), new Vector3(14f, 0.02f, 4f), gravel, root);

        // 생울타리 (양옆 + 뒤쪽 낮은 줄)
        Block("HedgeL", new Vector3(-10f, 0.8f, 3f), new Vector3(1.4f, 1.6f, 12f), hedge, root);
        Block("HedgeR", new Vector3(10f, 0.8f, 3f), new Vector3(1.4f, 1.6f, 12f), hedge, root);
        Block("HedgeBackL", new Vector3(-6f, 0.6f, 8f), new Vector3(7f, 1.2f, 1.2f), hedge, root);
        Block("HedgeBackR", new Vector3(6f, 0.6f, 8f), new Vector3(7f, 1.2f, 1.2f), hedge, root);
        for (int i = 0; i < 4; i++)   // 둥글게 다듬은 나무
        {
            float x = i < 2 ? -8.4f : 8.4f, z = 1f + (i % 2) * 5f;
            Cylinder("Trunk", new Vector3(x, 0.6f, z), new Vector3(0.2f, 0.6f, 0.2f), Vector3.zero, MakeMaterial(new Color(0.25f, 0.16f, 0.1f), 0f, 0.2f), root);
            Transform ball = Primitive(PrimitiveType.Sphere, "Topiary", hedge, root).transform;
            ball.localPosition = new Vector3(x, 1.8f, z);
            ball.localScale = Vector3.one * 1.6f;
        }

        BuildGreenhouse(root, iron);
        BuildFountain(root, stone, iron);

        // 태엽 꽃: 울타리 앞 · 길가에 흩어 심는다 (가운데 싸우는 자리는 비운다)
        // 울타리(x ±9.3 · 뒤 z 7.4) · 둥근 나무(x ±8.4)와 겹치지 않는 칸에만, 꽃끼리도 0.8 이상 떨어뜨린다
        List<Vector3> spots = new List<Vector3>();
        for (int tries = 0; tries < 120 && spots.Count < 16; tries++)
        {
            float x = -7.2f + (float)rng.NextDouble() * 14.4f;
            float z = 3f + (float)rng.NextDouble() * 3.8f;
            if (Mathf.Abs(x) < 1.8f) continue;   // 길은 비운다
            Vector3 p = new Vector3(x, 0f, z);
            if (spots.Exists(q => (q - p).sqrMagnitude < 0.64f)) continue;
            spots.Add(p);
            PlantFlower(root, p, 0.6f + (float)rng.NextDouble() * 0.9f, iron, brass);
        }

        // 울타리 너머 커다란 장식 톱니 (천천히 돈다)
        bigGear = new GameObject("GardenGear").transform;
        bigGear.SetParent(root, false);
        bigGear.localPosition = new Vector3(-5.5f, 2.6f, FrameZ - 0.8f);
        bigGear.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Cylinder("GearDisc", Vector3.zero, new Vector3(4f, 0.12f, 4f), Vector3.zero, brass, bigGear);
        Cylinder("GearHole", new Vector3(0f, -0.02f, 0f), new Vector3(1.2f, 0.14f, 1.2f), Vector3.zero, iron, bigGear);
        for (int i = 0; i < 12; i++)
        {
            float a = i * 30f * Mathf.Deg2Rad;
            Transform tooth = Block("Tooth", new Vector3(Mathf.Cos(a) * 2.1f, 0f, Mathf.Sin(a) * 2.1f), new Vector3(0.5f, 0.12f, 0.4f), brass, bigGear);
            tooth.localRotation = Quaternion.Euler(0f, -i * 30f, 0f);
        }

        // 정원 등 (꽃밭을 비추는 따뜻한 빛)
        AddLight("GardenGlow", new Vector3(-5f, 2.5f, 4f), glow, 1.4f, 10f, root).shadows = LightShadows.Soft;
        AddLight("GardenGlow", new Vector3(5f, 2.5f, 4f), glow, 1.4f, 10f, root).shadows = LightShadows.None;

        if (Variant == 2)
        {
            MakeMotes(root, "Leaves", new Vector3(0f, 7f, 4f), new Vector3(22f, 0.5f, 10f), 14f, new Color(0.8f, 0.45f, 0.15f, 0.9f), 0.14f, 0.05f, 8f);
            bugs = MakeMotes(root, "Seeds", new Vector3(0f, 1.2f, 4f), new Vector3(18f, 1.5f, 6f), 4f, new Color(1f, 0.85f, 0.6f, 0.6f), 0.05f, -0.01f, 5f);
        }
        else
            bugs = MakeMotes(root, "Fireflies", new Vector3(0f, 1.4f, 4f), new Vector3(18f, 2f, 7f), 7f, new Color(glow.r, glow.g, glow.b, 0.9f), 0.07f, 0f, 5f);
    }

    private void BuildGreenhouse(Transform root, Material iron)
    {
        // 세로 살 (뒷면) + 가로 띠
        for (float x = -12f; x <= 12f; x += 2f)
            Block("Mullion", new Vector3(x, 3.5f, FrameZ), new Vector3(0.1f, 7f, 0.1f), iron, root);
        for (float y = 1.5f; y <= 7f; y += 2.75f)
            Block("Band", new Vector3(0f, y, FrameZ), new Vector3(24f, 0.08f, 0.08f), iron, root);

        // 아치 지붕살: 반원을 조각으로 (몇 줄을 앞쪽으로)
        const int pieces = 10;
        const float radius = 12f;
        for (float z = FrameZ; z >= FrameZ - 6f; z -= 3f)
        {
            // 앞쪽 지붕살도 양 끝을 기둥이 받친다 (허공에 떠 있지 않게)
            if (z < FrameZ)
            {
                Block("Post", new Vector3(-radius, 3.5f, z), new Vector3(0.12f, 7f, 0.12f), iron, root);
                Block("Post", new Vector3(radius, 3.5f, z), new Vector3(0.12f, 7f, 0.12f), iron, root);
            }
            for (int i = 0; i < pieces; i++)
            {
                float a = Mathf.PI * (i + 0.5f) / pieces;
                Vector3 p = new Vector3(Mathf.Cos(a) * radius, 7f + Mathf.Sin(a) * radius * 0.35f, z);
                float chord = 2f * radius * Mathf.Sin(Mathf.PI / pieces / 2f) + 0.2f;
                Transform r = Block("Rib", p, new Vector3(chord, 0.1f, 0.1f), iron, root);
                float slope = Mathf.Atan2(Mathf.Cos(a) * 0.35f, -Mathf.Sin(a)) * Mathf.Rad2Deg;
                r.localRotation = Quaternion.Euler(0f, 0f, slope);
            }
        }

        // 깨진 유리 몇 장 (흐린 빛)
        Color pane = Color.Lerp(sky, Color.white, 0.25f);
        for (int i = 0; i < 6; i++)
        {
            float x = -11f + (float)rng.Next(12) * 2f;   // 홀수 칸 = 세로 살(짝수 x) 사이
            float y = 1.5f + (float)rng.Next(2) * 2.75f;
            Block("Pane", new Vector3(x, y + 1.375f, FrameZ + 0.08f), new Vector3(1.9f, 2.65f, 0.02f), MakeEmissive(pane, 0.35f), root);
        }
    }

    private void BuildFountain(Transform root, Material stone, Material iron)
    {
        Vector3 c = new Vector3(0f, 0f, 8.2f);   // 대야 끝이 온실 뒷살(z 10)에 닿지 않게
        Material water = MakeEmissive(Color.Lerp(glow, new Color(0.4f, 0.7f, 1f), 0.6f), 0.7f);
        Cylinder("Basin", c + Vector3.up * 0.3f, new Vector3(3.2f, 0.3f, 3.2f), Vector3.zero, stone, root);
        Cylinder("Pool", c + Vector3.up * 0.58f, new Vector3(2.8f, 0.03f, 2.8f), Vector3.zero, water, root);
        Cylinder("Column", c + Vector3.up * 1.2f, new Vector3(0.35f, 0.6f, 0.35f), Vector3.zero, stone, root);
        Cylinder("Bowl", c + Vector3.up * 1.85f, new Vector3(1.4f, 0.12f, 1.4f), Vector3.zero, stone, root);
        Cylinder("BowlWater", c + Vector3.up * 1.95f, new Vector3(1.2f, 0.02f, 1.2f), Vector3.zero, water, root);
        Cylinder("Spout", c + Vector3.up * 2.3f, new Vector3(0.1f, 0.35f, 0.1f), Vector3.zero, iron, root);
        MakeMotes(root, "Spray", c + Vector3.up * 2.6f, new Vector3(0.3f, 0.1f, 0.3f), 30f, new Color(0.8f, 0.9f, 1f, 0.6f), 0.05f, 0.6f, 0.9f);
        AddLight("FountainGlow", c + Vector3.up * 1.2f, new Color(0.5f, 0.75f, 1f), 1.1f, 5f, root).shadows = LightShadows.None;
    }

    private void PlantFlower(Transform root, Vector3 at, float height, Material iron, Material brass)
    {
        Block("Stem", at + Vector3.up * height * 0.5f, new Vector3(0.05f, height, 0.05f), iron, root);
        Transform head = new GameObject("FlowerHead").transform;
        head.SetParent(root, false);
        head.localPosition = at + Vector3.up * height;
        head.localRotation = Quaternion.Euler(-60f + (float)rng.NextDouble() * 20f, (float)rng.NextDouble() * 40f - 20f, 0f);   // 꽃이 카메라 쪽으로 고개를 든다
        Transform spinner = new GameObject("Spin").transform;
        spinner.SetParent(head, false);

        int petals = 5 + rng.Next(3);
        float size = 0.18f + (float)rng.NextDouble() * 0.12f;
        for (int i = 0; i < petals; i++)
        {
            Transform p = Block("Petal", Vector3.zero, new Vector3(size * 0.6f, 0.02f, size * 1.6f), brass, spinner);
            p.localRotation = Quaternion.Euler(0f, i * 360f / petals, 0f);
            p.localPosition = p.localRotation * new Vector3(0f, 0f, size * 0.8f);
        }
        Primitive(PrimitiveType.Sphere, "Core", MakeEmissive(glow, 1.2f), spinner).transform.localScale = Vector3.one * size * 0.8f;
        flowers.Add(new Flower { head = spinner, speed = (30f + (float)rng.NextDouble() * 50f) * (rng.NextDouble() < 0.3 ? -1f : 1f) });
    }
}
