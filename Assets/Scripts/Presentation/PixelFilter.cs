using System;
using System.Collections.Generic;

/// <summary>
/// 픽셀 UI용 그림 손질 (엔진 없이 도는 순수 계산 — 에디터 밖에서도 미리 그려 볼 수 있다).
/// 색 배열은 RGBA float 4개씩, 아래 줄(y = 0)부터 (Unity의 GetPixels 순서와 같다).
///
///   Pixelate : 부드러운 그림을 block × block 칸으로 뭉쳐 각진 픽셀 그림으로 (가장자리 계단 · 색 단계)
///   Box      : 픽셀 UI 상자 — 모서리를 계단으로 따낸 판 / 테두리만 (9-slice 경계와 칸이 딱 맞게)
///   Bevel    : 주 버튼의 도톰한 판 (윗줄 밝게 · 아랫줄 그늘)
///   Shadow   : 상자 밖 두 칸짜리 계단 그림자 (속은 비어 있다)
/// PixelStyle이 이 결과를 텍스처에 덮어써서 UI 전체를 픽셀 판으로 바꾼다.
/// </summary>
public static class PixelFilter
{
    /// <summary>
    /// 부드러운 그림 → 픽셀 그림.
    /// alphaLevels = 1이면 칸마다 있다/없다(가장자리가 딱 끊긴다), 2 이상이면 그만큼의 투명도 단계 (그림자 · 번짐용).
    /// colorLevels = 0이면 색은 칸 평균 그대로, 2 이상이면 그만큼의 밝기 단계로 (명암이 띠처럼 나뉜다).
    /// </summary>
    public static float[] Pixelate(float[] src, int w, int h, int block, int alphaLevels, int colorLevels)
    {
        float[] dst = new float[src.Length];
        int bw = Math.Max(1, Math.Min(block, w)), bh = Math.Max(1, Math.Min(block, h));
        for (int by = 0; by < h; by += bh)
        {
            for (int bx = 0; bx < w; bx += bw)
            {
                // 칸 안의 평균 (색은 투명도를 곱해 평균 → 투명한 가장자리 색이 섞여 탁해지지 않게)
                float sr = 0f, sg = 0f, sb = 0f, sa = 0f;
                int n = 0;
                int ex = Math.Min(w, bx + bw), ey = Math.Min(h, by + bh);
                for (int y = by; y < ey; y++)
                {
                    for (int x = bx; x < ex; x++)
                    {
                        int i = (y * w + x) * 4;
                        float a = src[i + 3];
                        sr += src[i] * a; sg += src[i + 1] * a; sb += src[i + 2] * a; sa += a;
                        n++;
                    }
                }
                float avgA = sa / n;
                float qa = alphaLevels <= 1 ? (avgA >= 0.5f ? 1f : 0f) : (float)Math.Round(avgA * alphaLevels) / alphaLevels;
                float r = 1f, g = 1f, b = 1f;
                if (sa > 1e-5f) { r = sr / sa; g = sg / sa; b = sb / sa; }
                if (colorLevels >= 2) { r = Step(r, colorLevels); g = Step(g, colorLevels); b = Step(b, colorLevels); }

                for (int y = by; y < ey; y++)
                {
                    for (int x = bx; x < ex; x++)
                    {
                        int i = (y * w + x) * 4;
                        dst[i] = r; dst[i + 1] = g; dst[i + 2] = b; dst[i + 3] = qa;
                    }
                }
            }
        }
        return dst;
    }

    private static float Step(float v, int levels)
    {
        float s = (float)Math.Round(Clamp01(v) * (levels - 1)) / (levels - 1);
        return s;
    }

    // ─────────────────────────── 픽셀 상자 ───────────────────────────

