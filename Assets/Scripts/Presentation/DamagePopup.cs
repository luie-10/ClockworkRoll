using UnityEngine;

/// <summary>
/// 머리 위에 떠오르는 숫자/글자 (주사위 굴림 값, 피해량 등).
/// 3D TextMesh로 만들어서 Canvas 없이 월드 공간에 뜬다. 항상 카메라를 바라보고, 위로 떠오르며 사라진다.
/// 직접 붙일 필요 없음 — DamagePopup.Spawn()을 호출하면 코드로 생성된다.
/// </summary>
public class DamagePopup : MonoBehaviour
{
    public float lifetime = 0.9f;     // 화면에 남아 있는 시간(초)
    public float riseSpeed = 1.2f;    // 떠오르는 속도

    private TextMesh textMesh;
    private TextMesh shadowMesh;
    private Color baseColor;
    private float age;

    private static Font builtinFont;

    /// <summary>position 위치에 text를 띄운다. size는 글자 크기 배율.</summary>
    public static DamagePopup Spawn(Vector3 position, string text, Color color, float size = 1f)
    {
        // 겹침 방지: 방금 같은 자리에 뜬 팝업이 있으면 그 위로 한 줄씩 쌓는다 (예: 피해 + 출혈이 동시에)
        float lineHeight = 0.42f * Mathf.Max(0.8f, size);
        for (int guard = 0; guard < 8; guard++)
        {
            bool blocked = false;
            foreach (DamagePopup p in active)
            {
                if (p == null || p.age > 0.45f) continue;
                Vector3 d = p.transform.position - position;
                if (Mathf.Abs(d.y) < lineHeight && new Vector2(d.x, d.z).magnitude < 1.2f)
                {
                    position.y = p.transform.position.y + lineHeight;
                    blocked = true;
                }
            }
            if (!blocked) break;
        }

        GameObject go = new GameObject("Popup " + text);
        go.transform.position = position;

        TextMesh tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.color = color;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontSize = 64;                    // 해상도용 (크게 잡고 characterSize로 줄인다)
        tm.characterSize = 0.06f * size;
        tm.fontStyle = FontStyle.Bold;

        Font font = GetBuiltinFont();
        if (font != null)
        {
            tm.font = font;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        // 검은 그림자 글자를 살짝 아래·뒤에 깔아 어두운/밝은 배경 모두에서 읽히게
        GameObject shadowGO = new GameObject("Shadow");
        shadowGO.transform.SetParent(go.transform, false);
        shadowGO.transform.localPosition = new Vector3(0.035f * size, -0.035f * size, 0.01f);
        TextMesh shadow = shadowGO.AddComponent<TextMesh>();
        shadow.text = text;
        shadow.anchor = tm.anchor;
        shadow.alignment = tm.alignment;
        shadow.fontSize = tm.fontSize;
        shadow.characterSize = tm.characterSize;
        shadow.fontStyle = tm.fontStyle;
        shadow.color = new Color(0f, 0f, 0f, 0.85f);
        if (font != null)
        {
            shadow.font = font;
            shadowGO.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }
        go.GetComponent<MeshRenderer>().sortingOrder = 31;
        shadowGO.GetComponent<MeshRenderer>().sortingOrder = 30;

        DamagePopup popup = go.AddComponent<DamagePopup>();
        popup.textMesh = tm;
        popup.shadowMesh = shadow;
        popup.baseColor = color;
        active.Add(popup);
        return popup;
    }

    private static readonly System.Collections.Generic.List<DamagePopup> active = new System.Collections.Generic.List<DamagePopup>();

    void OnDestroy()
    {
        active.Remove(this);
    }

    void LateUpdate()
    {
        age += Time.deltaTime;
        transform.position += Vector3.up * riseSpeed * Time.deltaTime;

        // 카메라를 정면으로 바라보게 (빌보드)
        Camera cam = Camera.main;
        if (cam != null) transform.rotation = cam.transform.rotation;

        // 후반부에 서서히 투명해진다
        float k = Mathf.Clamp01(age / lifetime);
        Color c = baseColor;
        c.a = 1f - Mathf.SmoothStep(0.5f, 1f, k);
        if (textMesh != null) textMesh.color = c;
        if (shadowMesh != null) shadowMesh.color = new Color(0f, 0f, 0f, 0.85f * c.a);

        if (age >= lifetime) Destroy(gameObject);
    }

    public static Font GetBuiltinFont()
    {
        if (builtinFont != null) return builtinFont;

        // 유니티 버전에 따라 내장 폰트 이름이 다르다 (2022+ : LegacyRuntime.ttf)
        try { builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (builtinFont == null)
        {
            try { builtinFont = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
        }
        return builtinFont;
    }
}
