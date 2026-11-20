using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 주조소 배경 — 태엽 부품을 부어 만드는 쇳물 공장. 에셋 없이 전부 코드로 만든다.
///
///   - 철판 바닥, 뒤쪽을 가로지르는 쇳물 도랑 (빛이 일렁인다)
///   - 천장 레일에 매달린 도가니가 도랑으로 쇳물을 붓는다, 쇠사슬에 매달린 갈고리들
///   - 오른쪽 벽의 거대한 플라이휠 (감정이 오를수록 빨리 돈다), 철골 기둥과 들보
///   - 떠오르는 불티 (타격 때 확 튄다)
///
/// 변형: 0 쇳물 주황 / 1 식어 가는 붉은빛 / 2 청동 불꽃 (초록)
/// 씬에 없어도 된다 — BattleStageData.backdrop이 Foundry인 무대에서 코드로 만들어진다.
/// </summary>
public class FoundryBackdrop : StageBackdrop
{
    public override BackdropType Type { get { return BackdropType.Foundry; } }
    public override int VariantCount { get { return 3; } }
    public override Color GroundPuffColor { get { return new Color(0.35f, 0.3f, 0.28f, 0.4f); } }   // 그을음
    public override string VariantName { get { return Variant == 1 ? "식어 가는 붉은빛" : Variant == 2 ? "청동 불꽃" : "쇳물 주황"; } }

    private const float BackZ = 14f, SideX = 11f, Ceiling = 9f;

    private Color molten, fog, ambient;
    private float glowBase;
    private readonly List<Light> glows = new List<Light>();
    private readonly List<Transform> hooks = new List<Transform>();
    private Material moltenMat;
    private Transform flywheel, crucible;
    private ParticleSystem embers;
    private float surge, swing, tempo = 1f;
    private System.Random rng;

    // ─────────────────────────── 배경 이벤트 ───────────────────────────

    protected override void OnShown()
    {
        ApplyAtmosphere(fog, 0.03f, ambient, fog * 0.6f, 0.15f);
    }

    protected override void OnImpact(float strength)
    {
        surge = Mathf.Max(surge, strength);
        swing = Mathf.Max(swing, strength);
        if (embers != null) embers.Emit(Mathf.RoundToInt(15 + 50 * strength));
    }

    protected override void OnTempo(int emotionLevel)
    {
        tempo = 1f + 0.35f * emotionLevel;
    }

    void Update()
    {
        if (!IsActive) return;
        float t = Time.time, dt = Time.deltaTime;

        surge = Mathf.MoveTowards(surge, 0f, dt * 1.5f);
        swing = Mathf.MoveTowards(swing, 0f, dt * 0.5f);

        for (int i = 0; i < glows.Count; i++)
        {
            float n = Mathf.PerlinNoise(t * 1.6f + i * 3.7f, 0.3f);
            glows[i].intensity = glowBase * (0.75f + 0.5f * n + 1.2f * surge);
        }
        if (moltenMat != null)
            moltenMat.SetColor("_EmissionColor", molten * (1.6f + 0.6f * Mathf.PerlinNoise(t * 2f, 1.1f) + 2f * surge));

        if (flywheel != null) flywheel.Rotate(0f, 40f * tempo * dt, 0f, Space.Self);
        if (crucible != null) crucible.localRotation = Quaternion.Euler(0f, 0f, 38f + Mathf.Sin(t * 0.7f) * 2f + swing * 6f * Mathf.Sin(t * 8f));
        for (int i = 0; i < hooks.Count; i++)
            hooks[i].localRotation = Quaternion.Euler(Mathf.Sin(t * 1.2f + i) * (3f + 18f * swing), 0f, Mathf.Cos(t * 0.9f + i) * (2f + 12f * swing));
    }

    // ─────────────────────────── 무대 구성 ───────────────────────────

