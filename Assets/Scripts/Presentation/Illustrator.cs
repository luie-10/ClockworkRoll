using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 그림이 비어 있는 곳을 채우는 '자동 일러스트' — 전부 코드로 그리는 픽셀 아트.
///
///   Character(이름, 색) : 캐릭터 스프라이트 + 합 포즈 7종 (오른쪽을 보는 그림, 발이 아래 가운데)
///                        이름으로 생김새를 고른다: 쥐 / 인형 / 경비·병정 / 거머리 / 시계공 / 종지기 / 종 / 이안 / 틱 / 그 밖의 사람
///   Portrait(화자)      : 대사창 초상화 (캐릭터 상반신을 크게)
///   CardArt(카드)       : 태엽판 그림 — 주사위 종류마다 문양 (참격 = 베는 호, 관통 = 시곗바늘, 타격 = 망치,
///                        가드 = 방패, 회피 = 잔상 소용돌이), 등급 색 바탕, 효과 표식
///
/// 같은 대상이면 같은 그림 (이름으로 시드를 정함) · 한 번 그린 것은 캐시.
/// 직접 그린 그림을 에셋에 넣으면 그쪽이 우선이다 (CardData.cardArt / EnemyData.sprite / 대사 초상화).
/// 에디터 메뉴 'Ruina/일러스트 굽기'로 PNG로 저장해 에셋에 연결할 수도 있다.
/// </summary>
public static class Illustrator
{
    public const int CharW = 48, CharH = 64;
    public const int CardW = 208, CardH = 80;

    public enum Pose { Idle, Slash, Thrust, Blunt, Guard, Evade, Hit }

    public class CharacterArt
    {
        public Sprite idle;
        public PoseSprites poses;
        public readonly Dictionary<Pose, Texture2D> textures = new Dictionary<Pose, Texture2D>();
    }

    private static readonly Dictionary<string, CharacterArt> characters = new Dictionary<string, CharacterArt>();
    private static readonly Dictionary<string, Sprite> portraits = new Dictionary<string, Sprite>();
    private static readonly Dictionary<CardData, Sprite> cards = new Dictionary<CardData, Sprite>();

    private static readonly Color OutlineColor = new Color(0.05f, 0.04f, 0.045f, 1f);
    private static readonly Color Brass = new Color(0.85f, 0.66f, 0.3f);
    private static readonly Color Steel = new Color(0.78f, 0.8f, 0.84f);
    private static readonly Color Skin = new Color(0.93f, 0.78f, 0.64f);

    // ─────────────────────────── 캐릭터 ───────────────────────────

    public static CharacterArt Character(string name, Color tint)
    {
        string key = name + "|" + ColorUtility.ToHtmlStringRGB(tint);
        if (characters.TryGetValue(key, out CharacterArt cached)) return cached;

        CharacterArt art = new CharacterArt { poses = new PoseSprites() };
        foreach (Pose p in System.Enum.GetValues(typeof(Pose)))
        {
            Texture2D tex = DrawCharacter(name, tint, p).ToTexture(name + " " + p);
            art.textures[p] = tex;
            Sprite s = Sprite.Create(tex, new Rect(0, 0, CharW, CharH), new Vector2(0.5f, 0f), 32f);
            s.name = name + " " + p;
            switch (p)
            {
                case Pose.Idle:   art.idle = s; break;
                case Pose.Slash:  art.poses.slash = s; break;
                case Pose.Thrust: art.poses.thrust = s; break;
                case Pose.Blunt:  art.poses.blunt = s; break;
                case Pose.Guard:  art.poses.guard = s; break;
                case Pose.Evade:  art.poses.evade = s; break;
                case Pose.Hit:    art.poses.hit = s; break;
            }
        }
        characters[key] = art;
        return art;
    }

    /// <summary>이름으로 생김새를 고른다 (순서 중요: '종지기'가 '종'보다 먼저)</summary>
    public static PixelCanvas DrawCharacter(string name, Color tint, Pose pose)
    {
        PixelCanvas c = new PixelCanvas(CharW, CharH);
        System.Random rng = new System.Random(Hash(name));
        name = name ?? "";

        if (name.Contains("박쥐")) DrawBat(c, tint, pose);   // '쥐'보다 먼저
        else if (name.Contains("쥐")) DrawRat(c, tint, pose, name.Contains("굶주린"));
        else if (name.Contains("벌레")) DrawBug(c, tint, pose);
        else if (name.EndsWith("게")) DrawCrab(c, tint, pose);
        else if (name.Contains("망령")) DrawWraith(c, tint, pose);
        else if (name.Contains("뻐꾸기")) DrawCuckoo(c, tint, pose);
        else if (name.Contains("수문장")) DrawHumanoid(c, pose, Warden(tint));
        else if (name.Contains("거머리")) DrawLeech(c, tint, pose);
        else if (name.Contains("종지기")) DrawHumanoid(c, pose, BellRinger(tint));
        else if (name.Contains("종")) DrawBell(c, tint, pose);
        else if (name.Contains("경비") || name.Contains("병정")) DrawHumanoid(c, pose, Soldier(tint));
        else if (name.Contains("인형") || name == "틱") DrawHumanoid(c, pose, Doll(name == "틱" ? new Color(0.8f, 0.62f, 0.3f) : tint, name == "틱"));
        else if (name.Contains("시계공")) DrawHumanoid(c, pose, Clockmaker(tint));
        else if (name.Contains("이안")) DrawHumanoid(c, pose, Hero());
        else DrawHumanoid(c, pose, Generic(tint, rng));

        c.Shade();
        c.Outline(OutlineColor);
        if (pose == Pose.Hit) Flash(c, new Color(1f, 0.3f, 0.25f), 0.35f);
        return c;
    }

    private static void Flash(PixelCanvas c, Color color, float amount)
    {
        for (int y = 0; y < c.height; y++)
            for (int x = 0; x < c.width; x++)
            {
                Color p = c.Get(x, y);
                if (p.a < 0.5f) continue;
                Color m = Color.Lerp(p, color, amount);
                m.a = p.a;
                c.Set(x, y, m);
            }
    }

    // ── 사람형 ──

    private enum Head { Hair, Helmet, Hood, Goggles, Doll }
    private enum Weapon { None, Blade, Spear, Wrench, Hammer }

    private struct Humanoid
    {
        public Color body, dark, skin, hair, accent;
        public int headR, bodyW, bodyH, legH;
        public Head head;
        public Weapon weapon;
        public bool apron, key, dress;
    }

    private static Humanoid Hero()
    {
        return new Humanoid
        {
            body = new Color(0.2f, 0.3f, 0.5f), dark = new Color(0.12f, 0.16f, 0.26f), skin = Skin, hair = new Color(0.32f, 0.2f, 0.12f),
            accent = Brass, headR = 7, bodyW = 12, bodyH = 18, legH = 13, head = Head.Hair, weapon = Weapon.Blade,
        };
    }

    private static Humanoid Doll(Color tint, bool tick)
    {
        return new Humanoid
        {
            body = tint, dark = Mul(tint, 0.6f), skin = tick ? new Color(0.9f, 0.78f, 0.5f) : new Color(0.88f, 0.86f, 0.8f), hair = Mul(tint, 0.5f),
            accent = Brass, headR = 9, bodyW = 12, bodyH = 14, legH = 9, head = Head.Doll, weapon = Weapon.None, key = true, dress = !tick,
        };
    }

    private static Humanoid Soldier(Color tint)
    {
        return new Humanoid
        {
            body = tint, dark = Mul(tint, 0.55f), skin = Mul(tint, 0.8f) + new Color(0.1f, 0.1f, 0.1f), hair = Mul(tint, 0.7f),
            accent = Brass, headR = 6, bodyW = 14, bodyH = 19, legH = 14, head = Head.Helmet, weapon = Weapon.Spear, key = true,
        };
    }

    private static Humanoid Clockmaker(Color tint)
    {
        return new Humanoid
        {
            body = new Color(0.35f, 0.25f, 0.18f), dark = new Color(0.2f, 0.14f, 0.1f), skin = Mul(Skin, 0.95f), hair = new Color(0.75f, 0.75f, 0.72f),
            accent = tint, headR = 7, bodyW = 13, bodyH = 18, legH = 13, head = Head.Goggles, weapon = Weapon.Wrench, apron = true,
        };
    }

