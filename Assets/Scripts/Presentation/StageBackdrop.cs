using System.Collections.Generic;
using UnityEngine;

/// <summary>무대 배경 종류 — BattleStageData.backdrop에서 고른다.</summary>
public enum BackdropType
{
    Alley,        // 뒷골목 (어둡고 거친, 비 오는 골목)
    ClockTower,   // 태엽 장치 (톱니바퀴 + 큰 시계)
    FloodedStreet,// 물에 잠긴 거리 (발목까지 찬 물 + 멀리 종탑)
    Sewer,        // 하수도 (둥근 천장 터널 + 수로)
    Workshop      // 시계공의 공방 (벽 가득한 시계 + 작업대)
}

/// <summary>
/// 코드로 만드는 무대 배경의 공통 부모.
/// 씬에 여러 배경을 같이 두고, 전투가 시작될 때 무대에 맞는 하나만 켠다 (StageBackdrop.Show).
/// 씬에 없는 종류는 Show할 때 코드로 만들어 쓴다 (원점 기준).
/// 타격 임팩트 / 감정 단계는 지금 켜진 배경에만 전달된다.
///
/// 변형(Variant): 배경마다 VariantCount가지 분위기(색 · 날씨 · 소품 배치 시드)가 있다.
/// Show할 때 변형을 고르지 않으면 무작위 — 같은 배경이라도 전투마다 모습이 달라진다.
/// 변형 0은 씬/인스펙터에 적힌 값 그대로.
/// </summary>
public abstract class StageBackdrop : MonoBehaviour
{
    private static readonly List<StageBackdrop> all = new List<StageBackdrop>();

    /// <summary>씬에 없을 때 코드로 만들 배경 클래스</summary>
    private static readonly Dictionary<BackdropType, System.Type> autoCreate = new Dictionary<BackdropType, System.Type>
    {
        { BackdropType.Alley, typeof(AlleyBackdrop) },
        { BackdropType.ClockTower, typeof(ClockworkBackdrop) },
        { BackdropType.FloodedStreet, typeof(FloodedStreetBackdrop) },
        { BackdropType.Sewer, typeof(SewerBackdrop) },
        { BackdropType.Workshop, typeof(WorkshopBackdrop) },
    };

    /// <summary>지금 화면에 보이는 배경</summary>
    public static StageBackdrop Active { get; private set; }

    [Tooltip("씬 시작 때 처음 보여줄 배경으로 쓸지 (여러 개면 먼저 켜진 것)")]
    public bool showOnStart = false;

    public abstract BackdropType Type { get; }

    /// <summary>이 배경의 변형 가짓수</summary>
    public virtual int VariantCount { get { return 1; } }

    /// <summary>지금 지어져 있는 변형 번호 (0 ~ VariantCount-1)</summary>
    public int Variant { get; private set; }

    /// <summary>변형 이름 (로그용)</summary>
    public virtual string VariantName { get { return Variant.ToString(); } }

    /// <summary>배경 오브젝트들이 들어가는 자식 (켜고 끄는 단위)</summary>
    protected Transform content;

    protected bool IsActive { get { return Active == this; } }

    protected virtual void Awake()
    {
        all.Add(this);
        content = new GameObject("Content").transform;
        content.SetParent(transform, false);
        Build(content);
        content.gameObject.SetActive(false);
    }

