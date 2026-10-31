using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 무대 뒤 '태엽 장치' 배경 — 에셋 없이 전부 코드로 만든다.
///
///   - 맞물려 도는 톱니바퀴 사슬 (좌/우), 시계 뒤 큰 톱니, 멀리 있는 흐릿한 톱니들
///   - 큰 시계(문자판 + 바늘), 흔들리는 추, 기둥, 파이프, 바닥, 뒷벽, 호박색 조명
///
/// 모든 톱니는 하나의 '주 회전각'에 톱니 수 비율로 묶여 있어서 실제로 맞물린 것처럼 돈다.
///   - StageBackdrop.Impact(세기) : 합에서 타격이 들어갈 때 → 태엽이 확 감기듯 빨라졌다가 서서히 원래대로
///   - StageBackdrop.SetTempo(감정 단계) : 감정이 오를수록 기본 회전이 빨라진다
///
/// 빈 오브젝트에 붙이면 Play 시작 때 생성된다 (편집 화면에서는 보이지 않음).
/// 기준 위치는 이 오브젝트의 위치 — 무대 중앙(바닥 높이)에 두면 된다.
/// Battle Stage의 Backdrop이 ClockTower인 무대에서 켜진다.
/// </summary>
public class ClockworkBackdrop : StageBackdrop
{
    public override BackdropType Type { get { return BackdropType.ClockTower; } }

    [Header("회전")]
    [Tooltip("평상시 주 회전 속도 (도/초)")]
    public float baseSpeed = 14f;
    [Tooltip("감정 단계 1당 빨라지는 비율")]
    public float tempoPerEmotion = 0.35f;
    [Tooltip("Impact(1) 한 번에 더해지는 회전 속도 (도/초)")]
    public float impactSpin = 520f;
    [Tooltip("임팩트로 빨라진 속도가 원래대로 돌아오는 빠르기")]
    public float impulseDecay = 2.8f;

    [Header("색")]
    public Color brass = new Color(0.72f, 0.55f, 0.24f);
    public Color copper = new Color(0.66f, 0.36f, 0.22f);
    public Color iron = new Color(0.28f, 0.28f, 0.31f);
    public Color lampColor = new Color(1f, 0.72f, 0.4f);

    private class Gear
    {
        public Transform transform;
        public float phase;   // 시작 각도 (도)
        public float ratio;   // 주 회전각 대비 회전 비율 (부호 = 방향)
    }

    private readonly List<Gear> gears = new List<Gear>();
    private Transform minuteHand, hourHand, pendulum;
    private Light lamp;
    private float lampBaseIntensity;

    private float masterAngle;   // 모든 톱니가 따라가는 주 회전각
    private float impulse;       // 임팩트로 더해진 속도 (점점 줄어듦)
    private float tempo = 1f;
    private float pendulumTime;

    private Material brassMat, copperMat, ironMat;
    private readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();

    private const float Module = 0.25f;   // 톱니 크기 단위 (같은 값이어야 맞물림)

    // ─────────────────────────── 배경 이벤트 ───────────────────────────

    protected override void OnShown()
    {
        RenderSettings.fog = false;   // 태엽 탑은 안개 없이 또렷하게
        Light sun = FindSun();
        if (sun != null) sun.intensity = 0.8f;
    }

    /// <summary>타격 임팩트 — 태엽이 확 감긴다. strength 1 = 큰 타격, 0.3 = 가벼운 충돌.</summary>
    protected override void OnImpact(float strength)
    {
        impulse += impactSpin * strength;

        // 조명이 순간 밝아진다
        if (lamp != null) lamp.intensity = lampBaseIntensity * (1f + 1.2f * strength);
    }

    /// <summary>감정 단계(0~5)에 맞춰 기본 회전 속도를 바꾼다.</summary>
    protected override void OnTempo(int emotionLevel)
    {
        tempo = 1f + tempoPerEmotion * emotionLevel;
    }

    // ─────────────────────────── 매 프레임 ───────────────────────────

