using UnityEngine;
using static SpritePainter;
using Pose = SpritePainter.Pose;

/// <summary>
/// 적 캐릭터 그림 — 이름을 보고 생김새를 골라 큰 도화지(96x128)에 그린다 (Illustrator가 부른다).
///   사람형 : 태엽 병정 · 경비 인형 / 태엽 인형 · 틱 / 시계공 / 종지기 / 수문장 / 그 밖의 사람
///   괴물형 : 박쥐 / 벌레 / 게 / 망령 / 뻐꾸기 시계 / 거머리 / 종
/// 쥐와 주인공은 SpritePainter. 모든 그림은 오른쪽을 보고 발은 아래쪽 가운데.
/// </summary>
public static class EnemyPainter
{
    /// <summary>이름으로 생김새를 고른다 (순서 중요: '박쥐'가 '쥐'보다, '종지기' · '인형'이 '종'보다 먼저)</summary>
    public static PixelCanvas Draw(string name, Color tint, Pose pose)
    {
        name = name ?? "";
        tint.a = 1f;
        PixelCanvas c;

        if (name.Contains("박쥐")) c = Bat(tint, pose);
        else if (name.Contains("쥐")) c = Rat(tint, pose, name.Contains("굶주린"));
        else if (name.Contains("벌레")) c = Bug(tint, pose);
        else if (name.EndsWith("게")) c = Crab(tint, pose);
        else if (name.Contains("망령")) c = Wraith(tint, pose);
        else if (name.Contains("뻐꾸기")) c = Cuckoo(tint, pose);
        else if (name.Contains("수문장")) c = Figure(pose, Warden(tint));
        else if (name.Contains("거머리")) c = Leech(tint, pose);
        else if (name.Contains("종지기")) c = Figure(pose, BellRinger(tint));
        else if (name.Contains("경비") || name.Contains("병정")) c = Figure(pose, Soldier(tint, name.Contains("석궁")));
        else if (name.Contains("인형") || name == "틱") c = Figure(pose, Doll(name == "틱" ? new Color(0.8f, 0.62f, 0.3f) : tint, name == "틱"));   // '자명종 인형'은 종이 아니라 인형
        else if (name.Contains("종")) c = Bell(tint, pose);
        else if (name.Contains("시계공")) c = Figure(pose, Clockmaker(tint));
        else if (name.Contains("이안"))
        {
            c = Hero(pose);
            if (name != "이안") Wash(c, tint, 0.4f);   // 다른 모습의 이안 (가라앉은 이안 등)은 제 색으로 물든다
        }
        else c = Figure(pose, Stranger(tint, name));

        if (pose == Pose.Hit) Wash(c, HitWash, 0.35f);
        return c;
    }

    // ─────────────────────────── 사람형 ───────────────────────────

    private enum HeadKind { Shako, GreatHelm, Hood, Goggles, Doll }
    private enum Arm { None, Spear, Hammer, Wrench, Crossbow, Blade }

    private struct Look
    {
        public Color cloth, legs, boot, skin, hair, accent, metal;
        public HeadKind head;
        public Arm weapon;
        public bool key, skirt, robe, apron, armor, boy;
        public float bulk, headR, legLen, torso;
    }

    private static Look Base(Color cloth)
    {
        return new Look
        {
            cloth = cloth, legs = new Color(0.2f, 0.19f, 0.26f), boot = new Color(0.2f, 0.14f, 0.12f), skin = new Color(0.95f, 0.8f, 0.66f),
            hair = new Color(0.3f, 0.2f, 0.14f), accent = Brass, metal = Steel, bulk = 1f, headR = 14f, legLen = 27f, torso = 29f,
        };
    }

    private static Look Soldier(Color tint, bool crossbow)
    {
        Look l = Base(tint);
        l.legs = new Color(0.86f, 0.84f, 0.78f);
        l.skin = new Color(0.95f, 0.82f, 0.68f);
        l.head = HeadKind.Shako;
        l.weapon = crossbow ? Arm.Crossbow : Arm.Spear;
        l.key = true;
        return l;
    }

    private static Look Doll(Color tint, bool tick)
    {
        Look l = Base(tint);
        l.legs = new Color(0.9f, 0.88f, 0.84f);
        l.skin = tick ? new Color(0.96f, 0.84f, 0.6f) : new Color(0.95f, 0.92f, 0.88f);
        l.hair = Cool(tint, 0.42f);
        l.head = HeadKind.Doll;
        l.weapon = Arm.None;
        l.key = true;
        l.skirt = !tick;
        l.boy = tick;
        l.headR = 16f; l.legLen = 19f; l.torso = 22f;
        return l;
    }

    private static Look Clockmaker(Color tint)
    {
        Look l = Base(new Color(0.4f, 0.29f, 0.2f));
        l.accent = Color.Lerp(tint, Brass, 0.4f);
        l.hair = new Color(0.82f, 0.82f, 0.8f);
        l.skin = new Color(0.92f, 0.76f, 0.62f);
        l.head = HeadKind.Goggles;
        l.weapon = Arm.Wrench;
        l.apron = true;
        return l;
    }

    private static Look BellRinger(Color tint)
    {
        Look l = Base(Mul(tint, 0.8f));
        l.head = HeadKind.Hood;
        l.weapon = Arm.Hammer;
        l.robe = true;
        l.bulk = 1.15f; l.torso = 31f;
        return l;
    }

    private static Look Warden(Color tint)
    {
        Look l = Base(Color.Lerp(tint, Steel, 0.25f));
        l.legs = Cool(tint, 0.7f);
        l.boot = Cool(tint, 0.5f);
        l.accent = new Color(0.78f, 0.42f, 0.2f);
        l.metal = Color.Lerp(tint, Steel, 0.5f);
        l.head = HeadKind.GreatHelm;
        l.weapon = Arm.Hammer;
        l.armor = true;
        l.bulk = 1.45f; l.headR = 12f; l.legLen = 23f; l.torso = 33f;
        return l;
    }

    private static Look Stranger(Color tint, string name)
    {
        int h = 17;
        foreach (char ch in name) h = h * 31 + ch;
        Look l = Base(tint);
        l.head = HeadKind.Hood;
        l.weapon = (h & 1) == 0 ? Arm.Blade : Arm.Hammer;
        return l;
    }

