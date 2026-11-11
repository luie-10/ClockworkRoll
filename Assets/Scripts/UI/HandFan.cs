using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 손패를 부채꼴(큰 원의 호)로 펼친다. HandManager가 손패 패널에 알아서 붙인다.
///   - 가운데 카드가 가장 높고, 바깥 카드일수록 내려가며 바깥으로 기운다
///   - 뒤에 뽑은 카드가 앞 카드의 오른쪽을 덮는다 → 왼쪽의 기력 배지는 겹쳐도 보인다
///   - 마우스를 올린 카드는 똑바로 서서 들리고, 옆 카드들은 비켜난다
/// </summary>
public class HandFan : MonoBehaviour
{
    [Tooltip("호의 반지름 — 클수록 완만하다")]
    public float radius = 1700f;
    [Tooltip("카드 사이 각도(도)")]
    public float stepDegrees = 5f;
    [Tooltip("손패 전체가 벌어지는 최대 각도(도)")]
    public float maxSpreadDegrees = 32f;
    [Tooltip("가운데 카드의 높이")]
    public float baseY = 70f;
    public float hoverLift = 50f;
    public float selectedLift = 30f;
    [Tooltip("마우스를 올린 카드 옆 카드가 비켜나는 거리")]
    public float neighborPush = 50f;
    public float followSpeed = 14f;
    [Tooltip("시계가 도는 동안(내 차례가 아닐 때) 손패가 내려가는 거리 — 카드 윗부분만 남아 싸움을 가리지 않는다")]
    public float tuckDrop = 205f;

    private static readonly Vector2 Anchor = new Vector2(0.5f, 0f);
    private readonly List<CardView> cards = new List<CardView>();

    void Awake()
    {
        HorizontalLayoutGroup row = GetComponent<HorizontalLayoutGroup>();
        if (row != null) row.enabled = false;   // 자리는 여기서 잡는다
    }

    void LateUpdate()
    {
        cards.Clear();
        int hovered = -1;
        foreach (Transform child in transform)
        {
            CardView view = child.GetComponent<CardView>();
            if (view == null || !child.gameObject.activeSelf) continue;
            if (view.IsHovered) hovered = cards.Count;
            cards.Add(view);
        }

        // 시계가 도는 동안에는 손패를 쓸 수 없으니 아래로 내려 둔다 (마우스를 올려도 들리지 않는다)
        BattleManager bm = BattleManager.Instance;
        bool tucked = bm != null && bm.Phase != BattleManager.BattlePhase.PlayerTurn && bm.Phase != BattleManager.BattlePhase.Ready;
        if (tucked) hovered = -1;

        int n = cards.Count;
        float step = n > 1 ? Mathf.Min(stepDegrees, maxSpreadDegrees / (n - 1)) : 0f;
        float k = 1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime);

        for (int i = 0; i < n; i++)
        {
            RectTransform rt = (RectTransform)cards[i].transform;
            if (rt.anchorMin != Anchor || rt.anchorMax != Anchor)
            {
                rt.anchorMin = rt.anchorMax = Anchor;
                rt.anchoredPosition = new Vector2(0f, -220f);   // 새 카드는 화면 아래에서 올라온다
            }

            float angle = (i - (n - 1) * 0.5f) * step;
            float rad = angle * Mathf.Deg2Rad;
            Vector2 pos = new Vector2(Mathf.Sin(rad) * radius, baseY - radius * (1f - Mathf.Cos(rad)));
            float tilt = -angle;

            if (i == hovered)
            {
                pos.y = baseY + hoverLift;
                tilt = 0f;
            }
            else
            {
                if (hovered >= 0) pos.x += Mathf.Sign(i - hovered) * neighborPush / Mathf.Abs(i - hovered);
                if (cards[i].IsSelected) pos.y += selectedLift;
            }

            if (tucked) pos.y -= tuckDrop;

            rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, pos, k);
            rt.localRotation = Quaternion.Slerp(rt.localRotation, Quaternion.Euler(0f, 0f, tilt), k);
        }
    }
}
