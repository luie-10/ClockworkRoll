using System;
using System.Collections.Generic;

/// <summary>
/// 코드로 문양을 그리는 작은 붓 (에셋 없이). 도형을 거리 함수(SDF)로 정의해서 가장자리가 매끈하다.
///
/// 좌표: 가운데가 (0,0), 오른쪽 +x, 위쪽 +y, 그림 전체가 -1 ~ 1.
/// 결과는 불투명도(0~1) 배열 — 흰색 문양으로 만들어 두고 쓰는 쪽에서 색을 입힌다 (Image.color).
/// UnityEngine을 쓰지 않으므로 에디터 밖에서도 그대로 그려 볼 수 있다. 스프라이트로 바꾸는 건 RouteIcons가 한다.
///
/// 새 문양은 Draw()의 switch에 추가한다.
/// </summary>
public class IconPainter
{
    public delegate float Sdf(float x, float y);

    public readonly int Size;
    /// <summary>불투명도. [y * Size + x], y = 0이 맨 아래 줄</summary>
    public readonly float[] Alpha;

    private float tx, ty, cos = 1f, sin, scale = 1f;

    public IconPainter(int size)
    {
        Size = size;
        Alpha = new float[size * size];
    }

    // ─────────────────────────── 문양 목록 ───────────────────────────

    public const string Fight = "fight", Elite = "elite", Rest = "rest", Event = "event", Remove = "remove",
                        Treasure = "treasure", Boss = "boss", Start = "start", Gear = "gear", GearSmall = "gear-small", ClockFace = "clock";

    // 부품 (RunItems) 그림
    public const string ItemSpring = "item-spring", ItemBarrel = "item-barrel", ItemCog = "item-cog", ItemPlate = "item-plate",
                        ItemOilCan = "item-oilcan", ItemMedkit = "item-medkit", ItemWhetstone = "item-whetstone",
                        ItemCoil = "item-coil", ItemRustOil = "item-rustoil";

    public static readonly string[] ItemIcons =
    {
        ItemSpring, ItemBarrel, ItemCog, ItemPlate, ItemOilCan, ItemMedkit, ItemWhetstone, ItemCoil, ItemRustOil,
    };

    public static float[] Draw(string kind, int size)
    {
        IconPainter p = new IconPainter(size);
        switch (kind)
        {
            case Fight:     p.CrossedSwords(); break;
            case Elite:     p.HornedSkull(); break;
            case Rest:      p.Lantern(); break;
            case Event:     p.KeyholeDoor(); break;
            case Remove:    p.BrokenGear(); break;
            case Treasure:  p.Chest(); break;
            case Boss:      p.Bell(); break;
            case Start:     p.Chevrons(); break;
            case Gear:      p.GearWheel(12, 0.74f, 0.2f, true); break;
            case GearSmall: p.GearWheel(8, 0.68f, 0.24f, false); break;
            case ClockFace: p.Clock(); break;

            case ItemSpring:    p.Mainspring(); break;
            case ItemBarrel:    p.SpringBarrel(); break;
            case ItemCog:       p.BrassCog(); break;
            case ItemPlate:     p.PatchedPlate(); break;
            case ItemOilCan:    p.OilCan(); break;
            case ItemMedkit:    p.Medkit(); break;
            case ItemWhetstone: p.Whetstone(); break;
            case ItemCoil:      p.CoilSpring(); break;
            case ItemRustOil:   p.RustOilFlask(); break;
        }
        return p.Alpha;
    }

    // ─────────────────────────── 부품 그림 ───────────────────────────

    // ── 여분의 태엽: 통 안에 감긴 태엽 (소용돌이) ──
    private void Mainspring()
    {
        Fill(Ring(0f, 0f, 0.86f, 0.065f));
        const int steps = 96;
        float px = 0f, py = 0f;
        for (int i = 0; i <= steps; i++)
        {
            double a = i / (double)steps * Math.PI * 2.0 * 2.6 + Math.PI * 0.5;   // 2.6바퀴
            float r = 0.14f + 0.66f * i / steps;
            float x = (float)(r * Math.Cos(a)), y = (float)(r * Math.Sin(a));
            if (i > 0) Fill(Seg(px, py, x, y, 0.05f));
            px = x; py = y;
        }
        Fill(Circle(0f, 0f, 0.17f));
        Cut(Box(0f, 0f, 0.06f, 0.06f, 0f));   // 감개가 들어가는 네모 구멍
    }

