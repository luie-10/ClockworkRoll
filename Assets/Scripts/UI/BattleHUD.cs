using UnityEngine;

/// <summary>
/// 전투 UI 묶음 (전투 기록 · 무대 배너 · 기력/감정 · 손패 · 턴 종료 버튼)의 표시를 관리한다.
///
/// - 스토리(대사) 중에는 서서히 사라지고 클릭도 막힌다 → 대사에 집중
/// - 전투가 시작되면 다시 나타난다
/// - 패널 자체가 반투명(UITheme)이므로 묶음 전체는 불투명하게 둔다 (전체를 흐리면 글자까지 탁해진다)
/// - 시작할 때 씬 UI를 UITheme 규칙으로 다시 칠한다
/// - 머리 위 이름표/체력바(월드 UI)도 같이 켜고 끈다
///
/// 전투 UI들의 부모 오브젝트에 붙인다 (CanvasGroup이 자동으로 붙음).
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class BattleHUD : MonoBehaviour
{
    [Tooltip("나타나고 사라지는 시간(초)")]
    public float fadeTime = 0.35f;

    private CanvasGroup group;
    private bool lastShown = true;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        group.alpha = 0f;   // 게임은 스토리부터 시작하므로 숨긴 채로 시작
        UITheme.ApplyToScene(transform.parent != null ? transform.parent : transform);
        Apply(false);
    }

    void Update()
    {
        bool show = GameManager.Instance != null && GameManager.Instance.State == GameManager.GameState.Battle;
        if (show != lastShown) Apply(show);

        float target = show ? 1f : 0f;
        float speed = fadeTime > 0f ? Time.unscaledDeltaTime / fadeTime : 1f;
        group.alpha = Mathf.MoveTowards(group.alpha, target, speed);
    }

    private void Apply(bool show)
    {
        lastShown = show;
        group.interactable = show;
        group.blocksRaycasts = show;   // 숨겨진 UI가 대사 클릭을 가로채지 않게
        UnitStatusLabel.SetAllVisible(show);
    }
}
