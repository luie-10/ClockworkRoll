using UnityEngine;

/// <summary>
/// 작은 픽셀 아트를 코드로 그리는 도화지 (Illustrator가 사용).
/// 좌표는 왼쪽 아래가 (0,0), 위로 갈수록 y가 커진다 (Texture2D와 같음).
/// </summary>
public class PixelCanvas
{
    public readonly int width, height;
    private readonly Color[] px;

    /// <summary>켜면 이미 칠해진 칸 위에만 그려진다 (몸 안쪽의 무늬 · 명암을 테두리 밖으로 넘치지 않게 얹을 때)</summary>
    public bool Clip;

    public PixelCanvas(int width, int height)
    {
        this.width = width;
        this.height = height;
        px = new Color[width * height];
    }

    public Color Get(int x, int y)
    {
        return x < 0 || y < 0 || x >= width || y >= height ? Color.clear : px[y * width + x];
    }

    /// <summary>점 찍기 (반투명 색은 아래 색과 섞는다)</summary>
    public void Set(int x, int y, Color c)
    {
        if (x < 0 || y < 0 || x >= width || y >= height || c.a <= 0f) return;
        int i = y * width + x;
        if (Clip && px[i].a < 0.5f) return;
        px[i] = c.a >= 1f ? c : Color.Lerp(px[i], new Color(c.r, c.g, c.b, Mathf.Max(px[i].a, c.a)), c.a);
    }

    public void Fill(Color c)
    {
        for (int i = 0; i < px.Length; i++) px[i] = c;
    }

    public void Rect(int x0, int y0, int x1, int y1, Color c)
    {
        for (int y = Mathf.Min(y0, y1); y <= Mathf.Max(y0, y1); y++)
            for (int x = Mathf.Min(x0, x1); x <= Mathf.Max(x0, x1); x++) Set(x, y, c);
    }

    public void Ellipse(float cx, float cy, float rx, float ry, Color c)
    {
        for (int y = Mathf.FloorToInt(cy - ry); y <= Mathf.CeilToInt(cy + ry); y++)
        {
            for (int x = Mathf.FloorToInt(cx - rx); x <= Mathf.CeilToInt(cx + rx); x++)
            {
                float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                if (dx * dx + dy * dy <= 1f) Set(x, y, c);
            }
        }
    }