    private static Humanoid BellRinger(Color tint)
    {
        return new Humanoid
        {
            body = Mul(tint, 0.75f), dark = Mul(tint, 0.45f), skin = new Color(0.75f, 0.7f, 0.68f), hair = Mul(tint, 0.5f),
            accent = Brass, headR = 6, bodyW = 15, bodyH = 24, legH = 12, head = Head.Hood, weapon = Weapon.Hammer, dress = true,
        };
    }

    private static Humanoid Generic(Color tint, System.Random rng)
    {
        return new Humanoid
        {
            body = tint, dark = Mul(tint, 0.6f), skin = Skin, hair = new Color(0.25f, 0.18f, 0.12f),
            accent = Brass, headR = 7, bodyW = 12, bodyH = 18, legH = 13, head = rng.NextDouble() < 0.5 ? Head.Hair : Head.Hood,
            weapon = rng.NextDouble() < 0.5 ? Weapon.Blade : Weapon.Hammer,
        };
    }

    private static void DrawHumanoid(PixelCanvas c, Pose pose, Humanoid h)
    {
        int lean = pose == Pose.Thrust ? 3 : pose == Pose.Evade ? -4 : pose == Pose.Hit ? -3 : 0;
        int cx = CharW / 2 + lean;
        int legTop = 1 + h.legH;
        int bodyTop = legTop + h.bodyH;
        int half = h.bodyW / 2;

        // 다리 (회피면 벌린다)
        int spread = pose == Pose.Evade || pose == Pose.Thrust ? 3 : 0;
        c.Rect(cx - 4 - spread, 1, cx - 2 - spread, legTop, h.dark);
        c.Rect(cx + 1 + spread / 2, 1, cx + 3 + spread / 2, legTop, h.dark);
        c.Rect(cx - 5 - spread, 0, cx - 1 - spread, 1, OutlineColor * 1.5f);
        c.Rect(cx + 1 + spread / 2, 0, cx + 5 + spread / 2, 1, OutlineColor * 1.5f);

        // 등 쪽 팔
        c.Rect(cx - half - 2, bodyTop - 10, cx - half, bodyTop - 2, h.dark);

        // 몸통 (치마/외투는 아래로 퍼진다)
        c.Rect(cx - half, legTop, cx + half, bodyTop, h.body);
        if (h.dress) for (int i = 0; i < 6; i++) c.Rect(cx - half - (6 - i) / 2, legTop + i, cx + half + (6 - i) / 2, legTop + i, h.body);
        else c.Rect(cx - half - 1, legTop, cx + half + 1, legTop + 3, h.body);
        if (h.apron) c.Rect(cx - 2, legTop - 1, cx + half, bodyTop - 5, new Color(0.62f, 0.55f, 0.42f));
        c.Rect(cx - half, bodyTop - 3, cx + half, bodyTop - 1, h.accent);   // 목도리 / 깃 / 띠
        c.Rect(cx - half, legTop + 4, cx + half, legTop + 5, h.dark);       // 허리띠

        // 등의 태엽 열쇠
        if (h.key)
        {
            c.Rect(cx - half - 4, bodyTop - 9, cx - half - 1, bodyTop - 8, h.accent);
            c.Ellipse(cx - half - 6, bodyTop - 6, 2.2f, 2.2f, h.accent);
            c.Ellipse(cx - half - 6, bodyTop - 11, 2.2f, 2.2f, h.accent);
        }

        // 머리
        float hy = bodyTop + h.headR - 1;
        float hx = cx + 1;
        DrawHead(c, h, hx, hy);

        // 앞팔 + 무기 (포즈에 따라 각도)
        Vector2 shoulder = new Vector2(cx + half - 1, bodyTop - 3);
        float armAngle = ArmAngle(pose);
        if (pose == Pose.Guard)
        {
            Vector2 hand = c.Ray(shoulder, 10f, 5f, 2.4f, h.body);
            c.Ellipse(hand.x, hand.y, 1.6f, 1.6f, h.skin);
            DrawGuard(c, h, hand);
            return;
        }
        Vector2 handPos = c.Ray(shoulder, armAngle, 7f, 2.4f, h.body);
        c.Ellipse(handPos.x, handPos.y, 1.6f, 1.6f, h.skin);
        DrawWeapon(c, h.weapon, handPos, WeaponAngle(pose));
        if (pose == Pose.Slash) c.Arc(handPos.x - 2f, handPos.y - 6f, 15f, 1.5f, -40f, 70f, new Color(1f, 1f, 1f, 0.55f));   // 베는 궤적
    }

    private static float ArmAngle(Pose pose)
    {
        switch (pose)
        {
            case Pose.Slash:  return 35f;
            case Pose.Thrust: return 0f;
            case Pose.Blunt:  return 75f;
            case Pose.Evade:  return -120f;
            case Pose.Hit:    return -100f;
            default:          return -65f;
        }
    }

    private static float WeaponAngle(Pose pose)
    {
        switch (pose)
        {
            case Pose.Slash:  return 10f;
            case Pose.Thrust: return 0f;
            case Pose.Blunt:  return 110f;
            case Pose.Evade:  return -150f;
            case Pose.Hit:    return -150f;
            default:          return 60f;
        }
    }

    private static void DrawHead(PixelCanvas c, Humanoid h, float hx, float hy)
    {
        float r = h.headR;
        switch (h.head)
        {
            case Head.Hood:
                c.Ellipse(hx - 1, hy + 1, r + 2.5f, r + 2.5f, Mul(h.body, 0.9f));
                c.Ellipse(hx + 1.5f, hy, r - 1.5f, r - 1.5f, Mul(h.skin, 0.55f));   // 그늘진 얼굴
                c.Set(Mathf.RoundToInt(hx + 3), Mathf.RoundToInt(hy), new Color(1f, 0.85f, 0.4f));
                c.Set(Mathf.RoundToInt(hx + 1), Mathf.RoundToInt(hy), new Color(1f, 0.85f, 0.4f));
                break;
            case Head.Helmet:
                c.Ellipse(hx, hy, r, r, h.skin);
                c.Ellipse(hx, hy + 2, r + 1, r - 1, Mul(h.body, 1.1f));
                c.Rect(Mathf.RoundToInt(hx - r - 1), Mathf.RoundToInt(hy), Mathf.RoundToInt(hx + r + 2), Mathf.RoundToInt(hy + 1), h.accent);
                c.Rect(Mathf.RoundToInt(hx + 1), Mathf.RoundToInt(hy - 2), Mathf.RoundToInt(hx + r), Mathf.RoundToInt(hy - 1), OutlineColor);   // 눈구멍
                c.Rect(Mathf.RoundToInt(hx - 1), Mathf.RoundToInt(hy + r + 1), Mathf.RoundToInt(hx), Mathf.RoundToInt(hy + r + 3), h.accent);   // 투구 장식
                break;
            case Head.Doll:
                c.Ellipse(hx, hy, r, r, h.skin);
                c.Ellipse(hx - 2, hy + 3, r - 1, r - 3, h.hair);   // 머리칼 (앞이마는 비움)
                c.Set(Mathf.RoundToInt(hx + 2), Mathf.RoundToInt(hy), OutlineColor);   // 단추 눈
                c.Set(Mathf.RoundToInt(hx + 5), Mathf.RoundToInt(hy), OutlineColor);
                c.Set(Mathf.RoundToInt(hx + 4), Mathf.RoundToInt(hy - 3), new Color(0.7f, 0.25f, 0.25f));
                c.Ellipse(hx - r + 1, hy + r - 1, 2.2f, 2.2f, h.accent);   // 리본
                break;
            case Head.Goggles:
                c.Ellipse(hx, hy, r, r, h.skin);
                c.Ellipse(hx - 2, hy + 3, r, r - 3, h.hair);
                c.Rect(Mathf.RoundToInt(hx - r), Mathf.RoundToInt(hy + 1), Mathf.RoundToInt(hx + r), Mathf.RoundToInt(hy + 1), OutlineColor);
                c.Ellipse(hx + 3.5f, hy + 1, 2f, 2f, h.accent);
                c.Set(Mathf.RoundToInt(hx + 3), Mathf.RoundToInt(hy + 1), new Color(0.7f, 0.9f, 1f));
                c.Rect(Mathf.RoundToInt(hx), Mathf.RoundToInt(hy - r + 1), Mathf.RoundToInt(hx + 3), Mathf.RoundToInt(hy - r + 2), h.hair);   // 수염
                break;
            default:
                c.Ellipse(hx, hy, r, r, h.skin);
                c.Ellipse(hx - 2, hy + 2.5f, r, r - 2, h.hair);
                c.Rect(Mathf.RoundToInt(hx - r), Mathf.RoundToInt(hy - 2), Mathf.RoundToInt(hx - r + 2), Mathf.RoundToInt(hy + 2), h.hair);   // 뒷머리
                c.Set(Mathf.RoundToInt(hx + 4), Mathf.RoundToInt(hy), OutlineColor);   // 눈
                c.Set(Mathf.RoundToInt(hx + 4), Mathf.RoundToInt(hy + 1), OutlineColor);
                break;
        }
    }

