using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스토리 상태의 대사 재생을 담당.
/// 좌클릭 / Space로 진행. 타이핑 중 클릭하면 문장 전체가 먼저 나온다.
/// 재생이 끝나면 GameManager가 넘겨준 콜백(전투 시작 등)이 실행된다.
/// </summary>
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [Header("UI 참조")]
    public GameObject dialoguePanel;    // 대사창 패널 (통째로 켜고 끔)
    public Text speakerNameText;        // 화자 이름
    public Text bodyText;               // 대사 내용
    public Image portraitImage;         // 초상화

    [Header("설정")]
    public float typeSpeed = 0.03f;     // 글자 하나당 출력 간격(초)

    [Header("화자별 기본 초상화 (대사 줄에 Portrait가 비어 있으면 이름으로 찾아 씀)")]
    public List<SpeakerPortrait> speakerPortraits = new List<SpeakerPortrait>();

    [Serializable]
    public class SpeakerPortrait
    {
        public string speaker;   // 대사의 Speaker와 똑같이 (예: "이안", "틱", "종지기")
        public Sprite portrait;
    }

    private List<DialogueLine> lines;

    // 초상화가 없을 때 이름표/대사를 왼쪽으로 당겨 빈 공간이 생기지 않게 하기 위한 원래 위치
    private RectTransform speakerRect;
    private float bodyLeft, speakerX, portraitShift;
    private int index;
    private Action onComplete;
    private Coroutine typingRoutine;
    private bool isTyping;

    public bool IsPlaying { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (dialoguePanel != null) dialoguePanel.SetActive(false);

        if (portraitImage != null && bodyText != null && speakerNameText != null)
        {
            // 이름표 상자(SpeakerPlate)가 있으면 상자째로, 없으면 글자만 옮긴다
            Transform parent = speakerNameText.transform.parent;
            speakerRect = dialoguePanel != null && parent != dialoguePanel.transform
                ? (RectTransform)parent : speakerNameText.rectTransform;
            speakerX = speakerRect.anchoredPosition.x;
            bodyLeft = bodyText.rectTransform.offsetMin.x;
            portraitShift = portraitImage.rectTransform.sizeDelta.x + 20f;
        }
    }

    /// <summary>대사 줄의 초상화 → 없으면 화자 이름으로 등록된 기본 초상화</summary>
    private Sprite ResolvePortrait(DialogueLine line)
    {
        if (line.portrait != null) return line.portrait;
        foreach (SpeakerPortrait sp in speakerPortraits)
        {
            if (sp != null && sp.portrait != null && sp.speaker == line.speaker) return sp.portrait;
        }
        return null;
    }

    /// <summary>대사 목록 재생 시작. 끝나면 onComplete 콜백 실행.</summary>
    public void PlayLines(List<DialogueLine> newLines, Action onComplete)
    {
        if (newLines == null || newLines.Count == 0)
        {
            if (onComplete != null) onComplete();
            return;
        }

        lines = newLines;
        this.onComplete = onComplete;
        index = 0;
        IsPlaying = true;

        if (dialoguePanel != null) dialoguePanel.SetActive(true);
        else Debug.LogWarning("DialoguePanel이 연결되지 않았습니다.");

        ShowCurrentLine();
    }

    private void ShowCurrentLine()
    {
        DialogueLine line = lines[index];

        if (speakerNameText != null) speakerNameText.text = line.speaker;

        Sprite portrait = ResolvePortrait(line);
        if (portraitImage != null)
        {
            portraitImage.gameObject.SetActive(portrait != null);
            portraitImage.sprite = portrait;

            // 초상화가 없으면 이름표와 대사를 왼쪽으로 당긴다 (겹침/빈칸 방지)
            if (speakerRect != null)
            {
                float shift = portrait != null ? 0f : -portraitShift;
                speakerRect.anchoredPosition = new Vector2(speakerX + shift, speakerRect.anchoredPosition.y);
                Vector2 min = bodyText.rectTransform.offsetMin;
                bodyText.rectTransform.offsetMin = new Vector2(bodyLeft + shift, min.y);
            }
        }

        // 화자가 없는 줄(나레이션 "―")은 이름표를 숨긴다
        if (speakerRect != null)
            speakerRect.gameObject.SetActive(!string.IsNullOrEmpty(line.speaker) && line.speaker != "―");

        if (typingRoutine != null) StopCoroutine(typingRoutine);
        typingRoutine = StartCoroutine(TypeRoutine(line.text));
    }

    private IEnumerator TypeRoutine(string fullText)
    {
        isTyping = true;
        if (bodyText != null) bodyText.text = "";

        foreach (char c in fullText)
        {
            if (bodyText != null) bodyText.text += c;
            yield return new WaitForSeconds(typeSpeed);
        }

        isTyping = false;
    }

    void Update()
    {
        if (!IsPlaying) return;

        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
            Advance();
    }

    /// <summary>대사를 한 단계 진행 (클릭 입력 또는 자동 진행용).</summary>
    public void Advance()
    {
        if (!IsPlaying) return;

        if (isTyping)
        {
            // 타이핑 스킵 → 문장 전체 표시
            if (typingRoutine != null) StopCoroutine(typingRoutine);
            isTyping = false;
            if (bodyText != null) bodyText.text = lines[index].text;
        }
        else
        {
            index++;
            if (index >= lines.Count) FinishDialogue();
            else ShowCurrentLine();
        }
    }

    private void FinishDialogue()
    {
        IsPlaying = false;
        if (dialoguePanel != null) dialoguePanel.SetActive(false);

        Action callback = onComplete;
        onComplete = null;
        if (callback != null) callback();
    }
}