    protected override void Build(Transform root)
    {
        rng = VariantRandom(7121);
        glows.Clear();
        hooks.Clear();

        switch (Variant)
        {
            case 1:  molten = new Color(0.9f, 0.18f, 0.05f); fog = new Color(0.07f, 0.03f, 0.02f); ambient = new Color(0.12f, 0.06f, 0.05f); glowBase = 1.6f; break;
            case 2:  molten = new Color(0.35f, 1f, 0.45f); fog = new Color(0.02f, 0.06f, 0.04f); ambient = new Color(0.07f, 0.13f, 0.1f); glowBase = 2.2f; break;
            default: molten = new Color(1f, 0.5f, 0.12f); fog = new Color(0.09f, 0.05f, 0.03f); ambient = new Color(0.16f, 0.1f, 0.07f); glowBase = 2.6f; break;
        }

        Material plate = MakeMaterial(new Color(0.22f, 0.21f, 0.2f), 0.6f, 0.45f, PlankTexture(rng, Color.white, 6, 0.3f), new Vector2(6f, 4f));
        Material brick = MakeMaterial(new Color(0.24f, 0.15f, 0.12f), 0f, 0.15f, PlankTexture(rng, Color.white, 14, 0.5f), new Vector2(6f, 2f));
        Material steel = MakeMaterial(new Color(0.18f, 0.18f, 0.2f), 0.8f, 0.4f);
        Material soot = MakeMaterial(new Color(0.08f, 0.07f, 0.07f), 0.3f, 0.2f);
        Material brass = MakeMaterial(new Color(0.65f, 0.48f, 0.22f), 0.85f, 0.5f);
        moltenMat = MakeEmissive(molten, 1.8f);

        // 방
        Block("Floor", new Vector3(0f, -0.1f, 2f), new Vector3(SideX * 2f, 0.2f, 26f), plate, root);
        Block("BackWall", new Vector3(0f, Ceiling * 0.5f, BackZ), new Vector3(SideX * 2f, Ceiling, 0.5f), brick, root);
        Block("WallL", new Vector3(-SideX, Ceiling * 0.5f, 2f), new Vector3(0.5f, Ceiling, 26f), brick, root);
        Block("WallR", new Vector3(SideX, Ceiling * 0.5f, 2f), new Vector3(0.5f, Ceiling, 26f), brick, root);
        Block("Ceiling", new Vector3(0f, Ceiling, 2f), new Vector3(SideX * 2f, 0.3f, 26f), soot, root);

        // 철골 기둥 + 들보 + 천장 레일
        for (float x = -8f; x <= 8f; x += 8f)
        {
            Block("Column", new Vector3(x, Ceiling * 0.5f, 11f), new Vector3(0.5f, Ceiling, 0.5f), steel, root);
            Block("ColumnFlange", new Vector3(x, Ceiling * 0.5f, 11f), new Vector3(0.9f, Ceiling, 0.12f), steel, root);
        }
        Block("Girder", new Vector3(0f, Ceiling - 1f, 11f), new Vector3(SideX * 2f, 0.6f, 0.4f), steel, root);
        Block("Rail", new Vector3(0f, Ceiling - 1.6f, 8.5f), new Vector3(SideX * 2f, 0.25f, 0.25f), steel, root);

        // 쇳물 도랑: 뒤쪽을 가로질러 흐른다 (돌 턱 + 빛나는 쇳물)
        Block("TroughFront", new Vector3(0f, 0.2f, 7.6f), new Vector3(SideX * 2f, 0.4f, 0.3f), brick, root);
        Block("TroughBack", new Vector3(0f, 0.2f, 9.4f), new Vector3(SideX * 2f, 0.4f, 0.3f), brick, root);
        Transform pool = Block("Molten", new Vector3(0f, 0.12f, 8.5f), new Vector3(SideX * 2f, 0.05f, 1.6f), moltenMat, root);
        pool.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        for (float x = -8f; x <= 8f; x += 5.3f)
        {
            Light l = AddLight("MoltenGlow", new Vector3(x, 1.2f, 8.2f), molten, glowBase, 9f, root);
            l.shadows = glows.Count < 2 ? LightShadows.Soft : LightShadows.None;
            glows.Add(l);
        }

        // 도가니: 레일에 매달려 기울어진 채 쇳물을 붓는다
        Vector3 cAt = new Vector3(-3.5f, 4.2f, 8.5f);   // 도랑(z 7.6~9.4) 바로 위
        Block("Hanger", new Vector3(cAt.x, (Ceiling - 1.6f + cAt.y) * 0.5f, cAt.z), new Vector3(0.12f, Ceiling - 1.6f - cAt.y, 0.12f), steel, root);
        crucible = new GameObject("Crucible").transform;
        crucible.SetParent(root, false);
        crucible.localPosition = cAt;
        Cylinder("Pot", new Vector3(0f, -0.6f, 0f), new Vector3(1.6f, 0.7f, 1.6f), Vector3.zero, soot, crucible);
        Cylinder("PotRim", new Vector3(0f, 0.1f, 0f), new Vector3(1.75f, 0.06f, 1.75f), Vector3.zero, steel, crucible);
        Cylinder("PotFill", new Vector3(0f, 0.18f, 0f), new Vector3(1.45f, 0.01f, 1.45f), Vector3.zero, moltenMat, crucible);
        // 기운 쪽 입술(-x 쪽으로 38° 기울어 가장 낮은 곳)에서 도랑 쇳물 위까지 곧게 떨어진다
        Vector3 lip = cAt + new Vector3(-0.79f, -0.41f, 0f);
        float pourLen = lip.y - 0.14f;
        Block("Pour", new Vector3(lip.x, 0.14f + pourLen * 0.5f, cAt.z), new Vector3(0.14f, pourLen, 0.14f), moltenMat, root);
        MakeMotes(root, "PourSpark", new Vector3(lip.x, 0.4f, cAt.z), new Vector3(0.5f, 0.1f, 0.5f), 18f, molten, 0.06f, -0.4f, 1.2f);

        // 갈고리 (쇠사슬)
        for (int i = 0; i < 3; i++)
        {
            Transform pivot = new GameObject("HookPivot").transform;
            pivot.SetParent(root, false);
            pivot.localPosition = new Vector3(2.5f + i * 2.6f, Ceiling - 1.6f, 8.5f);   // 레일에 매단다
            float len = 2f + (float)rng.NextDouble() * 1.5f;
            Block("Chain", new Vector3(0f, -len * 0.5f, 0f), new Vector3(0.07f, len, 0.07f), steel, pivot);
            Block("Hook", new Vector3(0.1f, -len - 0.15f, 0f), new Vector3(0.35f, 0.3f, 0.08f), soot, pivot);
            hooks.Add(pivot);
        }

        // 플라이휠: 뒷벽 앞 큰 바퀴
        flywheel = new GameObject("Flywheel").transform;
        flywheel.SetParent(root, false);
        flywheel.localPosition = new Vector3(5.5f, 4.6f, 12.9f);
        flywheel.localRotation = Quaternion.Euler(90f, 0f, 0f);   // 축이 카메라 쪽을 향한다
        Cylinder("Rim", Vector3.zero, new Vector3(5f, 0.25f, 5f), Vector3.zero, steel, flywheel);
        Cylinder("Hub", Vector3.zero, new Vector3(0.9f, 0.4f, 0.9f), Vector3.zero, brass, flywheel);
        for (int i = 0; i < 6; i++)
        {
            Transform spoke = Block("Spoke", Vector3.zero, new Vector3(4.6f, 0.3f, 0.3f), soot, flywheel);
            spoke.localRotation = Quaternion.Euler(0f, i * 30f, 0f);
        }
        Block("Axle", new Vector3(5.5f, 4.6f, 13.5f), new Vector3(0.4f, 0.4f, 1.2f), steel, root);

        // 왼쪽: 거푸집 더미 + 기어 부품 상자
        for (int i = 0; i < 5; i++)
        {
            float x = -9.5f + (float)rng.NextDouble() * 2.5f, z = 2f + i * 1.2f;
            Block("Mold", new Vector3(x, 0.3f, z), new Vector3(1.1f, 0.6f, 0.9f), soot, root);
            if (rng.NextDouble() < 0.6)
                Cylinder("CastGear", new Vector3(x, 0.66f, z), new Vector3(0.7f, 0.05f, 0.7f), Vector3.zero, brass, root);
        }

        // 뒷벽 높은 창 (밖은 어둡다) + 굴뚝 배관
        for (float x = -6f; x <= 6f; x += 4f)
            Block("HighWindow", new Vector3(x, Ceiling - 2.4f, BackZ - 0.32f), new Vector3(1.6f, 1.8f, 0.05f), MakeEmissive(new Color(0.2f, 0.25f, 0.35f), 0.5f), root);
        Cylinder("Flue", new Vector3(-9.3f, Ceiling * 0.5f, BackZ - 1f), new Vector3(1f, Ceiling * 0.5f, 1f), Vector3.zero, soot, root);

        embers = MakeMotes(root, "Embers", new Vector3(0f, 0.8f, 6f), new Vector3(18f, 1f, 6f), 22f, Color.Lerp(molten, Color.white, 0.25f), 0.06f, -0.12f, 3.5f);
        MakeMotes(root, "Haze", new Vector3(0f, 4f, 6f), new Vector3(18f, 4f, 8f), 5f, new Color(0.3f, 0.25f, 0.22f, 0.12f), 2.2f, -0.005f, 7f);
    }
}