    private static PixelCanvas Figure(Pose pose, Look l)
    {
        PixelCanvas c = new PixelCanvas(W, H);
        Stance s = HeroStance(pose);
        Color cloth = l.cloth, clothLit = Color.Lerp(cloth, new Color(1f, 0.96f, 0.86f), 0.22f), clothDark = Cool(cloth, 0.6f);
        clothLit.a = 1f;
        Color belt = Cool(cloth, 0.35f), glove = l.armor ? l.metal : Cool(l.accent, 0.6f);

        float cx = l.bulk > 1.3f ? 40f : 42f;
        const float ground = 2f;
        float half = 10.5f * l.bulk;
        float hipY = ground + l.legLen - s.crouch, hipX = cx + s.lean * 0.35f;
        float shY = hipY + l.torso, shX = cx + s.lean;
        Vector2 head = new Vector2(shX + 2f + s.headX, shY + l.headR + 1f + s.headY);

        // 뒷팔
        Vector2 backHand = c.Ray(new Vector2(shX - half * 0.65f, shY - 5f), s.backArm, 17f, 6f * Mathf.Sqrt(l.bulk), clothDark);
        c.Ellipse(backHand.x, backHand.y, 3f * l.bulk, 3f * l.bulk, Cool(glove, 0.8f));

        // 등의 태엽 열쇠 (자루 + 고리 두 개)
        if (l.key)
        {
            float kx = shX - half - 1f, ky = shY - 11f;
            c.Rect(R(kx - 7f), R(ky - 1f), R(kx + 1f), R(ky + 1f), BrassDark);
            for (int i = -1; i <= 1; i += 2)
            {
                c.Ellipse(kx - 10f, ky + i * 5f, 4.6f, 4.6f, Brass);
                c.EraseEllipse(kx - 10f, ky + i * 5f, 2f, 2f);
            }
        }

        // 다리 (긴 옷이면 발끝만 보인다)
        Vector2 footBack = new Vector2(cx - 7f * l.bulk - s.spread, ground + 5f), footFront = new Vector2(cx + 6f * l.bulk + s.spread * 0.7f, ground + 5f);
        if (l.robe)
        {
            c.Ellipse(footBack.x + 4f, ground + 2f, 7f, 3f, Mul(l.boot, 0.8f));
            c.Ellipse(footFront.x + 3f, ground + 2f, 7f, 3f, l.boot);
        }
        else
        {
            Leg(c, new Vector2(hipX - 4f * l.bulk, hipY), footBack, Mul(l.legs, 0.8f), Mul(l.boot, 0.6f), Mul(l.boot, 0.8f));
            Leg(c, new Vector2(hipX + 4f * l.bulk, hipY), footFront, l.legs, Mul(l.boot, 0.6f), l.boot);
        }

        // 몸통: 어깨 → 허리 → 옷자락 (치마는 종 모양으로 퍼지고, 긴 옷은 발목까지 내려온다)
        float hemY = l.robe ? ground + 4f : l.skirt ? hipY - 11f : hipY - 4f;
        float hemHalf = half + (l.robe ? 6f : l.skirt ? 9f : 2f);
        float hemX = l.robe ? cx : hipX;
        c.Polygon(new[]
        {
            new Vector2(shX - half, shY), new Vector2(shX + half - 1f, shY), new Vector2(hipX + half - 1.5f, hipY + 9f),
            new Vector2(hemX + hemHalf, hemY), new Vector2(hemX - hemHalf, hemY), new Vector2(hipX - half + 1.5f, hipY + 9f),
        }, cloth);
        c.Clip = true;
        c.Polygon(new[] { new Vector2(shX - half - 1f, shY), new Vector2(shX - half * 0.35f, shY), new Vector2(hemX - hemHalf * 0.4f, hemY), new Vector2(hemX - hemHalf - 1f, hemY) }, clothDark);      // 등 쪽 그늘
        c.Polygon(new[] { new Vector2(shX + half * 0.35f, shY), new Vector2(shX + half, shY), new Vector2(hemX + hemHalf, hemY), new Vector2(hemX + hemHalf * 0.55f, hemY) }, clothLit);               // 앞쪽 밝은 면
        if (l.skirt || l.robe) c.Rect(0, R(hemY), W, R(hemY + 2f), l.accent);                                                                                                                         // 옷단의 띠
        if (l.robe) for (int i = -2; i <= 2; i++) c.Line(hemX + i * hemHalf * 0.4f, hemY + 3f, shX + i * half * 0.3f, hipY + 8f, 1f, clothDark);                                                      // 주름
        if (l.apron)
        {
            c.Polygon(new[] { new Vector2(shX - 1f, shY - 7f), new Vector2(shX + half, shY - 7f), new Vector2(hipX + half + 1f, hipY - 3f), new Vector2(hipX - 2f, hipY - 3f) }, new Color(0.72f, 0.64f, 0.5f));
            c.Rect(R(hipX + 2f), R(hipY + 1f), R(hipX + 8f), R(hipY + 6f), new Color(0.58f, 0.5f, 0.38f));   // 주머니
            c.Line(hipX + 4f, hipY + 6f, hipX + 4f, hipY + 10f, 1f, Steel);                                   // 꽂아 둔 연장
        }
        if (l.armor)
        {
            c.Ellipse(shX + 2f, shY - 9f, half * 0.85f, 9f, l.metal);                                          // 가슴판
            c.Rect(0, R(shY - 19f), W, R(shY - 18f), Cool(l.metal, 0.5f));
            for (int i = -1; i <= 1; i++) c.Rect(R(shX + 2f + i * 7f), R(shY - 22f), R(shX + 3f + i * 7f), R(shY - 21f), l.accent);   // 리벳
            c.Rect(0, R(hipY - 1f), W, R(hipY), Cool(l.metal, 0.5f));                                          // 허리 아래 갑옷 자락의 이음
        }
        c.Rect(R(hipX - half - 4f), R(hipY + 8f), R(hipX + half + 4f), R(hipY + 11f), belt);                  // 허리띠
        c.Rect(R(hipX + 2f), R(hipY + 7f), R(hipX + 6f), R(hipY + 12f), l.accent);                             // 버클
        if (!l.armor && !l.apron) for (int i = 0; i < 2; i++) c.Rect(R(shX + half * 0.55f), R(shY - 9f - i * 6f), R(shX + half * 0.55f + 1f), R(shY - 8f - i * 6f), l.accent);   // 단추
        c.Clip = false;

        c.Ellipse(shX + 1f, shY + 1f, half * 0.95f, 3.6f, l.accent);                                           // 깃
        Head(c, head, pose, l);

        // 앞팔 + 무기
        Vector2 shoulder = new Vector2(shX + half * 0.55f, shY - 5f);
        float thick = 7f * Mathf.Sqrt(l.bulk);
        Vector2 elbow = c.Ray(shoulder, s.arm, 10f, thick, cloth);
        Vector2 hand = c.Ray(elbow, s.fore, 10f, thick - 1f, clothLit);
        if (l.armor)
        {
            c.Ellipse(shoulder.x - 1f, shoulder.y + 2f, 8f, 7f, l.metal);                                      // 큰 견갑
            c.Clip = true;
            c.Arc(shoulder.x - 1f, shoulder.y + 2f, 7f, 1.6f, 200f, 340f, l.accent);
            c.Clip = false;
        }
        else
        {
            c.Ellipse(shoulder.x, shoulder.y, 4.5f, 4.5f, clothLit);
            c.Ellipse(shoulder.x, shoulder.y + 1f, 2.4f, 2f, l.accent);
        }
        Weapon(c, l, hand, s.sword);
        c.Ellipse(hand.x, hand.y, 3.6f * Mathf.Sqrt(l.bulk), 3.6f * Mathf.Sqrt(l.bulk), glove);

        Finish(c);

        if (l.weapon != Arm.None && l.weapon != Arm.Crossbow)
        {
            if (pose == Pose.Slash) Trail(c, hand + Dir(s.sword) * 20f + new Vector2(-10f, 4f), 34f, -62f, 48f);
            if (pose == Pose.Blunt) Trail(c, hand + new Vector2(2f, -6f), 30f, 40f, 150f);
        }
        if (pose == Pose.Thrust) for (int i = 0; i < 3; i++) c.Line(hand.x - 4f, hand.y + 8f - i * 7f, hand.x + 26f - i * 4f, hand.y + 8f - i * 7f, 1f, new Color(1f, 1f, 1f, 0.55f));
        return c;
    }

