using UnityEngine;

/// <summary>
/// 전투 UI 묶음 (전투 기록 · 무대 배너 · 기력/감정 · 손패 · 턴 종료 버튼)의 표시를 관리한다.
///
/// - 스토리(대사) 중에는 서서히 사라지고 클릭도 막힌다 → 대사에 집중
/// - 전투가 시작되면 다시 나타난다
/// - 전투 중에도 Hud Alpha만큼 반투명 (뒤의 3D 무대가 비쳐 보이게)
/// - 머리 위 이름표/체력바(월드 UI)도 같이 켜고 끈다
///
/// 전투 UI들의 부모 오브젝트에 붙인다 (CanvasGroup이 자동으로 붙음).
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class BattleHUD : MonoBehaviour
{
    [Range(0.2f, 1f)]
    [Tooltip("전투 중 UI 불투명도 (1 = 불투명, 낮을수록 뒤가 비침)")]
    public float hudAlpha = 0.75f;
    [Tooltip("나타나고 사라지는 시간(초)")]
    public float fadeTime = 0.35f;

    private CanvasGroup group;
    private bool lastShown = true;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        group.alpha = 0f;   // 게임은 스토리부터 시작하므로 숨긴 채로 시작
        Apply(false);
    }

    void Update()
    {
        bool show = GameManager.Instance != null && GameManager.Instance.State == GameManager.GameState.Battle;
        if (show != lastShown) Apply(show);

        float target = show ? hudAlpha : 0f;
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
