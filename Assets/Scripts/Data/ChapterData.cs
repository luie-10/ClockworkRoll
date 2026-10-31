using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>대사 한 줄 (화자 + 내용 + 초상화)</summary>
[Serializable]
public class DialogueLine
{
    public string speaker;                 // 화자 이름
    [TextArea(2, 5)] public string text;   // 대사 내용
    public Sprite portrait;                // 초상화 (없으면 숨김)
}

/// <summary>
/// 챕터 1개 = 인트로 대사 + 전투 무대(선택) + 아웃트로 대사 + 보상 카드
/// Project 창 우클릭 → Create → Ruina → Chapter 로 에셋 생성
/// </summary>
[CreateAssetMenu(fileName = "NewChapter", menuName = "Ruina/Chapter", order = 2)]
public class ChapterData : ScriptableObject
{
    public string chapterTitle;   // 챕터 제목 (예: "1막 2층. 환영의 꿈")

    [Header("전투 전 스토리 (인트로)")]
    public List<DialogueLine> introLines = new List<DialogueLine>();

    [Header("전투 무대 (비워두면 전투 없는 스토리 챕터)")]
    public BattleStageData battle;

    [Header("전투 승리 후 스토리 (아웃트로)")]
    public List<DialogueLine> outroLines = new List<DialogueLine>();

    [Header("클리어 보상 (보유 덱에 추가되는 카드)")]
    public List<CardData> rewardCards = new List<CardData>();
}
