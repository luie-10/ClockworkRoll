using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스토리 1개 = 챕터 순서 + 플레이어 시작 덱.
/// GameManager의 Story 칸에 넣으면 그 스토리로 게임이 진행된다.
/// Project 창 우클릭 → Create → Ruina → Story 로 에셋 생성
/// </summary>
[CreateAssetMenu(fileName = "NewStory", menuName = "Ruina/Story", order = 0)]
public class StoryData : ScriptableObject
{
    public string storyTitle;
    [TextArea(2, 5)] public string summary;   // 줄거리 메모 (게임에는 표시되지 않음)

    [Header("플레이어가 처음부터 가지는 덱(책장)")]
    public List<CardData> startingDeck = new List<CardData>();

    [Header("챕터 (진행 순서대로)")]
    public List<ChapterData> chapters = new List<ChapterData>();
}