    /// <summary>원 테두리 (두께 thick), 각도 범위 a0~a1 (도, 0 = 오른쪽, 반시계)</summary>
    public void Arc(float cx, float cy, float r, float thick, float a0, float a1, Color c)
    {
        for (int y = Mathf.FloorToInt(cy - r - thick); y <= Mathf.CeilToInt(cy + r + thick); y++)
        {
            for (int x = Mathf.FloorToInt(cx - r - thick); x <= Mathf.CeilToInt(cx + r + thick); x++)
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (Mathf.Abs(d - r) > thick * 0.5f) continue;
                float a = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                if (a < 0f) a += 360f;
                if (InRange(a, a0, a1)) Set(x, y, c);
            }
        }
    }

    private static bool InRange(float a, float a0, float a1)
    {
        a0 = (a0 % 360f + 360f) % 360f;
        a1 = (a1 % 360f + 360f) % 360f;
        return a0 <= a1 ? a >= a0 && a <= a1 : a >= a0 || a <= a1;
    }

    public void Line(float x0, float y0, float x1, float y1, float thick, Color c)
    {
        float len = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
        int steps = Mathf.Max(1, Mathf.CeilToInt(len * 2f));
        float r = Mathf.Max(0.5f, thick * 0.5f);
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            float x = Mathf.Lerp(x0, x1, t), y = Mathf.Lerp(y0, y1, t);
            if (thick <= 1f) Set(Mathf.FloorToInt(x), Mathf.FloorToInt(y), c);
            else Ellipse(x, y, r, r, c);
        }
    }

    /// <summary>각도(도)와 길이로 선 긋기. 끝점을 돌려준다</summary>
    public Vector2 Ray(Vector2 from, float angle, float length, float thick, Color c)
    {
        Vector2 to = from + new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * length;
        Line(from.x, from.y, to.x, to.y, thick, c);
        return to;
    }

    /// <summary>입체감: 왼쪽 가장자리는 밝게, 오른쪽 가장자리는 어둡게 (빛이 왼쪽 위에서)</summary>
    public void Shade(float light = 0.22f, float dark = 0.25f)
    {
        Color[] src = (Color[])px.Clone();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color c = src[y * width + x];
                if (c.a < 0.5f) continue;
                bool leftOpen = x == 0 || src[y * width + x - 1].a < 0.5f;
                bool topOpen = y == height - 1 || src[(y + 1) * width + x].a < 0.5f;
                bool rightOpen = x == width - 1 || src[y * width + x + 1].a < 0.5f;
                if (leftOpen || topOpen)
                {
                    Color lit = Color.Lerp(c, Color.white, light);
                    lit.a = c.a;
                    px[y * width + x] = lit;
                }
                else if (rightOpen) px[y * width + x] = new Color(c.r * (1f - dark), c.g * (1f - dark), c.b * (1f - dark), c.a);
            }
        }
    }

    /// <summary>
    /// 덩어리감: 위쪽 가장자리 두 줄은 따뜻하게 밝히고, 아래쪽 가장자리 두 줄은 차갑게 어둡힌다 (빛이 위에서).
    /// 가장자리에 바로 붙은 줄은 세게, 그 안쪽 줄은 약하게 — 납작한 색 면이 둥글게 말려 보인다.
    /// </summary>
    public void Volume(float light = 0.3f, float dark = 0.36f)
    {
        Color[] src = (Color[])px.Clone();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color c = src[y * width + x];
                if (c.a < 0.5f) continue;

                float up = !Opaque(src, x, y + 1) ? 1f : (!Opaque(src, x, y + 2) || !Opaque(src, x - 1, y + 1)) ? 0.45f : 0f;
                float down = !Opaque(src, x, y - 1) ? 1f : (!Opaque(src, x, y - 2) || !Opaque(src, x + 1, y - 1)) ? 0.45f : 0f;
                if (up > 0f && down > 0f) continue;   // 가는 선(수염 · 칼날)은 그대로

                if (up > 0f)
                {
                    Color lit = Color.Lerp(c, new Color(1f, 0.96f, 0.84f), light * up);
                    lit.a = c.a;
                    px[y * width + x] = lit;
                }
                else if (down > 0f)
                {
                    float k = dark * down;
                    px[y * width + x] = new Color(c.r * (1f - k), c.g * (1f - k * 0.95f), c.b * (1f - k * 0.7f), c.a);
                }
            }
        }
    }

    /// <summary>
    /// 색 있는 테두리 한 줄: 검정 한 색이 아니라 맞닿은 면의 색을 짙게 한 색으로 두른다 (윤곽이 그림에 녹아든다).
    /// ink = 섞이는 먹색, tint = 맞닿은 색이 남는 정도 (0 = 먹색만)
    /// </summary>
    public void SoftOutline(Color ink, float tint = 0.3f)
    {
        Color[] src = (Color[])px.Clone();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (src[y * width + x].a >= 0.5f) continue;
                float r = 0f, g = 0f, b = 0f;
                int n = 0;
                for (int k = 0; k < 4; k++)
                {
                    int nx = x + (k == 0 ? -1 : k == 1 ? 1 : 0), ny = y + (k == 2 ? -1 : k == 3 ? 1 : 0);
                    if (!Opaque(src, nx, ny)) continue;
                    Color c = src[ny * width + nx];
                    r += c.r; g += c.g; b += c.b; n++;
                }
                if (n == 0) continue;
                px[y * width + x] = new Color(Mathf.Lerp(ink.r, r / n * 0.38f, tint), Mathf.Lerp(ink.g, g / n * 0.34f, tint), Mathf.Lerp(ink.b, b / n * 0.42f, tint), 1f);
            }
        }
    }

    /// <summary>
    /// 두 배로 키운다 (Scale2x): 계단진 대각선 모서리를 메워 넣어 그냥 늘린 것보다 윤곽이 매끈하다.
    /// 작은 도화지에 그린 그림을 큰 도화지 그림들과 같은 픽셀 크기로 맞출 때 쓴다.
    /// </summary>
    public PixelCanvas Upscale2x()
    {
        PixelCanvas big = new PixelCanvas(width * 2, height * 2);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color p = px[y * width + x];
                Color a = Get(x, y + 1), b = Get(x + 1, y), c = Get(x - 1, y), d = Get(x, y - 1);   // 위 · 오른쪽 · 왼쪽 · 아래
                Color tl = p, tr = p, bl = p, br = p;
                if (c == a && c != d && a != b) tl = a;
                if (a == b && a != c && b != d) tr = b;
                if (d == c && d != b && c != a) bl = c;
                if (b == d && b != a && d != c) br = d;
                int i = (y * 2) * big.width + x * 2;
                big.px[i] = bl; big.px[i + 1] = br;
                big.px[i + big.width] = tl; big.px[i + big.width + 1] = tr;
            }
        }
        return big;
    }

    /// <summary>바깥 테두리 한 줄 (투명한 칸 중 그림에 붙은 칸을 칠한다)</summary>
    public void Outline(Color c)
    {
        Color[] src = (Color[])px.Clone();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (src[y * width + x].a >= 0.5f) continue;
                bool near = Opaque(src, x - 1, y) || Opaque(src, x + 1, y) || Opaque(src, x, y - 1) || Opaque(src, x, y + 1);
                if (near) px[y * width + x] = c;
            }
        }
    }

    private bool Opaque(Color[] src, int x, int y)
    {
        return x >= 0 && y >= 0 && x < width && y < height && src[y * width + x].a >= 0.5f;
    }

    /// <summary>다른 도화지를 (dx, dy)에 scale배로 옮겨 그린다 (가장 가까운 점)</summary>
    public void Blit(PixelCanvas src, int srcX, int srcY, int srcW, int srcH, int dx, int dy, int scale)
    {
        for (int y = 0; y < srcH; y++)
            for (int x = 0; x < srcW; x++)
            {
                Color c = src.Get(srcX + x, srcY + y);
                if (c.a <= 0f) continue;
                Rect(dx + x * scale, dy + y * scale, dx + x * scale + scale - 1, dy + y * scale + scale - 1, c);
            }
    }

    /// <summary>다각형 채우기 (점들은 순서대로 둘레를 돈다)</summary>
    public void Polygon(Vector2[] pts, Color c)
    {
        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        foreach (Vector2 p in pts) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); }
        for (int y = Mathf.FloorToInt(minY); y <= Mathf.CeilToInt(maxY); y++)
        {
            for (int x = Mathf.FloorToInt(minX); x <= Mathf.CeilToInt(maxX); x++)
            {
                if (Inside(pts, x + 0.5f, y + 0.5f)) Set(x, y, c);
            }
        }
    }

    private static bool Inside(Vector2[] pts, float x, float y)
    {
        bool inside = false;
        for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
        {
            if ((pts[i].y > y) != (pts[j].y > y) &&
                x < (pts[j].x - pts[i].x) * (y - pts[i].y) / (pts[j].y - pts[i].y) + pts[i].x) inside = !inside;
        }
        return inside;
    }

    /// <summary>타원 영역을 지운다 (투명하게) — 초승달처럼 도려낸 모양을 만들 때</summary>
    public void EraseEllipse(float cx, float cy, float rx, float ry)
    {
        for (int y = Mathf.FloorToInt(cy - ry); y <= Mathf.CeilToInt(cy + ry); y++)
            for (int x = Mathf.FloorToInt(cx - rx); x <= Mathf.CeilToInt(cx + rx); x++)
            {
                if (x < 0 || y < 0 || x >= width || y >= height) continue;
                float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                if (dx * dx + dy * dy <= 1f) px[y * width + x] = Color.clear;
            }
    }

    /// <summary>다른 도화지를 같은 크기로 겹쳐 그린다 (반투명 섞임 포함)</summary>
    public void Layer(PixelCanvas top)
    {
        for (int y = 0; y < Mathf.Min(height, top.height); y++)
            for (int x = 0; x < Mathf.Min(width, top.width); x++)
            {
                Color c = top.px[y * top.width + x];
                if (c.a <= 0f) continue;
                int i = y * width + x;
                Color under = px[i];
                px[i] = new Color(Mathf.Lerp(under.r, c.r, c.a), Mathf.Lerp(under.g, c.g, c.a), Mathf.Lerp(under.b, c.b, c.a), Mathf.Max(under.a, c.a));
            }
    }

    /// <summary>factor배 작게 줄인다 (칸마다 평균) — 크게 그려서 줄이면 테두리가 부드러워진다</summary>
    public PixelCanvas Downsample(int factor)
    {
        PixelCanvas small = new PixelCanvas(width / factor, height / factor);
        float n = factor * factor;
        for (int y = 0; y < small.height; y++)
            for (int x = 0; x < small.width; x++)
            {
                float r = 0, g = 0, b = 0, a = 0;
                for (int sy = 0; sy < factor; sy++)
                    for (int sx = 0; sx < factor; sx++)
                    {
                        Color c = px[(y * factor + sy) * width + x * factor + sx];
                        r += c.r * c.a; g += c.g * c.a; b += c.b * c.a; a += c.a;   // 투명한 칸 색이 번지지 않게 알파 가중
                    }
                small.px[y * small.width + x] = a > 0f ? new Color(r / a, g / a, b / a, a / n) : Color.clear;
            }
        return small;
    }

    public Texture2D ToTexture(string name, FilterMode filter = FilterMode.Point)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = name,
            filterMode = filter,   // 캐릭터는 Point (픽셀이 뭉개지지 않게), 카드 그림은 Bilinear
            wrapMode = TextureWrapMode.Clamp,
        };
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }
}
