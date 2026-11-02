using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 시계공의 공방 배경 — 벽 가득 시계가 걸린 실내. 에셋 없이 전부 코드로 만든다.
///
///   - 판자 바닥, 낡은 벽지 벽, 천장 들보, 비 오는 바깥이 보이는 둥근 창
///   - 뒷벽의 크고 작은 시계들 (제각각 다른 빠르기로 돈다), 진자가 흔들리는 괘종시계
///   - 왼쪽 작업대 (바이스 · 부품 · 돋보기 등), 선반의 상자와 병, 바닥에 굴러다니는 톱니
///   - 천장에 매달린 등 (타격 때 그네처럼 흔들린다)
///
/// 변형: 0 따뜻한 등불 / 1 꺼진 등, 달빛 / 2 화로의 붉은 빛
/// 씬에 없어도 된다 — BattleStageData.backdrop이 Workshop인 무대에서 코드로 만들어진다.
/// </summary>
public class WorkshopBackdrop : StageBackdrop
{
    public override BackdropType Type { get { return BackdropType.Workshop; } }
    public override int VariantCount { get { return 3; } }
    public override string VariantName { get { return Variant == 1 ? "달빛" : Variant == 2 ? "화로" : "따뜻한 등불"; } }

    private const float BackZ = 9f, SideX = 10f, Ceiling = 7.5f;

    private class WallClock
    {
        public Transform minute, hour;
        public float speed, offset;
    }

    private class Swing
    {
        public Transform pivot;
        public float angle, velocity;
    }

    private readonly List<WallClock> clocks = new List<WallClock>();
    private readonly List<Swing> swings = new List<Swing>();
    private Transform pendulum;
    private Light furnace;
    private float tempo = 1f, clockTime, furnaceBase;
    private System.Random rng;

    private Color lampColor, ambient, sky;
    private float lampIntensity;

    // ─────────────────────────── 배경 이벤트 ───────────────────────────

    protected override void OnShown()
    {
        ApplyAtmosphere(ambient * 0.4f, 0.012f, ambient, sky, Variant == 1 ? 0.15f : 0.3f);
    }

    protected override void OnImpact(float strength)
    {
        foreach (Swing s in swings) s.velocity += (Random.value < 0.5f ? -1f : 1f) * 160f * strength;
        clockTime += 30f * strength;   // 시계들이 움찔하며 바늘이 튄다
        if (furnace != null) furnace.intensity = furnaceBase * (1.5f + strength);
    }

    protected override void OnTempo(int emotionLevel)
    {
        tempo = 1f + 0.4f * emotionLevel;
    }

    void Update()
    {
        if (!IsActive) return;
        float dt = Time.deltaTime;
        clockTime += dt * tempo;

        foreach (WallClock c in clocks)
        {
            float a = (clockTime * c.speed + c.offset) * 30f;
            c.minute.localRotation = Quaternion.Euler(0f, 0f, -a);
            c.hour.localRotation = Quaternion.Euler(0f, 0f, -a / 12f);
        }
        if (pendulum != null) pendulum.localRotation = Quaternion.Euler(0f, 0f, 14f * Mathf.Sin(clockTime * 2.4f));

        foreach (Swing s in swings)
        {
            s.velocity += -s.angle * 18f * dt;
            s.velocity *= Mathf.Exp(-1.2f * dt);
            s.angle += s.velocity * dt;
            s.pivot.localRotation = Quaternion.Euler(s.angle * 0.3f, 0f, s.angle);
        }

        if (furnace != null)
        {
            float wobble = 0.85f + 0.3f * Mathf.PerlinNoise(Time.time * 5f, 0.7f);
            furnace.intensity = Mathf.Lerp(furnace.intensity, furnaceBase * wobble, dt * 5f);
        }
    }

    // ─────────────────────────── 무대 구성 ───────────────────────────

