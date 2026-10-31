using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>무대에 등장할 적 1마리의 배치 정보</summary>
[Serializable]
public class EnemySpawnInfo
{
    public EnemyData enemy;
    public Vector3 spawnPosition;   // 월드 좌표 (적 프리팹이 생성될 위치)
}

/// <summary>
/// 전투 무대 1개의 데이터
/// Project 창 우클릭 → Create → Ruina → Battle Stage 로 에셋 생성
/// </summary>
[CreateAssetMenu(fileName = "NewStage", menuName = "Ruina/Battle Stage", order = 4)]
public class BattleStageData : ScriptableObject
{
    public string stageName;   // 무대 이름 (예: "1막 2층 4부 후반 무대")

    [Tooltip("이 무대의 배경 (씬에 해당 배경 오브젝트가 있어야 함)")]
    public BackdropType backdrop = BackdropType.Alley;

    [Header("기력 (자원)")]
    public int startKi = 3;      // 무대 시작 시 기력
    public int kiPerTurn = 1;    // 턴마다 회복되는 기력

    [Header("등장하는 적들")]
    public List<EnemySpawnInfo> enemies = new List<EnemySpawnInfo>();

    [Header("덱 오버라이드 (비워두면 GameManager의 보유 덱 사용)")]
    public List<CardData> deckOverride = new List<CardData>();
}
