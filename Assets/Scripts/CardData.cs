using System;
using System.Collections.Generic;
using UnityEngine;

#region 1. 카드를 구성하는 열거형 (Enums)
public enum CardRarity
{
    Paper,      // 일반 (종이)
    Stone,      // 고급 (돌)
    Iron,       // 희귀 (철)
    Gold,       // 예술
    EGO         // E.G.O
}

public enum DiceType
{
    Attack,     // 공격
    Defense     // 수비
}

public enum DiceDetail
{
    Slash,      // 참격
    Thrust,     // 관통
    Blunt,      // 타격
    Guard,      // 방어
    Evade       // 회피
}
#endregion

#region 2. 주사위 개별 데이터 클래스
[Serializable]
public class DiceData
{
    public string diceName;             // 주사위 설명 (생략 가능)
    public DiceType diceType;           // 공격 / 수비
    public DiceDetail diceDetail;       // 참격, 관통, 타격, 가드, 회피

    public int minRoll;                 // 최솟값
    public int maxRoll;                 // 최댓값

    [TextArea(2, 4)]
    public string diceEffectDescription; // [적중시], [합 승리시] 효과 텍스트
}
#endregion

#region 3. 전투 책장 스크립터블 오브젝트 (유니티 에셋 생성용)
[CreateAssetMenu(fileName = "NewCardData", menuName = "Ruina/Card Data", order = 1)]
public class CardData : ScriptableObject
{
    [Header("기본 정보")]
    public string cardName;             // 책장 이름
    public Sprite cardArt;              // 책장 일러스트
    public CardRarity rarity;           // 등급

    [Header("비용 및 조건")]
    public int cost;                    // 빛 코스트
    public int requireEmotionLevel;     // 필요 감정 단계

    [Header("책장 능력 설명")]
    [TextArea(3, 8)]
    public string cardDescription;      // [사용시], [무대 시작시] 효과 텍스트

    [Header("주사위 목록")]
    public List<DiceData> diceList = new List<DiceData>(); // 이 카드에 담긴 주사위 배열
}
#endregion
