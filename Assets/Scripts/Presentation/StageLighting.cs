using System.Collections;
using UnityEngine;

/// <summary>
/// 감정 단계에 따라 씬의 조명을 바꾸는 연출 컴포넌트.
/// Directional Light에 붙인다. BattleManager가 감정 단계 상승 시 SetEmotion()을 호출한다.
/// (연출 전용 — 이 컴포넌트가 없어도 전투는 정상 동작한다)
/// </summary>
public class StageLighting : MonoBehaviour
{
    public static StageLighting Instance { get; private set; }

    [Header("참조 (비워두면 자기 Light를 사용)")]
    public Light mainLight;

    [Header("감정 단계별 빛 색 (0~5단계)")]
    public Color[] emotionColors = new Color[]
    {
        new Color(0.55f, 0.60f, 0.70f),   // 0단계: 차분한 회청색
        new Color(0.60f, 0.55f, 0.65f),   // 1단계
        new Color(0.70f, 0.50f, 0.55f),   // 2단계
        new Color(0.75f, 0.45f, 0.40f),   // 3단계
        new Color(0.80f, 0.40f, 0.30f),   // 4단계
        new Color(0.90f, 0.25f, 0.20f),   // 5단계: 붉게 물든 무대
    };

    [Header("설정")]
    public float blendTime = 0.8f;    // 색이 변하는 데 걸리는 시간(초)
    public bool tintAmbient = true;   // 배경광(Ambient)도 함께 물릴지

    private Color baseLightColor;
    private Color baseAmbientColor;
    private Coroutine blendRoutine;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (mainLight == null) mainLight = GetComponent<Light>();
        if (mainLight != null) baseLightColor = mainLight.color;
        baseAmbientColor = RenderSettings.ambientLight;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>감정 단계(0~5)에 맞춰 무대의 빛을 서서히 바꾼다.</summary>
    public void SetEmotion(int level)
    {
        if (mainLight == null) return;

        Color target = baseLightColor;
        if (emotionColors != null && emotionColors.Length > 0)
        {
            int idx = Mathf.Clamp(level, 0, emotionColors.Length - 1);
            target = emotionColors[idx];
        }

        if (blendRoutine != null) StopCoroutine(blendRoutine);
        blendRoutine = StartCoroutine(BlendRoutine(target));
    }

    private IEnumerator BlendRoutine(Color target)
    {
        Color startLight = mainLight.color;
        Color startAmbient = RenderSettings.ambientLight;
        float t = 0f;

        while (t < blendTime)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / blendTime);

            mainLight.color = Color.Lerp(startLight, target, k);
            if (tintAmbient) RenderSettings.ambientLight = Color.Lerp(startAmbient, target * 0.5f, k);

            yield return null;
        }

        mainLight.color = target;
        if (tintAmbient) RenderSettings.ambientLight = target * 0.5f;
    }
}