    // ── 무거운 태엽통: 쇠띠 두른 통 + 위에 꽂힌 감개 ──
    private void SpringBarrel()
    {
        Fill(Box(0f, 0.38f, 0.055f, 0.22f, 0f));           // 감개 축
        Fill(Ring(-0.21f, 0.74f, 0.16f, 0.055f));          // 감개 날개 (고리 둘)
        Fill(Ring(0.21f, 0.74f, 0.16f, 0.055f));
        Fill(Box(0f, -0.3f, 0.64f, 0.56f, 0.14f));         // 통
        Cut(Seg(-0.7f, 0.0f, 0.7f, 0.0f, 0.026f));         // 쇠띠
        Cut(Seg(-0.7f, -0.6f, 0.7f, -0.6f, 0.026f));
        Cut(Circle(0f, -0.3f, 0.17f));                     // 가운데 축 구멍
        Fill(Circle(0f, -0.3f, 0.08f));
        Cut(Circle(-0.46f, -0.3f, 0.045f));                // 대갈못
        Cut(Circle(0.46f, -0.3f, 0.045f));
    }

    // ── 황동 톱니: 속을 한 겹 파낸 굵은 톱니바퀴 ──
    private void BrassCog()
    {
        Teeth(8, 0.56f, 0.24f, 0.15f);
        Fill(Circle(0f, 0f, 0.64f));
        Cut(Ring(0f, 0f, 0.43f, 0.035f));
        Cut(Circle(0f, 0f, 0.17f));
        for (int i = 0; i < 4; i++)                        // 십자 살 사이의 작은 구멍
        {
            Transform(0f, 0f, 45f + i * 90f);
            Cut(Circle(0f, 0.3f, 0.055f));
        }
        Reset();
    }

    // ── 덧댄 철판: 방패꼴 철판 위에 비뚤게 덧댄 조각과 대갈못 ──
    private void PatchedPlate()
    {
        Fill(Poly(-0.72f, 0.82f, 0.72f, 0.82f, 0.72f, -0.08f, 0.36f, -0.62f, 0f, -0.9f, -0.36f, -0.62f, -0.72f, -0.08f));
        Cut(Circle(-0.52f, 0.63f, 0.06f));                 // 모서리 대갈못
        Cut(Circle(0.52f, 0.63f, 0.06f));
        Cut(Circle(0f, -0.66f, 0.06f));

        Transform(0.04f, 0.12f, 14f);                      // 덧댄 조각
        Cut(Box(0f, 0f, 0.4f, 0.31f, 0.04f));
        Fill(Box(0f, 0f, 0.34f, 0.25f, 0.03f));
        Cut(Circle(-0.24f, 0.15f, 0.045f));
        Cut(Circle(0.24f, 0.15f, 0.045f));
        Cut(Circle(-0.24f, -0.15f, 0.045f));
        Cut(Circle(0.24f, -0.15f, 0.045f));
        Reset();
    }

    // ── 기름 깡통: 긴 주둥이 달린 기름통 + 떨어지는 기름 한 방울 ──
    private void OilCan()
    {
        Fill(Ring(-0.58f, -0.3f, 0.26f, 0.06f));                                   // 손잡이
        Fill(Seg(0.16f, -0.1f, 0.74f, 0.66f, 0.06f));                              // 주둥이
        Fill(Poly(-0.62f, -0.84f, 0.4f, -0.84f, 0.28f, 0.04f, -0.5f, 0.04f));      // 몸통
        Fill(Box(-0.11f, 0.13f, 0.2f, 0.1f, 0.04f));                               // 마개
        Cut(Seg(-0.7f, -0.56f, 0.5f, -0.56f, 0.026f));                             // 띠
        Cut(Seg(-0.7f, -0.2f, 0.5f, -0.2f, 0.026f));
        Drop(0.82f, 0.22f, 0.1f);
    }

    /// <summary>물방울 (cx, cy = 둥근 아랫부분의 가운데, r = 반지름)</summary>
    private void Drop(float cx, float cy, float r)
    {
        Fill(Circle(cx, cy, r));
        Fill(Poly(cx - r * 0.86f, cy + r * 0.5f, cx + r * 0.86f, cy + r * 0.5f, cx, cy + r * 2.3f));
    }