    private static void DrawWeapon(PixelCanvas c, Weapon w, Vector2 hand, float angle)
    {
        switch (w)
        {
            case Weapon.Blade:   // 시곗바늘 칼
                c.Ray(hand, angle + 180f, 2f, 2f, Brass);
                c.Ray(hand, angle + 90f, 2.5f, 1.5f, Brass);
                c.Ray(hand, angle - 90f, 2.5f, 1.5f, Brass);
                Vector2 tip = c.Ray(hand, angle, 15f, 2.2f, Steel);
                c.Ray(tip, angle, 2f, 1f, Color.white);
                break;
            case Weapon.Spear:
                c.Ray(hand, angle + 180f, 6f, 1.4f, new Color(0.4f, 0.28f, 0.16f));
                Vector2 end = c.Ray(hand, angle, 15f, 1.4f, new Color(0.4f, 0.28f, 0.16f));
                c.Ray(end, angle, 5f, 2.6f, Steel);
                break;
            case Weapon.Wrench:
                Vector2 head = c.Ray(hand, angle, 9f, 2f, Steel);
                c.Ellipse(head.x, head.y, 3f, 3f, Steel);
                c.Ellipse(head.x + Mathf.Cos(angle * Mathf.Deg2Rad) * 1.5f, head.y + Mathf.Sin(angle * Mathf.Deg2Rad) * 1.5f, 1.2f, 1.2f, Color.clear);
                break;
            case Weapon.Hammer:
                Vector2 top = c.Ray(hand, angle, 13f, 1.6f, new Color(0.4f, 0.28f, 0.16f));
                c.Ray(top, angle + 90f, 4f, 4.5f, Mul(Brass, 0.8f));
                c.Ray(top, angle - 90f, 4f, 4.5f, Mul(Brass, 0.8f));
                break;
        }
    }

    private static void DrawGuard(PixelCanvas c, Humanoid h, Vector2 hand)
    {
        // 무기를 세워 막거나 (사람) 팔을 엇갈려 막는다 (인형)
        if (h.weapon == Weapon.None)
        {
            c.Rect(Mathf.RoundToInt(hand.x), Mathf.RoundToInt(hand.y - 4), Mathf.RoundToInt(hand.x + 2), Mathf.RoundToInt(hand.y + 6), h.body);
            return;
        }
        c.Ellipse(hand.x + 3, hand.y, 3.5f, 8f, Mul(h.accent, 0.85f));   // 둥근 방패
        c.Ellipse(hand.x + 3, hand.y, 1.5f, 1.5f, Steel);
    }

    // ── 쥐 ──

    private static void DrawRat(PixelCanvas c, Color tint, Pose pose, bool thin)
    {
        int dx = pose == Pose.Thrust ? 4 : pose == Pose.Evade ? -4 : pose == Pose.Hit ? -3 : 0;
        int dy = pose == Pose.Blunt ? 5 : 0;
        float bodyRy = thin ? 8f : 10f;
        float cx = 22 + dx, cy = 12 + bodyRy * 0.6f + dy;
        Color dark = Mul(tint, 0.6f);
        dark.a = 1f;

        // 꼬리 (왼쪽으로 말려 올라간다)
        for (int i = 0; i < 18; i++)
        {
            float t = i / 17f;
            c.Set(Mathf.RoundToInt(cx - 11 - t * 9), Mathf.RoundToInt(cy - 6 + Mathf.Sin(t * 3.2f) * 6 + t * 4), new Color(0.75f, 0.5f, 0.5f));
        }
        // 발
        c.Rect(Mathf.RoundToInt(cx - 7), 1, Mathf.RoundToInt(cx - 4), 4, dark);
        c.Rect(Mathf.RoundToInt(cx + 4), 1, Mathf.RoundToInt(cx + 7), 4, dark);
        // 몸 (등이 굽은 타원) + 갈비뼈 (굶주림)
        c.Ellipse(cx, cy, 12f, bodyRy, tint);
        if (thin) for (int i = 0; i < 3; i++) c.Rect(Mathf.RoundToInt(cx - 3 + i * 3), Mathf.RoundToInt(cy - 4), Mathf.RoundToInt(cx - 3 + i * 3), Mathf.RoundToInt(cy + 1), dark);
        // 머리 + 주둥이
        float hx = cx + 11, hy = cy + 3 - (pose == Pose.Thrust ? 3 : 0);
        c.Ellipse(hx, hy, 7f, 6f, tint);
        c.Ellipse(hx + 6, hy - 1, 3.5f, 2.5f, tint);
        c.Set(Mathf.RoundToInt(hx + 9), Mathf.RoundToInt(hy - 1), new Color(0.85f, 0.45f, 0.5f));   // 코
        c.Ellipse(hx - 2, hy + 6, 2.8f, 3.2f, tint);   // 귀
        c.Ellipse(hx - 2, hy + 6, 1.4f, 1.8f, new Color(0.85f, 0.55f, 0.6f));
        c.Set(Mathf.RoundToInt(hx + 3), Mathf.RoundToInt(hy + 1), new Color(1f, 0.25f, 0.2f));   // 붉은 눈
        c.Rect(Mathf.RoundToInt(hx + 6), Mathf.RoundToInt(hy - 4), Mathf.RoundToInt(hx + 7), Mathf.RoundToInt(hy - 3), Color.white);   // 앞니

        // 앞발 (포즈)
        Vector2 paw = new Vector2(cx + 8, cy - 2);
        float a = pose == Pose.Slash ? 50f : pose == Pose.Blunt ? 80f : pose == Pose.Guard ? 100f : -60f;
        Vector2 end = c.Ray(paw, a, 7f, 2f, dark);
        if (pose == Pose.Slash || pose == Pose.Blunt)
            for (int i = -1; i <= 1; i++) c.Ray(end, a + i * 20f, 3f, 1f, Color.white);   // 발톱
        if (pose == Pose.Slash) c.Arc(end.x, end.y - 6, 12f, 1.5f, -30f, 60f, new Color(1f, 1f, 1f, 0.5f));
    }

    // ── 거머리 ──

    private static void DrawLeech(PixelCanvas c, Color tint, Pose pose)
    {
        float sway = pose == Pose.Thrust ? 5f : pose == Pose.Evade ? -5f : pose == Pose.Hit ? -3f : 2f;
        float x = 22;
        Color dark = Mul(tint, 0.6f);
        dark.a = 1f;
        int segments = pose == Pose.Blunt ? 7 : 6;
        for (int i = 0; i < segments; i++)
        {
            float t = i / (float)(segments - 1);
            float sx = x + Mathf.Sin(t * 2.4f) * sway * t;
            float r = 7.5f - t * 2.5f;
            c.Ellipse(sx, 5 + i * 6.5f, r + 1, r, i % 2 == 0 ? tint : Mul(tint, 0.88f) + new Color(0, 0, 0, 0.12f));
            c.Rect(Mathf.RoundToInt(sx - r + 1), Mathf.RoundToInt(5 + i * 6.5f - r + 1), Mathf.RoundToInt(sx + r - 1), Mathf.RoundToInt(5 + i * 6.5f - r + 1), dark);
        }
        // 입 (이빨 고리)
        float mx = x + sway + 2, my = 5 + (segments - 1) * 6.5f + 3;
        c.Ellipse(mx, my, 5f, 4f, Mul(tint, 0.8f) + new Color(0, 0, 0, 0.2f));
        c.Ellipse(mx, my, 3f, 2.4f, new Color(0.25f, 0.05f, 0.08f));
        for (int i = 0; i < 6; i++)
        {
            float a = i * 60f * Mathf.Deg2Rad;
            c.Set(Mathf.RoundToInt(mx + Mathf.Cos(a) * 2.6f), Mathf.RoundToInt(my + Mathf.Sin(a) * 2f), Color.white);
        }
        if (pose == Pose.Guard) c.Arc(x, 20, 14f, 1.2f, 100f, 260f, new Color(0.6f, 0.9f, 0.7f, 0.5f));   // 웅크림
    }

