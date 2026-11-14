using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 1종의 데이터
/// Project 창 우클릭 → Create → Ruina → Enemy Data 로 에셋 생성
/// </summary>
[CreateAssetMenu(fileName = "NewEnemy", menuName = "Ruina/Enemy Data", order = 3)]
public class EnemyData : ScriptableObject
{
    [Tooltip("종류 이름 (예: 골목 쥐) — 머리 위에 별명 옆에 작게 표시")]
    public string enemyName;
    [Tooltip("전투마다 이 중 하나를 무작위로 골라 개체 이름으로 쓴다 (같은 무대에서 겹치지 않게). 비워두면 Enemy Name 그대로")]
    public List<string> nicknames = new List<string>();
    public int maxHp = 40;

    [Header("내성 (참격/관통/타격에 맞을 때 피해 배율: 보통 x1.0 / 약점 x1.5 / 취약 x2.0)")]
    public ResistanceSet resistance = new ResistanceSet();

    [Header("2.5D 외형 (BattleManager의 Sprite Mode가 켜져 있을 때)")]
    [Tooltip("캐릭터 그림. 비워두면 Tint Color 색 실루엣으로 대체")]
    public Sprite sprite;
    [Tooltip("화면에서의 키 (월드 단위)")]
    public float spriteHeight = 2f;
    [Tooltip("합 중에 바뀌는 그림 (참격/관통/타격/가드/회피/피격/쓰러짐). 비워둔 칸은 기본 그림을 기울이는 임시 동작")]
    public PoseSprites poses = new PoseSprites();

    [Header("3D 외형 (Model Prefab이 있으면 Sprite보다 우선)")]
    public GameObject modelPrefab;
    [Tooltip("큐브/실루엣 색")]
    public Color tintColor = new Color(0.8f, 0.25f, 0.25f);

    [Header("시계판")]
    [Tooltip("한 라운드(시계판 한 바퀴)에 하는 행동 수 — 보스는 2 이상")]
    [Range(1, 4)]
    public int actionsPerRound = 1;

    [Header("유형 (정보창에 표시된다)")]
    [Tooltip("행동 유형 — 시계판의 어느 시각에, 어떤 순서로 태엽판을 놓는가")]
    public EnemyBehavior behavior = EnemyBehavior.Random;
    [Tooltip("특성 — 전투 내내 걸려 있는 고유 능력")]
    public EnemyTrait trait = EnemyTrait.None;
    [Tooltip("특성의 수치 n (특성마다 뜻이 다르다 — 철갑 n, 회복 n, 피해 n ...)")]
    [Min(0)]
    public int traitAmount = 1;

    [Header("이 적이 사용하는 태엽판(카드) 목록")]
    public List<CardData> deck = new List<CardData>();
}

/// <summary>
/// 적의 행동 유형 — 라운드마다 태엽판을 시계판의 어디에 놓는가.
/// 에셋에는 숫자로 저장되므로 순서를 바꾸지 말고 맨 뒤에만 추가할 것.
/// </summary>
public enum EnemyBehavior
{
    Random,     // 무작위: 아무 시각
    Rusher,     // 선공형: 이른 시각(1~4시)에 몰아친다
    Lurker,     // 후공형: 늦은 시각(끝이 9~12시)까지 기다린다
    Chimer,     // 정각형: 3·6·9·12시에 맞춰 시작한다
    Pendulum,   // 시계추형: 홀수 라운드는 이른 시각, 짝수 라운드는 늦은 시각
    Pattern,    // 순서형: 덱의 태엽판을 위에서부터 차례대로 쓴다 (읽을 수 있는 패턴)
    Berserker   // 광폭형: 체력이 절반 이하가 되면 라운드당 행동 +1
}