    // ── 시계공의 구급함: 손잡이 달린 상자 + 십자 ──
    private void Medkit()
    {
        Fill(Ring(0f, 0.42f, 0.27f, 0.06f));               // 손잡이 (아래 절반은 상자에 가린다)
        Fill(Box(0f, -0.2f, 0.8f, 0.58f, 0.1f));
        Cut(Seg(-0.86f, 0.2f, 0.86f, 0.2f, 0.024f));       // 뚜껑 선
        Cut(Box(0f, -0.26f, 0.11f, 0.33f, 0f));            // 십자
        Cut(Box(0f, -0.26f, 0.33f, 0.11f, 0f));
    }

    // ── 숫돌: 비스듬한 돌 위에 올린 칼날 + 튀는 불티 ──
    private void Whetstone()
    {
        Transform(0f, -0.42f, -8f);                        // 숫돌
        Fill(Box(0f, 0f, 0.82f, 0.24f, 0.06f));
        Cut(Seg(-0.62f, 0.07f, 0.4f, 0.07f, 0.022f));
        Cut(Seg(-0.3f, -0.08f, 0.62f, -0.08f, 0.022f));

        Transform(-0.14f, 0.04f, 22f);                     // 칼
        Sdf[] knife =
        {
            Poly(-0.1f, -0.1f, 0.72f, -0.1f, 0.98f, 0.1f, -0.1f, 0.1f),   // 날
            Box(-0.16f, 0f, 0.045f, 0.2f, 0.02f),                         // 코등이
            Box(-0.48f, 0f, 0.3f, 0.075f, 0.06f),                         // 손잡이
        };
        foreach (Sdf s in knife) Cut(s, 0.06f);            // 숫돌과 겹치는 자리를 살짝 파낸다
        foreach (Sdf s in knife) Fill(s);
        Reset();

        Fill(Seg(0.6f, 0.54f, 0.72f, 0.72f, 0.03f));       // 불티
        Fill(Seg(0.8f, 0.44f, 0.94f, 0.5f, 0.03f));
        Fill(Seg(0.44f, 0.6f, 0.44f, 0.76f, 0.03f));
    }

    // ── 팽팽한 용수철: 두 받침 사이에서 눌린 코일 ──
    private void CoilSpring()
    {
        Fill(Box(0f, 0.84f, 0.62f, 0.075f, 0.03f));
        Fill(Box(0f, -0.84f, 0.62f, 0.075f, 0.03f));
        const int turns = 6;
        float step = 1.52f / turns;
        for (int i = 0; i < turns; i++)
        {
            float y0 = 0.76f - i * step, y1 = y0 - step;
            float x0 = i % 2 == 0 ? -0.46f : 0.46f;
            Fill(Seg(x0, y0, -x0, y1, 0.065f));
        }
    }

    // ── 방청유: 목이 긴 둥근 병, 안에 기름 한 방울 ──
    private void RustOilFlask()
    {
        Fill(Box(0f, 0.38f, 0.17f, 0.32f, 0.02f));         // 목
        Fill(Box(0f, 0.74f, 0.27f, 0.07f, 0.03f));         // 주둥이 테
        Fill(Box(0f, 0.9f, 0.12f, 0.07f, 0.03f));          // 마개
        Fill(Circle(0f, -0.32f, 0.6f));                    // 몸통
        Cut(Seg(-0.3f, 0.5f, 0.3f, 0.5f, 0.022f));         // 목의 띠
        Cut(Circle(0f, -0.42f, 0.2f));                     // 기름 방울 (파낸 모양)
        Cut(Poly(-0.172f, -0.32f, 0.172f, -0.32f, 0f, 0.04f));
    }

    // ── 전투: 엇갈린 두 자루의 검 ──
    private void CrossedSwords()
    {
        Sword(38f, false);
        Sword(-38f, true);   // 위에 놓이는 검: 아래 검과 닿는 자리를 살짝 파내 겹침이 보이게
        Reset();
    }