    // ── 종 ──

    private static void DrawBell(PixelCanvas c, Color tint, Pose pose)
    {
        float tilt = pose == Pose.Thrust ? 0.18f : pose == Pose.Blunt ? 0.28f : pose == Pose.Evade ? -0.2f : pose == Pose.Hit ? -0.15f : 0.05f;
        Color body = Color.Lerp(tint, Brass, 0.35f);
        float baseY = 8f;
        for (int y = 0; y < 40; y++)
        {
            float t = y / 39f;   // 0 = 입, 1 = 꼭대기
            float hw = Mathf.Lerp(16f, 7f, Mathf.Pow(t, 0.7f));
            float sx = 24 + (y - 20) * tilt;
            c.Rect(Mathf.RoundToInt(sx - hw), Mathf.RoundToInt(baseY + y), Mathf.RoundToInt(sx + hw), Mathf.RoundToInt(baseY + y), body);
        }
        c.Rect(Mathf.RoundToInt(24 - 20 * tilt - 18), Mathf.RoundToInt(baseY - 1), Mathf.RoundToInt(24 - 20 * tilt + 18), Mathf.RoundToInt(baseY + 1), body * 0.8f + new Color(0, 0, 0, 0.2f));   // 입술
        c.Arc(24 + 22 * tilt, baseY + 42, 4f, 2f, 0f, 180f, body);   // 고리
        c.Rect(Mathf.RoundToInt(24 - 12 - 4 * tilt), Mathf.RoundToInt(baseY + 12), Mathf.RoundToInt(24 + 12 - 4 * tilt), Mathf.RoundToInt(baseY + 13), Brass);   // 띠
        // 빛나는 두 눈
        Color eye = new Color(0.55f, 0.9f, 1f);
        c.Ellipse(24 + 4 * tilt - 4, baseY + 24, 1.8f, 2.4f, eye);
        c.Ellipse(24 + 4 * tilt + 4, baseY + 24, 1.8f, 2.4f, eye);
        // 추 (흔들림 방향 반대로)
        float clapX = 24 - tilt * 30f;
        c.Line(24, baseY + 6, clapX, baseY - 4, 1.4f, Mul(Steel, 0.7f));
        c.Ellipse(clapX, baseY - 5, 3f, 3f, Mul(Steel, 0.75f));
        // 물방울
        c.Set(10, 5, new Color(0.6f, 0.85f, 1f, 0.9f));
        c.Set(37, 3, new Color(0.6f, 0.85f, 1f, 0.9f));
        if (pose == Pose.Blunt) c.Arc(24, baseY + 10, 22f, 1.2f, 200f, 340f, new Color(1f, 1f, 1f, 0.5f));   // 울림
    }

    // ── 강철 수문장 (덩치 큰 갑옷) ──

    private static Humanoid Warden(Color tint)
    {
        return new Humanoid
        {
            body = tint, dark = Mul(tint, 0.5f), skin = Mul(tint, 0.85f), hair = Mul(tint, 0.6f),
            accent = new Color(0.62f, 0.32f, 0.16f), headR = 6, bodyW = 18, bodyH = 22, legH = 12, head = Head.Helmet, weapon = Weapon.Hammer,
        };
    }

    // ── 녹 먹는 벌레 ──

    private static void DrawBug(PixelCanvas c, Color tint, Pose pose)
    {
        int dx = pose == Pose.Thrust ? 4 : pose == Pose.Evade ? -4 : pose == Pose.Hit ? -2 : 0;
        float cx = 22 + dx, cy = 10 + (pose == Pose.Blunt ? 4 : 0);
        Color dark = Mul(tint, 0.55f);
        // 다리 6개
        for (int i = 0; i < 3; i++)
        {
            float lx = cx - 7 + i * 6;
            c.Line(lx, cy - 2, lx - 3, 1, 1.2f, dark);
            c.Line(lx + 1, cy - 2, lx + 4, 1, 1.2f, dark);
        }
        // 몸 (두 마디) + 녹 반점
        c.Ellipse(cx - 4, cy + 2, 9f, 7f, tint);
        c.Ellipse(cx + 7, cy + 3, 6f, 5f, Mul(tint, 0.9f));
        System.Random rng = new System.Random(7);
        for (int i = 0; i < 7; i++) c.Set(Mathf.RoundToInt(cx - 10 + rng.Next(14)), Mathf.RoundToInt(cy + rng.Next(8)), new Color(0.95f, 0.55f, 0.2f));
        // 큰 턱 + 더듬이
        float a = pose == Pose.Slash || pose == Pose.Thrust ? 10f : 30f;
        c.Ray(new Vector2(cx + 12, cy + 2), a, 5f, 1.6f, Mul(tint, 0.4f));
        c.Ray(new Vector2(cx + 12, cy + 4), -a, 5f, 1.6f, Mul(tint, 0.4f));
        c.Ray(new Vector2(cx + 9, cy + 7), 70f, 8f, 1f, dark);
        c.Ray(new Vector2(cx + 7, cy + 7), 100f, 7f, 1f, dark);
        c.Set(Mathf.RoundToInt(cx + 10), Mathf.RoundToInt(cy + 4), new Color(1f, 0.85f, 0.3f));   // 눈
    }

    // ── 철갑 게 ──

    private static void DrawCrab(PixelCanvas c, Color tint, Pose pose)
    {
        int dx = pose == Pose.Thrust ? 3 : pose == Pose.Evade ? -4 : pose == Pose.Hit ? -2 : 0;
        float cx = 24 + dx, cy = 14;
        Color dark = Mul(tint, 0.5f);
        for (int i = 0; i < 3; i++)   // 다리
        {
            c.Line(cx - 8 + i * 3, cy - 3, cx - 14 + i * 3, 1, 1.4f, dark);
            c.Line(cx + 8 - i * 3, cy - 3, cx + 14 - i * 3, 1, 1.4f, dark);
        }
        c.Ellipse(cx, cy, 13f, 8f, tint);                    // 등껍질
        c.Rect(Mathf.RoundToInt(cx - 10), Mathf.RoundToInt(cy + 2), Mathf.RoundToInt(cx + 10), Mathf.RoundToInt(cy + 2), Mul(tint, 1.25f));   // 리벳 줄
        for (int i = -2; i <= 2; i++) c.Set(Mathf.RoundToInt(cx + i * 4), Mathf.RoundToInt(cy + 4), Steel);
        // 눈자루
        c.Line(cx + 2, cy + 7, cx + 3, cy + 12, 1.2f, dark);
        c.Line(cx + 6, cy + 7, cx + 7, cy + 12, 1.2f, dark);
        c.Ellipse(cx + 3, cy + 13, 1.4f, 1.4f, Color.white);
        c.Ellipse(cx + 7, cy + 13, 1.4f, 1.4f, Color.white);
        // 집게 (막기 = 앞을 가림, 공격 = 위로 들어 올림)
        float lift = pose == Pose.Blunt || pose == Pose.Slash ? 12f : pose == Pose.Guard ? 4f : 6f;
        float reach = pose == Pose.Thrust ? 20f : pose == Pose.Guard ? 14f : 16f;
        Vector2 claw = new Vector2(cx + reach, cy + lift);
        c.Line(cx + 9, cy + 2, claw.x - 3, claw.y - 2, 2.4f, tint);
        c.Ellipse(claw.x, claw.y, 4.5f, 3.5f, Mul(tint, 1.1f));
        c.Line(claw.x + 1, claw.y + 1, claw.x + 6, claw.y + 4, 2f, Mul(tint, 1.1f));
        c.Line(claw.x + 1, claw.y - 1, claw.x + 6, claw.y - 3, 2f, Mul(tint, 1.1f));
        c.Line(cx - 9, cy + 2, cx - 14, cy + 6, 2.2f, tint);   // 뒷집게
        c.Ellipse(cx - 15, cy + 7, 3f, 2.5f, tint);
    }

    // ── 시계추 망령 ──

