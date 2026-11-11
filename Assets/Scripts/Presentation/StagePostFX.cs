using UnityEngine;
using UnityEngine.PostProcessing;
using UnityEngine.SceneManagement;

/// <summary>
/// 화면 효과 — 3D 무대의 빛이 살아 보이게 한다. (연출 전용 — 없어도 게임은 정상 동작)
///   - 블룸: 네온 · 가로등 · 불 · 용광로처럼 밝은 곳이 번진다
///   - 비네트: 화면 가장자리를 살짝 어둡게 해 시선을 무대 가운데로 모은다
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

    [Header("조작")]
    [Tooltip("이 키로 효과를 켜고 끈다 (전/후 비교용). None이면 사용 안 함")]
    public KeyCode toggleKey = KeyCode.F9;

    private PostProcessingBehaviour behaviour;
    private PostProcessingProfile profile;

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

        VignetteModel.Settings vignette = VignetteModel.Settings.defaultSettings;
        vignette.intensity = vignetteIntensity;
        vignette.smoothness = vignetteSmoothness;
        profile.vignette.settings = vignette;
        profile.vignette.enabled = vignetteIntensity > 0f;
    }
}
