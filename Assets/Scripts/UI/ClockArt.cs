using UnityEngine;

/// <summary>
/// 시계판에 쓰는 그림을 코드로 그린다 (에셋 없이) — 황동 테두리가 둘린 검은 법랑 문자판, 속이 뚫린 바늘, 축 뚜껑, 칸 소켓.
/// Paint*() 는 색 배열만 만들고(엔진 없이도 돈다), 같은 이름의 Sprite 게터가 한 번 그려서 계속 쓴다.
/// ClockDial 이 사용한다.
/// </summary>
public static class ClockArt
{
    /// <summary>문자판 그림은 원판 바깥의 그림자까지 담는다 — 그림 크기 = 원판 지름 × FacePad</summary>
    public const float FacePad = 1.1f;

    // 문자판의 반지름 비율 (원판 반지름 = 1). ClockDial 의 칸 배치와 맞춘 값
    public const float InnerRingOuter = 0.668f, InnerRingInner = 0.628f;

    private static readonly Color BrassHi = new Color(1f, 0.9f, 0.64f);
    private static readonly Color BrassMid = new Color(0.8f, 0.62f, 0.33f);
    private static readonly Color BrassLo = new Color(0.3f, 0.21f, 0.1f);
    private static readonly Vector2 LightDir = new Vector2(-0.55f, 0.83f);   // 빛은 왼쪽 위에서

    private static Sprite face, hand, hub, ring, glow, socket, token;

    public static Sprite Face { get { return face != null ? face : face = Make("ClockFace", PaintFace(512), 512, 512, new Vector2(0.5f, 0.5f)); } }
    public static Sprite Hand { get { return hand != null ? hand : hand = Make("ClockHand", PaintHand(HandW, HandH), HandW, HandH, new Vector2(0.5f, HandPivot)); } }
    public static Sprite Hub { get { return hub != null ? hub : hub = Make("ClockHub", PaintHub(64), 64, 64, new Vector2(0.5f, 0.5f)); } }
    public static Sprite Ring { get { return ring != null ? ring : ring = Make("ClockRing", PaintRing(128, 0.86f, 0.97f, 0.015f), 128, 128, new Vector2(0.5f, 0.5f)); } }
    public static Sprite Glow { get { return glow != null ? glow : glow = Make("ClockGlow", PaintRing(128, 0.6f, 0.78f, 0.2f), 128, 128, new Vector2(0.5f, 0.5f)); } }
    public static Sprite Socket { get { return socket != null ? socket : socket = Make("ClockSocket", PaintDisc(128, 0.62f, 1f), 128, 128, new Vector2(0.5f, 0.5f)); } }
    public static Sprite Token { get { return token != null ? token : token = Make("ClockToken", PaintDisc(64, 1f, 0.8f), 64, 64, new Vector2(0.5f, 0.5f)); } }

    public const int HandW = 48, HandH = 288;
    public const float HandPivot = 48f / HandH;   // 축의 높이 (그 아래는 균형추 꼬리)

