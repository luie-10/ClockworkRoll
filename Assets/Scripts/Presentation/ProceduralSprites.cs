using UnityEngine;

/// <summary>
/// 코드로 만드는 공용 스프라이트 (에셋 없이).
///   RoundedBox : 모서리가 둥근 상자 — 9-slice(Sliced)라 어떤 크기로 늘려도 모서리가 찌그러지지 않는다
///   Square     : 흰 사각형 (게이지 바 등)
/// 월드 공간(SpriteRenderer, drawMode = Sliced)과 UI(Image, type = Sliced) 모두에 쓸 수 있다.
/// </summary>
public static class ProceduralSprites
{
    private static Sprite roundedBox, square;

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
