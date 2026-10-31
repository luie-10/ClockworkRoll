using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 1종의 데이터
/// Project 창 우클릭 → Create → Ruina → Enemy Data 로 에셋 생성
/// </summary>
[CreateAssetMenu(fileName = "NewEnemy", menuName = "Ruina/Enemy Data", order = 3)]
public class EnemyData : ScriptableObject
{
    public string enemyName;
    public int maxHp = 40;

    [Header("내성 (참격/관통/타격에 맞을 때 피해 배율: 보통 x1.0 / 약점 x1.5 / 취약 x2.0)")]
    public ResistanceSet resistance = new ResistanceSet();

    [Header("2.5D 외형 (BattleManager의 Sprite Mode가 켜져 있을 때)")]
    [Tooltip("캐릭터 그림. 비워두면 Tint Color 색 실루엣으로 대체")]
    public Sprite sprite;
    [Tooltip("화면에서의 키 (월드 단위)")]
    public float spriteHeight = 2f;

    [Header("3D 외형 (Model Prefab이 있으면 Sprite보다 우선)")]
    public GameObject modelPrefab;
    [Tooltip("큐브/실루엣 색")]
    public Color tintColor = new Color(0.8f, 0.25f, 0.25f);

    [Header("이 적이 사용하는 책장(카드) 목록")]
    public List<CardData> deck = new List<CardData>();
}
