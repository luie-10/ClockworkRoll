using UnityEngine;

/// <summary>
/// 타격 이펙트 모음 — 에셋 없이 코드로 그린 빛 조각(텍스처)을 잠깐 띄웠다가 지운다.
///   Clash  : 주사위가 맞부딪히는 순간 (충격파 고리 + 십자 섬광 + 번쩍이는 조명)
///   Hit    : 공격이 들어간 순간 — 종류마다 자국이 다르다 (참격 = 초승달 베기, 관통 = 긴 직선, 타격 = 터지는 고리)
///   Guard  : 막거나 피한 순간 (푸른 고리)
///   Dust   : 발밑 먼지 (돌진 · 밀려남)
/// 직접 붙일 필요 없음 — BattleManager가 부른다. (연출 전용 — 없어도 게임은 정상 동작)
/// </summary>
public static class ImpactFX
{
    private static readonly Color HotWhite = new Color(1f, 0.97f, 0.9f);

    // ─────────────────────────── 장면별 연출 ───────────────────────────

    /// <summary>주사위 한 쌍이 부딪히는 지점</summary>
    public static void Clash(Vector3 pos, Color color)
    {
        FxQuad.Spawn(RingTexture(), pos, color, 0.28f, Vector2.one * 0.3f, Vector2.one * 2.2f);
        FxQuad.Spawn(StarTexture(), pos, HotWhite, 0.16f, Vector2.one * 1.6f, Vector2.one * 0.5f, Random.Range(-20f, 20f));
        FxQuad.Spawn(GlowTexture(), pos, color, 0.2f, Vector2.one * 1.6f, Vector2.one * 0.6f);
        FxLight.Spawn(pos, color, 2.4f, 6f, 0.18f);
    }

    /// <summary>공격이 들어간 지점. from = 때린 쪽 위치 (자국의 방향을 정한다)</summary>
    public static void Hit(Vector3 pos, Vector3 from, DiceDetail detail, Color color, bool big)
    {
        float side = ScreenSide(pos, from);   // +1 = 화면 오른쪽으로 때림
        float power = big ? 1.35f : 1f;

        switch (detail)
        {
            case DiceDetail.Slash:
            {
                // 비스듬히 긋는 초승달: 색 있는 큰 자국 위에 흰 심을 겹친다
                float roll = Random.Range(18f, 42f) * (Random.value < 0.5f ? 1f : -1f);
                Vector2 a = new Vector2(1.5f * side, 1.5f) * power, b = new Vector2(2.6f * side, 2.6f) * power;
                FxQuad.Spawn(ArcTexture(), pos, color, 0.26f, a, b, roll, 220f * side);
                FxQuad.Spawn(ArcTexture(), pos, HotWhite, 0.16f, a * 0.92f, b * 0.9f, roll, 220f * side);
                break;
            }
            case DiceDetail.Thrust:
            {
                // 뚫고 지나가는 긴 직선 + 뚫린 자리의 작은 고리
                Vector3 through = pos + CameraRight() * (0.5f * side);
                FxQuad.Spawn(StreakTexture(), through, color, 0.24f, new Vector2(1.2f, 0.5f) * power, new Vector2(5f, 0.16f) * power);
                FxQuad.Spawn(StreakTexture(), through, HotWhite, 0.14f, new Vector2(1f, 0.3f) * power, new Vector2(4.2f, 0.08f) * power);
                FxQuad.Spawn(RingTexture(), pos, color, 0.22f, Vector2.one * 0.2f, Vector2.one * 1.1f * power);
                break;
            }
            default:
            {
                // 묵직하게 터지는 고리 + 별 모양 섬광 + 바닥을 타고 퍼지는 충격파
                FxQuad.Spawn(RingTexture(), pos, color, 0.3f, Vector2.one * 0.4f, Vector2.one * 2.8f * power);
                FxQuad.Spawn(StarTexture(), pos, HotWhite, 0.18f, Vector2.one * 2.2f * power, Vector2.one * 0.7f, Random.Range(0f, 90f));
                FxQuad.Spawn(RingTexture(), Ground(pos), color * 0.8f, 0.4f, Vector2.one * 0.5f, Vector2.one * 3.4f * power, 0f, 0f, true);
                break;
            }
        }

        FxQuad.Spawn(GlowTexture(), pos, color, 0.22f, Vector2.one * 2f * power, Vector2.one * 0.8f);
        FxLight.Spawn(pos, color, big ? 3.6f : 2.2f, big ? 9f : 6f, big ? 0.28f : 0.18f);

        if (big)
        {
            FxQuad.Spawn(RingTexture(), pos, HotWhite, 0.36f, Vector2.one * 0.6f, Vector2.one * 4.6f);
            FxQuad.Spawn(RingTexture(), Ground(pos), HotWhite * 0.7f, 0.5f, Vector2.one * 0.6f, Vector2.one * 5.5f, 0f, 0f, true);
        }

        Dust(Ground(pos), side, big ? 12 : 6);
    }

