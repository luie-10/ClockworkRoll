using UnityEngine;

/// <summary>
/// 공들여 그린 캐릭터 그림 (주인공 · 쥐) — 큰 도화지(96x128)에 부위별로 그리고 명암과 색 테두리를 입힌다.
/// Illustrator가 이름을 보고 부른다. 나머지 캐릭터는 예전 그림을 두 배로 키워 같은 마무리(Finish)를 거친다.
/// 모든 그림은 오른쪽을 보고, 발은 아래쪽 가운데.
/// </summary>
public static class SpritePainter
{
    public const int W = 96, H = 128;

    public enum Pose { Idle, Slash, Thrust, Blunt, Guard, Evade, Hit }

    internal static readonly Color Ink = new Color(0.07f, 0.05f, 0.09f, 1f);
    internal static readonly Color Brass = new Color(0.9f, 0.69f, 0.3f);
    internal static readonly Color BrassDark = new Color(0.6f, 0.41f, 0.16f);
    internal static readonly Color Steel = new Color(0.8f, 0.84f, 0.9f);
    internal static readonly Color SteelDark = new Color(0.47f, 0.52f, 0.63f);

    /// <summary>마무리: 덩어리 명암 + 색 있는 테두리 (모든 캐릭터 공통)</summary>
    public static void Finish(PixelCanvas c)
    {
        c.Volume();
        c.SoftOutline(Ink);
    }

    /// <summary>그림 전체에 색을 끼얹는다 (맞는 포즈의 붉은 기, 물에 잠긴 모습의 푸른 기 등)</summary>
    public static void Wash(PixelCanvas c, Color color, float amount)
    {
        for (int y = 0; y < c.height; y++)
            for (int x = 0; x < c.width; x++)
            {
                Color p = c.Get(x, y);
                if (p.a < 0.5f) continue;
                Color m = Color.Lerp(p, color, amount);
                m.a = 1f;
                c.Set(x, y, m);
            }
    }

    public static readonly Color HitWash = new Color(1f, 0.3f, 0.25f);

    // ─────────────────────────── 주인공 (이안) ───────────────────────────

    internal struct Stance
    {
        public float lean, crouch, spread;       // 상체 기울기(앞 +) · 주저앉은 정도 · 다리 벌림
        public float arm, fore, sword, backArm;  // 앞팔 위/아래 마디 각도 · 칼 각도 · 뒷팔 각도 (도, 0 = 오른쪽, 반시계)
        public float headX, headY;
    }

    internal static Stance HeroStance(Pose pose)
    {
        switch (pose)
        {
            case Pose.Slash:  return new Stance { lean = 5, crouch = 2, spread = 6, arm = 20, fore = -5, sword = -28, backArm = -150, headX = 1 };
            case Pose.Thrust: return new Stance { lean = 9, crouch = 4, spread = 9, arm = 8, fore = 2, sword = 2, backArm = -165, headX = 2, headY = -1 };
            case Pose.Blunt:  return new Stance { lean = 3, crouch = 0, spread = 5, arm = 62, fore = 80, sword = 118, backArm = -60, headY = 1 };
            case Pose.Guard:  return new Stance { lean = -2, crouch = 4, spread = 5, arm = -10, fore = 62, sword = 104, backArm = -40, headX = -1, headY = -1 };
            case Pose.Evade:  return new Stance { lean = -9, crouch = 6, spread = 8, arm = -140, fore = -160, sword = 160, backArm = 150, headX = -2 };
            case Pose.Hit:    return new Stance { lean = -7, crouch = 3, spread = 3, arm = -120, fore = -135, sword = -160, backArm = 160, headX = -3, headY = 1 };
            default:          return new Stance { lean = 0, crouch = 0, spread = 2, arm = -72, fore = -38, sword = 58, backArm = -104 };
        }
    }

