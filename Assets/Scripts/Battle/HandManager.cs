using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어의 덱/손패를 관리.
/// 덱은 DeckEntry(카드 + 흡수/침식 정보) 단위로 다룬다.
/// 턴 시작마다 손패를 채우고, 카드 사용 시 버림패로 보낸 뒤
/// 뽑을 더미가 비면 버림패를 다시 셔플해 순환시킨다.
/// </summary>
public class HandManager : MonoBehaviour
{
    [Header("참조")]
    public CardView cardViewPrefab;   // 카드 UI 프리팹
    public Transform handPanel;       // 손패가 놓일 패널 (Horizontal Layout Group 권장)

    [Header("설정")]
    public int handLimit = 7;         // 손패 최대 장수

    private readonly List<DeckEntry> drawPile = new List<DeckEntry>();     // 뽑을 더미
    private readonly List<DeckEntry> discardPile = new List<DeckEntry>();  // 버린 더미
    private readonly List<CardInstance> hand = new List<CardInstance>();   // 현재 손패

    public int HandCount { get { return hand.Count; } }

    /// <summary>전투 시작 시 덱 구성 (기존 손패/더미 전부 초기화)</summary>
    public void BuildDeck(List<DeckEntry> deck)
    {
        drawPile.Clear();
        discardPile.Clear();
        hand.Clear();

        // 이전 전투의 손패 UI 정리
        Transform panel = handPanel != null ? handPanel : transform;
        for (int i = panel.childCount - 1; i >= 0; i--)
            Destroy(panel.GetChild(i).gameObject);

        if (deck != null) drawPile.AddRange(deck);
        Shuffle(drawPile);
    }

    /// <summary>손패가 가득 찰 때까지(또는 더미가 빌 때까지) 뽑는다.</summary>
    public void RefillHand()
    {
        while (hand.Count < handLimit && DrawOne()) { }
    }

    /// <summary>태엽판 n장 뽑기 (주사위 효과용). 손패 한도를 넘지 않는다. 실제로 뽑은 장수를 돌려준다.</summary>
    public int DrawCards(int count)
    {
        int drawn = 0;
        while (drawn < count && hand.Count < handLimit && DrawOne()) drawn++;
        return drawn;
    }

    private bool DrawOne()
    {
        // 뽑을 더미가 비었으면 버림패를 셔플해서 재사용
        if (drawPile.Count == 0)
        {
            if (discardPile.Count == 0) return false;

            drawPile.AddRange(discardPile);
            discardPile.Clear();
            Shuffle(drawPile);
        }

        DeckEntry entry = drawPile[0];
        drawPile.RemoveAt(0);

        CardInstance instance = new CardInstance(entry);
        hand.Add(instance);
        CreateView(instance);
        return true;
    }

    private void CreateView(CardInstance instance)
    {
        if (cardViewPrefab == null)
        {
            Debug.LogError("HandManager에 CardView 프리팹이 지정되지 않았습니다.");
            return;
        }

        Transform panel = handPanel != null ? handPanel : transform;
        CardView view = Instantiate(cardViewPrefab, panel);
        view.gameObject.SetActive(true);   // 비활성 템플릿에서 복제된 경우를 대비
        view.Bind(instance, OnCardViewClicked);
    }

    private void OnCardViewClicked(CardView view)
    {
        if (BattleManager.Instance != null)
            BattleManager.Instance.OnCardClicked(view);
    }

    /// <summary>카드 사용 → 손패에서 제거하고 버림패로 보낸다.</summary>
    public void UseCard(CardInstance card)
    {
        if (card == null) return;

        hand.Remove(card);

        if (card.entry != null)
            discardPile.Add(card.entry);

        if (card.view != null) Destroy(card.view.gameObject);
    }

    /// <summary>응수 장착: 손패에서 빼서 따로 보관 (아직 버림패로 가지 않음).</summary>
    public void ReserveCard(CardInstance card)
    {
        if (card == null) return;
        hand.Remove(card);
        if (card.view != null) Destroy(card.view.gameObject);
        card.view = null;
    }

    /// <summary>응수 해제: 보관하던 카드를 손패로 되돌린다.</summary>
    public void ReturnToHand(CardInstance card)
    {
        if (card == null) return;
        hand.Add(card);
        CreateView(card);
    }

    /// <summary>응수로 쓴 카드를 버림패로 보낸다.</summary>
    public void DiscardReserved(CardInstance card)
    {
        if (card != null && card.entry != null) discardPile.Add(card.entry);
    }

    /// <summary>손패에서 무작위 한 장 (자동 진행 테스트용 — 카드를 쓰지는 않음).</summary>
    public CardInstance GetRandomHandCard()
    {
        if (hand.Count == 0) return null;
        return hand[Random.Range(0, hand.Count)];
    }

    private void Shuffle(List<DeckEntry> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            DeckEntry temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
}
