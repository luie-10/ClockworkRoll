using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 패배(GameOver) / 모든 챕터 클리어(Clear) 때 결과 화면을 띄운다.
/// 항상 켜져 있는 오브젝트에 붙이고, 평소에는 꺼둘 결과 패널(panel)을 연결한다.
/// 패널 안의 '다시 시작' 버튼은 GameManager.RestartScene에 연결한다 (R / Enter 키로도 재시작).
/// </summary>
public class GameStatePanel : MonoBehaviour
{
    public GameObject panel;     // 결과 화면 루트 (평소엔 꺼짐)
    public Text titleText;       // "패배..." / "승리!"
    public Text messageText;     // 부가 설명

    private const int PanelSortingOrder = 700;   // 시계판(50)·손패·툴팁(500)·승리 화면(600)보다 위

    private GameManager.GameState shownState = GameManager.GameState.Story;

    void Start()
    {
        if (panel != null) panel.SetActive(false);
    }

    void Update()
    {
        if (GameManager.Instance == null || panel == null) return;

        GameManager.GameState state = GameManager.Instance.State;
        bool ended = state == GameManager.GameState.GameOver || state == GameManager.GameState.Clear;

        // 결과 화면이 떠 있으면 키보드로도 처음부터 다시 시작
        if (ended && panel.activeSelf && (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Return)))
        {
            GameManager.Instance.RestartScene();
            return;
        }

        if (state == shownState) return;
        shownState = state;

        panel.SetActive(ended);
        if (!ended) return;

        BringToFront();

        if (state == GameManager.GameState.GameOver)
        {
            if (titleText != null) titleText.text = "패배...";
            if (messageText != null) messageText.text = "쓰러졌다. 처음부터 다시 시작한다. (R / Enter)";
        }
        else
        {
            if (titleText != null) titleText.text = "승리!";
            if (messageText != null) messageText.text = "모든 적을 쓰러뜨렸다. 처음부터 다시 할 수 있다. (R / Enter)";
        }
    }

    /// <summary>
    /// 결과 패널은 메인 HUD 캔버스(정렬 0) 안에 있어서 시계판·손패 캔버스에 가려지고 버튼 클릭도 막힌다.
    /// 자체 캔버스로 정렬 순서를 덮어써서 항상 맨 위에 그리고 클릭을 받게 한다.
    /// </summary>
    private void BringToFront()
    {
        Canvas canvas = panel.GetComponent<Canvas>();
        if (canvas == null) canvas = panel.AddComponent<Canvas>();
        canvas.overrideSorting = true;   // 패널이 켜진 뒤에 설정해야 적용된다
        canvas.sortingOrder = PanelSortingOrder;
        if (panel.GetComponent<GraphicRaycaster>() == null) panel.AddComponent<GraphicRaycaster>();
    }
}