    private void Sword(float angle, bool onTop)
    {
        Transform(0f, -0.1f, angle);
        Sdf[] parts =
        {
            Poly(-0.115f, -0.2f, 0.115f, -0.2f, 0.115f, 0.66f, 0f, 1.0f, -0.115f, 0.66f),   // 날
            Box(0f, -0.25f, 0.32f, 0.07f, 0.03f),                                           // 코등이
            Box(0f, -0.47f, 0.065f, 0.18f, 0.02f),                                          // 손잡이
            Circle(0f, -0.72f, 0.11f),                                                      // 자루 끝
        };
        if (onTop) foreach (Sdf s in parts) Cut(s, 0.06f);
        foreach (Sdf s in parts) Fill(s);
    }

    // ── 강적: 뿔 달린 해골 ──
    private void HornedSkull()
    {
        Fill(Poly(-0.34f, 0.44f, -0.6f, 0.62f, -0.92f, 0.95f, -0.8f, 0.42f, -0.5f, 0.14f));
        Fill(Poly(0.34f, 0.44f, 0.6f, 0.62f, 0.92f, 0.95f, 0.8f, 0.42f, 0.5f, 0.14f));
        Fill(Circle(0f, 0.1f, 0.52f));
        Fill(Box(0f, -0.4f, 0.3f, 0.22f, 0.08f));
        Cut(Circle(-0.22f, 0.06f, 0.15f));
        Cut(Circle(0.22f, 0.06f, 0.15f));
        Cut(Poly(0f, -0.1f, -0.075f, -0.27f, 0.075f, -0.27f));
        Cut(Seg(-0.12f, -0.42f, -0.12f, -0.7f, 0.022f));
        Cut(Seg(0f, -0.42f, 0f, -0.7f, 0.022f));
        Cut(Seg(0.12f, -0.42f, 0.12f, -0.7f, 0.022f));
    }

    // ── 휴식: 꺼지지 않은 가로등 (등불) ──
    private void Lantern()
    {
        Fill(Ring(0f, 0.84f, 0.1f, 0.055f));
        Fill(Poly(-0.38f, 0.48f, 0.38f, 0.48f, 0.17f, 0.74f, -0.17f, 0.74f));
        Fill(Box(0f, -0.04f, 0.37f, 0.5f, 0.07f));
        Cut(Box(0f, -0.04f, 0.27f, 0.4f, 0.04f));
        Fill(Box(0f, -0.62f, 0.46f, 0.075f, 0.03f));
        // 불꽃
        Fill(Circle(0f, -0.2f, 0.16f));
        Fill(Poly(-0.145f, -0.14f, 0.145f, -0.14f, 0.03f, 0.27f));
        Cut(Circle(0.01f, -0.22f, 0.06f));
    }

    // ── 이벤트: 열쇠 구멍이 난 낯선 문 ──
    private void KeyholeDoor()
    {
        Fill(Box(0f, -0.28f, 0.54f, 0.6f, 0f));
        Fill(Circle(0f, 0.32f, 0.54f));
        Cut(Box(0f, -0.3f, 0.42f, 0.62f, 0f));
        Cut(Circle(0f, 0.32f, 0.42f));
        Fill(Box(0f, -0.86f, 0.7f, 0.055f, 0.03f));
        Fill(Circle(0f, 0.1f, 0.17f));
        Fill(Poly(-0.06f, 0.02f, 0.06f, 0.02f, 0.15f, -0.46f, -0.15f, -0.46f));
    }

    // ── 고철 수거상: 금이 가 쪼개진 톱니 ──
    private void BrokenGear()
    {
        Teeth(8, 0.5f, 0.2f, 0.14f);
        Fill(Circle(0f, 0f, 0.56f));
        Cut(Circle(0f, 0f, 0.2f));
        float[] crack = { -0.5f, 0.95f, -0.2f, 0.42f, 0.12f, 0.3f, -0.08f, -0.16f, 0.26f, -0.42f, 0.42f, -0.95f };
        for (int i = 0; i + 3 < crack.Length; i += 2)
            Cut(Seg(crack[i], crack[i + 1], crack[i + 2], crack[i + 3], 0.045f));
    }

