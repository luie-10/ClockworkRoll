using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캐릭터(플레이어/적)에 마우스를 올리면 마우스 옆에 뜨는 정보창.
///
///   골목 쥐                  HP 24/30
///   ─ 내성 (받는 피해) ─
///   참격  x1.0 보통
///   관통  x1.5 약점
///   타격  x2.0 취약
///   ─ 장착 책장 ─
///   물어뜯기  (기력 1)
///     참격 1~4  [적중시] 다음 턴 출혈 1
///     타격 1~4
///
/// 직접 붙일 필요 없음 — Unit의 OnMouseEnter/Exit가 Show/Hide를 부르면 전용 Canvas를 알아서 만든다.
/// 전투 중에만 뜬다 (스토리 중에는 숨김).
/// </summary>
public class UnitTooltip : MonoBehaviour
{
    private static UnitTooltip instance;

    private RectTransform canvasRect, panel;
    private Text text;
    private Unit current;
    private string lastText;

    public static void Show(Unit unit)
    {
        if (unit == null) return;
        if (instance == null) instance = Create();
        instance.current = unit;
        instance.lastText = null;
    }

    public static void Hide(Unit unit)
    {
        if (instance != null && instance.current == unit) instance.current = null;
    }

    private static UnitTooltip Create()
    {
        GameObject go = new GameObject("UnitTooltipCanvas");
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;   // 다른 UI보다 위
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        UnitTooltip tip = go.AddComponent<UnitTooltip>();
        tip.canvasRect = (RectTransform)go.transform;

        // 내용 크기에 맞춰 늘어나는 패널
        Image bg = UIBuilder.Panel("Tooltip", go.transform, new Color(0.03f, 0.03f, 0.04f, 0.92f));
        bg.raycastTarget = false;
        tip.panel = bg.rectTransform;
        tip.panel.pivot = new Vector2(0f, 1f);
        tip.panel.anchorMin = tip.panel.anchorMax = Vector2.zero;

        VerticalLayoutGroup layout = bg.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 12, 12);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        ContentSizeFitter fitter = bg.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        tip.text = UIBuilder.Label("Text", bg.transform, "", 22, Color.white, TextAnchor.UpperLeft);
        tip.text.horizontalOverflow = HorizontalWrapMode.Overflow;   // 줄바꿈 없이 가장 긴 줄에 맞춰 폭 결정
        tip.text.lineSpacing = 1.1f;

        bg.gameObject.SetActive(false);
        return tip;
    }

    void LateUpdate()
    {
        bool inBattle = GameManager.Instance == null || GameManager.Instance.State == GameManager.GameState.Battle;
        bool show = current != null && current.gameObject.activeInHierarchy && inBattle;
        if (panel.gameObject.activeSelf != show) panel.gameObject.SetActive(show);
        if (!show) return;

        string content = BuildText(current);
        if (content != lastText)
        {
            text.text = content;
            lastText = content;
        }

        // 마우스 오른쪽 아래에 띄우되, 화면 밖으로 나가면 반대쪽으로
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, Input.mousePosition, null, out Vector2 local);
        Vector2 canvasSize = canvasRect.rect.size;
        Vector2 pos = local + canvasSize * 0.5f + new Vector2(22f, -22f);   // 왼쪽 아래(0,0) 기준 좌표로
        Vector2 size = panel.rect.size;

        if (pos.x + size.x > canvasSize.x - 8f) pos.x = local.x + canvasSize.x * 0.5f - size.x - 22f;
        if (pos.y - size.y < 8f) pos.y = size.y + 8f;
        pos.x = Mathf.Max(8f, pos.x);
        pos.y = Mathf.Min(canvasSize.y - 8f, pos.y);

        panel.anchoredPosition = pos;
    }

    private static string BuildText(Unit u)
    {
        string nameColor = u.isPlayer ? "#8cd9ff" : "#ff9e73";
        string s = $"<size=26><b><color={nameColor}>{u.unitName}</color></b></size>    HP {u.Hp}/{u.MaxHp}";
        if (u.Bleed > 0 || u.PendingBleed > 0)
            s += $"   <color=#ff4d66>출혈 {u.Bleed}{(u.PendingBleed > 0 ? $" (+{u.PendingBleed})" : "")}</color>";

        s += "\n<color=#e8c27a>─ 내성 (받는 피해) ─</color>";
        s += ResistLine(DiceDetail.Slash, u);
        s += ResistLine(DiceDetail.Thrust, u);
        s += ResistLine(DiceDetail.Blunt, u);

        CardData equipped = BattleManager.Instance != null ? BattleManager.Instance.GetEquippedCard(u) : null;
        if (equipped != null)
        {
            s += $"\n<color=#e8c27a>─ {(u.isPlayer ? "응수 책장" : "장착 책장")} ─</color>";
            s += $"\n{equipped.cardName}  <color=#8cd9ff>(기력 {equipped.cost})</color>";
            string detail = DiceInfo.Detail(equipped);
            if (detail.Length > 0) s += "\n" + detail;
        }
        else if (u.isPlayer)
        {
            s += "\n<color=#888888>응수 책장 없음 — 책장을 고르고 나를 클릭하면 장착</color>";
        }
        return s;
    }

    private static string ResistLine(DiceDetail type, Unit u)
    {
        Resistance r = u.resistance != null ? u.resistance.Get(type) : Resistance.Normal;
        return $"\n<color={DiceInfo.ColorHex(type)}>{DiceInfo.Name(type)}</color>  " +
               $"<color={ResistanceSet.ColorHex(r)}>x{ResistanceSet.Multiplier(r):0.0} {ResistanceSet.Label(r)}</color>";
    }
}