    public static PixelCanvas Hero(Pose pose)
    {
        PixelCanvas c = new PixelCanvas(W, H);
        Stance s = HeroStance(pose);

        Color coat = new Color(0.2f, 0.33f, 0.58f), coatLit = new Color(0.31f, 0.47f, 0.74f), coatDark = new Color(0.12f, 0.19f, 0.38f);
        Color pants = new Color(0.17f, 0.16f, 0.24f), boot = new Color(0.36f, 0.23f, 0.15f), bootDark = new Color(0.22f, 0.13f, 0.09f);
        Color skin = new Color(0.97f, 0.81f, 0.67f), skinDark = new Color(0.83f, 0.6f, 0.5f);
        Color hair = new Color(0.38f, 0.23f, 0.13f), hairLit = new Color(0.58f, 0.38f, 0.21f), hairDark = new Color(0.24f, 0.13f, 0.08f);
        Color shirt = new Color(0.9f, 0.88f, 0.82f), glove = new Color(0.45f, 0.29f, 0.18f);

        const float cx = 42f, ground = 2f;
        float hipY = ground + 27f - s.crouch, hipX = cx + s.lean * 0.35f;
        float shY = hipY + 29f, shX = cx + s.lean;                       // 어깨 높이의 몸 가운데
        Vector2 head = new Vector2(shX + 2f + s.headX, shY + 15f + s.headY);

        // 목도리 꼬리 (뒤로 흩날린다 — 앞으로 나갈수록 길게 눕는다)
        float flow = Mathf.Clamp01((s.lean + 2f) / 10f);
        Vector2 knot = new Vector2(shX - 8f, shY + 1f);
        Vector2 tail = knot + new Vector2(-14f - 8f * flow, -9f + 9f * flow);
        Vector2 mid = Vector2.Lerp(knot, tail, 0.5f) + new Vector2(0f, 3f - 2f * flow);
        c.Polygon(new[] { knot + new Vector2(0f, 3f), mid + new Vector2(0f, 3f), tail, mid + new Vector2(1f, -3f), knot + new Vector2(2f, -4f) }, BrassDark);
        c.Polygon(new[] { knot + new Vector2(0f, 3f), mid + new Vector2(0f, 3f), tail, mid + new Vector2(0f, 0f), knot + new Vector2(1f, 0f) }, Brass);

        // 뒷팔
        Vector2 backShoulder = new Vector2(shX - 7f, shY - 5f);
        Vector2 backHand = c.Ray(backShoulder, s.backArm, 17f, 6f, coatDark);
        c.Ellipse(backHand.x, backHand.y, 3f, 3f, glove);

        // 다리: 뒷다리 → 앞다리 (바지 + 목이 접힌 장화)
        Vector2 footBack = new Vector2(cx - 7f - s.spread, ground + 5f), footFront = new Vector2(cx + 6f + s.spread * 0.7f, ground + 5f);
        Leg(c, new Vector2(hipX - 4f, hipY), footBack, Mul(pants, 0.8f), bootDark, Mul(boot, 0.8f));
        Leg(c, new Vector2(hipX + 4f, hipY), footFront, pants, bootDark, boot);

        // 외투: 어깨 → 허리 → 아래로 퍼지는 자락 (앞이 갈라진다)
        c.Polygon(new[]
        {
            new Vector2(shX - 11f, shY), new Vector2(shX + 10f, shY), new Vector2(hipX + 9f, hipY + 9f),
            new Vector2(hipX + 12f, hipY - 8f), new Vector2(hipX + 3f, hipY - 7f), new Vector2(hipX + 1f, hipY + 2f),
            new Vector2(hipX - 3f, hipY - 9f), new Vector2(hipX - 13f, hipY - 7f), new Vector2(hipX - 9f, hipY + 9f),
        }, coat);
        c.Clip = true;
        c.Polygon(new[] { new Vector2(shX - 12f, shY), new Vector2(shX - 4f, shY), new Vector2(hipX - 6f, hipY - 9f), new Vector2(hipX - 14f, hipY - 8f) }, coatDark);   // 등 쪽 그늘
        c.Polygon(new[] { new Vector2(shX + 3f, shY), new Vector2(shX + 10f, shY), new Vector2(hipX + 10f, hipY + 10f), new Vector2(hipX + 6f, hipY + 10f) }, coatLit);   // 앞섶의 밝은 면
        c.Polygon(new[] { new Vector2(shX + 1f, shY + 1f), new Vector2(shX + 8f, shY + 1f), new Vector2(shX + 5f, shY - 11f) }, shirt);                                   // 셔츠 깃
        c.Rect(R(hipX - 10f), R(hipY + 8f), R(hipX + 10f), R(hipY + 11f), new Color(0.2f, 0.13f, 0.09f));                                                                  // 허리띠
        c.Rect(R(hipX + 2f), R(hipY + 7f), R(hipX + 6f), R(hipY + 12f), Brass);                                                                                            // 버클
        c.Rect(R(hipX + 3f), R(hipY + 9f), R(hipX + 5f), R(hipY + 10f), BrassDark);
        for (int i = 0; i < 3; i++) c.Rect(R(shX + 6f), R(shY - 14f - i * 5f), R(shX + 7f), R(shY - 13f - i * 5f), Brass);                                                // 단추
        c.Clip = false;

        // 목도리 (목을 감싼 띠)
        c.Ellipse(shX + 1f, shY + 2f, 11f, 4.5f, Brass);
        c.Clip = true;
        c.Ellipse(shX - 2f, shY, 11f, 2.5f, BrassDark);
        c.Clip = false;

        HeroHead(c, head, pose, skin, skinDark, hair, hairLit, hairDark);

        // 앞팔 + 칼
        Vector2 shoulder = new Vector2(shX + 6f, shY - 5f);
        Vector2 elbow = c.Ray(shoulder, s.arm, 10f, 7f, coat);
        Vector2 hand = c.Ray(elbow, s.fore, 10f, 6f, coatLit);
        c.Ellipse(shoulder.x, shoulder.y, 4.5f, 4.5f, coatLit);   // 어깨 견장
        c.Ellipse(shoulder.x, shoulder.y + 1f, 2.5f, 2f, Brass);
        Sword(c, hand, s.sword);
        c.Ellipse(hand.x, hand.y, 3.6f, 3.6f, glove);

        Finish(c);

        // 궤적 · 충격 표시는 테두리 뒤에 얹는다 (테두리가 둘러지지 않게)
        if (pose == Pose.Slash) Trail(c, hand + Dir(s.sword) * 20f + new Vector2(-10f, 4f), 34f, -62f, 48f);
        if (pose == Pose.Thrust) for (int i = 0; i < 3; i++) c.Line(hand.x - 4f, hand.y + 8f - i * 7f, hand.x + 26f - i * 4f, hand.y + 8f - i * 7f, 1f, new Color(1f, 1f, 1f, 0.55f));
        if (pose == Pose.Blunt) Trail(c, hand + new Vector2(2f, -6f), 30f, 40f, 150f);
        return c;
    }