    /// <summary>막음 / 회피 (지켜낸 쪽 앞에 뜨는 고리)</summary>
    public static void Guard(Vector3 pos, Color color)
    {
        FxQuad.Spawn(RingTexture(), pos, color, 0.3f, Vector2.one * 0.5f, Vector2.one * 1.9f);
        FxQuad.Spawn(RingTexture(), pos, HotWhite, 0.2f, Vector2.one * 0.3f, Vector2.one * 1.3f);
        FxQuad.Spawn(GlowTexture(), pos, color, 0.24f, Vector2.one * 1.4f, Vector2.one * 0.9f);
        FxLight.Spawn(pos, color, 1.6f, 5f, 0.16f);
    }

    /// <summary>바닥 먼지. side = 날리는 방향 (화면 기준 +1 오른쪽 / -1 왼쪽 / 0 사방)</summary>
    public static void Dust(Vector3 groundPos, float side, int count = 6)
    {
        GameObject go = new GameObject("Dust");
        go.transform.position = groundPos + Vector3.up * 0.05f;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        Color tone = StageBackdrop.Active != null ? StageBackdrop.Active.GroundPuffColor : new Color(0.62f, 0.58f, 0.52f, 0.34f);

        ParticleSystem.MainModule main = ps.main;
        main.duration = 0.4f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = tone;
        main.gravityModifier = -0.04f;   // 살짝 떠오른다
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        // 납작한 반구에서 옆으로 퍼진다 (side가 있으면 그쪽으로 쏠린다)
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.25f;
        shape.rotation = new Vector3(-90f, 0f, 0f);
        shape.scale = new Vector3(1f, 1f, 0.35f);

        if (Mathf.Abs(side) > 0.01f)
        {
            ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            Vector3 push = CameraRight() * (side * 1.2f);
            vel.x = new ParticleSystem.MinMaxCurve(push.x);
            vel.y = new ParticleSystem.MinMaxCurve(0f);
            vel.z = new ParticleSystem.MinMaxCurve(push.z);
        }

        ParticleSystem.LimitVelocityOverLifetimeModule drag = ps.limitVelocityOverLifetime;
        drag.enabled = true;
        drag.dampen = 0.12f;
        drag.limit = 0.3f;

        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.6f));

        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = SoftMaterial();
        ps.Play();
    }

    // ─────────────────────────── 도우미 ───────────────────────────

    private static Vector3 CameraRight()
    {
        Camera cam = Camera.main;
        Vector3 r = cam != null ? cam.transform.right : Vector3.right;
        r.y = 0f;
        return r.sqrMagnitude > 0.0001f ? r.normalized : Vector3.right;
    }

    /// <summary>from → pos 가 화면에서 오른쪽(+1)인가 왼쪽(-1)인가</summary>
    private static float ScreenSide(Vector3 pos, Vector3 from)
    {
        return Vector3.Dot(pos - from, CameraRight()) >= 0f ? 1f : -1f;
    }

    private static Vector3 Ground(Vector3 pos)
    {
        return new Vector3(pos.x, 0.04f, pos.z);
    }

    // ─────────────────────────── 머티리얼 ───────────────────────────

    private static Material additive, soft;

    /// <summary>빛을 더하는 머티리얼 (불꽃 · 자국 · 섬광). mainTexture는 쓰는 쪽이 MaterialPropertyBlock으로 넣는다</summary>
    public static Material AdditiveMaterial()
    {
        if (additive != null) return additive;

        Shader shader = Resources.Load<Shader>("StageFx");
        if (shader == null || !shader.isSupported) shader = Shader.Find("Sprites/Default");
        additive = new Material(shader) { name = "StageFx (Additive)" };
        return additive;
    }

    /// <summary>부드러운 점 텍스처의 보통 혼합 머티리얼 (먼지처럼 빛나지 않는 것)</summary>
    private static Material SoftMaterial()
    {
        if (soft != null) return soft;
        soft = new Material(Shader.Find("Sprites/Default")) { name = "StageFx (Soft)", mainTexture = GlowTexture() };
        return soft;
    }

    // ─────────────────────────── 텍스처 (한 번 그려서 계속 쓴다) ───────────────────────────

    private static Texture2D glow, ring, arc, streak, star;

    /// <summary>가운데가 밝고 가장자리로 사라지는 둥근 빛</summary>
    public static Texture2D GlowTexture()
    {
        if (glow == null) glow = Paint("FxGlow", 64, (x, y) =>
        {
            float d = Mathf.Sqrt(x * x + y * y);
            float a = Mathf.Clamp01(1f - d);
            return a * a * (0.4f + 0.6f * a);
        });
        return glow;
    }

    /// <summary>얇은 고리 (충격파)</summary>
    public static Texture2D RingTexture()
    {
        if (ring == null) ring = Paint("FxRing", 128, (x, y) =>
        {
            float d = Mathf.Sqrt(x * x + y * y);
            float line = Mathf.Clamp01(1f - Mathf.Abs(d - 0.84f) / 0.07f);      // 또렷한 선
            float halo = Mathf.Clamp01(1f - Mathf.Abs(d - 0.8f) / 0.2f) * 0.25f; // 선 안쪽의 옅은 번짐
            return Mathf.Max(line * line, halo);
        });
        return ring;
    }

    /// <summary>초승달 베기 자국 — 머리(진행 방향)가 굵고 밝으며 꼬리로 갈수록 가늘어진다</summary>
    public static Texture2D ArcTexture()
    {
        if (arc == null) arc = Paint("FxArc", 128, (x, y) =>
        {
            float d = Mathf.Sqrt(x * x + y * y);
            float ang = Mathf.Atan2(y, x) * Mathf.Rad2Deg;      // -180 ~ 180
            float t = Mathf.InverseLerp(-70f, 80f, ang);        // 0 = 꼬리, 1 = 머리
            if (t <= 0f || t >= 1f) return 0f;

            float along = Mathf.Sin(t * Mathf.PI);              // 양 끝이 뾰족하게
            float thickness = 0.02f + 0.13f * along * (0.35f + 0.65f * t);
            float outer = 0.88f;
            float inside = outer - d;                            // 바깥 테두리에서 안쪽으로 얼마나 들어왔나
            if (inside < 0f) return Mathf.Clamp01(1f + inside / 0.02f) * along;   // 바깥쪽은 날카롭게 끊는다
            float body = Mathf.Clamp01(1f - inside / thickness);
            return body * body * (0.25f + 0.75f * t) * Mathf.Clamp01(along * 3f);
        });
        return arc;
    }

    /// <summary>양 끝이 뾰족한 가로 직선</summary>
    public static Texture2D StreakTexture()
    {
        if (streak == null) streak = Paint("FxStreak", 128, (x, y) =>
        {
            float len = Mathf.Clamp01(1f - Mathf.Abs(x));
            float width = 0.5f * len * len + 0.02f;
            float a = Mathf.Clamp01(1f - Mathf.Abs(y) / width);
            return a * a * Mathf.Sqrt(len);
        });
        return streak;
    }

    /// <summary>네 갈래 별 섬광</summary>
    public static Texture2D StarTexture()
    {
        if (star == null) star = Paint("FxStar", 128, (x, y) =>
        {
            float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
            float h = Mathf.Clamp01(1f - ay / (0.03f + 0.12f * (1f - ax) * (1f - ax))) * Mathf.Clamp01(1f - ax);
            float v = Mathf.Clamp01(1f - ax / (0.03f + 0.12f * (1f - ay) * (1f - ay))) * Mathf.Clamp01(1f - ay);
            float core = Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y) / 0.3f);
            return Mathf.Clamp01(Mathf.Max(h, v) + core * core);
        });
        return star;
    }

    /// <summary>흰색 + 알파로 그린다. shape(x, y)는 -1~1 좌표에서의 밝기(0~1)</summary>
    private static Texture2D Paint(string name, int size, System.Func<float, float, float> shape)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.name = name;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.hideFlags = HideFlags.HideAndDontSave;

        Color[] px = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(shape(u, v)));
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }
}

