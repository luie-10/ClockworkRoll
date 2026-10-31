using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 뒷골목 배경 — 어둡고 거친, 비 내리는 골목. 에셋 없이 전부 코드로 만든다.
///
///   - 안쪽으로 길게 뻗은 골목: 양옆 벽돌 건물(높이 들쭉날쭉), 막다른 끝의 문과 희미한 등
///   - 젖은 아스팔트 + 물웅덩이, 쓰레기통 · 쓰레기봉투 · 나무상자 · 불타는 드럼통
///   - 깜빡이는 가로등, 흔들리는 네온 간판, 비상계단, 배관, 실외기, 늘어진 전선
///   - 안개 + 비 파티클 (감정 단계가 오를수록 빗줄기가 거세진다)
///   - 타격 임팩트: 가로등이 지직거리고, 간판이 흔들리고, 드럼통 불길이 치솟는다
///
/// 벽돌 / 아스팔트 텍스처도 코드로 그린다 (Mathf.PerlinNoise). 무작위 배치는 고정 시드라 매번 같은 모양.
/// Battle Stage의 Backdrop이 Alley인 무대(기본값)에서 켜진다.
/// </summary>
public class AlleyBackdrop : StageBackdrop
{
    public override BackdropType Type { get { return BackdropType.Alley; } }

    [Header("분위기")]
    public Color fogColor = new Color(0.035f, 0.04f, 0.05f);
    [Range(0f, 0.1f)] public float fogDensity = 0.035f;
    public Color ambient = new Color(0.1f, 0.1f, 0.13f);
    [Tooltip("방향광 세기 (뒷골목은 어둡게)")]
    public float sunIntensity = 0.25f;

    [Header("조명")]
    public Color lampColor = new Color(1f, 0.74f, 0.42f);
    public Color neonColor = new Color(1f, 0.2f, 0.45f);
    public Color fireColor = new Color(1f, 0.45f, 0.12f);

    [Header("비")]
    public float rainRate = 450f;

    private Light streetLamp, fireLight;
    private Material lampBulbMat, neonMat;
    private Transform sign;
    private ParticleSystem rain;
    private float lampBase, fireBase;

    private float flickerTime;      // 임팩트로 인한 지직거림 남은 시간
    private float signAngle, signVelocity;
    private float tempoMul = 1f;
    private float neonTimer;

    private System.Random rng;

    // ─────────────────────────── 배경 이벤트 ───────────────────────────

    protected override void OnShown()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = ambient;

        Light sun = FindSun();
        if (sun != null) sun.intensity = sunIntensity;