    protected override void Build(Transform root)
    {
        rng = VariantRandom(3307);
        clocks.Clear();
        swings.Clear();
        furnace = null;

        switch (Variant)
        {
            case 1:  lampColor = new Color(0.5f, 0.6f, 0.8f); lampIntensity = 0.35f; ambient = new Color(0.08f, 0.1f, 0.16f); sky = new Color(0.02f, 0.03f, 0.05f); break;
            case 2:  lampColor = new Color(1f, 0.6f, 0.35f); lampIntensity = 0.9f; ambient = new Color(0.16f, 0.08f, 0.06f); sky = new Color(0.05f, 0.02f, 0.02f); break;
            default: lampColor = new Color(1f, 0.75f, 0.45f); lampIntensity = 2.2f; ambient = new Color(0.2f, 0.15f, 0.1f); sky = new Color(0.05f, 0.04f, 0.03f); break;
        }

        Material floor = MakeMaterial(new Color(0.42f, 0.28f, 0.17f), 0f, 0.35f, PlankTexture(rng, Color.white, 8, 0.6f), new Vector2(2f, 6f));
        Material paper = MakeMaterial(new Color(0.32f, 0.27f, 0.2f), 0f, 0.1f, PlankTexture(rng, Color.white, 2, 0.3f), new Vector2(10f, 1f));
        Material wood = MakeMaterial(new Color(0.26f, 0.16f, 0.09f), 0f, 0.3f);
        Material darkWood = MakeMaterial(new Color(0.15f, 0.09f, 0.05f), 0f, 0.3f);
        Material brass = MakeMaterial(new Color(0.72f, 0.55f, 0.25f), 0.85f, 0.55f);
        Material iron = MakeMaterial(new Color(0.2f, 0.2f, 0.22f), 0.7f, 0.4f);

        // 방: 바닥 · 뒷벽 · 옆벽 · 천장 + 들보 + 벽 아래 걸레받이
        Block("Floor", new Vector3(0f, -0.1f, 0f), new Vector3(SideX * 2f, 0.2f, 24f), floor, root);
        Block("BackWall", new Vector3(0f, Ceiling * 0.5f, BackZ), new Vector3(SideX * 2f, Ceiling, 0.4f), paper, root);
        Block("WallL", new Vector3(-SideX, Ceiling * 0.5f, 0f), new Vector3(0.4f, Ceiling, 24f), paper, root);
        Block("WallR", new Vector3(SideX, Ceiling * 0.5f, 0f), new Vector3(0.4f, Ceiling, 24f), paper, root);
        Block("Ceiling", new Vector3(0f, Ceiling, 0f), new Vector3(SideX * 2f, 0.3f, 24f), darkWood, root);
        for (float x = -8f; x <= 8f; x += 4f) Block("Beam", new Vector3(x, Ceiling - 0.35f, 0f), new Vector3(0.45f, 0.5f, 24f), darkWood, root);
        Block("Skirting", new Vector3(0f, 0.3f, BackZ - 0.25f), new Vector3(SideX * 2f, 0.6f, 0.1f), darkWood, root);

        BuildWindow(root, darkWood);
        BuildWallClocks(root, wood, brass);
        BuildGrandfatherClock(root, darkWood, brass);
        BuildWorkbench(root, wood, darkWood, brass, iron);
        BuildShelves(root, wood);
        BuildFloorGears(root, brass, iron);
        BuildLamps(root, iron);
        if (Variant == 2) BuildFurnace(root, iron);

        MakeMotes(root, "Dust", new Vector3(0f, 3f, 3f), new Vector3(16f, 5f, 8f), 8f, new Color(1f, 0.9f, 0.7f, 0.3f), 0.04f, 0f, 6f);
    }