    internal static void Leg(PixelCanvas c, Vector2 hip, Vector2 foot, Color pants, Color bootDark, Color boot)
    {
        Vector2 knee = Vector2.Lerp(hip, foot, 0.5f) + new Vector2(2f, 0f);
        c.Line(hip.x, hip.y, knee.x, knee.y, 8f, pants);
        c.Line(knee.x, knee.y, foot.x, foot.y + 6f, 7f, pants);
        // 장화: 발목 통 + 앞코
        c.Line(foot.x, foot.y + 9f, foot.x, foot.y, 8f, boot);
        c.Ellipse(foot.x + 4f, foot.y - 1f, 7f, 3.6f, boot);
        c.Rect(R(foot.x - 4f), R(foot.y + 9f), R(foot.x + 4f), R(foot.y + 10f), bootDark);   // 접힌 목
        c.Rect(R(foot.x - 4f), R(foot.y - 5f), R(foot.x + 10f), R(foot.y - 4f), bootDark);   // 밑창
    }

    internal static void HeroHead(PixelCanvas c, Vector2 h, Pose pose, Color skin, Color skinDark, Color hair, Color hairLit, Color hairDark)
    {
        // 뒷머리 (목덜미까지 내려온다)
        c.Ellipse(h.x - 6f, h.y - 2f, 13f, 14f, hairDark);
        c.Polygon(new[] { new Vector2(h.x - 17f, h.y + 2f), new Vector2(h.x - 21f, h.y - 12f), new Vector2(h.x - 12f, h.y - 9f) }, hairDark);

        // 얼굴 + 귀
        c.Ellipse(h.x + 1f, h.y - 1f, 14f, 14f, skin);
        c.Clip = true;
        c.Ellipse(h.x - 4f, h.y - 14f, 12f, 4f, skinDark);                      // 턱 아래 그늘
        c.Ellipse(h.x + 9f, h.y - 9f, 3f, 1.6f, new Color(1f, 0.62f, 0.56f));   // 볼
        c.Clip = false;
        c.Ellipse(h.x - 7f, h.y - 2f, 3f, 4f, skin);
        c.Ellipse(h.x - 7f, h.y - 2f, 1.4f, 2.2f, skinDark);

        // 눈 · 눈썹 · 입
        int ex = R(h.x + 7f), ey = R(h.y - 5f);
        if (pose == Pose.Hit)
        {
            c.Line(ex - 2f, ey + 3f, ex + 2f, ey + 0.5f, 1f, Ink);               // 질끈 감은 눈
            c.Line(ex - 2f, ey - 2f, ex + 2f, ey + 0.5f, 1f, Ink);
            c.Ellipse(h.x + 10f, h.y - 11f, 2.2f, 2.4f, new Color(0.45f, 0.16f, 0.16f));
        }
        else
        {
            c.Rect(ex - 2, ey - 2, ex + 2, ey + 4, Color.white);                       // 흰자
            c.Rect(ex - 1, ey - 2, ex + 2, ey + 4, new Color(0.13f, 0.1f, 0.16f));     // 눈동자
            c.Rect(ex - 1, ey - 2, ex + 2, ey - 1, new Color(0.3f, 0.48f, 0.74f));     // 눈동자 아래의 푸른 빛
            c.Rect(ex + 1, ey + 3, ex + 2, ey + 4, Color.white);                       // 눈빛
            c.Rect(ex - 3, ey + 5, ex + 3, ey + 5, hairDark);                          // 속눈썹 선
            bool fierce = pose != Pose.Idle && pose != Pose.Evade;
            c.Line(ex - 4f, ey + (fierce ? 9f : 8f), ex + 3f, ey + (fierce ? 6.5f : 8f), 1.5f, hairDark);
            if (fierce) c.Rect(R(h.x + 9f), R(h.y - 13f), R(h.x + 12f), R(h.y - 12f), new Color(0.5f, 0.2f, 0.2f));   // 악문 입
            else c.Rect(R(h.x + 10f), R(h.y - 12f), R(h.x + 12f), R(h.y - 12f), new Color(0.62f, 0.34f, 0.3f));
        }

        // 머리칼: 정수리 덩어리 + 이마로 쏟아지는 앞머리 가닥 + 뻗친 머리
        c.Ellipse(h.x - 2f, h.y + 6f, 15.5f, 10.5f, hair);
        c.Polygon(new[] { new Vector2(h.x + 2f, h.y + 9f), new Vector2(h.x + 15f, h.y + 8f), new Vector2(h.x + 15f, h.y + 4f), new Vector2(h.x + 12f, h.y + 7f), new Vector2(h.x + 9f, h.y + 3f), new Vector2(h.x + 5f, h.y + 7f), new Vector2(h.x + 1f, h.y + 1f) }, hair);
        c.Polygon(new[] { new Vector2(h.x - 16f, h.y + 6f), new Vector2(h.x - 14f, h.y - 6f), new Vector2(h.x - 8f, h.y + 3f) }, hair);     // 옆머리
        c.Polygon(new[] { new Vector2(h.x - 6f, h.y + 14f), new Vector2(h.x - 1f, h.y + 22f), new Vector2(h.x + 3f, h.y + 14f) }, hair);    // 뻗친 머리
        c.Polygon(new[] { new Vector2(h.x - 13f, h.y + 10f), new Vector2(h.x - 15f, h.y + 17f), new Vector2(h.x - 7f, h.y + 14f) }, hair);
        c.Clip = true;
        c.Ellipse(h.x + 2f, h.y + 12f, 9f, 3f, hairLit);                          // 정수리의 윤기
        c.Line(h.x + 8f, h.y + 9f, h.x + 11f, h.y + 6f, 1f, hairLit);
        c.Line(h.x + 3f, h.y + 9f, h.x + 4f, h.y + 5f, 1f, hairLit);
        c.Clip = false;
    }