    /// <summary>변형을 바꿔 다시 짓는다 (기존 생성물은 지운다)</summary>
    private void Rebuild(int variant)
    {
        Variant = variant;
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            GameObject child = content.GetChild(i).gameObject;
            child.SetActive(false);   // 지워지기 전 한 프레임 동안 조명이 남지 않게
            Destroy(child);
        }
        Build(content);
    }

    /// <summary>변형마다 다른 무작위 배치를 위한 난수 (같은 변형이면 매번 같은 모양)</summary>
    protected System.Random VariantRandom(int seed)
    {
        return new System.Random(seed + Variant * 7919);
    }

    protected virtual void Start()
    {
        // 아무 배경도 안 켜져 있으면: showOnStart 우선, 없으면 뒷골목, 그것도 없으면 아무거나
        if (Active != null) return;
        if (showOnStart) { Show(Type); return; }
        if (!all.Exists(b => b.showOnStart)) Show(all.Exists(b => b.Type == BackdropType.Alley) ? BackdropType.Alley : Type);
    }

    protected virtual void OnDestroy()
    {
        all.Remove(this);
        if (Active == this) Active = null;
    }

    // ─────────────────────────── 정적 API ───────────────────────────

    /// <summary>
    /// type 배경만 켜고 나머지는 끈다. 씬에 없으면 코드로 만든다.
    /// variant &lt; 0 이면 변형을 무작위로 고른다 (방금 보던 것과는 되도록 다르게).
    /// </summary>
    public static void Show(BackdropType type, int variant = -1)
    {
        StageBackdrop target = all.Find(b => b != null && b.Type == type);
        if (target == null && autoCreate.TryGetValue(type, out System.Type cls))
        {
            GameObject go = new GameObject(cls.Name);
            target = (StageBackdrop)go.AddComponent(cls);   // Awake가 바로 돌며 목록에 등록 + 변형 0으로 지어진다
        }
        if (target == null) return;

        int count = Mathf.Max(1, target.VariantCount);
        int v = variant >= 0 ? variant % count : Random.Range(0, count);
        if (variant < 0 && count > 1 && v == target.Variant && target == Active) v = (v + 1 + Random.Range(0, count - 1)) % count;
        if (target == Active && v == target.Variant) return;

        foreach (StageBackdrop b in all)
        {
            if (b == null || b == target) continue;
            if (b.content != null) b.content.gameObject.SetActive(false);
        }

        if (v != target.Variant) target.Rebuild(v);
        Active = target;
        target.content.gameObject.SetActive(true);
        target.OnShown();
        Debug.Log($"배경: {type} — {target.VariantName}");
    }

    /// <summary>합에서 타격이 들어갈 때 (1 = 큰 타격, 0.3 = 가벼운 충돌)</summary>
    public static void Impact(float strength)
    {
        if (Active != null) Active.OnImpact(Mathf.Max(0f, strength));
    }

    /// <summary>감정 단계(0~5)가 바뀔 때</summary>
    public static void SetTempo(int emotionLevel)
    {
        if (Active != null) Active.OnTempo(Mathf.Max(0, emotionLevel));
    }

    // ─────────────────────────── 자식이 구현 ───────────────────────────

    protected abstract void Build(Transform root);
    protected virtual void OnShown() { }
    protected virtual void OnImpact(float strength) { }
    protected virtual void OnTempo(int emotionLevel) { }

    // ─────────────────────────── 공용 유틸 ───────────────────────────

    /// <summary>씬의 방향광 (없으면 null)</summary>
    protected static Light FindSun()
    {
        if (RenderSettings.sun != null) return RenderSettings.sun;
        foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.type == LightType.Directional) return l;
        }
        return null;
    }

    protected GameObject Primitive(PrimitiveType type, string name, Material mat, Transform parent)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        Destroy(go.GetComponent<Collider>());   // 배경은 클릭 판정에 끼지 않게
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    protected Transform Block(string name, Vector3 pos, Vector3 scale, Material mat, Transform parent)
    {
        Transform t = Primitive(PrimitiveType.Cube, name, mat, parent).transform;
        t.localPosition = pos;
        t.localScale = scale;
        return t;
    }

    protected static Material MakeMaterial(Color color, float metallic, float smoothness, Texture2D texture = null, Vector2? tiling = null)
    {
        Shader shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Diffuse");
        Material m = new Material(shader) { color = color };
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
        if (texture != null)
        {
            m.mainTexture = texture;
            if (tiling.HasValue) m.mainTextureScale = tiling.Value;
        }
        return m;
    }

    protected static Material MakeEmissive(Color color, float intensity)
    {
        Material m = MakeMaterial(color * 0.3f, 0f, 0.2f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", color * intensity);
        return m;
    }

    protected static Light AddLight(string name, Vector3 pos, Color color, float intensity, float range, Transform parent)
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

    protected Transform Cylinder(string name, Vector3 pos, Vector3 scale, Vector3 euler, Material mat, Transform parent)
    {
        Transform t = Primitive(PrimitiveType.Cylinder, name, mat, parent).transform;
        t.localPosition = pos;
        t.localScale = scale;
        t.localRotation = Quaternion.Euler(euler);
        return t;
    }

    /// <summary>안개 · 주변광 · 하늘색 · 방향광 세기를 한 번에 맞춘다</summary>
    protected static void ApplyAtmosphere(Color fog, float fogDensity, Color ambient, Color sky, float sunIntensity)
    {
        RenderSettings.fog = fogDensity > 0f;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = fog;
        RenderSettings.fogDensity = fogDensity;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = ambient;

        Light sun = FindSun();
        if (sun != null) sun.intensity = sunIntensity;

        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = sky;
        }
    }

    /// <summary>빗줄기 파티클 (area = 상자 크기, 위에서 아래로)</summary>
    protected static ParticleSystem MakeRain(Transform parent, Vector3 pos, Vector3 area, float rate, Color color, float slant = -1.2f)
    {
        GameObject go = new GameObject("Rain");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.startLifetime = 1.2f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.035f);
        main.startColor = color;
        main.maxParticles = 4000;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = rate;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = area;

        ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(slant);
        vel.y = new ParticleSystem.MinMaxCurve(-15f);
        vel.z = new ParticleSystem.MinMaxCurve(0f);

        ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
        pr.renderMode = ParticleSystemRenderMode.Stretch;
        pr.velocityScale = 0.05f;
        pr.lengthScale = 1f;
        pr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));

        ps.Play();
        return ps;
    }

    /// <summary>
    /// 떠다니는 먼지/물방울/불티 같은 잔입자 (area 상자 안에서 천천히 움직임).
    /// gravity &gt; 0 이면 떨어지고(물방울), &lt; 0 이면 떠오른다(불티).
    /// </summary>
    protected static ParticleSystem MakeMotes(Transform parent, string name, Vector3 pos, Vector3 area, float rate, Color color,
                                              float size, float gravity, float lifetime = 4f)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.startLifetime = lifetime;
        main.startSpeed = 0.1f;
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
        main.startColor = color;
        main.gravityModifier = gravity;
        main.maxParticles = 600;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = rate;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = area;

        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = ProceduralSprites.Circle().texture };

        ps.Play();
        return ps;
    }

    /// <summary>판자/돌판처럼 줄무늬가 있는 텍스처 (bands = 판 수, 세로 줄눈 + 결)</summary>
    protected static Texture2D PlankTexture(System.Random rng, Color baseColor, int bands, float grain)
    {
        const int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGB24, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        int bandH = size / bands;
        float[] tone = new float[bands];
        int[] seam = new int[bands];
        for (int i = 0; i < bands; i++) { tone[i] = 0.8f + (float)rng.NextDouble() * 0.35f; seam[i] = rng.Next(size); }

        for (int y = 0; y < size; y++)
        {
            int b = y / bandH;
            int ly = y % bandH;
            for (int x = 0; x < size; x++)
            {
                float v = tone[b] * (0.85f + grain * Mathf.PerlinNoise(x * 0.02f + b * 3.1f, y * 0.35f));
                if (ly < 2 || Mathf.Abs(x - seam[b]) < 2) v *= 0.45f;   // 줄눈
                v += (float)rng.NextDouble() * 0.04f - 0.02f;
                tex.SetPixel(x, y, baseColor * v);
            }
        }
        tex.Apply();
        return tex;
    }
}
