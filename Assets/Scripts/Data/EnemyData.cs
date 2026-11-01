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

    [Header("이 적이 사용하는 태엽판(카드) 목록")]
    public List<CardData> deck = new List<CardData>();
}