    void Update()
    {
        if (!IsActive) return;
        float dt = Time.deltaTime;

        impulse *= Mathf.Exp(-impulseDecay * dt);
        float speed = baseSpeed * tempo + impulse;
        masterAngle += speed * dt;

        foreach (Gear g in gears)
            g.transform.localRotation = Quaternion.Euler(0f, 0f, g.phase + masterAngle * g.ratio);

        // 시계 바늘: 시계 방향(화면 기준) = -Z 회전
        if (minuteHand != null) minuteHand.localRotation = Quaternion.Euler(0f, 0f, -masterAngle * 0.5f);
        if (hourHand != null) hourHand.localRotation = Quaternion.Euler(0f, 0f, -masterAngle * 0.5f / 12f);

        // 추: 회전 속도가 빠를수록 빨리 흔들린다
        pendulumTime += dt * (speed / baseSpeed);
        if (pendulum != null) pendulum.localRotation = Quaternion.Euler(0f, 0f, 18f * Mathf.Sin(pendulumTime * 2.2f));

        if (lamp != null) lamp.intensity = Mathf.Lerp(lamp.intensity, lampBaseIntensity, dt * 4f);
    }

    // ─────────────────────────── 무대 구성 ───────────────────────────

    private Transform root;   // 생성물이 들어갈 부모 (StageBackdrop.content)

    protected override void Build(Transform contentRoot)
    {
        root = contentRoot;
        brassMat = MakeMaterial(brass, 0.85f, 0.55f);
        copperMat = MakeMaterial(copper, 0.8f, 0.5f);
        ironMat = MakeMaterial(iron, 0.6f, 0.35f);

        // 바닥 / 뒷벽
        Block("Floor", new Vector3(0f, -0.11f, 10f), new Vector3(60f, 0.2f, 44f), MakeMaterial(new Color(0.11f, 0.1f, 0.09f), 0.2f, 0.3f));
        Block("BackWall", new Vector3(0f, 8f, 17f), new Vector3(60f, 30f, 0.5f), MakeMaterial(new Color(0.07f, 0.065f, 0.06f), 0f, 0.1f));

        // 큰 시계 + 그 뒤의 큰 톱니
        Vector3 clockCenter = new Vector3(0f, 6.5f, 11.5f);
        BuildClock(clockCenter, 3.6f);
        Gear behindClock = AddGear("ClockGear", new Vector2(0f, 6.5f), 12.6f, 40, 0.5f, 0.35f, brassMat);
        Chain(behindClock, 12.6f, new[] { (35f, 16), (-30f, 10) }, ironMat);
        Chain(behindClock, 12.6f, new[] { (145f, 16), (210f, 10) }, ironMat);

        // 좌우 톱니 사슬 (아래쪽 톱니는 바닥에 반쯤 묻힌다)
        Gear left = AddGear("LeftGear", new Vector2(-9.5f, 6f), 9.5f, 30, 1f, 0.4f, copperMat);
        Chain(left, 9.5f, new[] { (-40f, 14), (-100f, 24), (60f, 12) }, brassMat);

        Gear right = AddGear("RightGear", new Vector2(9.5f, 6f), 9.5f, 30, -1f, 0.4f, copperMat);
        Chain(right, 9.5f, new[] { (220f, 14), (280f, 24), (120f, 12) }, brassMat);

        // 멀리 있는 흐릿한 톱니들 (따로 천천히 돈다)
        Material farMat = MakeMaterial(iron * 0.6f, 0.4f, 0.2f);
        AddGear("FarGear", new Vector2(-15f, 9f), 15.5f, 48, 0.25f, 0.5f, farMat, 0.3f);
        AddGear("FarGear", new Vector2(15.5f, 2.5f), 15.5f, 44, -0.3f, 0.5f, farMat, 0.3f);
        AddGear("FarGear", new Vector2(-16f, -1f), 15.5f, 36, 0.4f, 0.5f, farMat, 0.3f);

        // 기둥 + 파이프
        for (int s = -1; s <= 1; s += 2)
        {
            Block("Pillar", new Vector3(13f * s, 6f, 8f), new Vector3(1.2f, 12f, 1.2f), ironMat);
            Block("PillarCap", new Vector3(13f * s, 0.4f, 8f), new Vector3(1.8f, 0.8f, 1.8f), brassMat);
        }
        for (int i = 0; i < 3; i++)
        {
            Transform pipe = Primitive(PrimitiveType.Cylinder, "Pipe", copperMat).transform;
            pipe.localPosition = new Vector3(0f, 10.2f + i * 0.7f, 13.5f + i * 0.4f);
            pipe.localRotation = Quaternion.Euler(0f, 0f, 90f);
            pipe.localScale = new Vector3(0.35f, 30f, 0.35f);
        }

        // 호박색 조명 (임팩트 때 번쩍)
        GameObject lampGO = new GameObject("ClockLamp");
        lampGO.transform.SetParent(root, false);
        lampGO.transform.localPosition = new Vector3(0f, 6f, 7f);
        lamp = lampGO.AddComponent<Light>();
        lamp.type = LightType.Point;
        lamp.color = lampColor;
        lamp.range = 18f;
        lamp.intensity = lampBaseIntensity = 1.6f;
    }