    private void BuildWindow(Transform root, Material frame)
    {
        // 오른쪽 위 둥근 창 — 바깥은 비 오는 밤 (변형 1이면 밝은 달빛이 쏟아진다)
        Vector3 c = new Vector3(5.2f, 4.6f, BackZ - 0.22f);
        Color outside = Variant == 1 ? new Color(0.55f, 0.7f, 1f) : Variant == 2 ? new Color(0.35f, 0.15f, 0.1f) : new Color(0.15f, 0.22f, 0.35f);
        Cylinder("WindowFrame", c, new Vector3(2.8f, 0.05f, 2.8f), new Vector3(90f, 0f, 0f), frame, root);
        Cylinder("WindowGlass", c + Vector3.back * 0.04f, new Vector3(2.4f, 0.05f, 2.4f), new Vector3(90f, 0f, 0f), MakeEmissive(outside, Variant == 1 ? 1.6f : 0.8f), root);
        Block("Mullion", c + Vector3.back * 0.1f, new Vector3(0.1f, 2.4f, 0.05f), frame, root);
        Block("Transom", c + Vector3.back * 0.1f, new Vector3(2.4f, 0.1f, 0.05f), frame, root);

        if (Variant == 1)
        {
            GameObject go = new GameObject("Moonlight");
            go.transform.SetParent(root, false);
            go.transform.localPosition = c + Vector3.back * 0.5f;
            go.transform.localRotation = Quaternion.Euler(35f, 200f, 0f);
            Light moon = go.AddComponent<Light>();
            moon.type = LightType.Spot;
            moon.color = outside;
            moon.spotAngle = 55f;
            moon.range = 22f;
            moon.intensity = 3.2f;
            moon.shadows = LightShadows.Soft;
            MakeMotes(root, "MoonDust", new Vector3(3f, 3f, 5f), new Vector3(3f, 3f, 3f), 10f, new Color(0.8f, 0.9f, 1f, 0.5f), 0.04f, 0f, 5f);
        }
    }

    private void BuildWallClocks(Transform root, Material wood, Material brass)
    {
        Material face = MakeMaterial(new Color(0.85f, 0.8f, 0.66f), 0f, 0.4f);
        Material hand = MakeMaterial(new Color(0.08f, 0.06f, 0.05f), 0f, 0.2f);
        int placed = 0;
        for (int tries = 0; tries < 60 && placed < 13; tries++)
        {
            float r = 0.35f + (float)rng.NextDouble() * 0.55f;
            float x = -8.5f + (float)rng.NextDouble() * 11f;
            float y = 2.4f + (float)rng.NextDouble() * 4.2f;
            if (x > 3f && y > 3f) continue;   // 창 자리는 비워 둔다
            if (Mathf.Abs(x - 0.5f) < 1.2f && y < 5.5f) continue;   // 괘종시계 자리

            Transform clock = new GameObject("WallClock").transform;
            clock.SetParent(root, false);
            clock.localPosition = new Vector3(x, y, BackZ - 0.22f);
            bool square = rng.NextDouble() < 0.3;
            if (square) Block("Case", new Vector3(0f, 0f, 0.02f), new Vector3(r * 2.4f, r * 2.4f, 0.12f), wood, clock);
            else Cylinder("Rim", new Vector3(0f, 0f, 0.02f), new Vector3(r * 2.25f, 0.06f, r * 2.25f), new Vector3(90f, 0f, 0f), rng.NextDouble() < 0.5 ? brass : wood, clock);
            Cylinder("Face", new Vector3(0f, 0f, -0.04f), new Vector3(r * 2f, 0.04f, r * 2f), new Vector3(90f, 0f, 0f), face, clock);

            WallClock wc = new WallClock
            {
                minute = MakeHand(clock, r * 0.85f, 0.05f, hand, -0.1f),
                hour = MakeHand(clock, r * 0.55f, 0.08f, hand, -0.12f),
                speed = 0.5f + (float)rng.NextDouble() * 3f * (rng.NextDouble() < 0.2 ? -1f : 1f),   // 몇 개는 거꾸로 돈다
                offset = (float)rng.NextDouble() * 12f,
            };
            clocks.Add(wc);
            placed++;
        }
    }

    private Transform MakeHand(Transform clock, float length, float width, Material mat, float z)
    {
        Transform pivot = new GameObject("Hand").transform;
        pivot.SetParent(clock, false);
        pivot.localPosition = new Vector3(0f, 0f, z);
        Block("Bar", new Vector3(0f, length * 0.5f, 0f), new Vector3(width, length, 0.02f), mat, pivot);
        return pivot;
    }