/// <summary>
/// 잠깐 떴다 사라지는 빛 조각 한 장. 크기가 from → to로 빠르게 퍼지며 옅어진다.
/// 히트스톱(시간 정지) 중에도 움직이도록 unscaled 시간을 쓴다.
/// </summary>
public class FxQuad : MonoBehaviour
{
    private static Mesh quad;
    private static MaterialPropertyBlock block;
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

    private MeshRenderer meshRenderer;
    private Texture2D texture;
    private Color color;
    private Vector2 sizeFrom, sizeTo;
    private float life, age, roll, rollSpeed;
    private bool flat;

    /// <summary>
    /// roll = 화면 안에서 돌린 각도(도), rollSpeed = 초당 회전(도).
    /// flat = true면 카메라를 보지 않고 바닥에 눕는다 (바닥을 타고 퍼지는 충격파).
    /// </summary>
    public static FxQuad Spawn(Texture2D tex, Vector3 pos, Color color, float life, Vector2 sizeFrom, Vector2 sizeTo,
                               float roll = 0f, float rollSpeed = 0f, bool flat = false)
    {
        GameObject go = new GameObject("Fx " + tex.name);
        go.transform.position = pos;

        go.AddComponent<MeshFilter>().sharedMesh = Quad();
        FxQuad fx = go.AddComponent<FxQuad>();
        fx.meshRenderer = go.AddComponent<MeshRenderer>();
        fx.meshRenderer.sharedMaterial = ImpactFX.AdditiveMaterial();
        fx.meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        fx.meshRenderer.receiveShadows = false;
        fx.meshRenderer.sortingOrder = 20;   // 캐릭터 위, 숫자 팝업(30) 아래

        fx.texture = tex;
        fx.color = color;
        fx.life = Mathf.Max(0.01f, life);
        fx.sizeFrom = sizeFrom;
        fx.sizeTo = sizeTo;
        fx.roll = roll;
        fx.rollSpeed = rollSpeed;
        fx.flat = flat;
        fx.Apply(0f);
        return fx;
    }

