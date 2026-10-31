using System;
using UnityEngine;

/// <summary>
/// 덱에 들어있는 카드 1장 단위의 정보.
/// - 시작 덱/보상 카드: absorbed = false, corruption = 0
/// - 적에게서 흡수한 카드: absorbed = true, corruption ≥ 1
///   → 사용할 때 확률적으로 '침식'이 발동해 위험 부작용이 생긴다.
/// </summary>
[Serializable]
public class DeckEntry
{
    public CardData data;       // 실제 카드 데이터 (흡수 카드는 런타임 클론)
    public bool absorbed;       // 적에게서 흡수했는가
    public int corruption;      // 침식 수치 (높을수록 위험)
    public string sourceName;   // 흡수 전 원본 이름 (중복 흡수 판별용)

    public DeckEntry(CardData data, bool absorbed, int corruption, string sourceName = null)
    {
        this.data = data;
        this.absorbed = absorbed;
        this.corruption = corruption;
        this.sourceName = sourceName != null ? sourceName : (data != null ? data.cardName : null);
    }
}