    /// <summary>시곗바늘 칼: 황동 자루 · 십자 손막이 · 고리 장식 · 끝으로 갈수록 가늘어지는 날</summary>
    internal static void Sword(PixelCanvas c, Vector2 hand, float angle)
    {
        Vector2 d = Dir(angle), n = new Vector2(-d.y, d.x);
        Vector2 pommel = hand - d * 6f, guard = hand + d * 4f, tip = guard + d * 40f;

        c.Line(pommel.x, pommel.y, guard.x, guard.y, 3f, BrassDark);
        c.Ellipse(pommel.x, pommel.y, 2.6f, 2.6f, Brass);
        c.Polygon(new[] { guard + n * 3.2f, guard + d * 26f + n * 2.2f, tip, guard + d * 26f - n * 2.2f, guard - n * 3.2f }, Steel);   // 날
        c.Clip = true;
        c.Polygon(new[] { guard - n * 3.4f, guard + d * 26f - n * 2.4f, tip, guard + d * 26f - n * 0.3f, guard - n * 0.4f }, SteelDark);   // 아래쪽 면
        c.Line(guard.x + n.x * 1.2f, guard.y + n.y * 1.2f, tip.x, tip.y, 1f, Color.white);                                                // 날의 빛줄
        c.Clip = false;
        c.Line(guard.x - n.x * 6f, guard.y - n.y * 6f, guard.x + n.x * 6f, guard.y + n.y * 6f, 3.4f, Brass);                              // 손막이
        Vector2 ringAt = guard + d * 8f;
        c.Ellipse(ringAt.x, ringAt.y, 3.6f, 3.6f, Brass);                                                                                 // 시곗바늘의 고리
        c.Ellipse(ringAt.x, ringAt.y, 1.6f, 1.6f, new Color(0.18f, 0.2f, 0.28f));
    }

