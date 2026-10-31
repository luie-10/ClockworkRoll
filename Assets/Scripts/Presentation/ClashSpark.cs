using UnityEngine;

/// <summary>
/// 주사위가 부딪히는 지점에 튀는 불꽃 파티클.
/// 파티클 에셋 없이 Particle System을 코드로 만들어 한 번 터뜨리고 스스로 사라진다.
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
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
        main.startColor = color;
        main.gravityModifier = 0.6f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;   // 다 터지면 오브젝트 자동 삭제

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.1f;

        // 시간이 지나며 작아지게
        ParticleSystem.SizeOverLifetimeModule sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
        psr.sharedMaterial = GetMaterial();

        ps.Play();
    }

    private static Material GetMaterial()
    {
        if (sparkMaterial != null) return sparkMaterial;

        // Built-in 렌더 파이프라인에 항상 있는 셰이더 (머티리얼이 없으면 분홍색으로 나온다)
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        sparkMaterial = new Material(shader);
        return sparkMaterial;
    }
}
