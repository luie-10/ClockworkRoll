using UnityEngine;

/// <summary>
/// 2.5D 캐릭터 외형 — 3D 공간에 2D 스프라이트(종이 인형)로 서 있는 캐릭터.
///
/// 유닛 루트 오브젝트에 Unit과 같이 붙인다. 시작할 때 자식 두 개를 코드로 만든다.
///   Body   : SpriteRenderer — 항상 카메라 쪽을 바라봄 (빌보드, 위아래로는 기울지 않음)
///   Shadow : 발밑의 둥근 그림자 (바닥에 눕혀진 스프라이트)
/// 스프라이트를 비워두면 단색 실루엣(임시 그림)을 자동으로 그린다.
///
/// 스프라이트 그림은 '오른쪽을 보는' 그림이 기본 (왼쪽을 보는 그림이면 Faces Right By Default 해제).
/// </summary>
[DefaultExecutionOrder(-50)]   // Unit.Awake보다 먼저 몸(Body)을 만들어야 Unit이 Renderer를 찾을 수 있다
[RequireComponent(typeof(BoxCollider))]
public class SpriteCharacter : MonoBehaviour
{
    [Header("외형")]
    [Tooltip("캐릭터 그림 (비워두면 단색 실루엣). Import Settings에서 Pivot을 Bottom으로 하면 발이 바닥에 붙는다")]
    public Sprite sprite;
    [Tooltip("화면에서의 키 (월드 단위, 그림 크기와 상관없이 이 높이로 맞춤)")]
    public float height = 2f;
    [Tooltip("스프라이트 색 (실루엣이면 몸 색, 그림이면 흰색 = 원본 색)")]
    public Color tint = Color.white;
    [Tooltip("그림이 원래 오른쪽을 보고 있는가")]
    public bool facesRightByDefault = true;

    [Header("그림자")]
    public bool showShadow = true;
    public float shadowSize = 1.1f;

    public SpriteRenderer BodyRenderer { get; private set; }

    private Transform body;
    private Transform shadow;
    private bool facingRight = true;

    void Awake()
    {
        Build();
    }

    /// <summary>코드로 생성할 때 사용 (BattleManager가 적을 만들 때). Unit을 붙이기 전에 호출할 것.</summary>
    public void Setup(Sprite newSprite, float newHeight, Color newTint)
    {
        sprite = newSprite;
        height = newHeight;
        tint = newTint;
        Build();
    }

    /// <summary>worldPoint 쪽을 보도록 좌우를 뒤집는다 (화면 기준 왼쪽/오른쪽).</summary>
    public void Face(Vector3 worldPoint)
    {
        Camera cam = Camera.main;
        Vector3 right = cam != null ? cam.transform.right : Vector3.right;

        float side = Vector3.Dot(worldPoint - transform.position, right);
        if (Mathf.Abs(side) < 0.01f) return;

        facingRight = side > 0f;
        if (BodyRenderer != null) BodyRenderer.flipX = facingRight != facesRightByDefault;
    }

    void LateUpdate()
    {
        // 빌보드: 카메라의 좌우 회전만 따라간다 (위에서 내려다봐도 캐릭터가 눕지 않게)
        Camera cam = Camera.main;
        if (cam != null && body != null)
            body.rotation = Quaternion.Euler(0f, cam.transform.eulerAngles.y, 0f);
    }

    // ─────────────────────────── 생성 ───────────────────────────

    private void Build()
    {
        // 1) 몸 (반드시 첫 번째 자식 — Unit이 GetComponentInChildren으로 찾는다)
        if (body == null)
        {
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            body.SetAsFirstSibling();
            BodyRenderer = body.gameObject.AddComponent<SpriteRenderer>();
        }

        Sprite shown = sprite != null ? sprite : PlaceholderSilhouette();
        BodyRenderer.sprite = shown;
        BodyRenderer.color = tint;
        BodyRenderer.flipX = facingRight != facesRightByDefault;

        // 키를 height에 맞추고, 피벗과 상관없이 발이 루트 위치(바닥)에 오게 한다
        float spriteHeight = Mathf.Max(0.01f, shown.bounds.size.y);
        float scale = height / spriteHeight;
        body.localScale = Vector3.one * scale;
        body.localPosition = new Vector3(0f, -shown.bounds.min.y * scale, 0f);

        // 2) 클릭 판정용 콜라이더를 몸 크기에 맞춤
        float width = shown.bounds.size.x * scale;
        BoxCollider col = GetComponent<BoxCollider>();
        col.size = new Vector3(width, height, Mathf.Max(0.4f, width * 0.5f));
        col.center = new Vector3(0f, height * 0.5f, 0f);

        // 3) 그림자
        if (showShadow && shadow == null)
        {
            shadow = new GameObject("Shadow").transform;
            shadow.SetParent(transform, false);
            shadow.localPosition = new Vector3(0f, 0.02f, 0f);   // 바닥과 겹쳐 깜빡이지 않게 살짝 위
            shadow.localRotation = Quaternion.Euler(90f, 0f, 0f);

            SpriteRenderer sr = shadow.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = ShadowSprite();
            sr.color = new Color(0f, 0f, 0f, 0.45f);
        }
        if (shadow != null) shadow.localScale = new Vector3(shadowSize * width, shadowSize * width * 0.5f, 1f);
    }

    // ─────────────────────────── 임시 그림 (에셋 없이) ───────────────────────────

    private static Sprite silhouette;
    private static Sprite shadowSprite;

    /// <summary>머리 + 몸통 모양의 흰색 실루엣 (tint로 색을 입힌다). 피벗은 발밑.</summary>
    private static Sprite PlaceholderSilhouette()
    {
        if (silhouette != null) return silhouette;

        const int w = 48, h = 96;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color clear = new Color(1f, 1f, 1f, 0f);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float cx = x - w * 0.5f + 0.5f;
                bool head = new Vector2(cx, y - 80f).sqrMagnitude <= 11f * 11f;                    // 머리 (원)
                bool torso = y >= 8 && y <= 66 && Mathf.Abs(cx) <= Mathf.Lerp(16f, 11f, (y - 8) / 58f); // 몸통 (사다리꼴)
                bool feet = y < 8 && Mathf.Abs(cx) <= 13f && Mathf.Abs(cx) >= 3f;                   // 다리 두 개
                bool neck = y > 66 && y < 72 && Mathf.Abs(cx) <= 5f;
                tex.SetPixel(x, y, head || torso || feet || neck ? Color.white : clear);
            }
        }
        tex.Apply();

        silhouette = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 48f);
        silhouette.name = "PlaceholderSilhouette";
        return silhouette;
    }

    /// <summary>가장자리가 부드러운 원 (그림자용).</summary>
    private static Sprite ShadowSprite()
    {
        if (shadowSprite != null) return shadowSprite;

        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2(x - size * 0.5f + 0.5f, y - size * 0.5f + 0.5f).magnitude / (size * 0.5f);
                float a = 1f - Mathf.SmoothStep(0.6f, 1f, d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();

        shadowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        shadowSprite.name = "BlobShadow";
        return shadowSprite;
    }
}
