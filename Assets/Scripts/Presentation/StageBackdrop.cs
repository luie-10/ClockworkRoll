using System.Collections.Generic;
using UnityEngine;

/// <summary>무대 배경 종류 — BattleStageData.backdrop에서 고른다.</summary>
public enum BackdropType
{
    Alley,        // 뒷골목 (어둡고 거친, 비 오는 골목)
    ClockTower    // 태엽 장치 (톱니바퀴 + 큰 시계)
}

/// <summary>
/// 코드로 만드는 무대 배경의 공통 부모.
/// 씬에 여러 배경을 같이 두고, 전투가 시작될 때 무대에 맞는 하나만 켠다 (StageBackdrop.Show).
/// 타격 임팩트 / 감정 단계는 지금 켜진 배경에만 전달된다.
/// </summary>
public abstract class StageBackdrop : MonoBehaviour
{
    private static readonly List<StageBackdrop> all = new List<StageBackdrop>();

    /// <summary>지금 화면에 보이는 배경</summary>
    public static StageBackdrop Active { get; private set; }

    [Tooltip("씬 시작 때 처음 보여줄 배경으로 쓸지 (여러 개면 먼저 켜진 것)")]
    public bool showOnStart = false;

    public abstract BackdropType Type { get; }

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

    /// <summary>type 배경만 켜고 나머지는 끈다. 해당 배경이 씬에 없으면 지금 배경 유지.</summary>
    public static void Show(BackdropType type)
    {
        StageBackdrop target = all.Find(b => b != null && b.Type == type);
        if (target == null || target == Active) return;

        foreach (StageBackdrop b in all)
        {
            if (b == null || b == target) continue;
            if (b.content != null) b.content.gameObject.SetActive(false);
        }

        Active = target;
        target.content.gameObject.SetActive(true);
        target.OnShown();
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
}