    private static Sprite Make(string name, Color[] px, int w, int h, Vector2 pivot)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        tex.hideFlags = HideFlags.HideAndDontSave;
        tex.SetPixels(px);
        tex.Apply();
        Sprite s = Sprite.Create(tex, new Rect(0, 0, w, h), pivot, 100f);
        s.name = name;
        return s;
    }

    // ─────────────────────────── 문자판 ───────────────────────────

    public static Color[] PaintFace(int size)
    {
        Color[] px = new Color[size * size];
        float half = size * 0.5f;
        float unit = half / FacePad;        // 원판 반지름(픽셀)
        float aa = 1.2f / unit;             // 가장자리를 부드럽게 하는 폭

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f - half) / unit, v = (y + 0.5f - half) / unit;
                float r = Mathf.Sqrt(u * u + v * v);
                float ang = Mathf.Atan2(u, v) * Mathf.Rad2Deg;   // 12시 = 0, 시계 방향
                if (ang < 0f) ang += 360f;
                Vector2 dir = r > 0.0001f ? new Vector2(u / r, v / r) : Vector2.up;

                Color c = Color.clear;

                // 바닥 그림자 (원판 밖으로 번진다, 아래쪽이 조금 더 짙다)
                float shadowR = Mathf.Sqrt(u * u + (v + 0.03f) * (v + 0.03f));
                Over(ref c, new Color(0f, 0f, 0f, 1f), 0.5f * Mathf.SmoothStep(FacePad, 0.96f, shadowR));

                // 칸이 놓이는 바깥 고리 (어두운 판)
                float track = 0.062f + 0.02f * Mathf.InverseLerp(InnerRingOuter, 1f, r);
                Over(ref c, new Color(track, track * 0.96f, track * 1.06f, 0.95f), Step(1f - r, aa));

                // 안쪽 문자판 (검푸른 법랑: 가운데가 조금 밝고, 고운 동심원 무늬)
                if (r < InnerRingInner + aa)
                {
                    float k = r / InnerRingInner;
                    float lum = Mathf.Lerp(0.105f, 0.04f, k * k) + 0.006f * Mathf.Sin(r * 260f);
                    lum *= Mathf.Lerp(1f, 0.5f, Mathf.SmoothStep(0.86f, 1f, k));   // 테 안쪽에 지는 그늘 → 움푹 들어가 보인다
                    Color enamel = new Color(lum * 0.92f, lum * 0.95f, lum * 1.22f, 0.97f);
                    Over(ref c, enamel, Step(InnerRingInner - r, aa));

                    // 눈금: 60개, 다섯째마다 길고 굵게
                    float minute = ang / 6f;
                    float off = Mathf.Abs(minute - Mathf.Round(minute)) * 6f * Mathf.Deg2Rad * r;   // 눈금 중심선까지의 거리
                    bool major = Mathf.RoundToInt(minute) % 5 == 0;
                    float tickIn = major ? 0.535f : 0.575f, tickHalf = major ? 0.008f : 0.0035f;
                    float tick = Step(tickHalf - off, aa) * Step(r - tickIn, aa) * Step(0.606f - r, aa);
                    Over(ref c, major ? new Color(0.95f, 0.8f, 0.5f, 0.95f) : new Color(0.8f, 0.66f, 0.42f, 0.6f), tick);

                    // 얇은 장식 원 두 줄
                    Over(ref c, new Color(0.85f, 0.68f, 0.4f, 0.28f), Step(0.004f - Mathf.Abs(r - 0.5f), aa));
                    Over(ref c, new Color(0.85f, 0.68f, 0.4f, 0.16f), Step(0.003f - Mathf.Abs(r - 0.2f), aa));

                    // 유리에 비친 빛 (왼쪽 위의 초승달)
                    float du = u - 0.14f, dv = v + 0.2f;
                    float crescent = Mathf.SmoothStep(0.56f, 0.7f, Mathf.Sqrt(du * du + dv * dv)) * Step(InnerRingInner - 0.02f - r, 0.03f);
                    Over(ref c, new Color(0.75f, 0.85f, 1f, 1f), 0.075f * crescent);
                }

                // 황동 테 두 줄 (안쪽 문자판 둘레 / 바깥 가장자리)
                Bezel(ref c, r, dir, InnerRingInner, InnerRingOuter, aa);
                Bezel(ref c, r, dir, 0.955f, 1f, aa);

                px[y * size + x] = c;
            }
        }
        return px;
    }

    /// <summary>둥근 단면의 황동 테 (r0~r1). 빛을 받는 쪽은 밝고 반대쪽은 어둡다</summary>
    private static void Bezel(ref Color c, float r, Vector2 dir, float r0, float r1, float aa)
    {
        float cover = Step(r - r0, aa) * Step(r1 - r, aa);
        if (cover <= 0f) return;

        float p = Mathf.InverseLerp(r0, r1, r) * 2f - 1f;                 // -1(안쪽 가장자리) ~ 1(바깥 가장자리)
        float facing = Vector2.Dot(dir, LightDir) * p;                    // 테의 바깥 경사는 바깥을, 안쪽 경사는 안을 본다
        float shade = Mathf.Clamp01(0.5f + 0.55f * facing + 0.25f * (1f - p * p));
        Color brass = shade < 0.5f ? Color.Lerp(BrassLo, BrassMid, shade * 2f) : Color.Lerp(BrassMid, BrassHi, shade * 2f - 1f);
        brass.a = 1f;
        Over(ref c, brass, cover);
    }

    // ─────────────────────────── 바늘 · 축 ───────────────────────────

    /// <summary>속이 뚫린 창 모양 바늘 (위가 끝). 축은 아래에서 HandPivot 높이, 그 아래는 균형추</summary>
    public static Color[] PaintHand(int w, int h)
    {
        Color[] px = new Color[w * h];
        float cx = w * 0.5f, pivot = h * HandPivot;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = x + 0.5f - cx, py = y + 0.5f;
                float d;   // 바늘 가장자리까지의 거리 (안쪽이 +)
                float hole = -1f;

                if (py >= pivot)
                {
                    float t = (py - pivot) / (h - pivot);                      // 0 = 축, 1 = 끝
                    float bulge = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.Clamp01(t / 0.72f) * Mathf.PI)), 1.6f) * 4.2f;
                    float halfW = Mathf.Lerp(6f, 0.9f, t) + bulge;
                    d = halfW - Mathf.Abs(dx);
                    if (t > 0.1f && t < 0.52f)                                 // 가운데를 길게 뚫는다
                        hole = (halfW - 3.6f) - Mathf.Abs(dx) - Mathf.Max(0f, 3f - (t - 0.1f) * 236f) - Mathf.Max(0f, 3f - (0.52f - t) * 236f);
                }
                else
                {
                    float stem = 3.2f - Mathf.Abs(dx);                         // 꼬리 막대
                    float weight = 8.5f - Mathf.Sqrt(dx * dx + (py - 15f) * (py - 15f));   // 균형추 (둥근 알)
                    d = Mathf.Max(py > 15f ? stem : -1f, weight);
                    hole = 3.6f - Mathf.Sqrt(dx * dx + (py - 15f) * (py - 15f));
                }

                float cover = Mathf.Clamp01(d + 0.5f) * (1f - Mathf.Clamp01(hole + 0.5f));
                if (cover <= 0f) continue;

                // 왼쪽 면은 밝게, 오른쪽 면은 어둡게 접힌 금속 + 가장자리는 어둡게
                float side = Mathf.Clamp(dx / 7f, -1f, 1f);
                Color metal = side < 0f ? Color.Lerp(BrassMid, BrassHi, -side * 0.9f + 0.1f) : Color.Lerp(BrassMid, BrassLo, side * 0.55f);
                float rim = Mathf.Clamp01(1.4f - Mathf.Min(d, hole > -900f && hole < 0f ? -hole : d));
                metal = Color.Lerp(metal, BrassLo, rim * 0.55f);
                metal.a = cover;
                px[y * w + x] = metal;
            }
        }
        return px;
    }

    /// <summary>바늘 축의 뚜껑: 볼록한 황동 단추 + 가운데 나사</summary>
    public static Color[] PaintHub(int size)
    {
        Color[] px = new Color[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f - half) / half, v = (y + 0.5f - half) / half;
                float r = Mathf.Sqrt(u * u + v * v);
                float cover = Mathf.Clamp01((0.96f - r) * half);
                if (cover <= 0f) continue;

                Vector2 dir = r > 0.001f ? new Vector2(u / r, v / r) : Vector2.zero;
                float shade = Mathf.Clamp01(0.62f + 0.6f * Vector2.Dot(dir, LightDir) * r + 0.25f * (1f - r));
                Color c = shade < 0.5f ? Color.Lerp(BrassLo, BrassMid, shade * 2f) : Color.Lerp(BrassMid, BrassHi, shade * 2f - 1f);
                c = Color.Lerp(c, BrassLo, Mathf.SmoothStep(0.82f, 0.96f, r) * 0.8f);                      // 둘레의 어두운 선
                c = Color.Lerp(c, new Color(0.12f, 0.08f, 0.05f), Mathf.Clamp01((0.15f - r) * half));     // 나사 구멍
                float spec = Mathf.Clamp01(1f - new Vector2(u + 0.32f, v - 0.4f).magnitude / 0.22f);
                c = Color.Lerp(c, Color.white, spec * spec * 0.8f);
                c.a = cover;
                px[y * size + x] = c;
            }
        }
        return px;
    }

    // ─────────────────────────── 칸 부품 (흰색 — Image.color 로 색을 입힌다) ───────────────────────────

    /// <summary>고리. inner~outer = 반지름 비율, soft = 가장자리가 번지는 폭 (크면 빛무리)</summary>
    public static Color[] PaintRing(int size, float inner, float outer, float soft)
    {
        Color[] px = new Color[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f - half) / half, v = (y + 0.5f - half) / half;
                float r = Mathf.Sqrt(u * u + v * v);
                float a = Mathf.Clamp01((r - inner) / soft + 0.5f) * Mathf.Clamp01((outer - r) / soft + 0.5f);
                px[y * size + x] = new Color(1f, 1f, 1f, soft > 0.1f ? a * a : a);
            }
        }
        return px;
    }

    /// <summary>원판. 위쪽 밝기 top → 아래쪽 밝기 bottom (top이 어두우면 움푹 들어간 소켓, 밝으면 볼록한 패)</summary>
    public static Color[] PaintDisc(int size, float top, float bottom)
    {
        Color[] px = new Color[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f - half) / half, v = (y + 0.5f - half) / half;
                float r = Mathf.Sqrt(u * u + v * v);
                float lum = Mathf.Lerp(bottom, top, Mathf.SmoothStep(-0.9f, 0.9f, v + (top < bottom ? r * r * 0.5f : -r * r * 0.3f)));
                px[y * size + x] = new Color(lum, lum, lum, Mathf.Clamp01((0.98f - r) * half));
            }
        }
        return px;
    }

    // ─────────────────────────── 도우미 ───────────────────────────

    /// <summary>d가 0을 넘는 곳이 1, 경계는 폭 aa로 부드럽게</summary>
    private static float Step(float d, float aa)
    {
        return Mathf.Clamp01(d / aa + 0.5f);
    }

    /// <summary>src를 dst 위에 덮는다 (coverage = 덮이는 비율)</summary>
    private static void Over(ref Color dst, Color src, float coverage)
    {
        float a = src.a * Mathf.Clamp01(coverage);
        if (a <= 0f) return;
        float outA = a + dst.a * (1f - a);
        dst = new Color((src.r * a + dst.r * dst.a * (1f - a)) / outA, (src.g * a + dst.g * dst.a * (1f - a)) / outA,
                        (src.b * a + dst.b * dst.a * (1f - a)) / outA, outA);
    }
}