    private static void Head(PixelCanvas c, Vector2 h, Pose pose, Look l)
    {
        float r = l.headR;
        Color skinDark = new Color(l.skin.r * 0.84f, l.skin.g * 0.74f, l.skin.b * 0.74f, 1f);
        bool hit = pose == Pose.Hit;

        switch (l.head)
        {
            case HeadKind.Shako:   // 장난감 병정: 높은 모자 + 까만 눈 + 붉은 볼
                c.Ellipse(h.x + 1f, h.y - 2f, r - 1f, r - 1f, l.skin);
                c.Clip = true;
                c.Ellipse(h.x - 4f, h.y - r, r, 4f, skinDark);
                c.Clip = false;
                Eye(c, h.x + 7f, h.y - 4f, hit, false);
                c.Ellipse(h.x + 10f, h.y - 9f, 2.6f, 2.2f, new Color(0.92f, 0.36f, 0.32f));                    // 볼
                c.Rect(R(h.x + 6f), R(h.y - 11f), R(h.x + 10f), R(h.y - 11f), Ink);                            // 일자 입
                c.Rect(R(h.x + 4f), R(h.y - 9f), R(h.x + 12f), R(h.y - 8f), Cool(l.hair, 0.8f));               // 콧수염
                // 모자: 통 + 띠 + 앞 장식 + 챙 + 턱끈
                c.Polygon(new[] { new Vector2(h.x - r + 1f, h.y + 3f), new Vector2(h.x + r, h.y + 3f), new Vector2(h.x + r - 1f, h.y + r + 12f), new Vector2(h.x - r + 2f, h.y + r + 12f) }, Cool(l.cloth, 0.45f));
                c.Rect(R(h.x - r + 1f), R(h.y + 3f), R(h.x + r), R(h.y + 6f), l.accent);
                c.Ellipse(h.x + 6f, h.y + 14f, 3.6f, 4.6f, l.accent);
                c.Ellipse(h.x + 6f, h.y + 14f, 1.6f, 2.2f, l.cloth);
                c.Polygon(new[] { new Vector2(h.x + 4f, h.y + 3f), new Vector2(h.x + r + 6f, h.y + 1f), new Vector2(h.x + r + 5f, h.y + 3f), new Vector2(h.x + r, h.y + 4f) }, Ink);
                c.Polygon(new[] { new Vector2(h.x + 2f, h.y + r + 12f), new Vector2(h.x + 6f, h.y + r + 20f), new Vector2(h.x + 9f, h.y + r + 12f) }, new Color(0.85f, 0.25f, 0.22f));   // 깃털 장식
                break;

            case HeadKind.GreatHelm:   // 통짜 투구: 눈구멍 속의 불빛 + 숨구멍 + 볏
                c.Ellipse(h.x + 1f, h.y, r + 2f, r + 1f, l.metal);
                c.Rect(R(h.x - r), R(h.y - r - 1f), R(h.x + r + 2f), R(h.y - 3f), l.metal);                    // 턱까지 내려오는 통
                c.Clip = true;
                c.Rect(R(h.x - r - 2f), R(h.y - r - 2f), R(h.x - 3f), R(h.y + r + 2f), Cool(l.metal, 0.72f));  // 뒤쪽 그늘
                c.Rect(R(h.x + 1f), R(h.y), R(h.x + r + 3f), R(h.y + 3f), new Color(0.06f, 0.05f, 0.08f));     // 눈구멍
                c.Ellipse(h.x + 8f, h.y + 1.5f, 2.6f, 1.6f, hit ? new Color(0.5f, 0.2f, 0.1f) : new Color(1f, 0.62f, 0.2f));
                for (int i = 0; i < 3; i++) c.Rect(R(h.x + 5f + i * 3f), R(h.y - 9f), R(h.x + 5f + i * 3f), R(h.y - 5f), new Color(0.1f, 0.09f, 0.12f));   // 숨구멍
                c.Rect(R(h.x - r - 2f), R(h.y + 5f), R(h.x + r + 3f), R(h.y + 6f), l.accent);                  // 띠
                c.Clip = false;
                c.Polygon(new[] { new Vector2(h.x - 6f, h.y + r), new Vector2(h.x - 1f, h.y + r + 7f), new Vector2(h.x + 7f, h.y + r) }, l.accent);        // 볏
                break;

            case HeadKind.Hood:   // 깊은 두건: 어둠 속의 두 눈
                c.Polygon(new[] { new Vector2(h.x - r - 2f, h.y + 4f), new Vector2(h.x - r - 9f, h.y + r + 6f), new Vector2(h.x - 2f, h.y + r + 2f) }, Cool(l.cloth, 0.8f));   // 처진 두건 끝
                c.Ellipse(h.x - 1f, h.y + 1f, r + 3f, r + 3f, l.cloth);
                c.Clip = true;
                c.Ellipse(h.x - 8f, h.y - 2f, r, r + 2f, Cool(l.cloth, 0.7f));
                c.Clip = false;
                c.Ellipse(h.x + 5f, h.y - 2f, r - 3f, r - 2f, new Color(0.07f, 0.06f, 0.1f));
                if (hit) { c.Line(h.x + 2f, h.y, h.x + 6f, h.y - 2f, 1.5f, new Color(1f, 0.85f, 0.4f)); c.Line(h.x + 8f, h.y - 2f, h.x + 12f, h.y, 1.5f, new Color(1f, 0.85f, 0.4f)); }
                else
                {
                    c.Ellipse(h.x + 3.5f, h.y - 1f, 2f, 2.6f, new Color(1f, 0.85f, 0.4f));
                    c.Ellipse(h.x + 10f, h.y - 1f, 2f, 2.6f, new Color(1f, 0.85f, 0.4f));
                }
                break;

            case HeadKind.Goggles:   // 시계공: 흰 머리 · 큰 고글 · 덥수룩한 수염
                c.Ellipse(h.x - 7f, h.y - 1f, 11f, 13f, Cool(l.hair, 0.75f));
                c.Ellipse(h.x + 1f, h.y - 1f, r, r, l.skin);
                c.Clip = true;
                c.Ellipse(h.x - 4f, h.y - r, r, 4f, skinDark);
                c.Clip = false;
                c.Ellipse(h.x - 2f, h.y + 8f, r, r - 6f, l.hair);                                              // 벗겨진 정수리를 덮은 머리
                c.Polygon(new[] { new Vector2(h.x - r, h.y + 6f), new Vector2(h.x - r - 4f, h.y + 14f), new Vector2(h.x - 6f, h.y + 12f) }, l.hair);
                c.Polygon(new[] { new Vector2(h.x + 3f, h.y - 6f), new Vector2(h.x + r + 2f, h.y - 7f), new Vector2(h.x + 11f, h.y - r - 6f), new Vector2(h.x + 3f, h.y - r - 1f) }, l.hair);   // 수염
                c.Rect(R(h.x + 6f), R(h.y - 6f), R(h.x + r + 3f), R(h.y - 5f), Cool(l.hair, 0.8f));            // 콧수염
                c.Rect(R(h.x - r), R(h.y), R(h.x + r), R(h.y + 2f), new Color(0.2f, 0.14f, 0.1f));             // 고글 끈
                c.Ellipse(h.x + 8f, h.y + 1f, 5.6f, 5.6f, l.accent);
                c.Ellipse(h.x + 8f, h.y + 1f, 3.6f, 3.6f, hit ? new Color(0.3f, 0.4f, 0.5f) : new Color(0.55f, 0.88f, 1f));
                c.Rect(R(h.x + 9f), R(h.y + 2f), R(h.x + 10f), R(h.y + 3f), Color.white);
                c.Ellipse(h.x + r + 1f, h.y - 3f, 2.2f, 2.2f, skinDark);                                       // 코
                break;

            default:   // 인형: 도자기 얼굴 · 단추 눈 · 단발 + 리본 (틱은 짧은 머리)
                c.Ellipse(h.x - 5f, h.y - 1f, r - 2f, r, Cool(l.hair, 0.8f));
                c.Ellipse(h.x + 1f, h.y - 1f, r - 1f, r - 1f, l.skin);
                c.Clip = true;
                c.Ellipse(h.x - 4f, h.y - r, r, 4f, skinDark);
                c.Ellipse(h.x + 10f, h.y - 9f, 3f, 2.2f, new Color(1f, 0.6f, 0.6f));                           // 볼
                c.Line(h.x + 4f, h.y - 8f, h.x + 2f, h.y - 13f, 1f, new Color(0.45f, 0.4f, 0.42f));            // 금 간 자국
                c.Clip = false;
                Eye(c, h.x + 7f, h.y - 4f, hit, true);
                c.Rect(R(h.x + 8f), R(h.y - 12f), R(h.x + 10f), R(h.y - 12f), new Color(0.75f, 0.25f, 0.28f)); // 입
                c.Ellipse(h.x - 2f, h.y + 7f, r, r - 5f, l.hair);                                              // 정수리
                c.Rect(R(h.x - 3f), R(h.y + 3f), R(h.x + r - 1f), R(h.y + 7f), l.hair);                        // 일자 앞머리
                if (l.boy) c.Polygon(new[] { new Vector2(h.x - 4f, h.y + r), new Vector2(h.x + 1f, h.y + r + 6f), new Vector2(h.x + 5f, h.y + r) }, l.hair);
                else
                {
                    c.Rect(R(h.x - r + 1f), R(h.y - r + 2f), R(h.x - 5f), R(h.y + 6f), l.hair);                // 턱까지 내려온 옆머리
                    Vector2 bow = new Vector2(h.x - 7f, h.y + r + 1f);
                    c.Polygon(new[] { bow, bow + new Vector2(-8f, 6f), bow + new Vector2(-8f, -4f) }, l.accent);
                    c.Polygon(new[] { bow, bow + new Vector2(8f, 6f), bow + new Vector2(8f, -3f) }, l.accent);
                    c.Ellipse(bow.x, bow.y, 2.4f, 2.4f, BrassDark);
                }
                c.Clip = true;
                c.Ellipse(h.x + 2f, h.y + r - 3f, 8f, 2.4f, Color.Lerp(l.hair, Color.white, 0.3f));            // 윤기
                c.Clip = false;
                break;
        }
    }

    /// <summary>까만 단추 눈 (button = 크고 동그랗게). 맞으면 X</summary>
    private static void Eye(PixelCanvas c, float x, float y, bool hit, bool button)
    {
        if (hit)
        {
            c.Line(x - 3f, y + 3f, x + 3f, y - 3f, 1.5f, Ink);
            c.Line(x - 3f, y - 3f, x + 3f, y + 3f, 1.5f, Ink);
            return;
        }
        c.Ellipse(x, y, button ? 3.4f : 2.4f, button ? 4f : 3.2f, new Color(0.08f, 0.07f, 0.1f));
        c.Rect(R(x), R(y + 1f), R(x + 1f), R(y + 2f), Color.white);
    }

    private static void Weapon(PixelCanvas c, Look l, Vector2 hand, float angle)
    {
        Vector2 d = Dir(angle), n = new Vector2(-d.y, d.x);
        Color wood = new Color(0.45f, 0.3f, 0.18f);

        switch (l.weapon)
        {
            case Arm.Blade:
                Sword(c, hand, angle);
                break;

            case Arm.Spear:
            {
                Vector2 butt = hand - d * 16f, neck = hand + d * 28f, tip = neck + d * 13f;
                c.Line(butt.x, butt.y, neck.x, neck.y, 2.6f, wood);
                c.Polygon(new[] { neck + n * 4f, tip, neck - n * 4f, neck - d * 3f }, Steel);
                c.Clip = true;
                c.Polygon(new[] { neck - n * 4f, tip, neck - d * 3f }, SteelDark);
                c.Clip = false;
                c.Line(neck.x - d.x * 4f - n.x * 3f, neck.y - d.y * 4f - n.y * 3f, neck.x - d.x * 4f + n.x * 3f, neck.y - d.y * 4f + n.y * 3f, 2.4f, l.accent);
                break;
            }
            case Arm.Hammer:
            {
                Vector2 butt = hand - d * 7f, top = hand + d * 27f;
                c.Line(butt.x, butt.y, top.x, top.y, 3f, wood);
                Color iron = l.armor ? Cool(l.metal, 0.75f) : BrassDark;
                c.Polygon(new[] { top + n * 10f - d * 6f, top + n * 10f + d * 6f, top - n * 10f + d * 6f, top - n * 10f - d * 6f }, iron);
                c.Clip = true;
                c.Polygon(new[] { top + n * 10f + d * 2f, top + n * 10f + d * 6f, top - n * 10f + d * 6f, top - n * 10f + d * 2f }, Color.Lerp(iron, Color.white, 0.3f));
                c.Line(top.x + n.x * 6f - d.x * 6f, top.y + n.y * 6f - d.y * 6f, top.x + n.x * 6f + d.x * 6f, top.y + n.y * 6f + d.y * 6f, 1.5f, l.accent);
                c.Line(top.x - n.x * 6f - d.x * 6f, top.y - n.y * 6f - d.y * 6f, top.x - n.x * 6f + d.x * 6f, top.y - n.y * 6f + d.y * 6f, 1.5f, l.accent);
                c.Clip = false;
                break;
            }
            case Arm.Wrench:
            {
                Vector2 butt = hand - d * 5f, top = hand + d * 20f;
                c.Line(butt.x, butt.y, top.x, top.y, 3.6f, SteelDark);
                c.Line(butt.x + n.x, butt.y + n.y, top.x + n.x, top.y + n.y, 1f, Steel);
                c.Ellipse(top.x, top.y, 6.5f, 6.5f, Steel);
                c.EraseEllipse(top.x + d.x * 3.5f, top.y + d.y * 3.5f, 3f, 3f);   // 벌어진 입
                c.Ellipse(butt.x, butt.y, 3f, 3f, Steel);
                break;
            }
            case Arm.Crossbow:
            {
                Vector2 back = hand - d * 8f, front = hand + d * 18f;
                c.Line(back.x, back.y, front.x, front.y, 4f, wood);
                Vector2 up = front + n * 12f - d * 5f, down = front - n * 12f - d * 5f, nock = hand + d * 3f;
                c.Line(front.x, front.y, up.x, up.y, 2.4f, SteelDark);
                c.Line(front.x, front.y, down.x, down.y, 2.4f, SteelDark);
                c.Line(up.x, up.y, nock.x, nock.y, 1f, new Color(0.9f, 0.88f, 0.8f));
                c.Line(down.x, down.y, nock.x, nock.y, 1f, new Color(0.9f, 0.88f, 0.8f));
                c.Line(nock.x, nock.y, front.x + d.x * 9f, front.y + d.y * 9f, 1.6f, Steel);   // 장전된 화살
                break;
            }
        }
    }

