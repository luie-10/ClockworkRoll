using UnityEngine;

/// <summary>
/// 코드로 만드는 공용 스프라이트 (에셋 없이).
///   RoundedBox : 모서리가 둥근 상자 — 9-slice(Sliced)라 어떤 크기로 늘려도 모서리가 찌그러지지 않는다
///   Square     : 흰 사각형 (게이지 바 등)
///   Circle     : 가장자리가 부드러운 흰 원 (시계판 등)
/// 월드 공간(SpriteRenderer, drawMode = Sliced)과 UI(Image, type = Sliced) 모두에 쓸 수 있다.
/// </summary>
public static class ProceduralSprites
{
    private static Sprite roundedBox, square, circle, verticalFade, horizontalFade, bevel, frameThin, frameBold, softShadow;

    public const int ShadowPad = 16;   // 그림자가 상자 밖으로 번지는 폭(px)

    public const int BoxSize = 64;
    public const int BoxCorner = 14;   // 모서리 반지름(px) = 9-slice 테두리

    public static Sprite RoundedBox()
    {
        if (roundedBox == null)
        {
            Texture2D tex = RoundedBoxTexture();
            // 100px = 1유닛 → 월드에서 테두리 0.14유닛. UI는 Pixels Per Unit Multiplier로 조절
            roundedBox = Sprite.Create(tex, new Rect(0, 0, BoxSize, BoxSize), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(BoxCorner + 2, BoxCorner + 2, BoxCorner + 2, BoxCorner + 2));
            roundedBox.name = "RoundedBox";
        }
        return roundedBox;
    }

    public static Sprite Circle()
    {
        if (circle == null)
        {
            const int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            Color[] px = new Color[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(r - d));   // 가장자리 1px 안티에일리어싱
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            circle.name = "Circle";
        }
        return circle;
    }

    /// <summary>아래는 불투명, 위로 갈수록 투명해지는 세로 그러데이션 (손패 받침대 그늘 등)</summary>
    public static Sprite VerticalFade()
    {
        if (verticalFade == null)
        {
            const int h = 64;
            Texture2D tex = new Texture2D(1, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < h; y++)
            {
                float a = 1f - y / (h - 1f);
                tex.SetPixel(0, y, new Color(1f, 1f, 1f, a * a * (3f - 2f * a)));   // smoothstep: 경계가 보이지 않게
            }
            tex.Apply();
            verticalFade = Sprite.Create(tex, new Rect(0, 0, 1, h), new Vector2(0.5f, 0.5f), 100f);
            verticalFade.name = "VerticalFade";
        }
        return verticalFade;
    }

    /// <summary>가운데는 불투명, 양 끝으로 갈수록 투명해지는 가로 그러데이션 (단계 전환 띠 등)</summary>
    public static Sprite HorizontalFade()
    {
        if (horizontalFade == null)
        {
            const int w = 128;
            Texture2D tex = new Texture2D(w, 1, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int x = 0; x < w; x++)
            {
                float a = Mathf.Sin((x + 0.5f) / w * Mathf.PI);
                tex.SetPixel(x, 0, new Color(1f, 1f, 1f, a * a));
            }
            tex.Apply();
            horizontalFade = Sprite.Create(tex, new Rect(0, 0, w, 1), new Vector2(0.5f, 0.5f), 100f);
            horizontalFade.name = "HorizontalFade";
        }
        return horizontalFade;
    }

    /// <summary>
    /// 입체감 있는 둥근 판 (주 버튼용) — 위는 밝고 아래로 갈수록 어두워지며, 윗변 안쪽에 밝은 줄 · 아랫변에 그늘이 있다.
    /// 흰색 바탕이라 Image.color로 색을 입힌다. RoundedBox와 같은 9-slice.
    /// </summary>
    public static Sprite Bevel()
    {
        if (bevel == null)
        {
            Texture2D tex = new Texture2D(BoxSize, BoxSize, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            float r = BoxCorner;
            for (int y = 0; y < BoxSize; y++)
            {
                for (int x = 0; x < BoxSize; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, r, BoxSize - r);
                    float cy = Mathf.Clamp(y + 0.5f, r, BoxSize - r);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    float a = Mathf.Clamp01(r - d + 0.5f);

                    float v = Mathf.Lerp(0.72f, 0.95f, y / (BoxSize - 1f));      // 아래 어둡게 → 위 밝게
                    float inset = Mathf.Min(Mathf.Min(x, BoxSize - 1 - x), Mathf.Min(y, BoxSize - 1 - y));
                    inset = Mathf.Min(inset, r - d);                             // 둥근 모서리에서도 테두리를 따라가게
                    if (inset < 2.5f) v += y > BoxSize / 2 ? 0.12f : -0.14f;     // 윗변 밝은 줄 / 아랫변 그늘
                    tex.SetPixel(x, y, new Color(Mathf.Clamp01(v), Mathf.Clamp01(v), Mathf.Clamp01(v), a));
                }
            }
            tex.Apply();
            bevel = Sprite.Create(tex, new Rect(0, 0, BoxSize, BoxSize), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(BoxCorner + 2, BoxCorner + 2, BoxCorner + 2, BoxCorner + 2));
            bevel.name = "Bevel";
        }
        return bevel;
    }