    // ── 궤짝 ──
    private void Chest()
    {
        // 뚜껑: 위가 둥근 반타원
        List<float> lid = new List<float>();
        for (int i = 0; i <= 16; i++)
        {
            double a = Math.PI * i / 16.0;
            lid.Add((float)(0.74 * Math.Cos(a)));
            lid.Add(0.1f + (float)(0.52 * Math.Sin(a)));
        }
        Fill(Poly(lid.ToArray()));
        Fill(Box(0f, -0.34f, 0.74f, 0.36f, 0.05f));
        Cut(Box(0f, 0.045f, 0.9f, 0.028f, 0f));               // 뚜껑과 몸통 사이
        Cut(Seg(-0.44f, 0.7f, -0.44f, -0.75f, 0.024f));      // 쇠띠
        Cut(Seg(0.44f, 0.7f, 0.44f, -0.75f, 0.024f));
        Cut(Box(0f, -0.08f, 0.2f, 0.22f, 0.06f));            // 자물쇠 자리
        Fill(Box(0f, -0.08f, 0.14f, 0.16f, 0.04f));
        Cut(Circle(0f, -0.05f, 0.045f));
        Cut(Seg(0f, -0.06f, 0f, -0.15f, 0.02f));
    }

    // ── 목적지: 종탑의 종 ──
    private void Bell()
    {
        Fill(Ring(0f, 0.8f, 0.11f, 0.06f));
        Fill(Poly(-0.15f, 0.68f, 0.15f, 0.68f, 0.31f, 0.52f, 0.41f, 0.2f, 0.47f, -0.14f, 0.6f, -0.38f, 0.78f, -0.5f,
                  -0.78f, -0.5f, -0.6f, -0.38f, -0.47f, -0.14f, -0.41f, 0.2f, -0.31f, 0.52f));
        Fill(Box(0f, -0.52f, 0.82f, 0.06f, 0.05f));
        Cut(Seg(-0.56f, -0.3f, 0.56f, -0.3f, 0.022f));        // 아랫단 띠
        Fill(Circle(0f, -0.76f, 0.13f));
    }

    // ── 출발점: 위를 가리키는 두 겹의 화살 ──
    private void Chevrons()
    {
        for (int i = 0; i < 2; i++)
        {
            float y = 0.1f - i * 0.5f;
            Fill(Seg(-0.5f, y - 0.2f, 0f, y + 0.32f, 0.11f));
            Fill(Seg(0.5f, y - 0.2f, 0f, y + 0.32f, 0.11f));
        }
    }

    // ── 톱니바퀴 (시작 화면 · 로딩) ──
    private void GearWheel(int teeth, float body, float hole, bool spokes)
    {
        Teeth(teeth, body - 0.04f, 0.2f, spokes ? 0.085f : 0.13f);
        Fill(Circle(0f, 0f, body));
        if (spokes)
        {
            Cut(Circle(0f, 0f, body - 0.16f));
            Fill(Circle(0f, 0f, hole + 0.14f));
            for (int i = 0; i < 6; i++)
            {
                Transform(0f, 0f, i * 60f);
                Fill(Box(0f, body * 0.5f, 0.06f, body * 0.5f, 0f));
            }
            Reset();
        }
        Cut(Circle(0f, 0f, hole));
    }

    // ── 시계판: 테두리 + 눈금 (바늘은 UI에서 돌린다) ──
    private void Clock()
    {
        Fill(Ring(0f, 0f, 0.95f, 0.035f));
        Fill(Ring(0f, 0f, 0.86f, 0.012f));
        for (int i = 0; i < 60; i++)
        {
            Transform(0f, 0f, i * 6f);
            bool hour = i % 5 == 0, quarter = i % 15 == 0;
            float len = quarter ? 0.1f : hour ? 0.07f : 0.025f;
            Fill(Box(0f, 0.84f - len, quarter ? 0.022f : hour ? 0.014f : 0.006f, len, 0f));
        }
        Reset();
    }

    /// <summary>바퀴 둘레에 톱니 n개 (rim = 톱니가 박히는 반지름)</summary>
    private void Teeth(int n, float rim, float length, float halfWidth)
    {
        for (int i = 0; i < n; i++)
        {
            Transform(0f, 0f, i * 360f / n);
            Fill(Box(0f, rim + length * 0.5f, halfWidth, length * 0.5f + 0.04f, 0.03f));
        }
        Reset();
    }

    // ─────────────────────────── 붓질 ───────────────────────────

    /// <summary>이후의 도형을 (x, y)로 옮기고 deg만큼(반시계) 돌려서 그린다</summary>
    public void Transform(float x, float y, float deg, float s = 1f)
    {
        tx = x; ty = y; scale = s;
        double r = deg * Math.PI / 180.0;
        cos = (float)Math.Cos(r);
        sin = (float)Math.Sin(r);
    }