    // ─────────────────────────── 박쥐 ───────────────────────────

    private static PixelCanvas Bat(Color tint, Pose pose)
    {
        PixelCanvas c = new PixelCanvas(W, H);
        Color fur = tint, furDark = Cool(tint, 0.6f), furLit = Color.Lerp(tint, new Color(1f, 0.95f, 0.9f), 0.25f);
        Color skinWing = Cool(Color.Lerp(tint, new Color(0.5f, 0.3f, 0.4f), 0.35f), 0.85f), bone = Cool(tint, 0.42f);
        furLit.a = 1f;

        float dx = pose == Pose.Thrust ? 6f : pose == Pose.Evade ? -6f : pose == Pose.Hit ? -4f : 0f;
        float dy = pose == Pose.Thrust ? -8f : pose == Pose.Blunt ? 6f : pose == Pose.Hit ? -6f : 0f;
        float flap = pose == Pose.Guard ? -26f : pose == Pose.Blunt ? 24f : pose == Pose.Slash ? 18f : pose == Pose.Thrust ? -8f : pose == Pose.Hit ? -14f : 8f;
        float cx = 48f + dx, cy = 62f + dy;

        // 날개: 뒤쪽(왼쪽)은 어둡게, 앞쪽(오른쪽)은 밝게. 팔뼈에서 손가락뼈 세 개가 부채처럼 퍼지고 그 사이에 막이 걸린다
        for (int side = -1; side <= 1; side += 2)
        {
            Color membrane = side < 0 ? Cool(skinWing, 0.75f) : skinWing;
            Vector2 shoulder = new Vector2(cx + side * 6f, cy + 8f);
            Vector2 wrist = shoulder + new Vector2(side * 16f, 12f + flap * 0.5f);
            Vector2 t1 = wrist + new Vector2(side * 18f, flap * 0.5f + 4f), t2 = wrist + new Vector2(side * 16f, -16f + flap * 0.3f), t3 = wrist + new Vector2(side * 5f, -26f + flap * 0.2f);
            Vector2 hip = new Vector2(cx + side * 5f, cy - 10f);
            Vector2 v1 = Vector2.Lerp(t1, t2, 0.5f) - new Vector2(side * 6f, -2f), v2 = Vector2.Lerp(t2, t3, 0.5f) - new Vector2(side * 4f, -5f), v3 = Vector2.Lerp(t3, hip, 0.5f) + new Vector2(0f, 5f);
            c.Polygon(new[] { shoulder, wrist, t1, v1, t2, v2, t3, v3, hip }, membrane);
            c.Line(shoulder.x, shoulder.y, wrist.x, wrist.y, 2.6f, bone);
            c.Line(wrist.x, wrist.y, t1.x, t1.y, 1.6f, bone);
            c.Line(wrist.x, wrist.y, t2.x, t2.y, 1.6f, bone);
            c.Line(wrist.x, wrist.y, t3.x, t3.y, 1.6f, bone);
            c.Ellipse(wrist.x, wrist.y + 2f, 2f, 2.6f, new Color(0.95f, 0.93f, 0.85f));   // 엄지 발톱
        }

        // 발 + 몸 + 가슴 털
        for (int side = -1; side <= 1; side += 2)
        {
            c.Line(cx + side * 4f, cy - 10f, cx + side * 5f, cy - 17f, 2f, bone);
            for (int i = -1; i <= 1; i++) c.Line(cx + side * 5f, cy - 17f, cx + side * 5f + i * 2.5f, cy - 21f, 1f, new Color(0.95f, 0.93f, 0.85f));
        }
        c.Ellipse(cx, cy, 10f, 13f, fur);
        c.Clip = true;
        c.Ellipse(cx + 2f, cy + 3f, 7f, 7f, furLit);
        c.Ellipse(cx - 6f, cy - 2f, 6f, 12f, furDark);
        for (int i = 0; i < 5; i++) c.Line(cx - 5f + i * 3f, cy - 4f, cx - 6f + i * 3f, cy - 9f, 1f, furDark);
        c.Clip = false;

        // 귀 → 머리 → 얼굴
        float hx = cx + 3f, hy = cy + 17f;
        for (int side = -1; side <= 1; side += 2)
        {
            Vector2 ear = new Vector2(hx + side * 6f - 1f, hy + 5f);
            c.Polygon(new[] { ear + new Vector2(-4f, 0f), ear + new Vector2(side * 3f, 15f), ear + new Vector2(4f, 0f) }, side < 0 ? furDark : fur);
            c.Polygon(new[] { ear + new Vector2(-1.5f, 1f), ear + new Vector2(side * 2.5f, 11f), ear + new Vector2(2.5f, 1f) }, new Color(0.78f, 0.45f, 0.5f));
        }
        c.Ellipse(hx, hy, 10f, 9f, fur);
        c.Clip = true;
        c.Ellipse(hx + 2f, hy + 5f, 7f, 3f, furLit);
        c.Clip = false;
        c.Ellipse(hx + 6f, hy - 3f, 4.5f, 3.6f, Color.Lerp(fur, new Color(0.8f, 0.5f, 0.5f), 0.5f));          // 납작한 코
        c.Rect(R(hx + 7f), R(hy - 3f), R(hx + 7f), R(hy - 2f), Ink);
        c.Rect(R(hx + 9f), R(hy - 3f), R(hx + 9f), R(hy - 2f), Ink);
        bool open = pose == Pose.Slash || pose == Pose.Thrust || pose == Pose.Blunt;
        if (open) c.Ellipse(hx + 5f, hy - 7f, 4f, 2.6f, new Color(0.4f, 0.08f, 0.12f));
        c.Rect(R(hx + 3f), R(hy - 9f), R(hx + 3f), R(hy - 6f), Color.white);                                    // 송곳니
        c.Rect(R(hx + 7f), R(hy - 9f), R(hx + 7f), R(hy - 6f), Color.white);
        Color eye = tint.r > tint.b * 1.5f ? new Color(1f, 0.85f, 0.25f) : new Color(1f, 0.25f, 0.2f);
        if (pose == Pose.Hit) { c.Line(hx - 3f, hy + 3f, hx + 1f, hy, 1.5f, Ink); c.Line(hx + 4f, hy, hx + 8f, hy + 3f, 1.5f, Ink); }
        else
        {
            c.Ellipse(hx - 1f, hy + 1.5f, 2.4f, 2.4f, eye);
            c.Ellipse(hx + 6f, hy + 1.5f, 2.4f, 2.4f, eye);
            c.Set(R(hx - 1f), R(hy + 2f), Color.white);
            c.Set(R(hx + 6f), R(hy + 2f), Color.white);
            c.Line(hx - 4f, hy + 5f, hx + 1f, hy + 4f, 1f, furDark);
            c.Line(hx + 4f, hy + 4f, hx + 9f, hy + 5f, 1f, furDark);
        }

        Finish(c);
        if (pose == Pose.Blunt) for (int i = 0; i < 3; i++) c.Arc(hx + 6f, hy - 4f, 12f + i * 7f, 1.5f, -38f, 38f, new Color(0.8f, 0.92f, 1f, 0.7f - i * 0.18f));   // 초음파
        if (pose == Pose.Slash) Trail(c, new Vector2(cx + 14f, cy + 2f), 22f, -60f, 50f);
        return c;
    }

    // ─────────────────────────── 녹 먹는 벌레 ───────────────────────────

