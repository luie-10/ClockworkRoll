using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 합을 하는 동안 유닛 머리 위에 뜨는 주사위 표시.
///
///   [범위 2~5]      [태엽판 이름]
///   ⬢ (큰 주사위)    ⬢ ⬢ ⬢  ← 이 태엽판의 주사위들 (종류별 색/글자, 쓴 것은 흐려짐)
///
/// - 굴릴 때: 큰 주사위가 돌면서 숫자가 빠르게 바뀌다가 최종 값에 '쾅' 하고 멈춘다
/// - 결과: 이긴 주사위는 번쩍 커지고, 진 주사위는 흔들리며 흐려진다 (동점은 둘 다 흔들림)
///
/// 에셋 없이 육각형 스프라이트와 3D 텍스트를 코드로 만든다. 직접 붙일 필요 없음 —
/// BattleManager가 ClashDiceDisplay.Show()로 띄우고 Hide()로 지운다.
/// </summary>
public class ClashDiceDisplay : MonoBehaviour
{
    public enum Result { Win, Lose, Tie }

    private const float BigHexSize = 0.46f;
    private const float SlotHexSize = 0.2f;
    private const float SlotSpacing = 0.24f;

    private Unit target;
    private List<DiceData> dice;
    private int currentIndex = -1;

    // 큰 주사위
    private Transform bigHex;
    private SpriteRenderer bigFill, bigRing;
    private TextMesh bigNumber, rangeText, boostText;

    // 작은 주사위 슬롯
    private readonly List<SpriteRenderer> slotFills = new List<SpriteRenderer>();
    private readonly List<SpriteRenderer> slotRings = new List<SpriteRenderer>();
    private readonly List<TextMesh> slotGlyphs = new List<TextMesh>();

    private float alpha = 1f;
    private bool hiding;

    private static Sprite hexFillSprite, hexRingSprite;

    // ─────────────────────────── 생성 / 제거 ───────────────────────────

    /// <summary>unit 머리 위에 card의 주사위 표시를 띄운다.</summary>
    public static ClashDiceDisplay Show(Unit unit, CardData card)
    {
        if (unit == null || card == null) return null;

        GameObject go = new GameObject("DiceDisplay " + unit.unitName);
        ClashDiceDisplay display = go.AddComponent<ClashDiceDisplay>();
        display.target = unit;
        display.dice = card.diceList != null ? card.diceList : new List<DiceData>();
        display.Build(card.cardName);
        display.FollowTarget();
        return display;
    }

    /// <summary>제자리에서 서서히 사라진 뒤 삭제.</summary>
    public void Hide()
    {
        if (hiding || this == null) return;
        hiding = true;
        StartCoroutine(FadeOutRoutine());
    }

    // ─────────────────────────── 굴림 / 결과 연출 ───────────────────────────

    /// <summary>
    /// index번째 주사위를 굴리는 연출. 숫자가 빠르게 바뀌며 돌다가 finalValue에서 멈춘다.
    /// die가 null이면(이 쪽은 주사위가 없음) 빈 주사위 "-"를 보여준다.
    /// </summary>
    public IEnumerator RollRoutine(int index, DiceData die, int finalValue, int bonus, float duration)
    {
        SelectSlot(index);
        boostText.text = "";
        bigHex.localScale = Vector3.one * BigHexSize;

        if (die == null)
        {
            SetBigColors(new Color(0.4f, 0.4f, 0.4f));
            bigNumber.text = "-";
            rangeText.text = "";
            yield return new WaitForSeconds(duration);
            yield break;
        }

        Color typeColor = TypeColor(die.diceDetail);
        SetBigColors(typeColor);
        rangeText.text = $"{die.minRoll}~{die.maxRoll}";

        // 돌면서 숫자가 바뀐다 (시간이 갈수록 느려짐)
        float t = 0f;
        float nextFlip = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);

            if (t >= nextFlip)
            {
                bigNumber.text = Random.Range(die.minRoll, die.maxRoll + 1).ToString();
                nextFlip = t + Mathf.Lerp(0.035f, 0.12f, k);
            }

