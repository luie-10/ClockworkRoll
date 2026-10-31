using UnityEngine;

/// <summary>
/// 덱에서 꺼내진 카드 1장의 런타임 인스턴스.
/// DeckEntry(덱 단위 정보: 흡수 여부, 침식)와 CardData(원본 에셋)를 함께 들고 있다.
/// </summary>
public class CardInstance
{
    public DeckEntry entry;   // 덱 단위 정보 (없을 수 있음 — 적 카드 등)
    public CardData data;     // 원본 카드 데이터
    public CardView view;     // 손패에 표시된 UI (없을 수 있음 — 적 카드 등)

    public CardInstance(DeckEntry entry)
    {
        this.entry = entry;
        this.data = entry != null ? entry.data : null;
    }

    /// <summary>적 AI용 — 덱 정보 없이 순수 카드 데이터만으로 생성</summary>
    public CardInstance(CardData data)
    {
        this.entry = null;
        this.data = data;
    }
}
