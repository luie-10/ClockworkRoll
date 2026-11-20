using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// UI 그림 스타일 전환: 기본(부드러운 테 · 둥근 모서리) ↔ 픽셀(계단 모서리 · 각진 칸).
/// 글자는 건드리지 않는다 — 판 · 테두리 · 버튼 · 그림자 · 시계판 · 문양만 바뀐다.
///
/// 코드로 그린 UI 텍스처(ProceduralSprites · ClockArt · RouteIcons)가 만들어질 때 Register로 등록해 두면,
/// 전환할 때 같은 텍스처의 픽셀만 덮어쓴다 → 이미 화면에 떠 있는 UI도 그 자리에서 바로 바뀐다 (다시 만들 필요 없음).
/// 처음 켜면 픽셀이 기본이다. 고른 스타일은 기억해 두었다가 다음 실행에도 그대로 쓴다. 전환: F8 또는 멈춤 메뉴.
/// </summary>
public static class PixelStyle
{
    // 키를 새로 바꿨다: 예전에 고른 값과 상관없이 처음엔 픽셀로 시작하고, 이후 고른 값은 다시 기억한다
    private const string PrefKey = "ui.style.v2";

    private static bool loaded, on;

    /// <summary>지금 픽셀 스타일인가</summary>
    public static bool On { get { Load(); return on; } }

    /// <summary>스타일이 바뀐 직후 (메뉴의 표시를 새로 칠할 때 쓴다)</summary>
    public static event Action Changed;

    private class Entry
    {
        public Texture2D tex;
        public Color[] smooth;
        public Func<float[]> makePixel;   // 픽셀판을 만드는 법 (처음 쓸 때 한 번 만들고 기억)
        public Color[] pixel;
    }

    private static readonly List<Entry> entries = new List<Entry>();

    /// <summary>
    /// 부드럽게 그려 둔 텍스처를 등록한다. 픽셀판은 그 그림을 block 칸으로 뭉쳐 만든다.
    /// alphaLevels = 1이면 가장자리를 딱 끊고, 2 이상이면 그만큼의 투명도 단계를 남긴다 (번짐 · 그림자).
    /// </summary>
    public static void Register(Texture2D tex, int block, int alphaLevels = 1)
    {
        if (tex == null) return;
        int w = tex.width, h = tex.height;
        Register(tex, smooth => PixelFilter.Pixelate(ToFloats(smooth), w, h, block, alphaLevels, 0));
    }

    /// <summary>픽셀판을 따로 그리는 텍스처를 등록한다 (상자 · 테두리처럼 칸에 맞춰 직접 그리는 편이 깔끔한 것)</summary>
    public static void Register(Texture2D tex, Func<float[]> pixelArt)
    {
        if (tex == null || pixelArt == null) return;
        Register(tex, _ => pixelArt());
    }

    private static void Register(Texture2D tex, Func<Color[], float[]> make)
    {
        Load();
        Entry e = new Entry { tex = tex, smooth = tex.GetPixels() };
        e.makePixel = () => make(e.smooth);
        entries.Add(e);
        if (on) Apply(e);
    }

    public static void Toggle() { Set(!On); }

    public static void Set(bool pixel)
    {
        Load();
        if (on == pixel) return;
        on = pixel;
        try { PlayerPrefs.SetInt(PrefKey, on ? 1 : 0); PlayerPrefs.Save(); } catch { }

        entries.RemoveAll(e => e.tex == null);
        foreach (Entry e in entries) Apply(e);
        if (Changed != null) Changed();
    }

    private static void Apply(Entry e)
    {
        if (e.tex == null) return;
        if (on)
        {
            if (e.pixel == null) e.pixel = ToColors(e.makePixel());
            e.tex.SetPixels(e.pixel);
            e.tex.filterMode = FilterMode.Point;   // 칸이 번지지 않고 각지게
        }
        else
        {
            e.tex.SetPixels(e.smooth);
            e.tex.filterMode = FilterMode.Bilinear;
        }
        e.tex.Apply(false);
    }

    private static void Load()
    {
        if (loaded) return;
        loaded = true;
        try { on = PlayerPrefs.GetInt(PrefKey, 1) == 1; } catch { on = true; }   // 기본 = 픽셀
    }

    private static float[] ToFloats(Color[] c)
    {
        float[] f = new float[c.Length * 4];
        for (int i = 0; i < c.Length; i++) { f[i * 4] = c[i].r; f[i * 4 + 1] = c[i].g; f[i * 4 + 2] = c[i].b; f[i * 4 + 3] = c[i].a; }
        return f;
    }

    private static Color[] ToColors(float[] f)
    {
        Color[] c = new Color[f.Length / 4];
        for (int i = 0; i < c.Length; i++) c[i] = new Color(f[i * 4], f[i * 4 + 1], f[i * 4 + 2], f[i * 4 + 3]);
        return c;
    }
}