            float spin = (1f - Mathf.Pow(1f - k, 3f)) * 360f;   // 빠르게 돌다가 감속
            bigHex.localRotation = Quaternion.Euler(0f, 0f, spin);
            yield return null;
        }

        // 착지: 최종 값 + 커졌다 돌아오는 펀치
        bigHex.localRotation = Quaternion.identity;
        bigNumber.text = finalValue.ToString();
        if (bonus != 0)
        {
            // 보정이 붙으면 금색(+) / 보라색(-) 숫자와 옆에 보정값 (상성·힘·허약·인내 등)
            Color bonusColor = bonus > 0 ? new Color(1f, 0.85f, 0.2f, alpha) : new Color(0.75f, 0.55f, 1f, alpha);
            bigNumber.color = bonusColor;
            boostText.color = bonusColor;
            boostText.text = bonus > 0 ? $"+{bonus}" : bonus.ToString();
        }
        yield return Punch(bigHex, BigHexSize, 1.35f, 0.15f);
    }

    /// <summary>이번 주사위의 승패 연출. 끝나면 슬롯이 '사용됨'으로 흐려진다.</summary>
    public IEnumerator ResultRoutine(Result result)
    {
        switch (result)
        {
            case Result.Win:
                Color baseRing = bigRing.color;
                bigRing.color = Color.white;
                bigFill.color = new Color(1f, 1f, 1f, 0.35f * alpha);
                yield return Punch(bigHex, BigHexSize, 1.3f, 0.2f);
                bigRing.color = baseRing;
                bigFill.color = new Color(0.07f, 0.07f, 0.09f, 0.9f * alpha);
                break;

            case Result.Lose:
                bigNumber.color = new Color(0.55f, 0.55f, 0.55f, alpha);
                yield return Shake(bigHex, 0.06f, 0.22f);
                SetBigAlpha(0.35f);
                break;

            case Result.Tie:
                yield return Shake(bigHex, 0.04f, 0.18f);
                break;
        }

        MarkSlotUsed(currentIndex);
    }

    // ─────────────────────────── 매 프레임 ───────────────────────────

    void LateUpdate()
    {
        if (target == null) { Destroy(gameObject); return; }
        FollowTarget();
    }

    private void FollowTarget()
    {
        // 머리 위 (HP/상태이상 표시보다 위)
        Vector3 pos = target.transform.position + Vector3.up * 2.6f;
        if (target.bodyRenderer != null)
        {
            Bounds b = target.bodyRenderer.bounds;
            pos = new Vector3(b.center.x, b.max.y + 1.15f, b.center.z);   // 상태창(장착 태엽판 줄 포함) 바로 위
        }
        transform.position = pos;

        Camera cam = Camera.main;
        if (cam != null) transform.rotation = cam.transform.rotation;   // 빌보드
    }

    // ─────────────────────────── 구성 ───────────────────────────

    private void Build(string cardName)
    {
        // 전체 폭을 계산해서 가운데 정렬 (큰 주사위 + 슬롯들)
        float slotsWidth = Mathf.Max(1, dice.Count) * SlotSpacing;
        float totalWidth = BigHexSize + 0.12f + slotsWidth;
        float left = -totalWidth * 0.5f;

        // 뒤에 어두운 상자를 깔아서 어떤 배경에서도 주사위/이름이 잘 보이게
        SpriteRenderer backing = ProceduralSprites.WorldBox(transform, "Backing",
            new Vector2(totalWidth + 0.34f, 0.9f), new Color(0.03f, 0.03f, 0.04f, 0.8f), 9);
        backing.transform.localPosition = new Vector3(0f, 0.37f, 0.01f);
        SpriteRenderer edge = ProceduralSprites.WorldBox(transform, "BackingEdge",
            new Vector2(totalWidth + 0.38f, 0.94f), new Color(0.75f, 0.6f, 0.35f, 0.55f), 8);   // 황동색 테두리
        edge.transform.localPosition = new Vector3(0f, 0.37f, 0.015f);

        // 큰 주사위 (왼쪽)
        bigHex = new GameObject("BigDie").transform;
        bigHex.SetParent(transform, false);
        bigHex.localPosition = new Vector3(left + BigHexSize * 0.5f, 0.3f, 0f);
        bigHex.localScale = Vector3.one * BigHexSize;
        bigFill = NewSprite(bigHex, HexFill(), new Color(0.07f, 0.07f, 0.09f, 0.9f), 10);
        bigRing = NewSprite(bigHex, HexRing(), Color.white, 11);

        // 숫자는 회전하지 않도록 bigHex가 아닌 루트에 둔다
        bigNumber = NewText(transform, "-", new Vector3(bigHex.localPosition.x, 0.3f, -0.01f), 0.05f, 72, TextAnchor.MiddleCenter, Color.white, 12);
        rangeText = NewText(transform, "", new Vector3(bigHex.localPosition.x, 0.6f, -0.01f), 0.022f, 64, TextAnchor.MiddleCenter, new Color(0.6f, 0.85f, 1f), 12);
        boostText = NewText(transform, "", new Vector3(bigHex.localPosition.x + BigHexSize * 0.45f, 0.48f, -0.02f), 0.02f, 64, TextAnchor.MiddleLeft, new Color(1f, 0.85f, 0.2f), 13);

        // 태엽판 이름 + 주사위 슬롯 (오른쪽)
        float slotsLeft = left + BigHexSize + 0.12f;
        NewText(transform, cardName, new Vector3(slotsLeft, 0.48f, -0.01f), 0.022f, 64, TextAnchor.MiddleLeft, new Color(1f, 0.75f, 0.55f), 12);

        for (int i = 0; i < dice.Count; i++)
        {
            DiceData die = dice[i];
            Transform slot = new GameObject("Slot" + i).transform;
            slot.SetParent(transform, false);
            slot.localPosition = new Vector3(slotsLeft + SlotSpacing * (i + 0.5f), 0.22f, 0f);
            slot.localScale = Vector3.one * SlotHexSize;

            Color c = die != null ? TypeColor(die.diceDetail) : Color.gray;
            slotFills.Add(NewSprite(slot, HexFill(), new Color(0.07f, 0.07f, 0.09f, 0.9f), 10));
            slotRings.Add(NewSprite(slot, HexRing(), c, 11));
            slotGlyphs.Add(NewText(transform, die != null ? TypeGlyph(die.diceDetail) : "?",
                new Vector3(slot.localPosition.x, 0.22f, -0.01f), 0.02f, 64, TextAnchor.MiddleCenter, c, 12));
        }
    }

    private void SelectSlot(int index)
    {
        currentIndex = index;
        for (int i = 0; i < slotRings.Count; i++)
        {
            if (i != index) continue;
            // 지금 굴리는 슬롯은 살짝 위로 + 커짐
            Transform slot = slotRings[i].transform.parent;
            slot.localScale = Vector3.one * SlotHexSize * 1.25f;
        }
    }

    private void MarkSlotUsed(int index)
    {
        if (index < 0 || index >= slotRings.Count) return;

        slotRings[index].transform.parent.localScale = Vector3.one * SlotHexSize;
        SetSpriteAlpha(slotRings[index], 0.3f);
        SetSpriteAlpha(slotFills[index], 0.3f);
        Color g = slotGlyphs[index].color;
        g.a = 0.3f;
        slotGlyphs[index].color = g;
    }

    private void SetBigColors(Color typeColor)
    {
        bigRing.color = new Color(typeColor.r, typeColor.g, typeColor.b, alpha);
        bigFill.color = new Color(0.07f, 0.07f, 0.09f, 0.9f * alpha);
        bigNumber.color = new Color(1f, 1f, 1f, alpha);
    }

    private void SetBigAlpha(float a)
    {
        SetSpriteAlpha(bigRing, a);
        SetSpriteAlpha(bigFill, a * 0.9f);
        Color c = bigNumber.color;
        c.a = a;
        bigNumber.color = c;
    }

    // ─────────────────────────── 애니메이션 유틸 ───────────────────────────

    private static IEnumerator Punch(Transform t, float baseScale, float peak, float duration)
    {
        float time = 0f;
        while (time < duration)
        {
            time += Time.deltaTime;
            float k = Mathf.Clamp01(time / duration);
            float s = Mathf.Lerp(peak, 1f, 1f - Mathf.Pow(1f - k, 2f));
            t.localScale = Vector3.one * baseScale * s;
            yield return null;
        }
        t.localScale = Vector3.one * baseScale;
    }

    private static IEnumerator Shake(Transform t, float strength, float duration)
    {
        Vector3 start = t.localPosition;
        float time = 0f;
        while (time < duration)
        {
            time += Time.deltaTime;
            float fade = 1f - Mathf.Clamp01(time / duration);
            t.localPosition = start + new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f) * strength * fade;
            yield return null;
        }
        t.localPosition = start;
    }

    private IEnumerator FadeOutRoutine()
    {
        float time = 0f;
        const float duration = 0.25f;

        SpriteRenderer[] sprites = GetComponentsInChildren<SpriteRenderer>();
        TextMesh[] texts = GetComponentsInChildren<TextMesh>();
        float[] spriteA = new float[sprites.Length];
        float[] textA = new float[texts.Length];
        for (int i = 0; i < sprites.Length; i++) spriteA[i] = sprites[i].color.a;
        for (int i = 0; i < texts.Length; i++) textA[i] = texts[i].color.a;

        while (time < duration)
        {
            time += Time.deltaTime;
            alpha = 1f - Mathf.Clamp01(time / duration);
            for (int i = 0; i < sprites.Length; i++) SetSpriteAlpha(sprites[i], spriteA[i] * alpha);
            for (int i = 0; i < texts.Length; i++)
            {
                Color c = texts[i].color;
                c.a = textA[i] * alpha;
                texts[i].color = c;
            }
            yield return null;
        }
        Destroy(gameObject);
    }

    // ─────────────────────────── 생성 유틸 ───────────────────────────

    private static SpriteRenderer NewSprite(Transform parent, Sprite sprite, Color color, int order)
    {
        GameObject go = new GameObject("Sprite");
        go.transform.SetParent(parent, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    private static TextMesh NewText(Transform parent, string text, Vector3 localPos, float charSize, int fontSize,
                                    TextAnchor anchor, Color color, int order)
    {
        GameObject go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;

        TextMesh tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.anchor = anchor;
        tm.alignment = TextAlignment.Center;
        tm.fontSize = fontSize;
        tm.characterSize = charSize;
        tm.fontStyle = FontStyle.Bold;
        tm.color = color;

        Font font = DamagePopup.GetBuiltinFont();
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        if (font != null)
        {
            tm.font = font;
            mr.sharedMaterial = font.material;
        }
        mr.sortingOrder = order;   // 육각형보다 앞에 그려지게
        return tm;
    }

    private static void SetSpriteAlpha(SpriteRenderer sr, float a)
    {
        Color c = sr.color;
        c.a = a;
        sr.color = c;
    }

    private static Color TypeColor(DiceDetail detail)
    {
        switch (detail)
        {
            case DiceDetail.Slash:  return new Color(1f, 0.38f, 0.32f);
            case DiceDetail.Thrust: return new Color(1f, 0.62f, 0.25f);
            case DiceDetail.Blunt:  return new Color(1f, 0.86f, 0.32f);
            case DiceDetail.Guard:  return new Color(0.38f, 0.64f, 1f);
            case DiceDetail.Evade:  return new Color(0.4f, 0.92f, 0.58f);
            default:                return Color.white;
        }
    }

    private static string TypeGlyph(DiceDetail detail)
    {
        switch (detail)
        {
            case DiceDetail.Slash:  return "참";
            case DiceDetail.Thrust: return "관";
            case DiceDetail.Blunt:  return "타";
            case DiceDetail.Guard:  return "가";
            case DiceDetail.Evade:  return "회";
            default:                return "?";
        }
    }

    // ─────────────────────────── 육각형 스프라이트 (코드로 생성) ───────────────────────────

    private static Sprite HexFill()
    {
        if (hexFillSprite == null) hexFillSprite = MakeHex(false);
        return hexFillSprite;
    }

    private static Sprite HexRing()
    {
        if (hexRingSprite == null) hexRingSprite = MakeHex(true);
        return hexRingSprite;
    }

    /// <summary>꼭짓점이 위아래인 정육각형. ring=true면 테두리만. 1 유닛 크기.</summary>
    private static Sprite MakeHex(bool ring)
    {
        const int size = 128;
        const float aa = 0.025f;          // 가장자리 부드럽게
        const float thickness = 0.13f;    // 테두리 두께 (반지름 대비)
        float radius = size * 0.48f;
        float halfWidth = radius * 0.8660254f;   // √3/2

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = Mathf.Abs(x - size * 0.5f + 0.5f);
                float py = Mathf.Abs(y - size * 0.5f + 0.5f);

                // 1 이하면 육각형 안쪽 (옆면 / 비스듬한 면 중 더 가까운 쪽 기준)
                float m = Mathf.Max(px / halfWidth, (py + px * 0.57735f) / radius);

                float a = 1f - Mathf.SmoothStep(1f - aa, 1f, m);
                if (ring) a *= Mathf.SmoothStep(1f - thickness - aa, 1f - thickness, m);

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();

        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        sprite.name = ring ? "HexRing" : "HexFill";
        return sprite;
    }
}