    private static PixelCanvas Bug(Color tint, Pose pose)
    {
        PixelCanvas c = new PixelCanvas(W, H);
        Color shell = tint, shellDark = Cool(tint, 0.55f), shellLit = Color.Lerp(tint, new Color(1f, 0.92f, 0.75f), 0.35f), leg = Cool(tint, 0.32f);
        shellLit.a = 1f;

        float dx = pose == Pose.Thrust ? 9f : pose == Pose.Evade ? -8f : pose == Pose.Hit ? -5f : 0f;
        float dy = pose == Pose.Blunt ? 9f : pose == Pose.Guard ? -3f : 0f;
        float rear = pose == Pose.Slash ? 9f : pose == Pose.Blunt ? 5f : 0f;   // 앞몸을 쳐든 높이
        float cx = 40f + dx, cy = 22f + dy;

        // 다리 여섯 개 (뒤쪽 줄은 어둡게): 넓적다리가 솟았다가 정강이가 땅을 짚는다
        for (int row = 0; row < 2; row++)
        {
            for (int i = 0; i < 3; i++)
            {
                float lx = cx - 14f + i * 14f + row * 4f;
                Vector2 hipJ = new Vector2(lx, cy - 4f + (i == 2 ? rear * 0.5f : 0f));
                Vector2 knee = hipJ + new Vector2(i == 0 ? -9f : i == 2 ? 8f : 3f, 7f - row * 3f);
                Vector2 foot = new Vector2(knee.x + (i == 0 ? -5f : i == 2 ? 6f : 2f), pose == Pose.Guard ? cy - 10f : 2f + dy * 0.6f);
                Color col = row == 0 ? Cool(leg, 0.7f) : leg;
                c.Line(hipJ.x, hipJ.y, knee.x, knee.y, 2.6f, col);
                c.Line(knee.x, knee.y, foot.x, foot.y, 1.8f, col);
            }
        }

        // 배 (둥근 딱지날개) → 가슴 → 머리
        c.Ellipse(cx - 6f, cy + 5f, 23f, 16f, shell);
        c.Clip = true;
        c.Ellipse(cx - 6f, cy - 8f, 24f, 7f, shellDark);                                  // 아랫배
        c.Ellipse(cx - 9f, cy + 15f, 15f, 4.5f, shellLit);                                // 등의 윤기
        c.Line(cx - 27f, cy + 6f, cx + 16f, cy + 9f, 1.2f, shellDark);                    // 날개가 맞닿은 선
        for (int i = 0; i < 4; i++) c.Arc(cx - 6f, cy + 5f, 8f + i * 5f, 1f, 185f, 265f, shellDark);
        System.Random rng = new System.Random(11);
        for (int i = 0; i < 12; i++)                                                      // 녹 반점
        {
            float sx = cx - 24f + (float)rng.NextDouble() * 36f, sy = cy - 2f + (float)rng.NextDouble() * 18f, sr = 1f + (float)rng.NextDouble() * 1.8f;
            c.Ellipse(sx, sy, sr, sr, new Color(0.98f, 0.58f, 0.2f));
            c.Set(R(sx), R(sy), new Color(1f, 0.82f, 0.4f));
        }
        c.Clip = false;

        float tx = cx + 19f, ty = cy + 3f + rear * 0.6f;
        c.Ellipse(tx, ty, 10f, 10f, Color.Lerp(shell, shellDark, 0.3f));
        c.Clip = true;
        c.Ellipse(tx - 1f, ty + 6f, 7f, 3f, shellLit);
        c.Arc(tx - 10f, ty, 10f, 1.2f, -60f, 60f, shellDark);
        c.Clip = false;

        float hx = tx + 11f, hy = ty - 1f + rear * 0.4f;
        // 더듬이 (마디진 곡선 끝에 곤봉)
        for (int k = 0; k < 2; k++)
        {
            Vector2 p = new Vector2(hx + 1f, hy + 5f);
            float a = 70f - k * 22f;
            for (int i = 0; i < 6; i++) { p = c.Ray(p, a, 4f, 1.4f, leg); a -= 13f; }
            c.Ellipse(p.x, p.y, 2f, 2f, shellDark);
        }
        c.Ellipse(hx, hy, 7.5f, 6.5f, shellDark);
        // 큰 턱 두 개 (공격할 땐 벌어진다)
        float gape = pose == Pose.Slash || pose == Pose.Blunt ? 34f : pose == Pose.Thrust ? 8f : 20f;
        for (int k = -1; k <= 1; k += 2)
        {
            Vector2 root = new Vector2(hx + 5f, hy - 1f + k * 2f);
            Vector2 mid = root + Dir(k * gape) * 8f, tip = mid + Dir(k * (gape - 50f)) * 7f;
            c.Polygon(new[] { root + new Vector2(0f, k * 2.5f), mid + Dir(k * (gape + 80f)) * 2.5f, tip, mid - Dir(k * (gape + 80f)) * 1.5f, root - new Vector2(0f, k * 1.5f) }, leg);
            c.Set(R(tip.x), R(tip.y), Color.white);
        }
        if (pose == Pose.Hit) { c.Line(hx - 1f, hy + 4f, hx + 4f, hy, 1.5f, new Color(1f, 0.85f, 0.3f)); c.Line(hx - 1f, hy, hx + 4f, hy + 4f, 1.5f, new Color(1f, 0.85f, 0.3f)); }
        else
        {
            c.Ellipse(hx + 1.5f, hy + 2f, 2.8f, 2.8f, new Color(1f, 0.82f, 0.25f));
            c.Set(R(hx + 2f), R(hy + 3f), Color.white);
        }

        Finish(c);
        if (pose == Pose.Slash) Trail(c, new Vector2(hx - 4f, hy), 20f, -55f, 55f);
        return c;
    }

    // ─────────────────────────── 게 ───────────────────────────