        // 하늘 대신 안개색으로 칠한다 (건물 사이로 보이는 밤하늘)
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = fogColor;
        }
    }

    protected override void OnImpact(float strength)
    {
        flickerTime = Mathf.Max(flickerTime, 0.12f + 0.35f * strength);
        signVelocity += (Random.value < 0.5f ? -1f : 1f) * 260f * strength;
        if (fireLight != null) fireLight.intensity = fireBase * (1.6f + strength);
    }

    protected override void OnTempo(int emotionLevel)
    {
        tempoMul = 1f + 0.35f * emotionLevel;
        if (rain != null)
        {
            ParticleSystem.EmissionModule e = rain.emission;
            e.rateOverTime = rainRate * tempoMul;
        }
    }

    // ─────────────────────────── 매 프레임 ───────────────────────────

    void Update()
    {
        if (!IsActive) return;
        float dt = Time.deltaTime;
        float t = Time.time;

        // 가로등: 평소엔 가끔 미세하게 떨리고, 임팩트 후엔 크게 지직거린다
        if (streetLamp != null)
        {
            float k = 1f;
            if (flickerTime > 0f)
            {
                flickerTime -= dt;
                k = Random.value < 0.45f ? Random.Range(0f, 0.25f) : Random.Range(0.8f, 1.3f);
            }
            else if (Random.value < 0.01f) k = Random.Range(0.5f, 0.9f);
            streetLamp.intensity = lampBase * k;
            lampBulbMat.SetColor("_EmissionColor", lampColor * 2.5f * k);
        }

        // 드럼통 불: 일렁이다가 임팩트로 치솟은 뒤 원래대로
        if (fireLight != null)
        {
            float wobble = 0.85f + 0.3f * Mathf.PerlinNoise(t * 6f, 0.3f);
            fireLight.intensity = Mathf.Lerp(fireLight.intensity, fireBase * wobble, dt * 6f);
        }

        // 간판: 스프링처럼 흔들리다 멈춘다
        if (sign != null)
        {
            signVelocity += -signAngle * 40f * dt;
            signVelocity *= Mathf.Exp(-2.2f * dt);
            signAngle += signVelocity * dt;
            sign.localRotation = Quaternion.Euler(signAngle, 0f, 0f);
        }

        // 네온: 감정이 오를수록 자주 깜빡인다
        if (neonMat != null)
        {
            neonTimer -= dt * tempoMul;
            float on = 1f;
            if (neonTimer < 0f)
            {
                on = Random.value < 0.5f ? 0.15f : 1f;
                if (neonTimer < -0.08f) neonTimer = Random.Range(0.4f, 3f);
            }
            neonMat.SetColor("_EmissionColor", neonColor * 3f * on);
        }
    }

    // ─────────────────────────── 무대 구성 ───────────────────────────

    protected override void Build(Transform root)
    {
        rng = new System.Random(20261031);   // 고정 시드 → 매번 같은 골목

        Texture2D brickTex = BrickTexture();
        Texture2D asphaltTex = AsphaltTexture();

        // 바닥 (젖은 아스팔트)
        Block("Ground", new Vector3(0f, -0.11f, 12f), new Vector3(17f, 0.2f, 50f),
            MakeMaterial(new Color(0.6f, 0.6f, 0.65f), 0.1f, 0.62f, asphaltTex, new Vector2(4f, 12f)), root);

        // 양옆 건물 (구간마다 높이/깊이를 달리해 들쭉날쭉하게)
        Material brickA = MakeMaterial(new Color(0.8f, 0.68f, 0.62f), 0f, 0.15f, brickTex, new Vector2(5f, 3.5f));
        Material brickB = MakeMaterial(new Color(0.6f, 0.6f, 0.62f), 0f, 0.12f, brickTex, new Vector2(5f, 4.5f));
        float[] segZ = { -6f, 12f, 28f };
        float[] segLen = { 18f, 18f, 14f };
        for (int i = 0; i < segZ.Length; i++)
        {
            float hL = 11f + i * 3f + (float)rng.NextDouble() * 2f;
            float hR = 13f + (2 - i) * 2f + (float)rng.NextDouble() * 2f;
            Block("WallL", new Vector3(-8.5f, hL * 0.5f, segZ[i]), new Vector3(2f, hL, segLen[i]), i % 2 == 0 ? brickA : brickB, root);
            Block("WallR", new Vector3(8.5f, hR * 0.5f, segZ[i]), new Vector3(2f, hR, segLen[i]), i % 2 == 0 ? brickB : brickA, root);
        }

        // 막다른 끝: 벽 + 철문 + 희미한 등
        Block("EndWall", new Vector3(0f, 7f, 35f), new Vector3(18f, 14f, 1f), brickB, root);
        Material ironMat = MakeMaterial(new Color(0.16f, 0.17f, 0.18f), 0.7f, 0.35f);
        Block("Door", new Vector3(1.5f, 1.4f, 34.45f), new Vector3(1.8f, 2.8f, 0.1f), ironMat, root);
        AddLight("DoorLight", new Vector3(1.5f, 3.6f, 33.6f), new Color(0.55f, 0.7f, 1f), 1.2f, 6f, root);
        Block("DoorLamp", new Vector3(1.5f, 3.3f, 34.4f), new Vector3(0.35f, 0.18f, 0.25f), MakeEmissive(new Color(0.55f, 0.7f, 1f), 1.5f), root);

        BuildWindows(root);
        BuildPipesAndUnits(root, ironMat);
        BuildFireEscape(root, ironMat);
        BuildProps(root, ironMat);
        BuildStreetLamp(root, ironMat);
        BuildSign(root, ironMat);
        BuildWires(root);
        BuildRain(root);
    }

    private void BuildWindows(Transform root)
    {
        Material dark = MakeMaterial(new Color(0.03f, 0.035f, 0.04f), 0.2f, 0.9f);
        Material warm = MakeEmissive(new Color(1f, 0.62f, 0.3f), 1.1f);
        Material cold = MakeEmissive(new Color(0.45f, 0.6f, 0.85f), 0.8f);
        Material frame = MakeMaterial(new Color(0.12f, 0.1f, 0.09f), 0f, 0.2f);

        for (int side = -1; side <= 1; side += 2)
        {
            for (float z = -3f; z <= 32f; z += 3.6f)
            {
                for (float y = 3.2f; y <= 13f; y += 3.4f)
                {
                    if (rng.NextDouble() < 0.18) continue;   // 군데군데 비워 둔다

                    double r = rng.NextDouble();
                    Material glass = r < 0.12 ? warm : r < 0.2 ? cold : dark;   // 대부분 불 꺼진 창
                    float x = side * 7.47f;
                    Block("WindowFrame", new Vector3(x, y, z), new Vector3(0.06f, 1.75f, 1.25f), frame, root);
                    Block("Window", new Vector3(x - side * 0.02f, y, z), new Vector3(0.05f, 1.55f, 1.05f), glass, root);
                }
            }
        }
    }

    private void BuildPipesAndUnits(Transform root, Material ironMat)
    {
        Material rust = MakeMaterial(new Color(0.32f, 0.17f, 0.1f), 0.5f, 0.25f);
        for (int i = 0; i < 6; i++)
        {
            int side = i % 2 == 0 ? -1 : 1;
            float z = -2f + i * 5.5f + (float)rng.NextDouble() * 2f;
            Transform pipe = Primitive(PrimitiveType.Cylinder, "Pipe", i % 3 == 0 ? rust : ironMat, root).transform;
            pipe.localPosition = new Vector3(side * 7.3f, 6f, z);
            pipe.localScale = new Vector3(0.22f, 6f, 0.22f);
        }

        // 가로 배관 (왼쪽 벽)
        Transform hp = Primitive(PrimitiveType.Cylinder, "PipeH", rust, root).transform;
        hp.localPosition = new Vector3(-7.25f, 5.2f, 10f);
        hp.localRotation = Quaternion.Euler(90f, 0f, 0f);
        hp.localScale = new Vector3(0.3f, 18f, 0.3f);

        // 실외기
        Material ac = MakeMaterial(new Color(0.42f, 0.43f, 0.42f), 0.4f, 0.3f);
        for (int i = 0; i < 4; i++)
        {
            int side = i % 2 == 0 ? 1 : -1;
            Block("AC", new Vector3(side * 7f, 4.5f + i * 1.7f, 3f + i * 6f), new Vector3(0.9f, 0.8f, 1.2f), ac, root);
        }
    }

    private void BuildFireEscape(Transform root, Material ironMat)
    {
        // 오른쪽 벽의 비상계단 (철판 + 난간 + 비스듬한 계단)
        for (int level = 0; level < 3; level++)
        {
            float y = 3.8f + level * 3.4f;
            Block("Landing", new Vector3(6.75f, y, 8f), new Vector3(1.4f, 0.08f, 3.4f), ironMat, root);
            Block("Rail", new Vector3(6.1f, y + 0.5f, 8f), new Vector3(0.05f, 0.05f, 3.4f), ironMat, root);
            for (float z = 6.4f; z <= 9.7f; z += 0.55f)
                Block("RailBar", new Vector3(6.1f, y + 0.25f, z), new Vector3(0.04f, 0.5f, 0.04f), ironMat, root);

            if (level < 2)
            {
                Transform stair = Block("Stair", new Vector3(6.75f, y + 1.7f, 8f), new Vector3(1.1f, 0.06f, 4.2f), ironMat, root);
                stair.localRotation = Quaternion.Euler(-38f, 0f, 0f);
            }
        }
    }

    private void BuildProps(Transform root, Material ironMat)
    {
        // 쓰레기통 (왼쪽)
        Material green = MakeMaterial(new Color(0.1f, 0.18f, 0.12f), 0.5f, 0.35f);
        Block("Dumpster", new Vector3(-6.1f, 0.75f, 4.5f), new Vector3(1.5f, 1.5f, 2.8f), green, root);
        Transform lid = Block("DumpsterLid", new Vector3(-6.1f, 1.58f, 4.5f), new Vector3(1.6f, 0.08f, 2.9f), green, root);
        lid.localRotation = Quaternion.Euler(0f, 0f, 8f);

        // 쓰레기봉투
        Material bag = MakeMaterial(new Color(0.03f, 0.03f, 0.035f), 0.1f, 0.75f);
        for (int i = 0; i < 6; i++)
        {
            Transform b = Primitive(PrimitiveType.Sphere, "TrashBag", bag, root).transform;
            b.localPosition = new Vector3(-6.3f + (float)rng.NextDouble() * 1.4f, 0.28f, 2.2f + (float)rng.NextDouble() * 1.5f);
            b.localScale = new Vector3(0.8f, 0.55f, 0.7f) * (0.8f + (float)rng.NextDouble() * 0.4f);
            b.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 180f, 0f);
        }

        // 나무 상자 더미 (오른쪽 안쪽)
        Material wood = MakeMaterial(new Color(0.32f, 0.22f, 0.13f), 0f, 0.2f);
        Block("Crate", new Vector3(6.2f, 0.45f, 13f), new Vector3(0.9f, 0.9f, 0.9f), wood, root);
        Block("Crate", new Vector3(6.4f, 0.45f, 14.1f), new Vector3(0.9f, 0.9f, 0.9f), wood, root);
        Transform top = Block("Crate", new Vector3(6.3f, 1.3f, 13.5f), new Vector3(0.8f, 0.8f, 0.8f), wood, root);
        top.localRotation = Quaternion.Euler(0f, 17f, 0f);

        // 불타는 드럼통 (오른쪽 앞) — 골목의 주 광원 중 하나
        Material rust = MakeMaterial(new Color(0.3f, 0.15f, 0.08f), 0.6f, 0.25f);
        Transform barrel = Primitive(PrimitiveType.Cylinder, "Barrel", rust, root).transform;
        barrel.localPosition = new Vector3(6.2f, 0.55f, 4.2f);
        barrel.localScale = new Vector3(0.75f, 0.55f, 0.75f);
        fireLight = AddLight("FireLight", new Vector3(6.2f, 1.6f, 4.2f), fireColor, 2.2f, 7f, root);
        fireBase = fireLight.intensity;
        BuildFire(root, new Vector3(6.2f, 1.1f, 4.2f));

        // 물웅덩이 (불빛이 번들거리게 아주 매끈하게)
        Material puddle = MakeMaterial(new Color(0.02f, 0.025f, 0.03f), 0.4f, 0.97f);
        Vector3[] puddles = { new Vector3(-1.5f, 0f, 3.5f), new Vector3(2.8f, 0f, 7f), new Vector3(-3.5f, 0f, 11f), new Vector3(1f, 0f, 17f), new Vector3(4.5f, 0f, -0.5f) };
        foreach (Vector3 p in puddles)
        {
            Transform pd = Primitive(PrimitiveType.Cylinder, "Puddle", puddle, root).transform;
            pd.localPosition = p + Vector3.up * 0.004f;
            pd.localScale = new Vector3(1.6f + (float)rng.NextDouble() * 1.8f, 0.004f, 1f + (float)rng.NextDouble() * 1.2f);
        }
    }

    private void BuildStreetLamp(Transform root, Material ironMat)
    {
        Transform pole = Primitive(PrimitiveType.Cylinder, "LampPole", ironMat, root).transform;
        pole.localPosition = new Vector3(-6.6f, 3.5f, 7f);
        pole.localScale = new Vector3(0.18f, 3.5f, 0.18f);
        Block("LampArm", new Vector3(-5.9f, 6.9f, 7f), new Vector3(1.5f, 0.1f, 0.1f), ironMat, root);
        Block("LampHead", new Vector3(-5.2f, 6.75f, 7f), new Vector3(0.6f, 0.25f, 0.4f), ironMat, root);

        lampBulbMat = MakeEmissive(lampColor, 2.5f);
        Block("LampBulb", new Vector3(-5.2f, 6.6f, 7f), new Vector3(0.4f, 0.08f, 0.28f), lampBulbMat, root);

        GameObject go = new GameObject("StreetLamp");
        go.transform.SetParent(root, false);
        go.transform.localPosition = new Vector3(-5.2f, 6.5f, 7f);
        go.transform.localRotation = Quaternion.Euler(80f, 15f, 0f);   // 아래로, 살짝 골목 가운데로
        streetLamp = go.AddComponent<Light>();
        streetLamp.type = LightType.Spot;
        streetLamp.color = lampColor;
        streetLamp.spotAngle = 85f;
        streetLamp.range = 16f;
        streetLamp.intensity = lampBase = 3.2f;
        streetLamp.shadows = LightShadows.Soft;
    }

    private void BuildSign(Transform root, Material ironMat)
    {
        // 오른쪽 벽에서 튀어나온 쇠막대 + 매달린 네온 간판 (임팩트에 흔들림)
        Block("SignBracket", new Vector3(6.6f, 6.2f, 1.5f), new Vector3(1.6f, 0.08f, 0.08f), ironMat, root);

        sign = new GameObject("SignPivot").transform;
        sign.SetParent(root, false);
        sign.localPosition = new Vector3(6.1f, 6.15f, 1.5f);

        neonMat = MakeEmissive(neonColor, 3f);
        Transform board = Block("SignBoard", Vector3.zero, new Vector3(0.08f, 0.9f, 1.6f), MakeMaterial(new Color(0.08f, 0.06f, 0.07f), 0.2f, 0.4f), sign);
        board.localPosition = new Vector3(0f, -0.6f, 0f);
        Transform neon = Block("SignNeon", Vector3.zero, new Vector3(0.1f, 0.62f, 1.3f), neonMat, sign);
        neon.localPosition = new Vector3(-0.02f, -0.6f, 0f);

        Light glow = AddLight("SignGlow", new Vector3(5.6f, 5.5f, 1.5f), neonColor, 0.9f, 4f, root);
        glow.shadows = LightShadows.None;
    }

    private void BuildWires(Transform root)
    {
        Material wireMat = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.02f, 0.02f, 0.02f) };
        for (int i = 0; i < 4; i++)
        {
            float z = 2f + i * 7f;
            float y = 9.5f + (float)rng.NextDouble() * 2.5f;
            float sag = 0.8f + (float)rng.NextDouble() * 0.8f;

            GameObject go = new GameObject("Wire");
            go.transform.SetParent(root, false);
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.sharedMaterial = wireMat;
            lr.widthMultiplier = 0.035f;
            lr.positionCount = 16;
            for (int p = 0; p < 16; p++)
            {
                float u = p / 15f;
                float x = Mathf.Lerp(-7.4f, 7.4f, u);
                float droop = 4f * u * (1f - u) * sag;   // 포물선으로 늘어짐
                lr.SetPosition(p, new Vector3(x, y - droop, z + (u - 0.5f) * 1.5f));
            }
        }
    }

    private void BuildRain(Transform root)
    {
        GameObject go = new GameObject("Rain");
        go.transform.SetParent(root, false);
        go.transform.localPosition = new Vector3(0f, 14f, 10f);
        rain = go.AddComponent<ParticleSystem>();
        rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = rain.main;
        main.loop = true;
        main.startLifetime = 1.2f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.035f);
        main.startColor = new Color(0.7f, 0.76f, 0.88f, 0.35f);
        main.maxParticles = 3000;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = rain.emission;
        emission.rateOverTime = rainRate;

        ParticleSystem.ShapeModule shape = rain.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(16f, 0.1f, 32f);

        ParticleSystem.VelocityOverLifetimeModule vel = rain.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-1.2f);
        vel.y = new ParticleSystem.MinMaxCurve(-15f);
        vel.z = new ParticleSystem.MinMaxCurve(0f);

        ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
        pr.renderMode = ParticleSystemRenderMode.Stretch;   // 빗줄기처럼 길게
        pr.velocityScale = 0.05f;
        pr.lengthScale = 1f;
        pr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));

        rain.Play();
    }

    private void BuildFire(Transform root, Vector3 pos)
    {
        GameObject go = new GameObject("BarrelFire");
        go.transform.SetParent(root, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // 위로 뿜게
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.75f, 0.25f, 0.8f), new Color(1f, 0.3f, 0.08f, 0.7f));

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 30f;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 12f;
        shape.radius = 0.25f;

        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));

        ps.Play();
    }

    private static Light AddLight(string name, Vector3 pos, Color color, float intensity, float range, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        return l;
    }

    // ─────────────────────────── 텍스처 (코드로 그림) ───────────────────────────

    /// <summary>낡은 벽돌: 벽돌마다 색이 다르고, 줄눈, 얼룩(때)이 진 텍스처.</summary>
    private Texture2D BrickTexture()
    {
        const int size = 256, bw = 64, bh = 32, mortar = 3;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGB24, true);
        tex.wrapMode = TextureWrapMode.Repeat;

        Dictionary<int, float> brickTone = new Dictionary<int, float>();
        Color mortarColor = new Color(0.2f, 0.19f, 0.18f);

        for (int y = 0; y < size; y++)
        {
            int row = y / bh;
            int offset = (row % 2) * (bw / 2);
            for (int x = 0; x < size; x++)
            {
                int bx = (x + offset) % size;
                int col = bx / bw;
                int lx = bx % bw, ly = y % bh;

                float grime = 0.7f + 0.35f * Mathf.PerlinNoise(x * 0.025f, y * 0.025f);   // 큰 얼룩
                float grain = (float)rng.NextDouble() * 0.08f - 0.04f;                     // 자잘한 거칠기

                Color c;
                if (lx < mortar || ly < mortar)
                {
                    c = mortarColor * (grime + grain);
                }
                else
                {
                    int key = row * 100 + col;
                    if (!brickTone.TryGetValue(key, out float tone))
                    {
                        tone = 0.7f + (float)rng.NextDouble() * 0.4f;
                        if (rng.NextDouble() < 0.1) tone *= 0.55f;   // 가끔 그을린 벽돌
                        brickTone[key] = tone;
                    }
                    // 벽돌 가장자리를 살짝 어둡게 (입체감)
                    float edge = Mathf.Min(Mathf.Min(lx - mortar, bw - lx), Mathf.Min(ly - mortar, bh - ly));
                    float bevel = Mathf.Lerp(0.8f, 1f, Mathf.Clamp01(edge / 4f));
                    c = new Color(0.42f, 0.2f, 0.14f) * tone * bevel * grime + new Color(grain, grain, grain);
                }
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return tex;
    }

    /// <summary>젖은 아스팔트: 어두운 회색 바탕 + 자갈 반점 + 갈라진 금.</summary>
    private Texture2D AsphaltTexture()
    {
        const int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGB24, true);
        tex.wrapMode = TextureWrapMode.Repeat;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float v = 0.15f + 0.06f * Mathf.PerlinNoise(x * 0.04f + 10f, y * 0.04f) + (float)rng.NextDouble() * 0.05f;
                if (rng.NextDouble() < 0.012) v += 0.12f;   // 자갈
                tex.SetPixel(x, y, new Color(v, v, v * 1.05f));
            }
        }

        // 금: 무작위로 꺾이며 이어지는 어두운 선
        for (int c = 0; c < 7; c++)
        {
            float px = (float)rng.NextDouble() * size, py = (float)rng.NextDouble() * size;
            float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
            int len = 40 + rng.Next(80);
            for (int i = 0; i < len; i++)
            {
                ang += ((float)rng.NextDouble() - 0.5f) * 0.6f;
                px = (px + Mathf.Cos(ang) + size) % size;
                py = (py + Mathf.Sin(ang) + size) % size;
                tex.SetPixel((int)px, (int)py, new Color(0.05f, 0.05f, 0.05f));
            }
        }
        tex.Apply();
        return tex;
    }
}
