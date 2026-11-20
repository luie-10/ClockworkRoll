using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

#region 1. 카드를 구성하는 열거형 (Enums)
public enum CardRarity
{
    Scrap,      // 고철: 주워 온 그대로
    Handmade,   // 수제: 손으로 다시 맞춘 것
    Precision,  // 정밀: 톱니 하나까지 깎아 맞춘 것 (컷인)
    Masterwork, // 걸작: 장인이 이름을 새긴 것 (컷인 + 레터박스)
    Forbidden   // 금단: 만들면 안 되는 태엽
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
    DestroyNextDie, // 상대 태엽판의 다음 주사위 파괴
    Overwind,       // 과감기: 체력 n을 태워 이 태엽판의 모든 주사위 위력 +n ([사용시]에 쓴다)
    Resonance,      // 울림: 다른 모든 적에게도 피해 n (범위 공격 — 플레이어만)
    Corrode,        // 부식: 상대의 녹 x n 만큼 추가 피해
    Plating,        // 철갑: 자신에게 철갑 n (받는 피해 -n)
    OnTheHour,     // 정각: 이 주사위를 3·6·9·12시에 굴리면 위력 +n (발동 시점과 상관없이 늘 적용)
    MaxKi,          // 이번 전투 동안 최대 기력 +n (플레이어만)
    Lifesteal,      // 흡혈: 적중하면 준 피해만큼 회복 (최대 n)
    Tension,        // 조임: 장력 +n (플레이어만)
    Execute,        // 마무리: 상대의 체력이 절반 이하이면 이 칸 위력 +n (굴릴 때 적용)
    LastStand,      // 배수진: 내 체력이 절반 이하이면 이 칸 위력 +n (굴릴 때 적용)
    Combo,          // 연쇄: 이번 라운드에 먼저 굴린 내 칸 하나마다 이 칸 위력 +1, 최대 n (굴릴 때 적용)
    Unopposed,      // 빈틈: 맞서는 상대 칸이 없으면(일방 공격) 이 칸 위력 +n (굴릴 때 적용)
    Burn,           // 과열: 상대에게 과열 n — 라운드가 시작될 때마다 n 피해를 주고 절반으로 식는다
    Dawn,           // 아침: 시계판 1~6시에 굴리면 이 칸 위력 +n (굴릴 때 적용)
    Dusk,           // 밤: 시계판 7~12시에 굴리면 이 칸 위력 +n (굴릴 때 적용)
    // ── 도박 ──
    Gamble,         // 도박: 굴릴 때 반반 — 위력 +n 또는 -n (굴릴 때 적용)
    Jackpot,        // 대박: 굴린 눈이 최댓값이면 위력 +n (굴릴 때 적용)
    DoubleOrNothing,// 모 아니면 도: 굴릴 때 반반 — 굴린 눈이 두 배 또는 0 (굴릴 때 적용)
    CoinFlip,       // 동전 던지기: 앞면이면 상대에게 피해 n, 뒷면이면 자신에게 피해 n/2 (체력 1은 남는다)
    Roulette,       // 룰렛: 여섯 칸 중 하나 — 기력 · 뽑기 · 힘 · 회복 (좋음) / 녹 · 마비 (나쁨). 수치 n
    AllIn,          // 올인: [사용시] 남은 기력을 모두 태워 기력 1당 이 태엽판 모든 칸 위력 +n (플레이어만)
    // ── 즉발 태엽판용 ──
    Damage,         // 피해: 상대에게 피해 n (취약 · 철갑 적용)
    DamageAll       // 광역 피해: 모든 적에게 피해 n
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
            case DiceEffectType.RecoverKi:      what = $"다음 라운드 기력 +{amount}"; break;
            case DiceEffectType.MaxKi:          what = $"이번 전투 최대 기력 +{amount}"; break;
            case DiceEffectType.DrawCard:       what = $"태엽판 {amount}장 뽑기"; break;
            case DiceEffectType.Heal:           what = $"체력 {amount} 회복"; break;
            case DiceEffectType.Strength:       what = $"힘 {amount} 얻음"; break;
            case DiceEffectType.Endurance:      what = $"인내 {amount} 얻음"; break;
            case DiceEffectType.Feeble:         what = $"허약 {amount}"; break;
            case DiceEffectType.Fragile:        what = $"취약 {amount}"; break;
            case DiceEffectType.Paralysis:      what = $"마비 {amount}"; break;
            case DiceEffectType.PowerNextDie:   what = $"다음 칸 위력 +{amount}"; break;
            case DiceEffectType.Overwind:       what = $"과감기 {amount}"; break;
            case DiceEffectType.Resonance:      what = $"울림 {amount}"; break;
            case DiceEffectType.Corrode:        what = $"부식 x{amount}"; break;
            case DiceEffectType.Plating:        what = $"철갑 {amount}"; break;
            case DiceEffectType.OnTheHour:      return $"[정각] 위력 +{amount}";   // 발동 시점이 아니라 굴리는 시각으로 정해진다
            // 아래 넷도 발동 시점이 아니라 굴리는 순간의 상황으로 정해진다
            case DiceEffectType.Execute:        return $"[마무리] 위력 +{amount}";
            case DiceEffectType.LastStand:      return $"[배수진] 위력 +{amount}";
            case DiceEffectType.Combo:          return $"[연쇄] 위력 최대 +{amount}";
            case DiceEffectType.Unopposed:      return $"[빈틈] 위력 +{amount}";
            case DiceEffectType.Dawn:           return $"[아침] 위력 +{amount}";
            case DiceEffectType.Dusk:           return $"[밤] 위력 +{amount}";
            case DiceEffectType.Gamble:         return $"[도박] 위력 ±{amount}";
            case DiceEffectType.Jackpot:        return $"[대박] 최댓값이면 위력 +{amount}";
            case DiceEffectType.DoubleOrNothing:return "[모 아니면 도] 두 배 또는 0";
            case DiceEffectType.CoinFlip:       what = $"동전 던지기 {amount}"; break;
            case DiceEffectType.Roulette:       what = $"룰렛 {amount}"; break;
            case DiceEffectType.AllIn:          what = $"올인 (기력 1당 위력 +{amount})"; break;
            case DiceEffectType.Damage:         what = $"피해 {amount}"; break;
            case DiceEffectType.DamageAll:      what = $"모든 적에게 피해 {amount}"; break;
            case DiceEffectType.Lifesteal:      what = $"흡혈 {amount}"; break;
            case DiceEffectType.Tension:        what = $"장력 +{amount}"; break;
            case DiceEffectType.Burn:           what = $"과열 {amount}"; break;
            default:                            what = "상대의 다음 칸 파괴"; break;
        }

        return nextTurn && IsStatus(effect) ? $"{when} 다음 턴 {what}" : $"{when} {what}";
    }

    /// <summary>굴리는 순간의 상황으로 위력이 정해지는 효과인가 (발동 시점이 따로 없다)</summary>
    public static bool IsRollTime(DiceEffectType type)
    {
        return type == DiceEffectType.OnTheHour || type == DiceEffectType.Execute || type == DiceEffectType.LastStand
            || type == DiceEffectType.Combo || type == DiceEffectType.Unopposed
            || type == DiceEffectType.Dawn || type == DiceEffectType.Dusk
            || type == DiceEffectType.Gamble || type == DiceEffectType.Jackpot || type == DiceEffectType.DoubleOrNothing;
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
            case DiceEffectType.Plating:
                return true;
            default:
                return false;
        }
    }
}
#endregion