    private void BuildClock(Vector3 center, float radius)
    {
        Transform clock = new GameObject("Clock").transform;
        clock.SetParent(root, false);
        clock.localPosition = center;

        Disc(clock, "Rim", Vector3.forward * 0.06f, radius * 2.15f, 0.1f, brassMat);
        Disc(clock, "Face", Vector3.zero, radius * 2f, 0.06f, MakeMaterial(new Color(0.82f, 0.77f, 0.62f), 0.1f, 0.4f));

        for (int i = 0; i < 12; i++)
        {
            Transform tick = Primitive(PrimitiveType.Cube, "Tick", ironMat).transform;
            tick.SetParent(clock, false);
            float a = i * 30f * Mathf.Deg2Rad;
            tick.localPosition = new Vector3(Mathf.Sin(a), Mathf.Cos(a), 0f) * radius * 0.86f + Vector3.back * 0.06f;
            tick.localRotation = Quaternion.Euler(0f, 0f, -i * 30f);
            tick.localScale = new Vector3(i % 3 == 0 ? 0.2f : 0.1f, i % 3 == 0 ? 0.6f : 0.35f, 0.04f);
        }

        hourHand = Hand(clock, "HourHand", radius * 0.5f, 0.2f, -0.1f);
        minuteHand = Hand(clock, "MinuteHand", radius * 0.78f, 0.12f, -0.13f);
        Disc(clock, "Cap", Vector3.back * 0.16f, 0.45f, 0.06f, brassMat);

        // 시계 아래 추
        pendulum = new GameObject("Pendulum").transform;
        pendulum.SetParent(clock, false);
        pendulum.localPosition = new Vector3(0f, -radius, 0.3f);
        Transform rod = Primitive(PrimitiveType.Cube, "Rod", brassMat).transform;
        rod.SetParent(pendulum, false);
        rod.localPosition = new Vector3(0f, -1.1f, 0f);
        rod.localScale = new Vector3(0.08f, 2.2f, 0.08f);
        Transform bob = Primitive(PrimitiveType.Sphere, "Bob", brassMat).transform;
        bob.SetParent(pendulum, false);
        bob.localPosition = new Vector3(0f, -2.3f, 0f);
        bob.localScale = new Vector3(0.7f, 0.7f, 0.25f);
    }

    private Transform Hand(Transform clock, string name, float length, float width, float z)
    {
        Transform pivot = new GameObject(name).transform;
        pivot.SetParent(clock, false);
        pivot.localPosition = new Vector3(0f, 0f, z);

        Transform bar = Primitive(PrimitiveType.Cube, "Bar", ironMat).transform;
        bar.SetParent(pivot, false);
        bar.localPosition = new Vector3(0f, length * 0.45f, 0f);
        bar.localScale = new Vector3(width, length, 0.04f);
        return pivot;
    }

