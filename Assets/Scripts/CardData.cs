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
    OnClashLose,    // [합 패배시] 합에서 졌을 때
    OnUse           // [사용시]   태엽판을 써서 합이 시작될 때 (주사위 순서와 상관없이 맨 처음)
}

/// <summary>
/// 효과 종류 — 새 효과는 여기에 추가하고 BattleManager.ApplyDiceEffect에 처리를 넣는다.
/// 에셋에는 숫자로 저장되므로 순서를 바꾸지 말고 맨 뒤에만 추가할 것.
/// </summary>
public enum DiceEffectType
{
    Rust,           // 상대에게 녹 부여 (주사위 최댓값 감소)
    RecoverKi,      // 자신의 기력 회복 (플레이어만)
    DrawCard,       // 태엽판 뽑기 (플레이어만)
    Heal,           // 자신의 체력 회복
    Strength,       // 자신에게 힘 (공격 주사위 위력 +n)
    Endurance,      // 자신에게 인내 (수비 주사위 위력 +n)
    Feeble,         // 상대에게 허약 (공격 주사위 위력 -n)
    Fragile,        // 상대에게 취약 (공격받을 때 피해 +n)
    Paralysis,      // 상대에게 마비 (주사위 n개가 최솟값으로 굴러감)
    PowerNextDie,   // 이 태엽판의 다음 주사위 위력 +n
    DestroyNextDie  // 상대 태엽판의 다음 주사위 파괴
}

[Serializable]
public class DiceEffect
{
    public DiceTrigger trigger;
    public DiceEffectType effect;
    public int amount = 1;
    [Tooltip("체크: 다음 턴에 부여 (녹·힘·마비 같은 상태이상용)")]
    public bool nextTurn;

    /// <summary>카드/로그 표시용 짧은 문구 (예: "[적중시] 다음 턴 녹 1")</summary>
    public string Describe()
    {
        string when;
        switch (trigger)
        {
            case DiceTrigger.OnHit:      when = "[적중시]"; break;
            case DiceTrigger.OnClashWin: when = "[합 승리시]"; break;
            case DiceTrigger.OnUse:      when = "[사용시]"; break;
            default:                     when = "[합 패배시]"; break;
        }

        string what;
        switch (effect)
        {
            case DiceEffectType.Rust:           what = $"녹 {amount}"; break;
            case DiceEffectType.RecoverKi:      what = $"기력 {amount} 회복"; break;
            case DiceEffectType.DrawCard:       what = $"태엽판 {amount}장 뽑기"; break;
            case DiceEffectType.Heal:           what = $"체력 {amount} 회복"; break;
            case DiceEffectType.Strength:       what = $"힘 {amount} 얻음"; break;
            case DiceEffectType.Endurance:      what = $"인내 {amount} 얻음"; break;
            case DiceEffectType.Feeble:         what = $"허약 {amount}"; break;
            case DiceEffectType.Fragile:        what = $"취약 {amount}"; break;
            case DiceEffectType.Paralysis:      what = $"마비 {amount}"; break;
            case DiceEffectType.PowerNextDie:   what = $"다음 주사위 위력 +{amount}"; break;
            default:                            what = "상대의 다음 주사위 파괴"; break;
        }

        return nextTurn && IsStatus(effect) ? $"{when} 다음 턴 {what}" : $"{when} {what}";
    }

    /// <summary>턴 단위로 남는 상태이상인가 (nextTurn 체크가 의미 있는 효과)</summary>
    public static bool IsStatus(DiceEffectType type)
    {
        switch (type)
        {
            case DiceEffectType.Rust:
            case DiceEffectType.Strength:
            case DiceEffectType.Endurance:
            case DiceEffectType.Feeble:
            case DiceEffectType.Fragile:
            case DiceEffectType.Paralysis:
                return true;
            default:
                return false;
        }
    }
}
#endregion

#region 3. 전투 태엽판 스크립터블 오브젝트 (유니티 에셋 생성용)
[CreateAssetMenu(fileName = "NewCardData", menuName = "Ruina/Card Data", order = 1)]
public class CardData : ScriptableObject
{
    [Header("기본 정보")]
    public string cardName;             // 태엽판 이름
    public Sprite cardArt;              // 태엽판 일러스트
    public CardRarity rarity;           // 등급

    [Header("비용 및 조건")]
    public int cost;                    // 빛 코스트
    public int requireEmotionLevel;     // 필요 감정 단계

    [Header("태엽판 능력 설명")]
    [TextArea(3, 8)]
    public string cardDescription;      // [사용시], [무대 시작시] 효과 텍스트

    [Header("주사위 목록")]
    public List<DiceData> diceList = new List<DiceData>(); // 이 카드에 담긴 주사위 배열
}
#endregion
