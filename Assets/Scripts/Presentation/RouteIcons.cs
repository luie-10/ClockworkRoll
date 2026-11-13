using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// IconPainter가 그린 문양을 스프라이트로 만들어 돌려준다 (한 번 그리면 계속 재사용).
/// 문양은 흰색이라 Image.color로 색을 입혀 쓴다 — 갈래길 지도 칸 · 시작 화면 · 로딩 화면이 같이 쓴다.
/// </summary>
public static class RouteIcons
{
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    /// <summary>kind = IconPainter의 문양 이름 (IconPainter.Fight 등), size = 텍스처 한 변(px)</summary>
    public static Sprite Get(string kind, int size = 128)
    {
        string key = kind + "@" + size;
        if (cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

        float[] alpha = IconPainter.Draw(kind, size);
        Color32[] px = new Color32[alpha.Length];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha[i] * 255f));

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.name = "Icon " + kind;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.SetPixels32(px);
        tex.Apply();

        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = "Icon " + kind;
        cache[key] = sprite;
        return sprite;
    }

    /// <summary>지도 칸 문양을 미리 그려 둔다 (로딩 화면에서 부른다 — 지도가 처음 뜰 때 멈칫하지 않게)</summary>
    public static void Warm()
    {
        foreach (string kind in new[] { IconPainter.Fight, IconPainter.Elite, IconPainter.Rest, IconPainter.Event,
                                        IconPainter.Remove, IconPainter.Treasure, IconPainter.Boss, IconPainter.Start })
            Get(kind);
    }
}