    public void Reset() { Transform(0f, 0f, 0f); }

    /// <summary>도형을 칠한다 (grow만큼 부풀려서)</summary>
    public void Fill(Sdf shape, float grow = 0f)
    {
        for (int i = 0; i < Alpha.Length; i++)
        {
            float c = Coverage(shape, i, grow);
            if (c > Alpha[i]) Alpha[i] = c;
        }
    }

    /// <summary>도형 모양으로 파낸다</summary>
    public void Cut(Sdf shape, float grow = 0f)
    {
        for (int i = 0; i < Alpha.Length; i++)
        {
            if (Alpha[i] <= 0f) continue;
            Alpha[i] *= 1f - Coverage(shape, i, grow);
        }
    }

    private float Coverage(Sdf shape, int index, float grow)
    {
        // 픽셀 가운데 → 그림 좌표 → 도형의 제자리 좌표 (옮김·돌림을 거꾸로)
        float px = ((index % Size) + 0.5f) / Size * 2f - 1f - tx;
        float py = ((index / Size) + 0.5f) / Size * 2f - 1f - ty;
        float lx = (px * cos + py * sin) / scale;
        float ly = (-px * sin + py * cos) / scale;
        float d = (shape(lx, ly) - grow) * scale * Size * 0.5f;   // 거리(픽셀)
        float c = 0.5f - d;
        return c < 0f ? 0f : c > 1f ? 1f : c;
    }

    // ─────────────────────────── 도형 (거리 함수) ───────────────────────────

    public static Sdf Circle(float cx, float cy, float r)
    {
        return (x, y) => Len(x - cx, y - cy) - r;
    }

    /// <summary>고리 (r = 가운데 반지름, half = 두께의 절반)</summary>
    public static Sdf Ring(float cx, float cy, float r, float half)
    {
        return (x, y) => Math.Abs(Len(x - cx, y - cy) - r) - half;
    }

    /// <summary>끝이 둥근 선분 (half = 두께의 절반)</summary>
    public static Sdf Seg(float ax, float ay, float bx, float by, float half)
    {
        return (x, y) =>
        {
            float ex = bx - ax, ey = by - ay, wx = x - ax, wy = y - ay;
            float t = Clamp01((wx * ex + wy * ey) / (ex * ex + ey * ey));
            return Len(wx - ex * t, wy - ey * t) - half;
        };
    }

    /// <summary>상자 (hw, hh = 절반 크기, round = 모서리 반지름)</summary>
    public static Sdf Box(float cx, float cy, float hw, float hh, float round)
    {
        return (x, y) =>
        {
            float dx = Math.Abs(x - cx) - (hw - round), dy = Math.Abs(y - cy) - (hh - round);
            float ox = Math.Max(dx, 0f), oy = Math.Max(dy, 0f);
            return Len(ox, oy) + Math.Min(Math.Max(dx, dy), 0f) - round;
        };
    }

    /// <summary>다각형 (x0, y0, x1, y1, ... 순서대로)</summary>
    public static Sdf Poly(params float[] v)
    {
        int n = v.Length / 2;
        return (x, y) =>
        {
            float d = (x - v[0]) * (x - v[0]) + (y - v[1]) * (y - v[1]);
            float sign = 1f;
            for (int i = 0, j = n - 1; i < n; j = i, i++)
            {
                float vix = v[i * 2], viy = v[i * 2 + 1], vjx = v[j * 2], vjy = v[j * 2 + 1];
                float ex = vjx - vix, ey = vjy - viy, wx = x - vix, wy = y - viy;
                float t = Clamp01((wx * ex + wy * ey) / (ex * ex + ey * ey));
                float bx = wx - ex * t, by = wy - ey * t;
                d = Math.Min(d, bx * bx + by * by);
                bool c1 = y >= viy, c2 = y < vjy, c3 = ex * wy > ey * wx;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) sign = -sign;
            }
            return sign * (float)Math.Sqrt(d);
        };
    }

    private static float Len(float x, float y) { return (float)Math.Sqrt(x * x + y * y); }

    private static float Clamp01(float t) { return t < 0f ? 0f : t > 1f ? 1f : t; }
}
