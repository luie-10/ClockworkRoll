using UnityEngine;

/// <summary>
/// 주사위가 부딪히는 지점에 튀는 불꽃 파티클.
/// 파티클 에셋 없이 Particle System을 코드로 만들어 한 번 터뜨리고 스스로 사라진다.
///   - 날아가는 방향으로 길게 늘어난 불티 (속도가 빠를수록 길다)
///   - 흰빛으로 튀어나와 제 색으로 식으며 사라진다 — 밝은 부분은 블룸으로 번진다
/// 직접 붙일 필요 없음 — ClashSpark.Burst()를 호출하면 된다.
/// </summary>
public static class ClashSpark
{
    private static Material sparkMaterial;

    /// <summary>position에서 color 색 불꽃을 count개 터뜨린다.</summary>
    public static void Burst(Vector3 position, Color color, int count = 18, float speed = 4f)
    {
        GameObject go = new GameObject("ClashSpark");
        go.transform.position = position;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);   // 설정 바꾸기 전에 정지

        ParticleSystem.MainModule main = ps.main;
        main.duration = 0.3f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.6f, speed * 1.7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
        main.startColor = Color.white;
        main.gravityModifier = 1.1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;   // 다 터지면 오브젝트 자동 삭제

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.08f;

        // 튀어나온 뒤 금방 느려진다 (처음에만 빠르게 → 터지는 느낌)
        ParticleSystem.LimitVelocityOverLifetimeModule drag = ps.limitVelocityOverLifetime;
        drag.enabled = true;
        drag.dampen = 0.08f;
        drag.limit = speed * 0.35f;

        // 흰빛 → 제 색 → 어둡게 식으며 사라진다
        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(color, 0.25f), new GradientColorKey(color * 0.7f, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        // 시간이 지나며 작아지게
        ParticleSystem.SizeOverLifetimeModule sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

        ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
        psr.renderMode = ParticleSystemRenderMode.Stretch;   // 날아가는 방향으로 늘어난 불티
        psr.velocityScale = 0.07f;
        psr.lengthScale = 1.6f;
        psr.sortingOrder = 21;
        psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        psr.sharedMaterial = GetMaterial();

        ps.Play();

        // 터지는 자리의 짧은 빛 번짐
        float glow = Mathf.Clamp(count / 18f, 0.5f, 1.6f);
        FxQuad.Spawn(ImpactFX.GlowTexture(), position, color, 0.18f, Vector2.one * 1.3f * glow, Vector2.one * 0.4f * glow);
    }

    private static Material GetMaterial()
    {
        if (sparkMaterial != null) return sparkMaterial;

        // 빛을 더하는 이펙트 셰이더 (없으면 Built-in에 항상 있는 스프라이트 셰이더)
        sparkMaterial = new Material(ImpactFX.AdditiveMaterial()) { name = "ClashSpark", mainTexture = ImpactFX.GlowTexture() };
        if (sparkMaterial.HasProperty("_Gain")) sparkMaterial.SetFloat("_Gain", 2.4f);
        return sparkMaterial;
    }
}