    // ─────────────────────────── 톱니 ───────────────────────────

    /// <summary>톱니 하나 생성. ratio = 주 회전각 대비 회전 비율.</summary>
    private Gear AddGear(string name, Vector2 xy, float z, int teeth, float ratio, float thickness, Material mat, float phase = 0f)
    {
        GameObject go = new GameObject(name + " T" + teeth);
        go.transform.SetParent(root, false);
        go.transform.localPosition = new Vector3(xy.x, xy.y, z);

        go.AddComponent<MeshFilter>().sharedMesh = GetGearMesh(teeth, thickness);
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;

        // 가운데 축
        Transform hub = Primitive(PrimitiveType.Cylinder, "Hub", ironMat).transform;
        hub.SetParent(go.transform, false);
        hub.localRotation = Quaternion.Euler(90f, 0f, 0f);
        float hubSize = PitchRadius(teeth) * 0.5f;
        hub.localScale = new Vector3(hubSize, thickness * 0.8f, hubSize);

        Gear gear = new Gear { transform = go.transform, phase = phase, ratio = ratio };
        gears.Add(gear);
        return gear;
    }

    /// <summary>
    /// start 톱니에 맞물리는 톱니들을 차례로 붙인다. links = (이전 톱니에서 본 방향(도), 톱니 수).
    /// 중심 거리 = 두 피치 반지름의 합, 회전 비율 = -이전비율 × 이전톱니/새톱니, 시작 각도는 톱니가 엇갈리게 맞춘다.
    /// </summary>
    private void Chain(Gear start, float z, (float angleDeg, int teeth)[] links, Material mat)
    {
        Gear prev = start;
        int prevTeeth = TeethOf(start);

        foreach ((float angleDeg, int teeth) in links)
        {
            float dist = PitchRadius(prevTeeth) + PitchRadius(teeth);
            float a = angleDeg * Mathf.Deg2Rad;
            Vector3 p = prev.transform.localPosition + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * dist;

            float ratio = -prev.ratio * prevTeeth / teeth;

            // 맞물림 위상: 이전 톱니의 '이'가 접점에 오면 새 톱니는 '골'이 오도록
            float sPrev = 360f / prevTeeth, sNew = 360f / teeth;
            float betaPrev = angleDeg - prev.phase - 0.3f * sPrev;
            float phase = angleDeg + 180f - 0.3f * sNew - betaPrev * prevTeeth / teeth - sNew * 0.5f;

            Gear g = AddGear("ChainGear", new Vector2(p.x, p.y), z, teeth, ratio, 0.3f, mat, phase);
            prev = g;
            prevTeeth = teeth;
        }
    }

    private static int TeethOf(Gear g)
    {
        // 이름 끝의 "T숫자"에서 톱니 수를 읽는다
        string n = g.transform.name;
        int idx = n.LastIndexOf('T');
        return int.Parse(n.Substring(idx + 1));
    }

    private static float PitchRadius(int teeth)
    {
        return Module * teeth * 0.5f;
    }

    private Mesh GetGearMesh(int teeth, float thickness)
    {
        string key = teeth + "_" + thickness;
        if (!meshCache.TryGetValue(key, out Mesh mesh))
        {
            float pitch = PitchRadius(teeth);
            mesh = BuildGearMesh(teeth, pitch + Module * 0.6f, pitch - Module * 0.6f, pitch * 0.18f, thickness);
            meshCache[key] = mesh;
        }
        return mesh;
    }