    /// <summary>
    /// size × size 텍스처 위의 cell 크기 칸 격자에 상자를 그린다 (흰색).
    /// notch = 모서리에서 따낼 계단 수 (2면 모서리 칸 셋이 빈다 — 고전 픽셀 UI 모서리).
    /// frame = 0이면 꽉 찬 판, 1 이상이면 그 칸 수만큼의 테두리만.
    /// </summary>
    public static float[] Box(int size, int cell, int notch, int frame)
    {
        int g = size / cell;
        bool[,] inside = BoxShape(g, 0, notch);
        int[,] depth = frame > 0 ? Depth(inside, g) : null;

        float[] px = new float[size * size * 4];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int cx = x / cell, cy = y / cell;
                bool on = cx < g && cy < g && inside[cx, cy] && (frame <= 0 || depth[cx, cy] <= frame);
                Put(px, size, x, y, 1f, on ? 1f : 0f);
            }
        }
        return px;
    }

    /// <summary>주 버튼 판: 꽉 찬 상자 + 위쪽 한 칸 밝은 줄 + 아래쪽 한 칸 그늘 (위 절반이 아래 절반보다 한 단계 밝다)</summary>
    public static float[] Bevel(int size, int cell, int notch)
    {
        int g = size / cell;
        bool[,] inside = BoxShape(g, 0, notch);
        int[,] depth = Depth(inside, g);
        float[] px = new float[size * size * 4];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int cx = x / cell, cy = y / cell;
                if (cx >= g || cy >= g || !inside[cx, cy]) { Put(px, size, x, y, 1f, 0f); continue; }
                float v = cy >= g / 2 ? 0.92f : 0.8f;
                if (depth[cx, cy] <= 1) v = cy >= g / 2 ? 1f : 0.6f;
                Put(px, size, x, y, v, 1f);
            }
        }
        return px;
    }

    /// <summary>
    /// 상자 그림자: 텍스처 가장자리에서 pad만큼 들어온 곳이 상자. 상자 밖 한 칸은 진하게, 두 칸째는 옅게 (속은 비어 있다).
    /// 검은색이다 (SoftShadow와 같다).
    /// </summary>
    public static float[] Shadow(int size, int cell, int pad, int notch)
    {
        int g = size / cell, p = pad / cell;
        bool[,] box = BoxShape(g, p, notch);
        float[] px = new float[size * size * 4];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int cx = Math.Min(g - 1, x / cell), cy = Math.Min(g - 1, y / cell);
                float a = 0f;
                if (!box[cx, cy])
                {
                    int d = Ring(box, g, cx, cy);
                    a = d == 1 ? 0.75f : d == 2 ? 0.35f : 0f;
                }
                Put(px, size, x, y, 0f, a);
            }
        }
        return px;
    }

    /// <summary>g × g 격자에서 가장자리로부터 inset 칸 들어온 상자, 모서리 계단 notch</summary>
    private static bool[,] BoxShape(int g, int inset, int notch)
    {
        bool[,] s = new bool[g, g];
        int lo = inset, hi = g - 1 - inset;
        for (int y = 0; y < g; y++)
        {
            for (int x = 0; x < g; x++)
            {
                if (x < lo || x > hi || y < lo || y > hi) continue;
                int dx = Math.Min(x - lo, hi - x), dy = Math.Min(y - lo, hi - y);
                s[x, y] = dx + dy >= notch;
            }
        }
        return s;
    }

    /// <summary>안쪽 칸마다 바깥까지의 거리 (상하좌우로 몇 칸 걸어야 밖인가, 가장자리 칸 = 1)</summary>
    private static int[,] Depth(bool[,] inside, int g)
    {
        int[,] d = new int[g, g];
        Queue<int> q = new Queue<int>();
        for (int y = 0; y < g; y++)
        {
            for (int x = 0; x < g; x++)
            {
                d[x, y] = int.MaxValue;
                if (!inside[x, y]) continue;
                bool edge = x == 0 || y == 0 || x == g - 1 || y == g - 1 ||
                            !inside[x - 1, y] || !inside[x + 1, y] || !inside[x, y - 1] || !inside[x, y + 1];
                if (edge) { d[x, y] = 1; q.Enqueue(y * g + x); }
            }
        }
        int[] ox = { 1, -1, 0, 0 }, oy = { 0, 0, 1, -1 };
        while (q.Count > 0)
        {
            int c = q.Dequeue(), x = c % g, y = c / g;
            for (int k = 0; k < 4; k++)
            {
                int nx = x + ox[k], ny = y + oy[k];
                if (nx < 0 || ny < 0 || nx >= g || ny >= g || !inside[nx, ny] || d[nx, ny] <= d[x, y] + 1) continue;
                d[nx, ny] = d[x, y] + 1;
                q.Enqueue(ny * g + nx);
            }
        }
        return d;
    }

    /// <summary>상자 밖 칸이 상자에서 몇 칸 떨어졌나 (대각선도 한 칸, 3 이상은 3)</summary>
    private static int Ring(bool[,] box, int g, int cx, int cy)
    {
        for (int r = 1; r <= 2; r++)
        {
            for (int y = cy - r; y <= cy + r; y++)
            {
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (x < 0 || y < 0 || x >= g || y >= g) continue;
                    if (box[x, y]) return r;
                }
            }
        }
        return 3;
    }

    private static void Put(float[] px, int w, int x, int y, float v, float a)
    {
        int i = (y * w + x) * 4;
        px[i] = v; px[i + 1] = v; px[i + 2] = v; px[i + 3] = a;
    }

    private static float Clamp01(float v) { return v < 0f ? 0f : v > 1f ? 1f : v; }
}
