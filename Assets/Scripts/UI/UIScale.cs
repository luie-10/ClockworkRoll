using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 배율 — 모든 UI(전투 HUD · 시계판 · 창 · 툴팁 …)의 크기를 한꺼번에 키우고 줄인다.
/// 멈춤 메뉴의 [화면 배율] 줄이나 Ctrl + '+' / Ctrl + '-' (Ctrl + 0 = 100%)로 바꾼다. 값은 PlayerPrefs에 남는다.
///
/// 씬의 캔버스는 대부분 실행 중에 만들어지므로, 몇 번에 한 번씩 새 CanvasScaler를 찾아 원래 값을 기억해 두고 배율을 곱한다.
///   ScaleWithScreenSize → referenceResolution = 원래 / 배율 (기준 화면이 작아질수록 UI가 커진다)
///   ConstantPixelSize   → scaleFactor = 원래 x 배율
/// </summary>
public class UIScale : MonoBehaviour
{
    public const float Min = 0.7f, Max = 1.5f, Step = 0.1f;
    private const string Key = "ui.scale";

    private static UIScale instance;
    private static float value = -1f;

    private struct Original { public Vector2 reference; public float factor; }
    private readonly Dictionary<CanvasScaler, Original> seen = new Dictionary<CanvasScaler, Original>();
    private float scanTimer;
    private float applied = -1f;

    public static float Value
    {
        get
        {
            if (value < 0f)
            {
                try { value = PlayerPrefs.GetFloat(Key, 1f); } catch { value = 1f; }
                value = Mathf.Clamp(value, Min, Max);
            }
            return value;
        }
        set
        {
            UIScale.value = Mathf.Clamp(Mathf.Round(value * 10f) / 10f, Min, Max);
            try { PlayerPrefs.SetFloat(Key, UIScale.value); PlayerPrefs.Save(); } catch { }
            if (instance != null) instance.ApplyAll(true);
        }
    }

    public static void Ensure()
    {
        if (instance != null) return;
        GameObject go = new GameObject("UIScale");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<UIScale>();
        instance.ApplyAll(true);
    }

    void Update()
    {
        // 단축키: Ctrl + '+' / '-' / '0'
        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        if (ctrl)
        {
            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus)) Change(Step);
            else if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus)) Change(-Step);
            else if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0)) { Value = 1f; PauseMenu.Toast("화면 배율 100%"); }
        }

        scanTimer -= Time.unscaledDeltaTime;
        if (scanTimer <= 0f)
        {
            scanTimer = 0.25f;
            ApplyAll(false);   // 새로 생긴 캔버스에도 배율을 입힌다
        }
    }

    private static void Change(float delta)
    {
        Value = Value + delta;
        PauseMenu.Toast($"화면 배율 {Mathf.RoundToInt(Value * 100f)}%  (Ctrl + 0 = 100%)");
    }

    private void ApplyAll(bool force)
    {
        float v = Value;
        bool changed = force || !Mathf.Approximately(v, applied);
        applied = v;

        CanvasScaler[] scalers = FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (CanvasScaler s in scalers)
        {
            if (s == null) continue;
            bool fresh = !seen.TryGetValue(s, out Original o);
            if (fresh)
            {
                o = new Original { reference = s.referenceResolution, factor = s.scaleFactor };
                seen[s] = o;
            }
            if (!fresh && !changed) continue;

            if (s.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize) s.referenceResolution = o.reference / v;
            else if (s.uiScaleMode == CanvasScaler.ScaleMode.ConstantPixelSize) s.scaleFactor = o.factor * v;
        }

        // 사라진 캔버스는 잊는다
        if (seen.Count > scalers.Length + 16)
        {
            List<CanvasScaler> dead = new List<CanvasScaler>();
            foreach (CanvasScaler s in seen.Keys) if (s == null) dead.Add(s);
            foreach (CanvasScaler s in dead) seen.Remove(s);
        }
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
