using UnityEngine;
using UnityEngine.PostProcessing;
using UnityEngine.SceneManagement;

/// <summary>
/// 화면 효과 — 3D 무대의 빛이 살아 보이게 한다. (연출 전용 — 없어도 게임은 정상 동작)
///   - 블룸: 네온 · 가로등 · 불 · 용광로처럼 밝은 곳이 번진다
///   - 비네트: 화면 가장자리를 살짝 어둡게 해 시선을 무대 가운데로 모은다
///   - 색 보정: 밝은 빛이 하얗게 뭉개지지 않게 눌러 주고(톤 매핑), 대비와 채도를 조금 올린다
///   - 필름 입자: 아주 옅은 입자로 단색 면(안개 · 벽)의 밋밋함과 색 띠를 가린다
///   - 타격 펄스: 큰 타격이 들어가는 순간 색이 살짝 갈라지고(색수차) 가장자리가 조여든다 — StagePostFX.Punch()
///   - 계단 현상 제거(FXAA): 3D 배경의 모서리를 부드럽게
///
/// 씬에 따로 넣을 필요 없다 — 씬이 열릴 때마다 Main Camera에 자동으로 붙는다.
/// 값을 바꾸고 싶으면 Main Camera에 이 컴포넌트를 직접 붙여 인스펙터에서 조절한다 (직접 붙인 것이 우선).
/// 끄려면 컴포넌트를 비활성화하거나 F9. UI(카드 · 시계판)는 화면 위에 따로 그려지므로 영향받지 않는다.
/// </summary>
[RequireComponent(typeof(Camera))]
public class StagePostFX : MonoBehaviour
{
    [Header("블룸 (빛 번짐)")]
    [Range(0f, 2f)] public float bloomIntensity = 0.55f;
    [Tooltip("이 밝기보다 밝은 곳만 번진다 (낮출수록 화면 전체가 뿌옇게 된다)")]
    [Range(0.5f, 2f)] public float bloomThreshold = 0.95f;
    [Range(1f, 7f)] public float bloomRadius = 4.5f;

    [Header("비네트 (가장자리 어둡게)")]
    [Range(0f, 1f)] public float vignetteIntensity = 0.3f;
    [Range(0.01f, 1f)] public float vignetteSmoothness = 0.35f;

    [Header("색 보정")]
    [Tooltip("밝은 빛을 부드럽게 눌러 준다 (끄면 1보다 밝은 곳이 그대로 하얗게 잘린다)")]
    public bool toneMapping = true;
    [Range(0.8f, 1.4f)] public float contrast = 1.08f;
    [Range(0.6f, 1.6f)] public float saturation = 1.12f;
    [Tooltip("전체 밝기 보정 (스톱 단위, 0 = 그대로)")]
    [Range(-1f, 1f)] public float exposure = 0.1f;

    [Header("필름 입자")]
    [Range(0f, 0.5f)] public float grainIntensity = 0.1f;

    [Header("타격 펄스 (큰 타격 순간)")]
    [Tooltip("평소 색수차 (화면 가장자리의 색 갈라짐). 0 = 없음")]
    [Range(0f, 0.5f)] public float chromaticBase = 0.04f;
    [Tooltip("가장 센 타격에서 더해지는 색수차")]
    [Range(0f, 1.5f)] public float chromaticPunch = 0.75f;
    [Tooltip("가장 센 타격에서 더해지는 비네트")]
    [Range(0f, 0.3f)] public float vignettePunch = 0.1f;
    [Tooltip("펄스가 가라앉는 데 걸리는 시간(초)")]
    public float punchTime = 0.35f;

    [Header("계단 현상 제거")]
    [Tooltip("FXAA. 3D 배경의 모서리는 부드러워지지만 픽셀 그림의 또렷한 경계도 같이 뭉개져서 기본은 끔")]
    public bool antialiasing = false;

    [Header("조작")]
    [Tooltip("이 키로 효과를 켜고 끈다 (전/후 비교용). None이면 사용 안 함")]
    public KeyCode toggleKey = KeyCode.F9;

    private PostProcessingBehaviour behaviour;
    private PostProcessingProfile profile;

    private static StagePostFX instance;
    private float punch;          // 지금 남아 있는 타격 펄스 (0~1)
    private float appliedPunch = -1f;

