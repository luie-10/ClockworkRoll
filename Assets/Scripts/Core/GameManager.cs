using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 전체 진행을 관리하는 최상위 매니저.
/// 한 씬 안에서 상태만 전환하며 [스토리 → 전투 → 승리/보상 → 다음 스토리]를 반복한다.
///
/// 덱 성장 시스템:
/// - 전투에서 이기면 쓰러뜨린 적의 태엽판을 내 덱으로 '흡수'한다 (◈ 표시).
/// - 흡수 카드는 강력하지만, 사용할 때 확률적으로 '침식'이 발동해 사용자가 피해를 입는다.
/// - 같은 태엽판을 중복 흡수하면 침식 수치가 누적되어 발동 확률과 피해가 커진다.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState
    {
        Story,      // 대사 진행 중
        Battle,     // 전투 진행 중
        Clear,      // 모든 챕터 클리어
        GameOver    // 패배
    }

    [Header("스토리 (넣으면 아래 Chapters / Starting Deck 대신 이 에셋의 것을 사용)")]
    public StoryData story;

    [Header("챕터 목록 (진행 순서대로 등록)")]
    public List<ChapterData> chapters = new List<ChapterData>();

    [Header("씬 배치 참조")]
    public Unit playerUnit;
    public DialogueManager dialogueManager;
    public BattleManager battleManager;

    [Header("플레이어가 처음부터 가지는 덱(태엽판)")]
    public List<CardData> startingDeck = new List<CardData>();

    [Header("침식(흡수 카드 부작용) 설정")]
    [Tooltip("침식 1인 흡수 카드를 사용할 때 발동 확률")]
    public float corruptionBaseChance = 0.20f;
    [Tooltip("침식 1pt당 추가되는 발동 확률")]
    public float corruptionChancePerPoint = 0.05f;

    /// <summary>현재 보유 덱 (적 태엽판 흡수 + 챕터 보상으로 계속 불어난다)</summary>
    public List<DeckEntry> OwnedDeck { get; private set; }

    public GameState State { get; private set; }
    public int ChapterIndex { get; private set; } = -1;

    /// <summary>상태가 바뀔 때마다 호출 (UI 갱신용으로 구독)</summary>
    public event Action<GameState> OnStateChanged;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        // 스토리 에셋 하나로 챕터 순서 + 시작 덱을 한 번에 지정
        if (story != null)
        {
            chapters = new List<ChapterData>(story.chapters);
            startingDeck = new List<CardData>(story.startingDeck);
            Debug.Log($"── 스토리: {story.storyTitle} ──");
        }

        OwnedDeck = new List<DeckEntry>();
        if (startingDeck != null)
        {
            foreach (CardData card in startingDeck)
            {
                if (card != null) OwnedDeck.Add(new DeckEntry(card, false, 0));
            }
        }

        NextChapter();
    }

    // ─────────────────────────── 챕터 진행 ───────────────────────────

    /// <summary>다음 챕터로 진행. 마지막 챕터면 클리어 상태로 전환.</summary>
    public void NextChapter()
    {
        ChapterIndex++;

        if (ChapterIndex >= chapters.Count)
        {
            SetState(GameState.Clear);
            Debug.Log("── 모든 챕터 클리어! 게임 클리어 ──");
            return;
        }

        Debug.Log($"── [{ChapterIndex + 1}] {chapters[ChapterIndex].chapterTitle} ──");
        PlayChapterIntro();
    }

    private void PlayChapterIntro()
    {
        SetState(GameState.Story);
        ChapterData chapter = chapters[ChapterIndex];

        if (dialogueManager == null)
        {
            Debug.LogError("GameManager에 DialogueManager가 연결되지 않았습니다.");
            return;
        }

        // 인트로 대사가 없으면 바로 전투로
        if (chapter.introLines == null || chapter.introLines.Count == 0)
        {
            StartChapterBattle();
            return;
        }

        dialogueManager.PlayLines(chapter.introLines, StartChapterBattle);
    }

    /// <summary>인트로 대사가 끝나면 호출됨.</summary>
    private void StartChapterBattle()
    {
        ChapterData chapter = chapters[ChapterIndex];

        // 전투가 없는 순수 스토리 챕터 → 승리 처리로 아웃트로/다음 챕터 진행
        if (chapter.battle == null)
        {
            OnBattleWon();
            return;
        }

        if (battleManager == null)
        {
            Debug.LogError("GameManager에 BattleManager가 연결되지 않았습니다.");
            return;
        }

        if (playerUnit == null)
        {
            Debug.LogError("GameManager에 Player Unit이 연결되지 않았습니다.");
            return;
        }

        SetState(GameState.Battle);
        battleManager.StartBattle(chapter.battle, playerUnit);
    }

    // ─────────────────────────── 전투 결과 수신 ───────────────────────────

    /// <summary>BattleManager가 승리 시 호출.</summary>
    public void OnBattleWon()
    {
        ChapterData chapter = chapters[ChapterIndex];

        // 1) 쓰러뜨린 적들의 태엽판을 흡수한다 (덱 오염 = 이 게임의 핵심 트레이드오프)
        if (chapter.battle != null && chapter.battle.enemies != null)
        {
            List<CardData> spoils = new List<CardData>();
            foreach (EnemySpawnInfo info in chapter.battle.enemies)
            {
                if (info == null || info.enemy == null || info.enemy.deck == null) continue;
                spoils.AddRange(info.enemy.deck);
            }
            AbsorbCards(spoils);
        }

        // 2) 기존 보상 카드 지급 (침식 없는 순수 획득)
        if (chapter.rewardCards != null)
        {
            foreach (CardData reward in chapter.rewardCards)
            {
                if (reward == null) continue;
                OwnedDeck.Add(new DeckEntry(reward, false, 0));
                Debug.Log($"보상 태엽판 획득: [{reward.cardName}]");
            }
        }

        // 3) 아웃트로 대사 → 다음 챕터
        if (chapter.outroLines != null && chapter.outroLines.Count > 0)
        {
            SetState(GameState.Story);
            dialogueManager.PlayLines(chapter.outroLines, NextChapter);
        }
        else
        {
            NextChapter();
        }
    }

    /// <summary>BattleManager가 패배 시 호출.</summary>
    public void OnBattleLost()
    {
        SetState(GameState.GameOver);
        Debug.Log("패배했습니다. 재시작 버튼 등으로 RestartScene()을 호출하세요.");
    }

    // ─────────────────────────── 흡수 시스템 ───────────────────────────

    /// <summary>
    /// 적의 태엽판을 내 덱으로 흡수한다.
    /// 원본 에셋은 절대 건드리지 않고 런타임 클론에 ◈ 표식을 남긴다.
    /// 중복 흡수 시 침식 수치만 +1 (같은 태엽판을 또 삼키면 원한이 깊어진다).
    /// </summary>
    public void AbsorbCards(List<CardData> cards)
    {
        if (cards == null) return;

        foreach (CardData card in cards)
        {
            if (card == null) continue;

            DeckEntry existing = OwnedDeck.Find(
                e => e.absorbed && e.sourceName == card.cardName);

            if (existing != null)
            {
                existing.corruption++;
                Debug.Log($"태엽판 [{card.cardName}] 재흡수 — 침식이 깊어진다 ({existing.corruption})");
            }
            else
            {
                CardData clone = Instantiate(card);
                clone.name = card.cardName + " ◈";
                // ◈ 표식은 CardView가 붙이므로 cardName은 원본 그대로 둔다 (◈◈ 중복 방지)

                OwnedDeck.Add(new DeckEntry(clone, true, 1, card.cardName));
                Debug.Log($"적의 태엽판 흡수: [{card.cardName}] ◈ (침식 1)");
            }
        }
    }

    /// <summary>침식 수치에 대응하는 '침식 발동' 확률 (0~0.8로 제한).</summary>
    public float CorruptionChance(int corruption)
    {
        return Mathf.Min(0.8f, corruptionBaseChance + corruptionChancePerPoint * Mathf.Max(0, corruption - 1));
    }

    // ─────────────────────────── 기타 ───────────────────────────

    /// <summary>패배 UI의 버튼에 연결해서 사용.</summary>
    public void RestartScene()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    private void SetState(GameState state)
    {
        State = state;
        if (OnStateChanged != null) OnStateChanged(state);
    }
}