    /// <summary>
    /// 톱니바퀴 메시 (Z축이 회전축, -Z 면이 카메라 쪽).
    /// 이 하나 = 골→이 올라감→이 끝→내려감 4점, 가운데에 축 구멍. 면마다 정점을 따로 둬서 각진 금속 느낌.
    /// </summary>
    private static Mesh BuildGearMesh(int teeth, float tipR, float rootR, float holeR, float thickness)
    {
        int n = teeth * 4;
        Vector2[] outer = new Vector2[n];
        Vector2[] inner = new Vector2[n];
        float step = Mathf.PI * 2f / teeth;
        float[] frac = { 0f, 0.15f, 0.45f, 0.6f };   // 이 하나 안에서의 각도 위치
        float[] rad = { rootR, tipR, tipR, rootR };

        for (int t = 0; t < teeth; t++)
        {
            for (int k = 0; k < 4; k++)
            {
                float a = (t + frac[k]) * step;
                outer[t * 4 + k] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad[k];
                inner[t * 4 + k] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * holeR;
            }
        }

        List<Vector3> v = new List<Vector3>();
        List<int> tri = new List<int>();
        float zf = -thickness * 0.5f, zb = thickness * 0.5f;

        // 앞면 / 뒷면 (바깥 윤곽과 구멍 사이를 띠로 채움)
        for (int face = 0; face < 2; face++)
        {
            float z = face == 0 ? zf : zb;
            int baseIdx = v.Count;
            for (int i = 0; i < n; i++) v.Add(new Vector3(outer[i].x, outer[i].y, z));
            for (int i = 0; i < n; i++) v.Add(new Vector3(inner[i].x, inner[i].y, z));

            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                int o0 = baseIdx + i, o1 = baseIdx + j, h0 = baseIdx + n + i, h1 = baseIdx + n + j;
                if (face == 0) { tri.Add(o0); tri.Add(h0); tri.Add(o1); tri.Add(h0); tri.Add(h1); tri.Add(o1); }
                else           { tri.Add(o0); tri.Add(o1); tri.Add(h0); tri.Add(h0); tri.Add(o1); tri.Add(h1); }
            }
        }

        // 바깥 옆면 (바깥을 향함) / 구멍 안쪽 옆면 (안을 향함)
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            AddQuad(v, tri,
                new Vector3(outer[i].x, outer[i].y, zf), new Vector3(outer[j].x, outer[j].y, zf),
                new Vector3(outer[j].x, outer[j].y, zb), new Vector3(outer[i].x, outer[i].y, zb));
            AddQuad(v, tri,
                new Vector3(inner[j].x, inner[j].y, zf), new Vector3(inner[i].x, inner[i].y, zf),
                new Vector3(inner[i].x, inner[i].y, zb), new Vector3(inner[j].x, inner[j].y, zb));
        }

        Mesh mesh = new Mesh { name = "Gear" + teeth };
        mesh.SetVertices(v);
        mesh.SetTriangles(tri, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // a→b→c→d 순서의 사각형 (바깥에서 봤을 때 시계 방향)
    private static void AddQuad(List<Vector3> v, List<int> tri, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        int i = v.Count;
        v.Add(a); v.Add(b); v.Add(c); v.Add(d);
        tri.Add(i); tri.Add(i + 1); tri.Add(i + 2);
        tri.Add(i); tri.Add(i + 2); tri.Add(i + 3);
    }

    // ─────────────────────────── 기본 도형 유틸 ───────────────────────────

    // 부모를 root(content)로 고정한 짧은 버전
    private GameObject Primitive(PrimitiveType type, string name, Material mat)
    {
        return Primitive(type, name, mat, root);
    }

    private void Block(string name, Vector3 pos, Vector3 scale, Material mat)
    {
        Block(name, pos, scale, mat, root);
    }

    private void Disc(Transform parent, string name, Vector3 localPos, float diameter, float depth, Material mat)
    {
        Transform t = Primitive(PrimitiveType.Cylinder, name, mat).transform;
        t.SetParent(parent, false);
        t.localPosition = localPos;
        t.localRotation = Quaternion.Euler(90f, 0f, 0f);   // 원기둥 축(Y)을 Z로 눕혀 카메라를 향하게
        t.localScale = new Vector3(diameter, depth * 0.5f, diameter);
    }
}
