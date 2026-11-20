using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 마우스를 올리면 그 위에 짧은 설명 상자가 뜬다 (기력 · 장력 게이지처럼 글자 없이 그림만 있는 UI용).
/// HoverHint.Attach(대상, () => "설명")으로 붙인다. 올려 둔 동안 설명을 계속 새로 읽어서 숫자가 바뀌어도 맞게 보인다.
/// 설명 상자는 전용 Canvas 하나를 모두가 같이 쓴다 (정보창들과 같은 높이).
/// </summary>
public class HoverHint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Func<string> text;
    private bool hovered;

    /// <summary>go에 마우스 반응을 켜고 설명을 붙인다 (그림이 마우스를 받도록 raycastTarget도 켠다)</summary>
    public static HoverHint Attach(GameObject go, Func<string> text)
    {
        if (go == null) return null;
        Graphic g = go.GetComponent<Graphic>();
        if (g != null) g.raycastTarget = true;
        HoverHint h = go.GetComponent<HoverHint>();
        if (h == null) h = go.AddComponent<HoverHint>();
        h.text = text;
        return h;
    }

    public void OnPointerEnter(PointerEventData e) { hovered = true; Box.Show((RectTransform)transform, Read()); }
    public void OnPointerExit(PointerEventData e) { hovered = false; Box.Hide((RectTransform)transform); }

    void Update()
    {
        if (hovered) Box.Show((RectTransform)transform, Read());
    }

    void OnDisable()
    {
        hovered = false;
        Box.Hide((RectTransform)transform);
    }

    private string Read()
    {
        try { return text != null ? text() : ""; } catch { return ""; }
    }

    // ─────────────────────────── 설명 상자 ───────────────────────────

    private class Box : MonoBehaviour
    {
        private const float Width = 380f, Pad = 14f;
        private static Box instance;

        private RectTransform canvasRect, panel;
        private Text label;
        private RectTransform target;

        public static void Show(RectTransform target, string message)
        {
            Ensure();
            instance.target = target;
            instance.label.text = message;
            instance.panel.sizeDelta = new Vector2(Width, instance.panel.sizeDelta.y);   // 폭을 먼저 정해야 줄바꿈 높이가 맞다
            float h = instance.label.preferredHeight;
            instance.panel.sizeDelta = new Vector2(Width, h + Pad * 2f);
            if (!instance.panel.gameObject.activeSelf) instance.panel.gameObject.SetActive(true);
            instance.Place();
        }

        public static void Hide(RectTransform target)
        {
            if (instance == null || instance.target != target) return;
            instance.target = null;
            instance.panel.gameObject.SetActive(false);
        }

        private static void Ensure()
        {
            if (instance != null) return;
            GameObject go = new GameObject("HoverHintCanvas");
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 510;   // 정보창들(490~500)과 같은 높이, 승리 화면(600)보다 아래
            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            instance = go.AddComponent<Box>();
            instance.canvasRect = (RectTransform)go.transform;

            Image bg = UIBuilder.Panel("Hint", go.transform, UITheme.Window);
            bg.raycastTarget = false;
            instance.panel = bg.rectTransform;
            instance.panel.anchorMin = instance.panel.anchorMax = Vector2.zero;
            instance.panel.pivot = new Vector2(0.5f, 0f);
            UITheme.Elevate(bg, 0.6f);
            UIPop.Ensure(bg.gameObject, 0.96f, 0.1f);

            instance.label = UIBuilder.Label("Text", bg.transform, "", 19, UITheme.TextMain, TextAnchor.UpperLeft);
            instance.label.supportRichText = true;
            instance.label.horizontalOverflow = HorizontalWrapMode.Wrap;
            instance.label.verticalOverflow = VerticalWrapMode.Overflow;
            instance.label.lineSpacing = 1.1f;
            instance.label.raycastTarget = false;
            UIBuilder.Stretch(instance.label.rectTransform, Pad);

            instance.panel.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (target != null && !target.gameObject.activeInHierarchy) Hide(target);
            if (target != null) Place();
        }

        /// <summary>대상 윗변 가운데 바로 위에 띄운다 (화면 밖으로는 나가지 않게)</summary>
        private void Place()
        {
            if (target == null) return;
            Canvas c = target.GetComponentInParent<Canvas>();
            Camera cam = c != null && c.renderMode != RenderMode.ScreenSpaceOverlay ? c.worldCamera : null;

            Vector3[] corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector2 screenTop = RectTransformUtility.WorldToScreenPoint(cam, (corners[1] + corners[2]) * 0.5f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenTop, null, out Vector2 local);

            Vector2 canvasSize = canvasRect.rect.size;
            Vector2 pos = local + canvasSize * 0.5f + new Vector2(0f, 10f);
            Vector2 size = panel.sizeDelta;
            pos.x = Mathf.Clamp(pos.x, size.x * 0.5f + 8f, canvasSize.x - size.x * 0.5f - 8f);
            pos.y = Mathf.Min(pos.y, canvasSize.y - size.y - 8f);
            panel.anchoredPosition = pos;
        }
    }
}