    private static void DrawWraith(PixelCanvas c, Color tint, Pose pose)
    {
        int dx = pose == Pose.Thrust ? 4 : pose == Pose.Evade ? -5 : pose == Pose.Hit ? -3 : 0;
        float cx = 24 + dx;
        Color cloak = new Color(tint.r, tint.g, tint.b, 0.92f);
        // 너울거리는 망토 (아래는 찢어진 끝자락)
        for (int y = 8; y < 46; y++)
        {
            float t = (y - 8) / 38f;
            float hw = Mathf.Lerp(12f, 6f, t) + Mathf.Sin(y * 0.6f) * 1f;
            c.Rect(Mathf.RoundToInt(cx - hw), y, Mathf.RoundToInt(cx + hw), y, cloak);
        }
        for (int i = 0; i < 5; i++) c.Line(cx - 10 + i * 5, 8, cx - 11 + i * 5, 2 + (i % 2) * 3, 2f, cloak);
        // 두건 속 얼굴 = 시계판
        c.Ellipse(cx + 1, 50, 8f, 8f, Mul(tint, 0.7f));
        c.Ellipse(cx + 2, 49, 5.5f, 5.5f, new Color(0.88f, 0.84f, 0.7f));
        c.Line(cx + 2, 49, cx + 2, 53, 1f, OutlineColor);
        c.Line(cx + 2, 49, cx + 5, 49, 1f, OutlineColor);
        // 가슴의 진자 (공격 자세마다 흔들린 각도)
        float swing = pose == Pose.Slash ? 50f : pose == Pose.Blunt ? 80f : pose == Pose.Thrust ? 20f : pose == Pose.Guard ? -10f : -20f;
        Vector2 pivot = new Vector2(cx + 2, 38);
        Vector2 bob = c.Ray(pivot, -90f + swing, 18f, 1.2f, Brass);
        c.Ellipse(bob.x, bob.y, 3.5f, 3.5f, Brass);
        if (pose == Pose.Slash || pose == Pose.Blunt) c.Arc(pivot.x, pivot.y, 18f, 1.4f, -90f, -90f + swing, new Color(1f, 1f, 1f, 0.5f));
    }

    // ── 뻐꾸기 시계 ──

    private static void DrawCuckoo(PixelCanvas c, Color tint, Pose pose)
    {
        int dx = pose == Pose.Evade ? -4 : pose == Pose.Hit ? -2 : 0;
        float cx = 24 + dx;
        Color wood = tint, dark = Mul(tint, 0.6f);
        // 다리 (태엽 다리 두 개)
        c.Rect(Mathf.RoundToInt(cx - 6), 1, Mathf.RoundToInt(cx - 4), 10, Steel);
        c.Rect(Mathf.RoundToInt(cx + 4), 1, Mathf.RoundToInt(cx + 6), 10, Steel);
        // 집 모양 몸 + 지붕
        c.Rect(Mathf.RoundToInt(cx - 11), 10, Mathf.RoundToInt(cx + 11), 38, wood);
        for (int i = 0; i < 10; i++) c.Rect(Mathf.RoundToInt(cx - 14 + i), 38 + i, Mathf.RoundToInt(cx + 14 - i), 38 + i, dark);
        // 시계판
        c.Ellipse(cx, 22, 7f, 7f, new Color(0.9f, 0.85f, 0.7f));
        float hour = pose == Pose.Idle ? 90f : 45f;
        c.Ray(new Vector2(cx, 22), hour, 5f, 1f, OutlineColor);
        c.Ray(new Vector2(cx, 22), -30f, 4f, 1.4f, OutlineColor);
        // 문과 튀어나온 새 (공격 땐 멀리 튀어나온다)
        float out_ = pose == Pose.Thrust || pose == Pose.Slash || pose == Pose.Blunt ? 9f : pose == Pose.Guard ? 0f : 4f;
        c.Rect(Mathf.RoundToInt(cx - 3), 31, Mathf.RoundToInt(cx + 3), 36, OutlineColor);
        if (out_ > 0f)
        {
            c.Line(cx, 33, cx + out_, 33, 1f, Steel);   // 스프링
            c.Ellipse(cx + out_ + 2, 34, 3.2f, 2.6f, new Color(0.85f, 0.75f, 0.45f));
            c.Line(cx + out_ + 5, 34, cx + out_ + 8, 33, 1.4f, new Color(1f, 0.6f, 0.2f));   // 부리
            c.Set(Mathf.RoundToInt(cx + out_ + 3), 35, OutlineColor);
        }
        // 추 두 개
        c.Line(cx - 5, 10, cx - 5, 4, 1f, Brass);
        c.Ellipse(cx - 5, 3, 1.6f, 2.4f, Brass);
    }

    // ── 종루 박쥐 ──

    private static void DrawBat(PixelCanvas c, Color tint, Pose pose)
    {
        int dx = pose == Pose.Thrust ? 5 : pose == Pose.Evade ? -5 : pose == Pose.Hit ? -3 : 0;
        float cx = 24 + dx, cy = 30;
        float flap = pose == Pose.Guard ? -12f : pose == Pose.Blunt ? 18f : pose == Pose.Slash ? 10f : 4f;   // 날개 각도
        Color wing = Mul(tint, 0.8f);
        for (int s = -1; s <= 1; s += 2)
        {
            Vector2 shoulder = new Vector2(cx + s * 3, cy + 2);
            Vector2 tip = shoulder + new Vector2(s * 18, flap);
            for (int i = 0; i <= 10; i++)   // 날개막: 어깨에서 끝으로 부채꼴
            {
                float t = i / 10f;
                Vector2 edge = Vector2.Lerp(shoulder + new Vector2(s * 4, -8), tip, t) + new Vector2(0, -Mathf.Sin(t * Mathf.PI * 3f) * 2f);
                c.Line(shoulder.x, shoulder.y, edge.x, edge.y, 1.4f, wing);
            }
            c.Line(shoulder.x, shoulder.y, tip.x, tip.y, 1.4f, Mul(tint, 0.5f));
        }
        c.Ellipse(cx, cy, 5f, 6f, tint);
        c.Ellipse(cx + 1, cy + 7, 4f, 3.5f, tint);
        c.Line(cx - 2, cy + 9, cx - 3, cy + 13, 1.4f, tint);   // 귀
        c.Line(cx + 3, cy + 9, cx + 4, cy + 13, 1.4f, tint);
        c.Set(Mathf.RoundToInt(cx), Mathf.RoundToInt(cy + 7), new Color(1f, 0.3f, 0.3f));
        c.Set(Mathf.RoundToInt(cx + 3), Mathf.RoundToInt(cy + 7), new Color(1f, 0.3f, 0.3f));
        c.Line(cx - 2, cy - 6, cx - 3, cy - 9, 1f, Mul(tint, 0.5f));   // 발
        c.Line(cx + 2, cy - 6, cx + 3, cy - 9, 1f, Mul(tint, 0.5f));
        if (pose == Pose.Blunt) c.Arc(cx + 6, cy + 6, 9f, 1.2f, -40f, 40f, new Color(0.8f, 0.9f, 1f, 0.6f));   // 초음파
    }

    // ─────────────────────────── 초상화 ───────────────────────────

    /// <summary>화자 초상화 (캐릭터 상반신을 3배로). '―' '???' 같은 화자는 null</summary>
    public static Sprite Portrait(string speaker, Color tint)
    {
        if (string.IsNullOrEmpty(speaker) || speaker == "―" || speaker.Contains("?")) return null;
        if (portraits.TryGetValue(speaker, out Sprite cached)) return cached;

        PixelCanvas body = DrawCharacter(speaker, tint, Pose.Idle);
        const int size = 80;
        PixelCanvas p = new PixelCanvas(size, size);

        // 바탕: 어두운 원 + 황동 테두리
        p.Ellipse(size / 2f, size / 2f, size / 2f - 1, size / 2f - 1, new Color(0.86f, 0.69f, 0.42f));
        p.Ellipse(size / 2f, size / 2f, size / 2f - 3, size / 2f - 3, new Color(0.09f, 0.08f, 0.1f));

        // 그림에서 머리 위쪽 영역을 찾아 상반신을 확대
        int top = 0;
        for (int y = CharH - 1; y >= 0 && top == 0; y--)
            for (int x = 0; x < CharW; x++) if (body.Get(x, y).a > 0.5f) { top = y; break; }
        int cropH = 24, cropW = 26;
        int srcY = Mathf.Max(0, top - cropH + 2);
        int srcX = CharW / 2 - cropW / 2 + 2;
        const int scale = 3;
        PixelCanvas bust = new PixelCanvas(size, size);
        bust.Blit(body, srcX, srcY, cropW, cropH, size / 2 - cropW * scale / 2, 0, scale);

        // 둥근 틀 안쪽만 남긴다
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Color c = bust.Get(x, y);
                float dx = x + 0.5f - size / 2f, dy = y + 0.5f - size / 2f;
                if (c.a > 0.5f && dx * dx + dy * dy < (size / 2f - 3) * (size / 2f - 3)) p.Set(x, y, c);
            }