    /// <summary>큰 타격 순간 화면을 한 번 울린다 (0~1). 더 센 펄스가 남아 있으면 무시된다.</summary>
    public static void Punch(float strength)
    {
        if (instance != null) instance.punch = Mathf.Max(instance.punch, Mathf.Clamp01(strength));
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        if (cam.GetComponent<StagePostFX>() != null) return;
        if (cam.GetComponent<PostProcessingBehaviour>() != null) return;   // 이미 직접 꾸며 둔 카메라는 건드리지 않는다
        cam.gameObject.AddComponent<StagePostFX>();
    }

    void OnEnable()
    {
        instance = this;
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<PostProcessingProfile>();
            profile.hideFlags = HideFlags.HideAndDontSave;
        }
        Apply();

        GetComponent<Camera>().allowHDR = true;   // 1보다 밝은 빛(발광체)이 있어야 블룸이 그 부분만 번진다

        if (behaviour == null)
        {
            behaviour = gameObject.AddComponent<PostProcessingBehaviour>();
            behaviour.hideFlags = HideFlags.DontSave;
        }
        behaviour.profile = profile;
        behaviour.enabled = true;
    }

    void OnDisable()
    {
        if (instance == this) instance = null;
        if (behaviour != null) behaviour.enabled = false;
    }

    void OnDestroy()
    {
        if (behaviour != null) Destroy(behaviour);
        if (profile != null) Destroy(profile);
    }

    void OnValidate()
    {
        if (profile != null) Apply();   // 플레이 중 인스펙터에서 바꾼 값이 바로 보이게
    }

    void Update()
    {
        if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey) && behaviour != null)
            behaviour.enabled = !behaviour.enabled;

        // 타격 펄스: 히트스톱(시간 정지) 중에도 가라앉도록 unscaled
        if (punch > 0f) punch = Mathf.Max(0f, punch - Time.unscaledDeltaTime / Mathf.Max(0.05f, punchTime));
        if (profile != null && !Mathf.Approximately(punch, appliedPunch)) ApplyPunch();
    }

    /// <summary>펄스에 따라 달라지는 값만 다시 넣는다 (색수차 · 비네트)</summary>
    private void ApplyPunch()
    {
        appliedPunch = punch;
        float k = punch * punch;   // 빠르게 가라앉는 꼬리

        ChromaticAberrationModel.Settings ca = ChromaticAberrationModel.Settings.defaultSettings;
        ca.intensity = chromaticBase + chromaticPunch * k;
        profile.chromaticAberration.settings = ca;
        profile.chromaticAberration.enabled = ca.intensity > 0.001f;

        VignetteModel.Settings vignette = VignetteModel.Settings.defaultSettings;
        vignette.intensity = vignetteIntensity + vignettePunch * k;
        vignette.smoothness = vignetteSmoothness;
        profile.vignette.settings = vignette;
        profile.vignette.enabled = vignette.intensity > 0f;
    }

    private void Apply()
    {
        BloomModel.Settings bloom = BloomModel.Settings.defaultSettings;
        bloom.bloom.intensity = bloomIntensity;
        bloom.bloom.threshold = bloomThreshold;
        bloom.bloom.softKnee = 0.6f;
        bloom.bloom.radius = bloomRadius;
        bloom.bloom.antiFlicker = true;   // 빗줄기 · 불티 같은 작은 밝은 점이 번쩍거리지 않게
        profile.bloom.settings = bloom;
        profile.bloom.enabled = bloomIntensity > 0f;

        ColorGradingModel.Settings grade = ColorGradingModel.Settings.defaultSettings;
        grade.tonemapping.tonemapper = toneMapping ? ColorGradingModel.Tonemapper.Neutral : ColorGradingModel.Tonemapper.None;
        grade.basic.contrast = contrast;
        grade.basic.saturation = saturation;
        grade.basic.postExposure = exposure;
        profile.colorGrading.settings = grade;
        profile.colorGrading.enabled = toneMapping || !Mathf.Approximately(contrast, 1f)
                                       || !Mathf.Approximately(saturation, 1f) || !Mathf.Approximately(exposure, 0f);

        GrainModel.Settings grain = GrainModel.Settings.defaultSettings;
        grain.colored = false;
        grain.intensity = grainIntensity;
        grain.size = 1.1f;
        grain.luminanceContribution = 0.85f;   // 어두운 곳에서는 덜 보이게
        profile.grain.settings = grain;
        profile.grain.enabled = grainIntensity > 0f;

        profile.antialiasing.settings = AntialiasingModel.Settings.defaultSettings;   // FXAA
        profile.antialiasing.enabled = antialiasing;

        ApplyPunch();   // 비네트 + 색수차
    }
}