    /// <summary>
    /// 둥근 상자의 테두리 선만 (속은 비어 있다). RoundedBox와 같은 모양 · 같은 9-slice라 그 위에 겹치면 딱 맞는다.
    /// Outline 컴포넌트(그림을 네 번 어긋나게 겹쳐 그리는 방식)와 달리 선 굵기가 고르고 모서리가 뭉개지지 않는다.
    /// bold = 굵은 선 (고른 것 · 마우스를 올린 것)
    /// </summary>
    public static Sprite Frame(bool bold = false)
    {
        Sprite cached = bold ? frameBold : frameThin;
        if (cached != null) return cached;

        float thick = bold ? 4.6f : 2.4f;   // 텍스처 px — Pixels Per Unit Multiplier 1.6이면 화면에서 약 2.9 / 1.5px
        Texture2D tex = new Texture2D(BoxSize, BoxSize, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float r = BoxCorner;
        for (int y = 0; y < BoxSize; y++)
        {
            for (int x = 0; x < BoxSize; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, r, BoxSize - r);
                float cy = Mathf.Clamp(y + 0.5f, r, BoxSize - r);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                // 가장자리에서 안쪽으로 들어온 깊이 (곧은 변에서는 변까지의 거리, 모서리에서는 둥근 호까지의 거리)
                float depth = d > 0f ? r - d : Mathf.Min(Mathf.Min(x + 0.5f, BoxSize - x - 0.5f), Mathf.Min(y + 0.5f, BoxSize - y - 0.5f));
                float a = Mathf.Clamp01(depth + 0.5f) * Mathf.Clamp01(thick - depth + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        Sprite s = Sprite.Create(tex, new Rect(0, 0, BoxSize, BoxSize), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(BoxCorner + 2, BoxCorner + 2, BoxCorner + 2, BoxCorner + 2));
        s.name = bold ? "FrameBold" : "Frame";
        if (bold) frameBold = s; else frameThin = s;
        return s;
    }

    /// <summary>
    /// 상자 밖으로 번지는 부드러운 그림자 (속은 비어 있어 상자 자체는 어두워지지 않는다).
    /// 상자보다 사방으로 ShadowPad만큼 크게 놓는다. 9-slice.
    /// </summary>
    public static Sprite SoftShadow()
    {
        if (softShadow == null)
        {
            const int size = 96;
            const float box = ShadowPad, r = 9f;   // 상자 = 가장자리에서 ShadowPad 들어온 둥근 사각형
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, box + r, size - box - r);
                    float cy = Mathf.Clamp(y + 0.5f, box + r, size - box - r);
                    float outside = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy)) - r;   // 상자 밖으로 나온 거리
                    float fall = Mathf.Clamp01(1f - outside / ShadowPad);
                    float a = outside < -1f ? 0f : fall * fall * Mathf.Clamp01(outside + 1.5f);
                    tex.SetPixel(x, y, new Color(0f, 0f, 0f, a));
                }
            }
            tex.Apply();
            int b = ShadowPad + 12;
            softShadow = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(b, b, b, b));
            softShadow.name = "SoftShadow";
        }
        return softShadow;
    }

    public static Sprite Square()
    {
        if (square == null)
        {
            Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            Color[] px = new Color[16];
            for (int i = 0; i < px.Length; i++) px[i] = Color.white;
            tex.SetPixels(px);
            tex.Apply();
            square = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0f, 0.5f), 4f);   // 피벗 왼쪽 → 오른쪽으로 늘어나는 바
            square.name = "Square";
        }
        return square;
    }

    /// <summary>둥근 상자 텍스처 (흰색, 가장자리는 부드럽게). 에디터에서 PNG로 저장할 때도 사용.</summary>
    public static Texture2D RoundedBoxTexture()
    {
        Texture2D tex = new Texture2D(BoxSize, BoxSize, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        float r = BoxCorner;
        for (int y = 0; y < BoxSize; y++)
        {
            for (int x = 0; x < BoxSize; x++)
            {
                // 가장 가까운 모서리 원의 중심까지 거리
                float cx = Mathf.Clamp(x + 0.5f, r, BoxSize - r);
                float cy = Mathf.Clamp(y + 0.5f, r, BoxSize - r);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                float a = Mathf.Clamp01(r - d + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return tex;
    }

    /// <summary>월드 공간 둥근 상자 (SpriteRenderer, 크기 = size 유닛).</summary>
    public static SpriteRenderer WorldBox(Transform parent, string name, Vector2 size, Color color, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = RoundedBox();
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = size;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }
}