    /// <summary>휘두른 궤적 (바깥은 옅고 안쪽 심은 밝다)</summary>
    internal static void Trail(PixelCanvas c, Vector2 center, float radius, float a0, float a1)
    {
        c.Arc(center.x, center.y, radius, 5f, a0, a1, new Color(0.75f, 0.88f, 1f, 0.3f));
        c.Arc(center.x, center.y, radius + 1f, 2f, a0 + 8f, a1 - 4f, new Color(1f, 1f, 1f, 0.8f));
    }

    // ─────────────────────────── 쥐 ───────────────────────────

    /// <summary>thin = 굶주린 쥐 (마르고 갈비뼈가 드러난다)</summary>
    public static PixelCanvas Rat(Color tint, Pose pose, bool thin)
    {
        PixelCanvas c = new PixelCanvas(W, H);
        // 적마다의 색(tint)은 살리되 털빛으로 가라앉힌다 — 원색 그대로면 장난감처럼 보인다
        bool reddish = tint.r > tint.g * 1.5f && tint.r > tint.b * 1.5f;
        tint = Color.Lerp(tint, new Color(0.46f, 0.41f, 0.39f), 0.42f);
        tint.a = 1f;
        Color eye = reddish ? new Color(1f, 0.82f, 0.2f) : new Color(1f, 0.2f, 0.16f);   // 붉은 털에는 노란 눈 (묻히지 않게)
        Color fur = tint, furDark = Cool(tint, 0.62f), furLit = Color.Lerp(tint, new Color(1f, 0.95f, 0.85f), 0.22f);
        Color belly = Color.Lerp(tint, new Color(0.88f, 0.8f, 0.7f), 0.5f);
        Color flesh = new Color(0.86f, 0.56f, 0.56f), fleshDark = new Color(0.62f, 0.36f, 0.4f);
        furLit.a = belly.a = 1f;

        float dx = pose == Pose.Thrust ? 9f : pose == Pose.Evade ? -8f : pose == Pose.Hit ? -6f : pose == Pose.Slash ? 3f : 0f;
        float dy = pose == Pose.Blunt ? 9f : pose == Pose.Guard ? -2f : 0f;
        float rx = thin ? 21f : 24f, ry = thin ? 15f : 19f;
        float cx = 44f + dx, cy = 8f + ry + dy;

        // 꼬리: 엉덩이에서 왼쪽으로 뻗어 끝이 말려 올라간다 (마디마다 어두운 줄)
        for (int i = 0; i <= 44; i++)
        {
            float t = i / 44f;
            float tx = cx - rx + 4f - Mathf.Sin(t * 1.9f) * 15f, ty = cy - ry * 0.55f - 3f * Mathf.Sin(t * 2.6f) + t * t * 32f + (pose == Pose.Hit ? t * 4f : 0f);
            float r = Mathf.Lerp(3.2f, 1.1f, t);
            c.Ellipse(tx, ty, r, r, i % 5 == 4 ? fleshDark : flesh);
        }

        // 뒷발 (길쭉한 맨발 + 발톱)
        Foot(c, cx - 8f, 4f + dy, flesh, fleshDark);
        Foot(c, cx + 7f, 4f + dy, flesh, fleshDark);

        // 등의 곤두선 털 (몸 뒤에 깔린다)
        for (int i = 0; i < 7; i++)
        {
            float a = Mathf.Lerp(165f, 60f, i / 6f);
            Vector2 on = new Vector2(cx, cy) + new Vector2(Mathf.Cos(a * Mathf.Deg2Rad) * rx, Mathf.Sin(a * Mathf.Deg2Rad) * ry) * 0.94f;
            Vector2 tipDir = Dir(a + 28f);
            float len = (i % 2 == 0 ? 7f : 5f) + (pose == Pose.Hit || pose == Pose.Guard ? 2f : 0f);
            c.Polygon(new[] { on + Dir(a + 90f) * 3.5f, on + tipDir * len, on - Dir(a + 90f) * 3.5f }, furDark);
        }

        // 몸통 (등이 굽은 타원) + 허벅지 + 배
        c.Ellipse(cx, cy, rx, ry, fur);
        c.Clip = true;
        c.Ellipse(cx - 4f, cy + ry * 0.75f, rx * 0.8f, ry * 0.4f, furLit);                   // 등의 밝은 면
        c.Ellipse(cx + 8f, cy - ry * 0.55f, rx * 0.62f, ry * 0.5f, belly);                    // 배
        c.Ellipse(cx - 10f, cy - ry * 0.35f, rx * 0.5f, ry * 0.62f, Color.Lerp(fur, furDark, 0.45f));   // 허벅지
        c.Arc(cx - 10f, cy - ry * 0.35f, rx * 0.5f, 1.2f, 20f, 170f, furDark);
        System.Random rng = new System.Random(thin ? 7 : 3);
        for (int i = 0; i < 9; i++)                                                           // 헝클어진 털 결
        {
            float fx = cx - rx * 0.7f + (float)rng.NextDouble() * rx * 1.2f, fy = cy - ry * 0.1f + (float)rng.NextDouble() * ry * 0.8f;
            c.Line(fx, fy, fx - 3f, fy - 2f, 1f, furDark);
        }
        if (thin) for (int i = 0; i < 4; i++) c.Arc(cx + 2f + i * 5f, cy - 2f, 7f, 1.2f, 200f, 290f, furDark);   // 갈비뼈
        c.Clip = false;

        // 뒤쪽 귀 → 머리 → 앞쪽 귀
        float hx = cx + rx * 0.82f, hy = cy + ry * 0.55f + (pose == Pose.Thrust ? -6f : pose == Pose.Evade ? 3f : pose == Pose.Guard ? -3f : 0f);
        Ear(c, hx - 2f, hy + 11f, 6.5f, furDark, fleshDark);
        c.Ellipse(hx, hy, 12.5f, 10.5f, fur);

        // 주둥이: 코끝으로 좁아지는 원뿔 (물 때는 위턱을 들고 아래턱을 벌린다)
        bool bite = pose == Pose.Thrust || pose == Pose.Slash || pose == Pose.Blunt;
        float jaw = bite ? 5f : 0f;
        Vector2 nose = new Vector2(hx + 24f, hy - 2f + jaw * 0.6f);
        c.Polygon(new[] { new Vector2(hx + 4f, hy + 8f), nose + new Vector2(0f, 2.5f), nose + new Vector2(0f, -2f), new Vector2(hx + 6f, hy - 7f) }, fur);
        if (bite)
        {
            c.Polygon(new[] { new Vector2(hx + 3f, hy - 6f), new Vector2(hx + 19f, hy - 12f), new Vector2(hx + 17f, hy - 15f), new Vector2(hx + 2f, hy - 11f) }, furDark);   // 벌린 아래턱
            c.Polygon(new[] { new Vector2(hx + 7f, hy - 6f), new Vector2(hx + 17f, hy - 10f), new Vector2(hx + 8f, hy - 9f) }, new Color(0.45f, 0.1f, 0.14f));              // 입 안
            c.Rect(R(hx + 15f), R(hy - 12f), R(hx + 16f), R(hy - 9f), Color.white);                                                                                          // 아랫니
        }
        c.Rect(R(nose.x - 5f), R(nose.y - 6f), R(nose.x - 4f), R(nose.y - 2f), Color.white);    // 앞니 두 개
        c.Rect(R(nose.x - 8f), R(nose.y - 5f), R(nose.x - 7f), R(nose.y - 2f), new Color(0.92f, 0.9f, 0.8f));
        c.Ellipse(nose.x, nose.y + 0.5f, 2.8f, 2.6f, flesh);
        c.Set(R(nose.x), R(nose.y + 1.5f), Color.white);

        c.Clip = true;
        c.Ellipse(hx - 3f, hy + 7f, 9f, 3.5f, furLit);                                          // 정수리의 밝은 면
        c.Ellipse(hx + 6f, hy - 7f, 9f, 4f, belly);                                             // 턱 밑
        c.Clip = false;

        Ear(c, hx - 7f, hy + 9f, 8.5f, fur, flesh);

        // 눈: 어두운 눈두덩 속에서 타는 붉은 눈
        if (pose == Pose.Hit)
        {
            c.Line(hx + 5f, hy + 5f, hx + 10f, hy + 1f, 1.5f, Ink);
            c.Line(hx + 5f, hy + 1f, hx + 10f, hy + 5f, 1.5f, Ink);
        }
        else
        {
            c.Ellipse(hx + 7.5f, hy + 3f, 4.2f, 3.8f, Cool(tint, 0.35f));
            c.Ellipse(hx + 8f, hy + 3f, 2.8f, 2.6f, eye);
            c.Ellipse(hx + 8.5f, hy + 3.5f, 1.2f, 1.2f, Color.Lerp(eye, Color.white, 0.6f));
            c.Line(hx + 3f, hy + 8f, hx + 11f, hy + 6f, 1.5f, furDark);                         // 찌푸린 눈썹 털
        }

        // 수염

        // 앞발: 뒤쪽 → 앞쪽 (포즈마다 각도)
        float a0 = pose == Pose.Slash ? 55f : pose == Pose.Blunt ? 85f : pose == Pose.Guard ? 70f : pose == Pose.Thrust ? -10f : pose == Pose.Hit ? -120f : -55f;
        bool claws = pose == Pose.Slash || pose == Pose.Blunt || pose == Pose.Thrust;
        Paw(c, new Vector2(cx + rx * 0.45f, cy - ry * 0.1f), a0 - 25f, furDark, fleshDark, claws);
        Vector2 paw = Paw(c, new Vector2(cx + rx * 0.62f, cy - ry * 0.25f), a0, Color.Lerp(fur, furDark, 0.3f), flesh, claws);

        Finish(c);

        // 수염 (테두리 뒤에 얹는 가는 선)
        Color whisker = new Color(1f, 1f, 1f, 0.55f);
        for (int i = -1; i <= 1; i++) c.Line(nose.x - 6f, nose.y - 2f, nose.x - 16f, nose.y - 8f + i * 4f, 1f, whisker);

        if (pose == Pose.Slash) Trail(c, paw + new Vector2(-12f, -4f), 24f, -50f, 65f);
        if (pose == Pose.Blunt) Trail(c, paw + new Vector2(-4f, -14f), 22f, 20f, 140f);
        return c;
    }