    void LateUpdate()
    {
        age += Time.unscaledDeltaTime;
        if (age >= life) { Destroy(gameObject); return; }
        Apply(age / life);
    }

    private void Apply(float t)
    {
        float grow = 1f - (1f - t) * (1f - t) * (1f - t);   // 처음에 확 퍼지고 천천히 멈춘다
        Vector2 size = Vector2.LerpUnclamped(sizeFrom, sizeTo, grow);
        transform.localScale = new Vector3(size.x, size.y, 1f);

        if (flat) transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        else
        {
            Camera cam = Camera.main;
            Quaternion face = cam != null ? cam.transform.rotation : Quaternion.identity;
            transform.rotation = face * Quaternion.Euler(0f, 0f, roll + rollSpeed * grow * life);
        }

        if (block == null) block = new MaterialPropertyBlock();
        Color c = color;
        c.a *= (1f - t) * (1f - t);
        block.SetColor(ColorId, c);
        block.SetTexture(MainTexId, texture);
        meshRenderer.SetPropertyBlock(block);
    }

    private static Mesh Quad()
    {
        if (quad != null) return quad;

        quad = new Mesh { name = "FxQuad", hideFlags = HideFlags.HideAndDontSave };
        quad.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
        };
        quad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        quad.colors = new[] { Color.white, Color.white, Color.white, Color.white };
        quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        quad.RecalculateBounds();
        return quad;
    }
}

/// <summary>타격 순간 주변(바닥 · 벽 · 캐릭터)을 잠깐 물들이는 조명. 금방 꺼지고 스스로 사라진다.</summary>
public class FxLight : MonoBehaviour
{
    private Light flash;
    private float intensity, life, age;

    public static void Spawn(Vector3 pos, Color color, float intensity, float range, float life)
    {
        GameObject go = new GameObject("FxLight");
        go.transform.position = pos;

        FxLight fx = go.AddComponent<FxLight>();
        fx.flash = go.AddComponent<Light>();
        fx.flash.type = LightType.Point;
        fx.flash.color = color;
        fx.flash.range = range;
        fx.flash.intensity = intensity;
        fx.flash.shadows = LightShadows.None;
        fx.intensity = intensity;
        fx.life = Mathf.Max(0.01f, life);
    }

    void Update()
    {
        age += Time.unscaledDeltaTime;
        if (age >= life) { Destroy(gameObject); return; }
        float k = 1f - age / life;
        flash.intensity = intensity * k * k;
    }
}