/// <summary>
/// 적의 특성 (패시브). n = EnemyData.traitAmount.
/// 에셋에는 숫자로 저장되므로 순서를 바꾸지 말고 맨 뒤에만 추가할 것.
/// </summary>
public enum EnemyTrait
{
    None,
    Shell,      // 껍질: 라운드 시작 시 철갑 n
    Regen,      // 자가 수리: 라운드 시작 시 체력 n 회복
    RustAura,   // 녹 기운: 라운드 시작 시 상대에게 녹 n
    Pack,       // 무리: 살아 있는 동료가 있으면 라운드 시작 시 힘 n
    Winding,    // 감김: 2라운드가 지날 때마다 힘 +n (최대 3)
    Punctual,   // 정시: 3·6·9·12시 칸에서 굴리면 위력 +n
    Counter,    // 반격: 수비 칸으로 합을 이기면 상대에게 피해 n
    Leech,      // 흡혈: 공격이 적중하면 체력 n 회복
    Volatile    // 폭발: 쓰러질 때 상대에게 피해 n
}

/// <summary>행동 유형 / 특성의 표시용 이름과 설명 (정보창)</summary>
public static class EnemyTypeInfo
{
    public const int WindingCap = 3;       // 감김으로 쌓이는 힘의 최대치
    public const int WindingInterval = 2;  // 감김: 몇 라운드마다 한 번 오르는가

    public static string Name(EnemyBehavior b)
    {
        switch (b)
        {
            case EnemyBehavior.Rusher:    return "선공형";
            case EnemyBehavior.Lurker:    return "후공형";
            case EnemyBehavior.Chimer:    return "정각형";
            case EnemyBehavior.Pendulum:  return "시계추형";
            case EnemyBehavior.Pattern:   return "순서형";
            case EnemyBehavior.Berserker: return "광폭형";
            default:                      return "";
        }
    }

    public static string Describe(EnemyBehavior b)
    {
        switch (b)
        {
            case EnemyBehavior.Rusher:    return "이른 시각(1~4시)에 몰아친다";
            case EnemyBehavior.Lurker:    return "늦은 시각까지 기다렸다 움직인다";
            case EnemyBehavior.Chimer:    return "3·6·9·12시에 맞춰 움직인다";
            case EnemyBehavior.Pendulum:  return "한 라운드는 이르게, 다음 라운드는 늦게 움직인다";
            case EnemyBehavior.Pattern:   return "태엽판을 정해진 순서대로 쓴다";
            case EnemyBehavior.Berserker: return "체력이 절반 이하가 되면 행동 +1";
            default:                      return "";
        }
    }

    public static string Name(EnemyTrait t)
    {
        switch (t)
        {
            case EnemyTrait.Shell:    return "껍질";
            case EnemyTrait.Regen:    return "자가 수리";
            case EnemyTrait.RustAura: return "녹 기운";
            case EnemyTrait.Pack:     return "무리";
            case EnemyTrait.Winding:  return "감김";
            case EnemyTrait.Punctual: return "정시";
            case EnemyTrait.Counter:  return "반격";
            case EnemyTrait.Leech:    return "흡혈";
            case EnemyTrait.Volatile: return "폭발";
            default:                  return "";
        }
    }

    public static string Describe(EnemyTrait t, int n)
    {
        switch (t)
        {
            case EnemyTrait.Shell:    return $"라운드마다 철갑 {n}";
            case EnemyTrait.Regen:    return $"라운드마다 체력 {n} 회복";
            case EnemyTrait.RustAura: return $"라운드마다 상대에게 녹 {n}";
            case EnemyTrait.Pack:     return $"살아 있는 동료가 있으면 라운드마다 힘 {n}";
            case EnemyTrait.Winding:  return $"{WindingInterval}라운드가 지날 때마다 힘 +{n} (최대 {WindingCap})";
            case EnemyTrait.Punctual: return $"3·6·9·12시 칸의 위력 +{n}";
            case EnemyTrait.Counter:  return $"수비 칸으로 합을 이기면 상대에게 피해 {n}";
            case EnemyTrait.Leech:    return $"공격이 적중하면 체력 {n} 회복";
            case EnemyTrait.Volatile: return $"쓰러질 때 상대에게 피해 {n}";
            default:                  return "";
        }
    }
}
