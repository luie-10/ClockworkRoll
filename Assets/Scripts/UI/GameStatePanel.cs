using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 패배(GameOver) / 모든 챕터 클리어(Clear) 때 결과 화면을 띄운다.
/// 항상 켜져 있는 오브젝트에 붙이고, 평소에는 꺼둘 결과 패널(panel)을 연결한다.
/// 패널 안의 '다시 시작' 버튼은 GameManager.RestartScene에 연결한다.
/// </summary>
public class GameStatePanel : MonoBehaviour
{
    public GameObject panel;     // 결과 화면 루트 (평소엔 꺼짐)
    public Text titleText;       // "패배..." / "클리어!"
    public Text messageText;     // 부가 설명

    private GameManager.GameState shownState = GameManager.GameState.Story;

    void Start()
    {
        if (panel != null) panel.SetActive(false);
    }

    void Update()
    {
        if (GameManager.Instance == null || panel == null) return;

        GameManager.GameState state = GameManager.Instance.State;
        if (state == shownState) return;
        shownState = state;

        bool show = state == GameManager.GameState.GameOver || state == GameManager.GameState.Clear;
        panel.SetActive(show);
        if (!show) return;

        if (state == GameManager.GameState.GameOver)
        {
            if (titleText != null) titleText.text = "패배...";
            if (messageText != null) messageText.text = "삼킨 기억에 짓눌렸다. 처음부터 다시 시작한다.";
        }
        else
        {
            if (titleText != null) titleText.text = "클리어!";
            if (messageText != null) messageText.text = "모든 챕터를 마쳤다.";
        }
    }
}