        Texture2D tex = p.ToTexture("Portrait " + speaker);
        Sprite s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
        s.name = tex.name;
        portraits[speaker] = s;
        return s;
    }

    // ─────────────────────────── 태엽판 그림 ───────────────────────────
    //
    //   ┌──────────────────────────────┐  바탕: 등급 색이 은은한 어두운 판 + 흐릿한 시계 눈금
    //   │   (톱니메달)──(톱니메달)       │  주사위마다 메달 하나, 사슬로 잇는다
    //   │      참           관    ○○  │  공격 = 톱니 테두리, 수비 = 리벳 원판 / 오른쪽 아래 = 효과 칩
    //   └──────────────────────────────┘
    //   2배 크기로 그린 뒤 줄여서 테두리를 부드럽게 한다 (Hi = 2).

    private const int Hi = 2;
    private static readonly Color Ink = new Color(0.055f, 0.05f, 0.06f, 1f);

    public static Sprite CardArt(CardData card)
    {
        if (card == null) return null;
        if (cards.TryGetValue(card, out Sprite cached) && cached != null) return cached;
        Texture2D tex = DrawCard(card).ToTexture("Card " + card.cardName, FilterMode.Bilinear);
        Sprite s = Sprite.Create(tex, new Rect(0, 0, CardW, CardH), new Vector2(0.5f, 0.5f), 32f);
        s.name = tex.name;
        cards[card] = s;
        return s;
    }

    public static PixelCanvas DrawCard(CardData card)
    {
        int W = CardW * Hi, H = CardH * Hi;
        PixelCanvas c = new PixelCanvas(W, H);
        System.Random rng = new System.Random(Hash(card.cardName));
        Color rarity = DiceInfo.RarityColor(card.rarity);
        Color rarityDim = Mul(rarity, 0.3f);

        // 1) 바탕: 위는 등급 색이 은은하게, 아래로 갈수록 먹색
        for (int y = 0; y < H; y++)
        {
            Color row = Color.Lerp(Ink, Color.Lerp(Ink, rarity, 0.22f), y / (float)H);
            row.a = 1f;
            c.Rect(0, y, W - 1, y, row);
        }

        // 2) 흐릿한 시계 눈금 (카드마다 바늘 각도만 다르게)
        Vector2 dial = new Vector2(W * 0.5f, H * 0.5f);
        float dialR = H * 0.62f;
        Color faint = Color.Lerp(Ink, rarity, 0.16f);
        faint.a = 1f;
        c.Arc(dial.x, dial.y, dialR, 3f, 0f, 359.9f, faint);
        for (int i = 0; i < 12; i++)
        {
            float a = i * 30f * Mathf.Deg2Rad;
            Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            float len = i % 3 == 0 ? 14f : 7f;
            c.Line(dial.x + d.x * (dialR - 6 - len), dial.y + d.y * (dialR - 6 - len), dial.x + d.x * (dialR - 6), dial.y + d.y * (dialR - 6), i % 3 == 0 ? 4f : 2.5f, faint);
        }
        c.Ray(dial, (float)rng.NextDouble() * 360f, dialR * 0.55f, 4f, faint);
        c.Ray(dial, (float)rng.NextDouble() * 360f, dialR * 0.8f, 2.5f, faint);

        // 3) 안쪽 테두리 (등급 색)
        Color frame = Color.Lerp(Ink, rarity, 0.55f);
        frame.a = 1f;
        c.Rect(6, 6, W - 7, 7, frame); c.Rect(6, H - 8, W - 7, H - 7, frame);
        c.Rect(6, 6, 7, H - 7, frame); c.Rect(W - 8, 6, W - 7, H - 7, frame);
        foreach (Vector2 k in new[] { new Vector2(7, 7), new Vector2(W - 8, 7), new Vector2(7, H - 8), new Vector2(W - 8, H - 8) })
            c.Ellipse(k.x, k.y, 5f, 5f, frame);   // 모서리 리벳

        // 4) 주사위 메달 (최대 4개) + 사슬
        List<DiceData> dice = new List<DiceData>();
        if (card.diceList != null) foreach (DiceData d in card.diceList) if (d != null) dice.Add(d);
        int n = Mathf.Clamp(dice.Count, 1, 4);
        float R = n == 1 ? 58f : n == 2 ? 52f : n == 3 ? 44f : 36f;
        float cy = H * 0.5f + 4f;
        Vector2[] centers = new Vector2[n];
        for (int i = 0; i < n; i++) centers[i] = new Vector2(W * (i + 0.5f) / n, cy);
        Color chain = Mul(Brass, 0.55f);
        for (int i = 0; i + 1 < n; i++)
        {
            c.Line(centers[i].x, cy, centers[i + 1].x, cy, 7f, Mul(chain, 0.6f));
            for (float x = centers[i].x + R; x < centers[i + 1].x - R; x += 14f) c.Ellipse(x + 7f, cy, 6f, 4f, chain);   // 사슬 고리
        }
        for (int i = 0; i < n && i < dice.Count; i++) DrawMedallion(c, dice[i], centers[i], R, rng);

        // 5) 효과 칩 (오른쪽 아래)
        DrawEffectChips(c, dice, W, H);

        return c.Downsample(Hi);
    }

    private static Color DieColor(DiceDetail d)
    {
        ColorUtility.TryParseHtmlString(DiceInfo.ColorHex(d), out Color col);
        col.a = 1f;
        return col;
    }

    /// <summary>주사위 하나 = 메달 하나. 공격은 톱니 테두리, 수비는 리벳 박힌 원판</summary>
    private static void DrawMedallion(PixelCanvas c, DiceData die, Vector2 o, float R, System.Random rng)
    {
        Color col = DieColor(die.diceDetail);
        bool attack = die.diceType == DiceType.Attack;
        Color ring = attack ? Color.Lerp(Brass, col, 0.25f) : Color.Lerp(Steel, col, 0.3f);
        ring.a = 1f;

        PixelCanvas m = new PixelCanvas(c.width, c.height);
        if (attack)
        {
            // 톱니 12개
            for (int i = 0; i < 12; i++)
            {
                float a = (i * 30f + 15f) * Mathf.Deg2Rad;
                Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                m.Polygon(new[]
                {
                    o + Rot(d, 0.16f) * (R - 2), o + Rot(d, 0.1f) * (R + 9),
                    o + Rot(d, -0.1f) * (R + 9), o + Rot(d, -0.16f) * (R - 2),
                }, ring);
            }
        }
        m.Ellipse(o.x, o.y, R, R, ring);
        m.Ellipse(o.x, o.y, R - 7, R - 7, Mul(ring, 0.55f));
        m.Ellipse(o.x, o.y, R - 10, R - 10, new Color(0.075f, 0.068f, 0.08f, 1f));
        Color glow = Color.Lerp(new Color(0.075f, 0.068f, 0.08f), col, 0.16f);
        glow.a = 1f;
        m.Ellipse(o.x - R * 0.12f, o.y + R * 0.12f, R - 18, R - 18, glow);
        if (!attack)
            for (int i = 0; i < 4; i++)
            {
                float a = (i * 90f + 45f) * Mathf.Deg2Rad;
                m.Ellipse(o.x + Mathf.Cos(a) * (R - 4), o.y + Mathf.Sin(a) * (R - 4), 3.2f, 3.2f, Color.Lerp(ring, Color.white, 0.5f));
            }
        m.Arc(o.x, o.y, R - 1.5f, 2.5f, 100f, 170f, new Color(1f, 1f, 1f, 0.45f));   // 테두리 반짝임
        m.Outline(OutlineColor);
        m.Outline(OutlineColor);
        c.Layer(m);

        // 문양
        PixelCanvas icon = new PixelCanvas(c.width, c.height);
        float s = (R - 12) * 0.78f;
        switch (die.diceDetail)
        {
            case DiceDetail.Slash:  IconSlash(icon, o, s, col); break;
            case DiceDetail.Thrust: IconThrust(icon, o, s, col); break;
            case DiceDetail.Blunt:  IconBlunt(icon, o, s, col); break;
            case DiceDetail.Guard:  IconGuard(icon, o, s, col); break;
            default:                IconEvade(icon, o, s, col); break;
        }
        icon.Shade(0.3f, 0.3f);
        icon.Outline(OutlineColor);
        icon.Outline(OutlineColor);
        c.Layer(icon);
    }

    private static Vector2 Rot(Vector2 v, float rad)
    {
        float cs = Mathf.Cos(rad), sn = Mathf.Sin(rad);
        return new Vector2(v.x * cs - v.y * sn, v.x * sn + v.y * cs);
    }

    private static Vector2 Dir(float deg) { return new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad)); }

    /// <summary>참격: 초승달 칼날 + 베는 바람</summary>
    private static void IconSlash(PixelCanvas c, Vector2 o, float s, Color col)
    {
        c.Ellipse(o.x, o.y, s, s, col);
        c.EraseEllipse(o.x + s * 0.42f, o.y - s * 0.3f, s * 0.92f, s * 0.92f);
        c.Arc(o.x, o.y, s - 3f, 3f, 120f, 230f, Color.Lerp(col, Color.white, 0.75f));   // 날 끝 반짝임
        Color wind = new Color(1f, 1f, 1f, 0.55f);
        c.Line(o.x + s * 0.15f, o.y + s * 0.55f, o.x + s * 0.75f, o.y + s * 0.05f, 2.5f, wind);
        c.Line(o.x + s * 0.35f, o.y + s * 0.7f, o.x + s * 0.85f, o.y + s * 0.3f, 2f, wind);
    }

    /// <summary>관통: 시곗바늘 (마름모 끝 · 가는 대 · 축 · 균형추)</summary>
    private static void IconThrust(PixelCanvas c, Vector2 o, float s, Color col)
    {
        Vector2 d = Dir(40f), side = new Vector2(-d.y, d.x);
        Vector2 tip = o + d * s * 1.2f, tail = o - d * s * 0.95f;
        Vector2 bulge = o + d * s * 0.42f;
        Color edge = Color.Lerp(col, Color.white, 0.55f);
        // 굵은 시곗바늘: 마름모 날 + 대 + 꼬리 고리
        c.Polygon(new[] { tip, bulge + side * s * 0.42f, o + d * s * 0.02f + side * s * 0.13f, tail + side * s * 0.11f,
                          tail - side * s * 0.11f, o + d * s * 0.02f - side * s * 0.13f, bulge - side * s * 0.42f }, col);
        c.Polygon(new[] { tip, bulge + side * s * 0.42f, bulge + side * s * 0.12f, o + d * s * 0.62f }, edge);   // 빛 받는 쪽 날
        c.Ellipse(tail.x, tail.y, s * 0.26f, s * 0.26f, col);
        c.Ellipse(tail.x, tail.y, s * 0.12f, s * 0.12f, new Color(0.075f, 0.068f, 0.08f, 1f));
        Vector2 hub = o - d * s * 0.18f;
        c.Ellipse(hub.x, hub.y, s * 0.27f, s * 0.27f, Brass);
        c.Ellipse(hub.x, hub.y, s * 0.11f, s * 0.11f, Mul(Brass, 0.45f));
    }

    /// <summary>타격: 리벳 박힌 쇠망치 + 충격선</summary>
    private static void IconBlunt(PixelCanvas c, Vector2 o, float s, Color col)
    {
        Vector2 d = Dir(55f), side = new Vector2(-d.y, d.x);
        Vector2 grip = o - d * s * 0.95f, neck = o + d * s * 0.25f;
        c.Line(grip.x, grip.y, neck.x, neck.y, s * 0.2f, new Color(0.48f, 0.32f, 0.18f, 1f));
        c.Line(grip.x, grip.y, grip.x + d.x * s * 0.35f, grip.y + d.y * s * 0.35f, s * 0.24f, Mul(Brass, 0.7f));   // 손잡이 감개
        Vector2 head = o + d * s * 0.45f;
        Vector2 a = side * s * 0.62f, b = d * s * 0.3f;
        c.Polygon(new[] { head + a + b, head - a + b, head - a - b, head + a - b }, col);
        c.Polygon(new[] { head + a * 1.08f + b * 1.15f, head + a * 0.82f + b * 1.15f, head + a * 0.82f - b * 1.15f, head + a * 1.08f - b * 1.15f }, Mul(col, 0.7f));   // 망치 면
        foreach (float t in new[] { -0.35f, 0f, 0.35f }) c.Ellipse(head.x + side.x * s * t * 1.2f, head.y + side.y * s * t * 1.2f, s * 0.06f, s * 0.06f, Color.white);
        Vector2 hit = head + side * s * 0.95f;
        Color spark = new Color(1f, 1f, 0.85f, 0.7f);
        foreach (float ang in new[] { -40f, 0f, 40f })
        {
            Vector2 r = Rot(side, ang * Mathf.Deg2Rad);
            c.Line(hit.x + r.x * s * 0.1f, hit.y + r.y * s * 0.1f, hit.x + r.x * s * 0.35f, hit.y + r.y * s * 0.35f, 2.5f, spark);
        }
    }

    /// <summary>가드: 문장 방패 (가운데 띠 + 둥근 장식)</summary>
    private static void IconGuard(PixelCanvas c, Vector2 o, float s, Color col)
    {
        Vector2[] outer =
        {
            o + new Vector2(-0.78f, 0.82f) * s, o + new Vector2(0f, 0.95f) * s, o + new Vector2(0.78f, 0.82f) * s,
            o + new Vector2(0.72f, 0.05f) * s, o + new Vector2(0.38f, -0.6f) * s, o + new Vector2(0f, -0.95f) * s,
            o + new Vector2(-0.38f, -0.6f) * s, o + new Vector2(-0.72f, 0.05f) * s,
        };
        c.Polygon(outer, Color.Lerp(col, Color.white, 0.25f));
        Vector2[] inner = new Vector2[outer.Length];
        for (int i = 0; i < outer.Length; i++) inner[i] = o + (outer[i] - o) * 0.8f;
        c.Polygon(inner, col);
        c.Polygon(new[] { o + new Vector2(-0.1f, 0.78f) * s, o + new Vector2(0.1f, 0.78f) * s, o + new Vector2(0.1f, -0.78f) * s, o + new Vector2(-0.1f, -0.78f) * s }, Mul(Brass, 0.95f));
        c.Polygon(new[] { o + new Vector2(-0.6f, 0.3f) * s, o + new Vector2(0.6f, 0.3f) * s, o + new Vector2(0.58f, 0.12f) * s, o + new Vector2(-0.58f, 0.12f) * s }, Mul(Brass, 0.95f));
        c.Ellipse(o.x, o.y + 0.21f * s, s * 0.17f, s * 0.17f, Color.Lerp(Brass, Color.white, 0.35f));
    }

    /// <summary>회피: 세 겹으로 휘도는 바람 (잔상)</summary>
    private static void IconEvade(PixelCanvas c, Vector2 o, float s, Color col)
    {
        for (int i = 0; i < 3; i++)
        {
            Color k = Color.Lerp(col, Color.white, i * 0.15f);
            k.a = 1f - i * 0.22f;
            float r = s * (0.95f - i * 0.22f);
            c.Arc(o.x - i * s * 0.12f, o.y - i * s * 0.08f, r, s * 0.16f, 70f, 330f, k);
        }
        Vector2 tip = o + Dir(330f) * s * 0.95f;
        c.Polygon(new[] { tip + new Vector2(s * 0.28f, -s * 0.05f), tip + new Vector2(-s * 0.08f, s * 0.2f), tip + new Vector2(-s * 0.1f, -s * 0.22f) }, col);   // 바람 끝 화살
    }

    /// <summary>효과마다 작은 원 칩 하나 (오른쪽 아래부터 왼쪽으로)</summary>
    private static void DrawEffectChips(PixelCanvas c, List<DiceData> dice, int W, int H)
    {
        List<DiceEffectType> seen = new List<DiceEffectType>();
        foreach (DiceData d in dice)
        {
            if (d.effects == null) continue;
            foreach (DiceEffect fx in d.effects) if (fx != null && !seen.Contains(fx.effect)) seen.Add(fx.effect);
        }

        const float r = 19f;
        float x = W - 30f, y = 30f;
        foreach (DiceEffectType t in seen)
        {
            if (x < W * 0.55f) break;
            PixelCanvas chip = new PixelCanvas(W, H);
            chip.Ellipse(x, y, r, r, Mul(Brass, 0.6f));
            chip.Ellipse(x, y, r - 3, r - 3, new Color(0.08f, 0.07f, 0.09f, 1f));
            ChipIcon(chip, t, new Vector2(x, y), r - 4f);
            chip.Outline(OutlineColor);
            c.Layer(chip);
            x -= r * 2f + 6f;
        }
    }

    private static void ChipIcon(PixelCanvas c, DiceEffectType t, Vector2 o, float s)
    {
        switch (t)
        {
            case DiceEffectType.Rust:
                foreach (Vector2 p in new[] { new Vector2(-0.4f, 0.3f), new Vector2(0.3f, 0.45f), new Vector2(0f, -0.1f), new Vector2(-0.3f, -0.5f), new Vector2(0.45f, -0.35f) })
                    c.Ellipse(o.x + p.x * s, o.y + p.y * s, s * 0.22f, s * 0.22f, new Color(0.88f, 0.45f, 0.16f));
                break;
            case DiceEffectType.Heal:
                c.Rect(Mathf.RoundToInt(o.x - s * 0.2f), Mathf.RoundToInt(o.y - s * 0.75f), Mathf.RoundToInt(o.x + s * 0.2f), Mathf.RoundToInt(o.y + s * 0.75f), new Color(0.45f, 0.95f, 0.5f));
                c.Rect(Mathf.RoundToInt(o.x - s * 0.75f), Mathf.RoundToInt(o.y - s * 0.2f), Mathf.RoundToInt(o.x + s * 0.75f), Mathf.RoundToInt(o.y + s * 0.2f), new Color(0.45f, 0.95f, 0.5f));
                break;
            case DiceEffectType.DrawCard:
                c.Rect(Mathf.RoundToInt(o.x - s * 0.7f), Mathf.RoundToInt(o.y - s * 0.6f), Mathf.RoundToInt(o.x + s * 0.1f), Mathf.RoundToInt(o.y + s * 0.5f), new Color(0.85f, 0.85f, 0.8f));
                c.Rect(Mathf.RoundToInt(o.x - s * 0.2f), Mathf.RoundToInt(o.y - s * 0.4f), Mathf.RoundToInt(o.x + s * 0.65f), Mathf.RoundToInt(o.y + s * 0.75f), new Color(0.7f, 0.82f, 1f));
                break;
            case DiceEffectType.RecoverKi:
                c.Polygon(new[] { o + new Vector2(0f, 0.85f) * s, o + new Vector2(0.55f, 0f) * s, o + new Vector2(0f, -0.85f) * s, o + new Vector2(-0.55f, 0f) * s }, new Color(0.5f, 0.85f, 1f));
                break;
            case DiceEffectType.Strength:
            case DiceEffectType.PowerNextDie:
                for (int i = 0; i < 2; i++)
                    c.Polygon(new[] { o + new Vector2(-0.7f, -0.35f + i * 0.55f) * s, o + new Vector2(0f, 0.25f + i * 0.55f) * s, o + new Vector2(0.7f, -0.35f + i * 0.55f) * s, o + new Vector2(0f, -0.05f + i * 0.55f) * s }, new Color(1f, 0.62f, 0.3f));
                break;
            case DiceEffectType.Endurance:
                c.Polygon(new[] { o + new Vector2(-0.6f, 0.6f) * s, o + new Vector2(0.6f, 0.6f) * s, o + new Vector2(0.5f, -0.2f) * s, o + new Vector2(0f, -0.8f) * s, o + new Vector2(-0.5f, -0.2f) * s }, new Color(0.55f, 0.75f, 1f));
                break;
            case DiceEffectType.Paralysis:
                c.Polygon(new[] { o + new Vector2(0.15f, 0.9f) * s, o + new Vector2(-0.5f, -0.05f) * s, o + new Vector2(0f, -0.05f) * s, o + new Vector2(-0.2f, -0.9f) * s, o + new Vector2(0.5f, 0.15f) * s, o + new Vector2(0f, 0.15f) * s }, new Color(1f, 0.9f, 0.3f));
                break;
            case DiceEffectType.DestroyNextDie:
                c.Line(o.x - s * 0.6f, o.y - s * 0.6f, o.x + s * 0.6f, o.y + s * 0.6f, s * 0.28f, new Color(1f, 0.38f, 0.38f));
                c.Line(o.x - s * 0.6f, o.y + s * 0.6f, o.x + s * 0.6f, o.y - s * 0.6f, s * 0.28f, new Color(1f, 0.38f, 0.38f));
                break;
            case DiceEffectType.Overwind:   // 핏방울
                c.Ellipse(o.x, o.y - s * 0.25f, s * 0.5f, s * 0.5f, new Color(1f, 0.28f, 0.28f));
                c.Polygon(new[] { o + new Vector2(0f, 0.9f) * s, o + new Vector2(0.45f, -0.1f) * s, o + new Vector2(-0.45f, -0.1f) * s }, new Color(1f, 0.28f, 0.28f));
                break;
            case DiceEffectType.Resonance:  // 종 + 퍼지는 소리
                c.Polygon(new[] { o + new Vector2(-0.25f, 0.6f) * s, o + new Vector2(0.25f, 0.6f) * s, o + new Vector2(0.55f, -0.45f) * s, o + new Vector2(-0.55f, -0.45f) * s }, new Color(1f, 0.82f, 0.35f));
                c.Ellipse(o.x, o.y - s * 0.62f, s * 0.15f, s * 0.15f, new Color(1f, 0.82f, 0.35f));
                break;
            case DiceEffectType.Corrode:    // 녹물 방울 떨어짐
                c.Rect(Mathf.RoundToInt(o.x - s * 0.7f), Mathf.RoundToInt(o.y + s * 0.4f), Mathf.RoundToInt(o.x + s * 0.7f), Mathf.RoundToInt(o.y + s * 0.65f), new Color(0.85f, 0.42f, 0.15f));
                c.Ellipse(o.x - s * 0.2f, o.y - s * 0.2f, s * 0.22f, s * 0.32f, new Color(0.85f, 0.42f, 0.15f));
                c.Ellipse(o.x + s * 0.35f, o.y - s * 0.55f, s * 0.18f, s * 0.26f, new Color(0.85f, 0.42f, 0.15f));
                break;
            case DiceEffectType.Plating:    // 리벳 쇠판
                c.Rect(Mathf.RoundToInt(o.x - s * 0.65f), Mathf.RoundToInt(o.y - s * 0.65f), Mathf.RoundToInt(o.x + s * 0.65f), Mathf.RoundToInt(o.y + s * 0.65f), new Color(0.62f, 0.75f, 0.85f));
                foreach (Vector2 p in new[] { new Vector2(-0.45f, 0.45f), new Vector2(0.45f, 0.45f), new Vector2(-0.45f, -0.45f), new Vector2(0.45f, -0.45f) })
                    c.Ellipse(o.x + p.x * s, o.y + p.y * s, s * 0.12f, s * 0.12f, Color.white);
                break;
            case DiceEffectType.OnTheHour:  // 시계
                c.Ellipse(o.x, o.y, s * 0.85f, s * 0.85f, new Color(1f, 0.87f, 0.6f));
                c.Line(o.x, o.y, o.x, o.y + s * 0.6f, s * 0.16f, OutlineColor);
                c.Line(o.x, o.y, o.x + s * 0.45f, o.y, s * 0.16f, OutlineColor);
                break;
            default:   // 허약 / 취약: 아래 화살표
                c.Polygon(new[] { o + new Vector2(-0.6f, 0f) * s, o + new Vector2(0.6f, 0f) * s, o + new Vector2(0f, -0.85f) * s }, new Color(0.82f, 0.55f, 1f));
                c.Rect(Mathf.RoundToInt(o.x - s * 0.2f), Mathf.RoundToInt(o.y), Mathf.RoundToInt(o.x + s * 0.2f), Mathf.RoundToInt(o.y + s * 0.8f), new Color(0.82f, 0.55f, 1f));
                break;
        }
    }

    /// <summary>색을 어둡게/밝게 (Color * float은 투명도까지 곱해 버리므로 투명도는 1로 고정)</summary>
    private static Color Mul(Color c, float f)
    {
        return new Color(c.r * f, c.g * f, c.b * f, 1f);
    }
    private static int Hash(string s)
    {
        unchecked
        {
            int h = 17;
            foreach (char ch in s ?? "") h = h * 31 + ch;
            return h;
        }
    }
}