    internal static void Foot(PixelCanvas c, float x, float y, Color flesh, Color fleshDark)
    {
        c.Ellipse(x + 3f, y, 8.5f, 3.4f, flesh);
        c.Ellipse(x - 2f, y + 3f, 4.5f, 4.5f, fleshDark);
        for (int i = 0; i < 3; i++) c.Rect(R(x + 10f), R(y - 2f + i * 2f), R(x + 12f), R(y - 2f + i * 2f), new Color(0.95f, 0.93f, 0.85f));
    }

    internal static void Ear(PixelCanvas c, float x, float y, float r, Color outer, Color inner)
    {
        c.Ellipse(x, y, r, r * 1.05f, outer);
        c.Ellipse(x + 1f, y - 1f, r * 0.5f, r * 0.6f, Color.Lerp(inner, outer, 0.25f));
        c.EraseEllipse(x - r * 0.9f, y + r * 0.75f, r * 0.3f, r * 0.3f);   // 뜯긴 자국
    }

    internal static Vector2 Paw(PixelCanvas c, Vector2 shoulder, float angle, Color fur, Color flesh, bool claws)
    {
        Vector2 elbow = c.Ray(shoulder, angle - 25f, 7f, 5.5f, fur);
        Vector2 hand = c.Ray(elbow, angle + 12f, 8f, 4.5f, fur);
        c.Ellipse(hand.x, hand.y, 3.2f, 3.2f, flesh);
        if (claws)
        {
            for (int i = -1; i <= 1; i++)
            {
                Vector2 from = hand + Dir(angle + 12f + i * 32f) * 2.5f, tip = hand + Dir(angle + 12f + i * 32f) * 7f;
                c.Line(from.x, from.y, tip.x, tip.y, 1f, new Color(0.97f, 0.95f, 0.88f));
            }
        }
        return hand;
    }

    // ─────────────────────────── 도우미 ───────────────────────────

    internal static int R(float v) { return Mathf.RoundToInt(v); }

    internal static Vector2 Dir(float deg) { return new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad)); }

    internal static Color Mul(Color c, float f) { return new Color(c.r * f, c.g * f, c.b * f, 1f); }

    /// <summary>어두운 색은 그냥 검게 죽이지 않고 푸른 쪽으로 식힌다</summary>
    internal static Color Cool(Color c, float f) { return new Color(c.r * f * 0.92f, c.g * f * 0.95f, c.b * f * 1.12f, 1f); }
}