    private void BuildGrandfatherClock(Transform root, Material darkWood, Material brass)
    {
        Vector3 b = new Vector3(0.5f, 0f, BackZ - 0.7f);
        Block("GCBody", b + new Vector3(0f, 2.2f, 0f), new Vector3(1.4f, 4.4f, 0.8f), darkWood, root);
        Block("GCHead", b + new Vector3(0f, 4.9f, 0f), new Vector3(1.7f, 1.3f, 0.9f), darkWood, root);
        Block("GCWindow", b + new Vector3(0f, 2.3f, -0.41f), new Vector3(0.9f, 2.6f, 0.02f), MakeMaterial(new Color(0.05f, 0.05f, 0.06f), 0.3f, 0.9f), root);

        Transform clock = new GameObject("GCClock").transform;
        clock.SetParent(root, false);
        clock.localPosition = b + new Vector3(0f, 4.9f, -0.47f);
        Cylinder("Face", Vector3.zero, new Vector3(1.1f, 0.03f, 1.1f), new Vector3(90f, 0f, 0f), MakeMaterial(new Color(0.88f, 0.82f, 0.65f), 0f, 0.4f), clock);
        Material hand = MakeMaterial(new Color(0.08f, 0.06f, 0.05f), 0f, 0.2f);
        clocks.Add(new WallClock { minute = MakeHand(clock, 0.45f, 0.04f, hand, -0.05f), hour = MakeHand(clock, 0.3f, 0.06f, hand, -0.06f), speed = 1f, offset = 3f });

        pendulum = new GameObject("GCPendulum").transform;
        pendulum.SetParent(root, false);
        pendulum.localPosition = b + new Vector3(0f, 3.4f, -0.3f);
        Block("Rod", new Vector3(0f, -0.9f, 0f), new Vector3(0.05f, 1.8f, 0.03f), brass, pendulum);
        Cylinder("Bob", new Vector3(0f, -1.85f, 0f), new Vector3(0.4f, 0.03f, 0.4f), new Vector3(90f, 0f, 0f), brass, pendulum);
    }

    private void BuildWorkbench(Transform root, Material wood, Material darkWood, Material brass, Material iron)
    {
        Vector3 b = new Vector3(-6.3f, 0f, 4.5f);
        Block("BenchTop", b + new Vector3(0f, 1.05f, 0f), new Vector3(3.6f, 0.15f, 1.6f), wood, root);
        foreach (Vector2 leg in new[] { new Vector2(-1.6f, -0.65f), new Vector2(1.6f, -0.65f), new Vector2(-1.6f, 0.65f), new Vector2(1.6f, 0.65f) })
            Block("Leg", b + new Vector3(leg.x, 0.5f, leg.y), new Vector3(0.15f, 1f, 0.15f), darkWood, root);
        Block("Vise", b + new Vector3(1.4f, 1.3f, -0.5f), new Vector3(0.4f, 0.35f, 0.3f), iron, root);

        // 흩어진 부품: 작은 톱니 · 나사 · 회중시계
        for (int i = 0; i < 9; i++)
        {
            Vector3 p = b + new Vector3(-1.4f + (float)rng.NextDouble() * 2.4f, 1.15f, -0.5f + (float)rng.NextDouble() * 1f);
            float s = 0.1f + (float)rng.NextDouble() * 0.2f;
            Cylinder("Part", p, new Vector3(s, 0.02f, s), Vector3.zero, rng.NextDouble() < 0.6 ? brass : iron, root);
        }
        // 작업등 (갓 + 전구) — 변형 0/2에선 작업대가 가장 밝은 곳
        Block("BenchLampArm", b + new Vector3(-1.2f, 1.7f, 0.4f), new Vector3(0.06f, 1.2f, 0.06f), iron, root);
        Block("BenchLampHead", b + new Vector3(-1f, 2.3f, 0.2f), new Vector3(0.45f, 0.2f, 0.35f), iron, root);
        Light l = AddLight("BenchLamp", b + new Vector3(-0.9f, 2.0f, 0.1f), lampColor, lampIntensity * 0.9f, 5f, root);
        l.shadows = LightShadows.Soft;
    }

