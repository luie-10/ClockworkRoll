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
    public string diceEffectDescription; // [적중시], [합 승리시] 효과 텍스트 (설명용 — 실제 효과는 아래 effects)

    [Tooltip("실제로 발동하는 효과 목록 (+ 버튼으로 추가)")]
    public List<DiceEffect> effects = new List<DiceEffect>();
}
#endregion

#region 2-1. 주사위 효과 (스킬)
/// <summary>효과가 발동하는 시점</summary>
public enum DiceTrigger
{
    OnHit,          // [적중시]   공격 주사위가 합을 이겨 피해를 줬을 때
    OnClashWin,     // [합 승리시] 합에서 이겼을 때 (수비 주사위 포함)
    OnClashLose     // [합 패배시] 합에서 졌을 때
}

/// <summary>효과 종류 — 새 효과는 여기에 추가하고 BattleManager.ApplyDiceEffect에 처리를 넣는다</summary>
public enum DiceEffectType
{
    Bleed,          // 상대에게 출혈 부여
    RecoverKi,      // 자신의 기력 회복 (플레이어만)
    DrawCard        // 책장 뽑기 (플레이어만)
}

[Serializable]
public class DiceEffect
{
    public DiceTrigger trigger;
    public DiceEffectType effect;
    public int amount = 1;
    [Tooltip("체크: 다음 턴에 부여 (출혈 등 상태이상용)")]
    public bool nextTurn;

    /// <summary>카드/로그 표시용 짧은 문구 (예: "[적중시] 다음 턴 출혈 1")</summary>
    public string Describe()
    {
        string when;
        switch (trigger)
        {
            case DiceTrigger.OnHit:      when = "[적중시]"; break;
            case DiceTrigger.OnClashWin: when = "[합 승리시]"; break;
            default:                     when = "[합 패배시]"; break;
        }

        string what;
        switch (effect)
        {
            case DiceEffectType.Bleed:     what = $"출혈 {amount}"; break;
            case DiceEffectType.RecoverKi: what = $"기력 {amount} 회복"; break;
            default:                       what = $"책장 {amount}장 뽑기"; break;
        }

        return nextTurn ? $"{when} 다음 턴 {what}" : $"{when} {what}";
    }
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
