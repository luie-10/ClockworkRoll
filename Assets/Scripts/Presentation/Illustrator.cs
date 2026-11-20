using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 그림이 비어 있는 곳을 채우는 '자동 일러스트' — 전부 코드로 그리는 픽셀 아트.
///
///   Character(이름, 색) : 캐릭터 스프라이트 + 합 포즈 7종 (오른쪽을 보는 그림, 발이 아래 가운데, 96x128)
///                        실제 그림은 SpritePainter(주인공 · 쥐)와 EnemyPainter(나머지 적)가 그린다
///                        이름으로 생김새를 고른다: 쥐 / 인형 / 경비·병정 / 거머리 / 시계공 / 종지기 / 종 / 이안 / 틱 / 그 밖의 사람
///   Portrait(화자)      : 대사창 초상화 (캐릭터 상반신을 크게)
///   CardArt(카드)       : 태엽판 그림 — 주사위 종류마다 문양 (참격 = 베는 호, 관통 = 시곗바늘, 타격 = 망치,
///                        가드 = 방패, 회피 = 잔상 소용돌이), 등급 색 바탕, 효과 표식
///
/// 같은 대상이면 같은 그림 (이름으로 시드를 정함) · 한 번 그린 것은 캐시.
/// 직접 그린 그림을 에셋에 넣으면 그쪽이 우선이다 (CardData.cardArt / EnemyData.sprite / 대사 초상화).
/// 에디터 메뉴 'ClockworkRoll/일러스트 굽기'로 PNG로 저장해 에셋에 연결할 수도 있다.
/// </summary>
public static class Illustrator
{
    public const int SpriteW = SpritePainter.W, SpriteH = SpritePainter.H;   // 캐릭터 스프라이트 크기 (모든 캐릭터 공통)
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
            Sprite s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0f), 64f);
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

    /// <summary>캐릭터 한 장 (96x128). 이름으로 생김새를 고르는 일과 그리기는 EnemyPainter / SpritePainter가 한다</summary>
    public static PixelCanvas DrawCharacter(string name, Color tint, Pose pose)
    {
        return EnemyPainter.Draw(name, tint, (SpritePainter.Pose)(int)pose);   // 같은 순서의 포즈
    }

    // ─────────────────────────── 초상화 ───────────────────────────

    /// <summary>화자 초상화 (캐릭터 상반신을 3배로). '―' '???' 같은 화자는 null</summary>
    public static Sprite Portrait(string speaker, Color tint)
    {
        if (string.IsNullOrEmpty(speaker) || speaker == "―" || speaker.Contains("?")) return null;
        if (portraits.TryGetValue(speaker, out Sprite cached)) return cached;

        PixelCanvas body = DrawCharacter(speaker, tint, Pose.Idle);
        const int size = 160;
        PixelCanvas p = new PixelCanvas(size, size);

        // 바탕: 어두운 원 + 황동 테두리
        p.Ellipse(size / 2f, size / 2f, size / 2f - 2, size / 2f - 2, new Color(0.86f, 0.69f, 0.42f));
        p.Ellipse(size / 2f, size / 2f, size / 2f - 5, size / 2f - 5, new Color(0.62f, 0.45f, 0.22f));
        p.Ellipse(size / 2f, size / 2f, size / 2f - 7, size / 2f - 7, new Color(0.09f, 0.08f, 0.1f));

        // 그림에서 머리 위쪽 영역을 찾아 상반신을 확대
        int top = 0;
        for (int y = body.height - 1; y >= 0 && top == 0; y--)
            for (int x = 0; x < body.width; x++) if (body.Get(x, y).a > 0.5f) { top = y; break; }
        int cropH = 48, cropW = 52;
        int srcY = Mathf.Max(0, top - cropH + 4);
        int srcX = body.width / 2 - cropW / 2 + 4;
        const int scale = 3;
        PixelCanvas bust = new PixelCanvas(size, size);
        bust.Blit(body, srcX, srcY, cropW, cropH, size / 2 - cropW * scale / 2, 0, scale);

        // 둥근 틀 안쪽만 남긴다
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Color c = bust.Get(x, y);
                float dx = x + 0.5f - size / 2f, dy = y + 0.5f - size / 2f;
                if (c.a > 0.5f && dx * dx + dy * dy < (size / 2f - 7) * (size / 2f - 7)) p.Set(x, y, c);
            }

        Texture2D tex = p.ToTexture("Portrait " + speaker);
        Sprite s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
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
            case DiceEffectType.MaxKi:
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