    private void BuildShelves(Transform root, Material wood)
    {
        Color[] jar = { new Color(0.3f, 0.5f, 0.35f, 1f), new Color(0.5f, 0.35f, 0.2f, 1f), new Color(0.35f, 0.4f, 0.55f, 1f) };
        for (int s = 0; s < 3; s++)
        {
            float y = 2.2f + s * 1.5f;
            Block("Shelf", new Vector3(SideX - 0.6f, y, 2f), new Vector3(1f, 0.08f, 5f), wood, root);
            for (float z = 0f; z < 4f; z += 0.5f + (float)rng.NextDouble() * 0.5f)
            {
                if (rng.NextDouble() < 0.5)
                    Block("Box", new Vector3(SideX - 0.6f, y + 0.25f, z), new Vector3(0.6f, 0.45f, 0.45f), MakeMaterial(new Color(0.35f, 0.26f, 0.17f), 0f, 0.2f), root);
                else
                    Cylinder("Jar", new Vector3(SideX - 0.6f, y + 0.25f, z), new Vector3(0.3f, 0.22f, 0.3f), Vector3.zero, MakeMaterial(jar[rng.Next(jar.Length)], 0.2f, 0.85f), root);
            }
        }
    }

    private void BuildFloorGears(Transform root, Material brass, Material iron)
    {
        for (int i = 0; i < 6; i++)
        {
            Vector3 p = new Vector3(-8f + (float)rng.NextDouble() * 16f, 0.02f, 4f + (float)rng.NextDouble() * 4f);
            float s = 0.3f + (float)rng.NextDouble() * 0.6f;
            Cylinder("FloorGear", p, new Vector3(s, 0.02f, s), Vector3.zero, rng.NextDouble() < 0.5 ? brass : iron, root);
        }
    }

    private void BuildLamps(Transform root, Material iron)
    {
        Material bulb = MakeEmissive(lampColor, Variant == 1 ? 0.3f : 2f);
        float[] xs = { -4f, 0.5f, 5f };
        foreach (float x in xs)
        {
            Transform pivot = new GameObject("LampPivot").transform;
            pivot.SetParent(root, false);
            pivot.localPosition = new Vector3(x, Ceiling - 0.6f, 3.5f);
            float cord = 1.6f + (float)rng.NextDouble() * 0.6f;
            Block("Cord", new Vector3(0f, -cord * 0.5f, 0f), new Vector3(0.03f, cord, 0.03f), iron, pivot);
            Cylinder("Shade", new Vector3(0f, -cord - 0.1f, 0f), new Vector3(0.7f, 0.12f, 0.7f), Vector3.zero, iron, pivot);
            Primitive(PrimitiveType.Sphere, "Bulb", bulb, pivot).transform.localPosition = new Vector3(0f, -cord - 0.25f, 0f);
            Light l = AddLight("PendantLight", new Vector3(0f, -cord - 0.4f, 0f), lampColor, lampIntensity, 9f, pivot);
            if (x < 1f) l.shadows = LightShadows.Soft;
            swings.Add(new Swing { pivot = pivot, angle = (float)rng.NextDouble() * 4f - 2f });
        }
    }

    private void BuildFurnace(Transform root, Material iron)
    {
        // 오른쪽 구석의 화로: 붉게 달아오른 입구 + 일렁이는 불빛 + 불티
        Vector3 b = new Vector3(7.8f, 0f, 6.5f);
        Block("Furnace", b + new Vector3(0f, 1.1f, 0f), new Vector3(2f, 2.2f, 1.6f), iron, root);
        Block("FurnaceMouth", b + new Vector3(-1.01f, 0.9f, 0f), new Vector3(0.05f, 0.8f, 0.9f), MakeEmissive(new Color(1f, 0.35f, 0.08f), 3f), root);
        Cylinder("Chimney", b + new Vector3(0f, 4.8f, 0.2f), new Vector3(0.5f, 2.6f, 0.5f), Vector3.zero, iron, root);
        furnace = AddLight("FurnaceLight", b + new Vector3(-1.8f, 1.2f, 0f), new Color(1f, 0.4f, 0.12f), 3.5f, 14f, root);
        furnace.shadows = LightShadows.Soft;
        furnaceBase = furnace.intensity;
        MakeMotes(root, "Sparks", b + new Vector3(-1.3f, 1f, 0f), new Vector3(0.6f, 0.4f, 0.8f), 14f, new Color(1f, 0.55f, 0.15f, 1f), 0.05f, -0.12f, 2f);
    }
}