    private static PixelCanvas Crab(Color tint, Pose pose)
    {
        PixelCanvas c = new PixelCanvas(W, H);
        Color shell = tint, shellDark = Cool(tint, 0.55f), shellLit = Color.Lerp(tint, new Color(1f, 0.97f, 0.9f), 0.32f), leg = Cool(tint, 0.4f);
        shellLit.a = 1f;

        float dx = pose == Pose.Thrust ? 5f : pose == Pose.Evade ? -8f : pose == Pose.Hit ? -5f : 0f;
        float dy = pose == Pose.Guard ? -4f : pose == Pose.Blunt ? 3f : 0f;
        float cx = 35f + dx, cy = 25f + dy;

        // 다리: 양쪽 세 개씩 (바깥으로 뻗었다가 꺾여 땅을 짚는다)
        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < 3; i++)
            {
                Vector2 root = new Vector2(cx + side * (9f + i * 4f), cy - 6f);
                Vector2 knee = root + new Vector2(side * (7f + i * 2f), 6f - i * 2f);
                Vector2 foot = new Vector2(knee.x + side * 4f, 2f);
                c.Line(root.x, root.y, knee.x, knee.y, 3.4f, leg);
                c.Line(knee.x, knee.y, foot.x, foot.y, 2.2f, leg);
                c.Ellipse(knee.x, knee.y, 2.4f, 2.4f, shellDark);
            }
        }

        // 뒷집게 (작게)
        Vector2 backClaw = new Vector2(cx - 23f, cy + 13f);
        c.Line(cx - 16f, cy + 2f, backClaw.x, backClaw.y, 5f, shellDark);
        c.Ellipse(backClaw.x, backClaw.y, 6f, 5f, shellDark);
        c.Polygon(new[] { backClaw + new Vector2(-3f, 3f), backClaw + new Vector2(-7f, 9f), backClaw + new Vector2(1f, 5f) }, shellDark);

        // 등딱지: 위는 둥글고 아래는 납작한 판 + 판 이음매 + 리벳 줄
        c.Ellipse(cx, cy, 25f, 14f, shell);
        c.Rect(R(cx - 22f), R(cy - 9f), R(cx + 22f), R(cy - 2f), shell);
        c.Clip = true;
        c.Rect(0, 0, W, R(cy - 5f), shellDark);                                               // 배 쪽
        c.Ellipse(cx - 3f, cy + 10f, 17f, 3.6f, shellLit);                                    // 윗면의 윤기
        c.Line(cx - 9f, cy + 14f, cx - 12f, cy - 4f, 1.2f, shellDark);                        // 판 이음매
        c.Line(cx + 8f, cy + 14f, cx + 11f, cy - 4f, 1.2f, shellDark);
        for (int i = -4; i <= 4; i++) c.Rect(R(cx + i * 5f), R(cy - 2f), R(cx + i * 5f + 1f), R(cy - 1f), Color.Lerp(Steel, shellLit, 0.4f));   // 리벳
        for (int i = 0; i < 5; i++) c.Ellipse(cx - 18f + i * 8f + (i % 2) * 2f, cy + 5f - (i % 3) * 2f, 2f, 1.4f, Cool(shell, 0.78f));          // 얼룩
        c.Clip = false;

        // 눈자루 두 개 (오른쪽을 본다)
        for (int k = 0; k < 2; k++)
        {
            float ex = cx + 6f + k * 9f;
            c.Line(ex, cy + 11f, ex + 1f, cy + 21f, 2.2f, leg);
            c.Ellipse(ex + 1f, cy + 23f, 3.4f, 3.4f, Color.white);
            if (pose == Pose.Hit) c.Line(ex - 1f, cy + 23f, ex + 3f, cy + 23f, 1.5f, Ink);
            else c.Ellipse(ex + 2.2f, cy + 23f, 1.6f, 2f, Ink);
        }

        // 큰 집게: 막기 = 몸 앞을 가림 / 내려치기 = 높이 듦 / 찌르기 = 멀리 뻗음
        float lift = pose == Pose.Blunt ? 34f : pose == Pose.Slash ? 26f : pose == Pose.Guard ? 6f : pose == Pose.Thrust ? 8f : pose == Pose.Hit ? 2f : 14f;
        float reach = pose == Pose.Thrust ? 37f : pose == Pose.Guard ? 24f : pose == Pose.Blunt ? 30f : 33f;
        float open = pose == Pose.Slash || pose == Pose.Idle ? 22f : pose == Pose.Guard ? 6f : 12f;
        Vector2 claw = new Vector2(cx + reach, cy + lift);
        Vector2 elbow = new Vector2(cx + 22f, cy + lift * 0.4f + 6f);
        c.Line(cx + 16f, cy + 2f, elbow.x, elbow.y, 6f, shellDark);
        c.Line(elbow.x, elbow.y, claw.x - 6f, claw.y - 1f, 6f, shell);
        c.Ellipse(elbow.x, elbow.y, 3.6f, 3.6f, shellLit);
        float big = pose == Pose.Guard ? 1.2f : 1f;
        c.Ellipse(claw.x, claw.y, 10f * big, 8.5f * big, shell);
        c.Polygon(new[] { claw + new Vector2(4f, 6f) * big, claw + Dir(open) * 21f * big, claw + new Vector2(9f, 1f) * big }, shell);             // 윗집게
        c.Polygon(new[] { claw + new Vector2(5f, -1f) * big, claw + Dir(-open * 0.6f - 6f) * 19f * big, claw + new Vector2(2f, -7f) * big }, shellDark);   // 아랫집게
        c.Clip = true;
        c.Ellipse(claw.x - 2f, claw.y + 5f * big, 6f * big, 2.6f, shellLit);
        for (int i = 0; i < 3; i++) c.Set(R(claw.x + 8f + i * 3f), R(claw.y + 2f + i * (open * 0.08f)), Color.white);                              // 톱니
        c.Clip = false;

        Finish(c);
        if (pose == Pose.Slash) Trail(c, claw + new Vector2(-12f, -6f), 24f, -55f, 60f);
        if (pose == Pose.Blunt) Trail(c, claw + new Vector2(-6f, -12f), 22f, 20f, 130f);
        return c;
    }

    // ─────────────────────────── 망령 ───────────────────────────

    private static PixelCanvas Wraith(Color tint, Pose pose)
    {
        PixelCanvas c = new PixelCanvas(W, H);
        Color cloak = tint, cloakDark = Cool(tint, 0.5f), cloakLit = Color.Lerp(tint, new Color(0.9f, 0.95f, 1f), 0.25f);
        Color dial = new Color(0.93f, 0.89f, 0.75f);
        cloakLit.a = 1f;

        float dx = pose == Pose.Thrust ? 5f : pose == Pose.Evade ? -7f : pose == Pose.Hit ? -5f : 0f;
        float sway = pose == Pose.Thrust ? -10f : pose == Pose.Evade ? 9f : pose == Pose.Hit ? 7f : 3f;   // 끝자락이 뒤로 끌리는 정도
        float cx = 34f + dx;

        // 너울거리는 망토: 어깨는 넓고 아래로 좁아지며, 끝은 갈기갈기 찢어져 있다 (발이 없다 — 떠 있다)
        for (int y = 12; y <= 92; y++)
        {
            float t = (y - 12) / 80f;   // 0 = 끝자락, 1 = 어깨
            float hw = Mathf.Lerp(11f, 19f, Mathf.Pow(t, 0.6f)) + Mathf.Sin(y * 0.42f) * 1.6f * (1f - t);
            float mx = cx + (1f - t) * (1f - t) * sway;
            c.Rect(R(mx - hw), y, R(mx + hw), y, cloak);
        }
        for (int i = 0; i < 6; i++)   // 찢어진 끝자락
        {
            float fx = cx + sway - 12f + i * 4.6f;
            c.Polygon(new[] { new Vector2(fx - 2.6f, 13f), new Vector2(fx + sway * 0.2f, 2f + (i % 2) * 5f), new Vector2(fx + 2.6f, 13f) }, cloak);
        }
        c.Clip = true;
        for (int y = 2; y <= 92; y++)
        {
            float t = Mathf.Clamp01((y - 12) / 80f);
            float mx = cx + (1f - t) * (1f - t) * sway;
            c.Rect(R(mx - 22f), y, R(mx - 9f - t * 2f), y, cloakDark);                        // 등 쪽 그늘
            c.Rect(R(mx + 11f + t * 3f), y, R(mx + 24f), y, cloakLit);                         // 앞자락의 빛
        }
        for (int i = -1; i <= 1; i++) c.Line(cx + i * 6f, 84f, cx + sway * 0.6f + i * 7f, 14f, 1f, cloakDark);   // 주름
        c.Clip = false;

        // 두건 + 그 속의 얼굴 = 시계판 (바늘이 표정)
        float hx = cx + 2f, hy = 100f;
        c.Polygon(new[] { new Vector2(hx - 15f, hy + 2f), new Vector2(hx - 24f, hy - 12f), new Vector2(hx - 8f, hy - 10f) }, cloakDark);
        c.Ellipse(hx, hy, 17f, 17f, cloak);
        c.Polygon(new[] { new Vector2(hx - 8f, hy + 14f), new Vector2(hx - 3f, hy + 25f), new Vector2(hx + 6f, hy + 15f) }, cloak);   // 뾰족한 두건 끝
        c.Clip = true;
        c.Ellipse(hx - 9f, hy, 11f, 18f, cloakDark);
        c.Arc(hx, hy, 16f, 1.6f, 20f, 110f, cloakLit);
        c.Clip = false;
        c.Ellipse(hx + 4f, hy - 2f, 11.5f, 11.5f, BrassDark);
        c.Ellipse(hx + 4f, hy - 2f, 9.8f, 9.8f, dial);
        for (int i = 0; i < 12; i++) { Vector2 p = new Vector2(hx + 4f, hy - 2f) + Dir(i * 30f) * 8f; c.Set(R(p.x), R(p.y), i % 3 == 0 ? Ink : new Color(0.5f, 0.45f, 0.4f)); }
        float hourA = pose == Pose.Hit ? 250f : pose == Pose.Idle || pose == Pose.Evade ? 120f : 60f;
        float minA = pose == Pose.Hit ? 290f : pose == Pose.Idle || pose == Pose.Evade ? 60f : 120f;   // 공격할 땐 바늘이 찌푸린 눈썹 모양(∨)이 된다
        c.Ray(new Vector2(hx + 4f, hy - 2f), hourA, 5f, 1.6f, Ink);
        c.Ray(new Vector2(hx + 4f, hy - 2f), minA, 7f, 1.2f, Ink);
        c.Ellipse(hx + 4f, hy - 2f, 1.4f, 1.4f, new Color(0.7f, 0.2f, 0.2f));

        // 소매에서 나온 앙상한 손이 진자를 쥐고 있다 (공격마다 흔들린 각도)
        float swing = pose == Pose.Slash ? 58f : pose == Pose.Blunt ? 100f : pose == Pose.Thrust ? 78f : pose == Pose.Guard ? 8f : pose == Pose.Hit ? -30f : -8f;
        Vector2 shoulder = new Vector2(cx + 12f, 80f);
        Vector2 hand = shoulder + (pose == Pose.Guard ? new Vector2(10f, 4f) : pose == Pose.Blunt ? new Vector2(10f, 14f) : new Vector2(11f, -4f));
        c.Polygon(new[] { shoulder + new Vector2(-4f, 5f), hand + new Vector2(2f, 5f), hand + new Vector2(0f, -7f), shoulder + new Vector2(-2f, -9f) }, cloak);
        c.Clip = true;
        c.Polygon(new[] { shoulder + new Vector2(-4f, -2f), hand + new Vector2(0f, -3f), hand + new Vector2(0f, -7f), shoulder + new Vector2(-2f, -9f) }, cloakDark);
        c.Clip = false;
        c.Ellipse(hand.x + 2f, hand.y - 1f, 3.2f, 3.2f, new Color(0.85f, 0.88f, 0.82f));
        Vector2 bob = hand + new Vector2(2f, -1f) + Dir(-90f + swing) * 25f;
        c.Line(hand.x + 2f, hand.y - 1f, bob.x, bob.y, 1.6f, BrassDark);
        c.Ellipse(bob.x, bob.y, 7.5f, 7.5f, Brass);
        c.Ellipse(bob.x, bob.y, 4.6f, 4.6f, BrassDark);
        c.Ellipse(bob.x - 2f, bob.y + 2f, 1.6f, 1.6f, new Color(1f, 0.95f, 0.75f));

        Finish(c);

        if (pose == Pose.Slash || pose == Pose.Blunt) Trail(c, hand, 25f, -90f, -90f + swing);
        for (int i = 0; i < 4; i++) c.Ellipse(cx + sway - 14f + i * 9f, 5f + (i % 2) * 4f, 3.5f, 2f, new Color(tint.r, tint.g, tint.b, 0.3f));   // 흩어지는 자락
        return c;
    }

    // ─────────────────────────── 뻐꾸기 시계 ───────────────────────────

    private static PixelCanvas Cuckoo(Color tint, Pose pose)
    {
        PixelCanvas c = new PixelCanvas(W, H);
        Color wood = tint, woodDark = Cool(tint, 0.55f), woodLit = Color.Lerp(tint, new Color(1f, 0.9f, 0.7f), 0.3f), roof = Cool(tint, 0.38f);
        woodLit.a = 1f;

        float dx = pose == Pose.Evade ? -8f : pose == Pose.Hit ? -5f : pose == Pose.Thrust ? 4f : 0f;
        float dy = pose == Pose.Blunt ? 7f : pose == Pose.Guard ? -3f : 0f;
        float cx = 34f + dx, floor = 26f + dy;   // floor = 집의 바닥 높이

        // 새 다리 두 개 (무릎이 뒤로 꺾인다) + 발가락
        for (int k = 0; k < 2; k++)
        {
            float lx = cx - 9f + k * 16f;
            Vector2 hip = new Vector2(lx, floor), knee = new Vector2(lx - 5f, floor * 0.5f + 2f), foot = new Vector2(lx + (pose == Pose.Evade ? -4f : 2f), 3f + dy * 0.4f);
            Color col = k == 0 ? SteelDark : Steel;
            c.Line(hip.x, hip.y, knee.x, knee.y, 3f, col);
            c.Line(knee.x, knee.y, foot.x, foot.y, 2.4f, col);
            for (int i = -1; i <= 1; i++) c.Line(foot.x, foot.y, foot.x + 7f + i, foot.y - 1f + i * 2f, 1.4f, col);
        }

        // 추 두 개 (사슬 끝의 솔방울) + 흔들리는 진자
        for (int k = 0; k < 2; k++)
        {
            float wx = cx - 12f + k * 9f, len = 9f + k * 5f;
            c.Line(wx, floor, wx, floor - len, 1f, BrassDark);
            c.Ellipse(wx, floor - len - 4f, 2.6f, 4.6f, k == 0 ? BrassDark : Brass);
        }
        Vector2 bob = c.Ray(new Vector2(cx + 8f, floor), -90f + (pose == Pose.Slash || pose == Pose.Blunt ? 28f : pose == Pose.Hit ? -24f : 8f), 15f, 1.4f, BrassDark);
        c.Ellipse(bob.x, bob.y, 4.6f, 4.6f, Brass);

        // 집 모양 몸통 + 널빤지 결 + 테두리 조각
        c.Rect(R(cx - 20f), R(floor), R(cx + 20f), R(floor + 50f), wood);
        c.Clip = true;
        c.Rect(R(cx - 20f), R(floor), R(cx - 13f), R(floor + 50f), woodDark);
        c.Rect(R(cx + 15f), R(floor), R(cx + 20f), R(floor + 50f), woodLit);
        for (int i = 0; i < 4; i++) c.Rect(R(cx - 20f), R(floor + 9f + i * 11f), R(cx + 20f), R(floor + 9f + i * 11f), woodDark);
        c.Rect(R(cx - 20f), R(floor), R(cx + 20f), R(floor + 3f), roof);
        c.Clip = false;

        // 지붕: 처마가 튀어나온 박공 + 기와 줄 + 꼭대기 장식
        float ry = floor + 50f;
        c.Polygon(new[] { new Vector2(cx - 27f, ry - 3f), new Vector2(cx, ry + 24f), new Vector2(cx + 27f, ry - 3f), new Vector2(cx + 24f, ry - 6f), new Vector2(cx - 24f, ry - 6f) }, roof);
        c.Clip = true;
        for (int i = 0; i < 4; i++) c.Arc(cx, ry - 6f, 10f + i * 7f, 1f, 30f, 150f, Cool(tint, 0.26f));   // 기와 줄
        c.Polygon(new[] { new Vector2(cx + 4f, ry + 20f), new Vector2(cx + 27f, ry - 3f), new Vector2(cx + 20f, ry - 3f) }, Cool(tint, 0.5f));
        c.Clip = false;
        c.Ellipse(cx, ry + 26f, 3f, 3f, Brass);

        // 시계판
        Vector2 face = new Vector2(cx - 1f, floor + 19f);
        c.Ellipse(face.x, face.y, 13f, 13f, BrassDark);
        c.Ellipse(face.x, face.y, 11f, 11f, new Color(0.94f, 0.9f, 0.76f));
        for (int i = 0; i < 12; i++) { Vector2 p = face + Dir(i * 30f) * 9f; c.Set(R(p.x), R(p.y), Ink); }
        bool angry = pose != Pose.Idle && pose != Pose.Evade && pose != Pose.Guard;
        c.Ray(face, angry ? 35f : 90f, 6f, 1.6f, Ink);
        c.Ray(face, angry ? 145f : -30f, 8f, 1.2f, Ink);
        c.Ellipse(face.x, face.y, 1.5f, 1.5f, new Color(0.7f, 0.2f, 0.2f));

        // 문 + 용수철 끝의 새 (공격하면 멀리 튀어나오고, 막을 땐 문을 닫는다)
        Vector2 door = new Vector2(cx + 6f, floor + 41f);
        float pop = pose == Pose.Thrust ? 27f : pose == Pose.Slash || pose == Pose.Blunt ? 21f : pose == Pose.Guard ? 0f : pose == Pose.Hit ? 14f : 9f;
        c.Rect(R(door.x - 6f), R(door.y - 6f), R(door.x + 6f), R(door.y + 4f), new Color(0.07f, 0.05f, 0.07f));
        c.Ellipse(door.x, door.y + 4f, 6.4f, 4f, new Color(0.07f, 0.05f, 0.07f));
        if (pop <= 0f)
        {
            c.Rect(R(door.x - 5f), R(door.y - 5f), R(door.x + 5f), R(door.y + 4f), woodDark);
            c.Set(R(door.x + 3f), R(door.y), Brass);
        }
        else
        {
            Vector2 bird = door + new Vector2(pop + 6f, pose == Pose.Blunt ? -8f : pose == Pose.Slash ? 4f : 0f);
            Vector2 p = door;
            int coils = 6;
            for (int i = 1; i <= coils; i++)   // 지그재그 용수철
            {
                Vector2 q = Vector2.Lerp(door, bird, i / (float)coils) + new Vector2(0f, i % 2 == 0 ? 3f : -3f) * (i < coils ? 1f : 0f);
                c.Line(p.x, p.y, q.x, q.y, 1.4f, Steel);
                p = q;
            }
            Color feather = new Color(0.95f, 0.82f, 0.4f);
            c.Ellipse(bird.x + 2f, bird.y, 7f, 6f, feather);
            c.Ellipse(bird.x + 6f, bird.y + 4f, 5f, 4.6f, feather);                                            // 머리
            c.Polygon(new[] { bird + new Vector2(-2f, 2f), bird + new Vector2(-9f, 6f), bird + new Vector2(-8f, -2f), bird + new Vector2(0f, -3f) }, new Color(0.75f, 0.55f, 0.25f));   // 날개
            bool beakOpen = pose == Pose.Thrust || pose == Pose.Slash || pose == Pose.Blunt;
            c.Polygon(new[] { bird + new Vector2(10f, 6f), bird + new Vector2(18f, beakOpen ? 7f : 4f), bird + new Vector2(10f, 3f) }, new Color(1f, 0.55f, 0.15f));
            if (beakOpen) c.Polygon(new[] { bird + new Vector2(10f, 3f), bird + new Vector2(17f, 0f), bird + new Vector2(10f, 1f) }, new Color(0.85f, 0.4f, 0.1f));
            c.Ellipse(bird.x + 7f, bird.y + 5f, 1.5f, 1.5f, Ink);
            c.Polygon(new[] { bird + new Vector2(4f, 8f), bird + new Vector2(5f, 13f), bird + new Vector2(8f, 8f) }, new Color(0.85f, 0.3f, 0.25f));   // 볏
        }

        Finish(c);
        if (pose == Pose.Blunt) for (int i = 0; i < 2; i++) c.Arc(cx, floor + 30f, 34f + i * 6f, 1.5f, 150f, 210f, new Color(1f, 1f, 1f, 0.5f - i * 0.2f));
        return c;
    }

    // ─────────────────────────── 거머리 ───────────────────────────

    private static PixelCanvas Leech(Color tint, Pose pose)
    {
        PixelCanvas c = new PixelCanvas(W, H);
        Color skin = tint, skinDark = Cool(tint, 0.55f), skinLit = Color.Lerp(tint, new Color(0.9f, 1f, 0.8f), 0.3f);
        Color belly = Color.Lerp(tint, new Color(0.85f, 0.8f, 0.6f), 0.5f);
        skinLit.a = belly.a = 1f;

        float height = pose == Pose.Blunt ? 98f : pose == Pose.Guard ? 58f : pose == Pose.Thrust ? 70f : 84f;
        float bend = pose == Pose.Thrust ? 34f : pose == Pose.Slash ? 22f : pose == Pose.Evade ? -12f : pose == Pose.Hit ? -16f : pose == Pose.Guard ? 4f : 12f;   // 머리가 앞으로 숙은 정도
        const float baseX = 36f;

        // 바닥에 똬리 튼 꼬리
        c.Ellipse(baseX - 12f, 8f, 16f, 6.5f, skinDark);
        c.Ellipse(baseX - 22f, 12f, 6f, 5f, skin);

        // 몸: 아래는 굵고 위로 갈수록 가늘어지며 S자로 일어선다
        const int steps = 44;
        Vector2 headPos = Vector2.zero, prev = Vector2.zero;
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector2 p = new Vector2(baseX + Mathf.Sin(t * Mathf.PI * 1.15f) * -7f + t * t * bend, 9f + t * height * (1f - 0.12f * t * Mathf.Abs(bend) / 30f));
            float r = Mathf.Lerp(14f, 9f, t);
            c.Ellipse(p.x, p.y, r, r * 0.9f, i % 6 == 5 ? skinDark : skin);
            prev = headPos;
            headPos = p;
        }
        c.Clip = true;
        for (int i = 0; i <= steps; i++)   // 배 쪽(앞)의 밝은 줄 + 등 쪽 그늘 + 점무늬
        {
            float t = i / (float)steps;
            Vector2 p = new Vector2(baseX + Mathf.Sin(t * Mathf.PI * 1.15f) * -7f + t * t * bend, 9f + t * height * (1f - 0.12f * t * Mathf.Abs(bend) / 30f));
            float r = Mathf.Lerp(14f, 9f, t);
            c.Ellipse(p.x + r * 0.62f, p.y, r * 0.36f, 2.2f, i % 6 == 5 ? Cool(belly, 0.8f) : belly);
            c.Ellipse(p.x - r * 0.75f, p.y, r * 0.3f, 2.2f, skinDark);
            if (i % 6 == 2) c.Ellipse(p.x - 2f, p.y, 1.8f, 1.4f, skinLit);
        }
        c.Clip = false;

        // 입: 몸 끝에 달린 둥근 빨판 + 이빨 고리 (앞을 향한다)
        Vector2 dir = (headPos - prev).normalized;
        Vector2 mouth = headPos + dir * 5f + new Vector2(5f, 0f);
        float gape = pose == Pose.Slash || pose == Pose.Thrust || pose == Pose.Blunt ? 1.25f : pose == Pose.Guard ? 0.75f : 1f;
        c.Ellipse(mouth.x, mouth.y, 10f * gape, 9.5f * gape, Color.Lerp(skin, new Color(0.75f, 0.4f, 0.4f), 0.45f));
        c.Ellipse(mouth.x + 1f, mouth.y, 7f * gape, 6.6f * gape, new Color(0.42f, 0.08f, 0.12f));
        c.Ellipse(mouth.x + 1.5f, mouth.y, 3.2f * gape, 3f * gape, new Color(0.14f, 0.02f, 0.05f));
        for (int i = 0; i < 10; i++)
        {
            Vector2 root = mouth + new Vector2(1f, 0f) + Dir(i * 36f) * 6.4f * gape, tip = mouth + new Vector2(1f, 0f) + Dir(i * 36f) * 3.4f * gape;
            c.Line(root.x, root.y, tip.x, tip.y, 1.5f, Color.white);
        }
        if (pose == Pose.Hit) c.Line(mouth.x - 5f, mouth.y, mouth.x + 7f, mouth.y, 2f, new Color(0.14f, 0.02f, 0.05f));

        Finish(c);

        // 점액 방울
        Color slime = new Color(0.7f, 0.95f, 0.6f, 0.75f);
        c.Line(mouth.x + 2f, mouth.y - 9f * gape, mouth.x + 2f, mouth.y - 15f * gape, 1f, slime);
        c.Ellipse(mouth.x + 2f, mouth.y - 17f * gape, 1.6f, 2f, slime);
        if (pose == Pose.Guard) c.Arc(baseX, 34f, 34f, 1.5f, 110f, 250f, new Color(0.7f, 0.95f, 0.75f, 0.5f));
        if (pose == Pose.Slash) Trail(c, mouth + new Vector2(-12f, -4f), 22f, -60f, 55f);
        return c;
    }

    // ─────────────────────────── 종 ───────────────────────────

    private static PixelCanvas Bell(Color tint, Pose pose)
    {
        PixelCanvas c = new PixelCanvas(W, H);
        Color body = Color.Lerp(tint, Brass, 0.4f), bodyDark = Cool(body, 0.55f), bodyLit = Color.Lerp(body, new Color(1f, 0.97f, 0.85f), 0.38f);
        Color verdigris = new Color(0.36f, 0.72f, 0.64f);
        body.a = bodyLit.a = 1f;

        float tilt = pose == Pose.Thrust ? 0.2f : pose == Pose.Blunt ? 0.3f : pose == Pose.Slash ? 0.16f : pose == Pose.Evade ? -0.22f : pose == Pose.Hit ? -0.18f : 0.05f;
        float hop = pose == Pose.Blunt ? 8f : pose == Pose.Guard ? -6f : 0f;
        const float cx = 44f;
        float baseY = 20f + hop;
        const int rows = 70;

        // 추 (몸이 기운 반대쪽으로 흔들린다)
        Vector2 clapTop = new Vector2(cx - tilt * 10f, baseY + 16f), clap = new Vector2(cx - tilt * 62f, baseY - 9f);
        c.Line(clapTop.x, clapTop.y, clap.x, clap.y, 2.6f, SteelDark);
        c.Ellipse(clap.x, clap.y, 6.5f, 6.5f, SteelDark);
        c.Ellipse(clap.x - 1.5f, clap.y + 2f, 2.4f, 2f, Steel);

        // 종의 옆모습: 입은 넓게 벌어지고 어깨에서 둥글게 모인다
        for (int y = 0; y <= rows; y++)
        {
            float t = y / (float)rows;   // 0 = 입, 1 = 꼭대기
            float hw = Mathf.Lerp(34f, 15f, Mathf.Pow(t, 0.5f)) + (t < 0.08f ? (0.08f - t) * 50f : 0f) - (t > 0.86f ? Mathf.Pow((t - 0.86f) / 0.14f, 2f) * 11f : 0f);
            float mx = cx + (y - rows * 0.5f) * tilt;
            c.Rect(R(mx - hw), R(baseY + y), R(mx + hw), R(baseY + y), body);
        }
        c.Clip = true;
        for (int y = 0; y <= rows; y++)
        {
            float t = y / (float)rows;
            float hw = Mathf.Lerp(34f, 15f, Mathf.Pow(t, 0.5f));
            float mx = cx + (y - rows * 0.5f) * tilt;
            c.Rect(R(mx - hw - 6f), R(baseY + y), R(mx - hw * 0.5f), R(baseY + y), bodyDark);          // 왼쪽 그늘
            c.Rect(R(mx + hw * 0.25f), R(baseY + y), R(mx + hw * 0.5f), R(baseY + y), bodyLit);         // 세로로 흐르는 윤기
            if (y == 6 || y == 7 || y == 16 || y == 54) c.Rect(0, R(baseY + y), W, R(baseY + y), bodyDark);   // 새겨진 띠
            if (y == 8 || y == 17) c.Rect(0, R(baseY + y), W, R(baseY + y), bodyLit);
        }
        c.Rect(0, R(baseY), W, R(baseY + 3f), bodyDark);                                                // 입술의 두꺼운 테
        System.Random rng = new System.Random(5);
        for (int i = 0; i < 9; i++)                                                                     // 푸른 녹 + 따개비
        {
            float px = cx - 28f + (float)rng.NextDouble() * 56f, py = baseY + 4f + (float)rng.NextDouble() * 52f;
            c.Ellipse(px, py, 2f + (float)rng.NextDouble() * 3.5f, 1.5f + (float)rng.NextDouble() * 2.5f, i % 3 == 0 ? new Color(0.85f, 0.85f, 0.78f) : verdigris);
        }
        c.Clip = false;

        // 꼭대기의 고리
        float topX = cx + rows * 0.5f * tilt, topY = baseY + rows;
        c.Arc(topX, topY + 3f, 6f, 3.4f, -10f, 190f, bodyDark);
        c.Rect(R(topX - 8f), R(topY), R(topX + 8f), R(topY + 2f), body);

        // 얼굴: 어두운 눈구멍 속의 빛나는 눈 (공격할 땐 치켜뜬다)
        float fy = baseY + 36f, fx = cx + 5f + (36f - rows * 0.5f) * tilt;
        Color glow = new Color(0.55f, 0.95f, 1f);
        bool angry = pose == Pose.Slash || pose == Pose.Thrust || pose == Pose.Blunt;
        for (int k = -1; k <= 1; k += 2)
        {
            float ex = fx + k * 8f;
            c.Ellipse(ex, fy, 5.4f, 6f, new Color(0.08f, 0.07f, 0.1f));
            if (pose == Pose.Hit) c.Line(ex - 3f, fy, ex + 3f, fy, 2f, glow);
            else
            {
                c.Ellipse(ex + 1f, fy - 0.5f, 3f, 3.8f, glow);
                c.Ellipse(ex + 1.5f, fy + 0.5f, 1.2f, 1.6f, Color.white);
            }
            if (angry) c.Polygon(new[] { new Vector2(ex - 6f, fy + 7f), new Vector2(ex + 6f, fy + 7f), new Vector2(ex + k * -6f, fy + 2f) }, body);   // 찌푸린 눈두덩
        }

        // 입술에 늘어진 물풀
        for (int i = 0; i < 3; i++)
        {
            float sx = cx - 22f + i * 19f - rows * 0.5f * tilt;
            c.Line(sx, baseY, sx - 2f + i, baseY - 9f - i * 3f, 1.6f, new Color(0.25f, 0.5f, 0.3f));
        }

        Finish(c);

        // 물방울 + 울림
        Color drop = new Color(0.65f, 0.88f, 1f, 0.85f);
        c.Ellipse(cx - 26f, baseY - 8f, 1.6f, 2.4f, drop);
        c.Ellipse(cx + 24f, baseY - 13f, 1.6f, 2.4f, drop);
        c.Ellipse(cx + 6f, 4f, 1.6f, 2.4f, drop);
        if (pose == Pose.Blunt || pose == Pose.Slash)
            for (int i = 0; i < 3; i++) c.Arc(cx, baseY + 4f, 40f + i * 5f, 1.5f, 200f, 340f, new Color(1f, 1f, 1f, 0.6f - i * 0.17f));
        return c;
    }
}