#region 3. 전투 태엽판 스크립터블 오브젝트 (유니티 에셋 생성용)
[CreateAssetMenu(fileName = "NewCardData", menuName = "ClockworkRoll/Card Data", order = 1)]
public class CardData : ScriptableObject
{
    [Header("기본 정보")]
    public string cardName;             // 태엽판 이름
    public Sprite cardArt;              // 태엽판 일러스트
    public CardRarity rarity;           // 등급

    [Header("비용 및 조건")]
    public int cost;                    // 빛 코스트

    [Tooltip("이 단계 이상의 장력(0~3)이 쌓여야 낼 수 있다. 0 = 언제나")]
    [Range(0, 3)]
    [FormerlySerializedAs("requireEmotionLevel")]
    public int requireTension;          // 필요 장력 단계

    [Header("키워드")]
    [Tooltip("소멸: 한 번 쓰면 이번 전투에서 사라진다 (버림패로 가지 않는다)")]
    public bool exhaust;
    [Tooltip("선천: 전투가 시작될 때 반드시 첫 손패에 들어온다")]
    public bool innate;

    [Header("즉발 (시계판에 놓지 않고 누르는 즉시 쓴다)")]
    [Tooltip("즉발: 손패에서 누르면 바로 기력을 내고 아래 효과가 발동한다. 칸(주사위)이 없다")]
    public bool instant;
    [Tooltip("즉발 효과 — 발동 시점은 무시하고 쓰는 순간 모두 발동 (대상 = 지정한 적)")]
    public List<DiceEffect> instantEffects = new List<DiceEffect>();

    [Header("태엽판 능력 설명")]
    [TextArea(3, 8)]
    public string cardDescription;      // [사용시], [무대 시작시] 효과 텍스트

    [Header("칸 목록")]
    public List<DiceData> diceList = new List<DiceData>(); // 이 카드에 담긴 주사위 배열
}
#endregion
