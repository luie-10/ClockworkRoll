using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 전투 카메라.
///   - 합이 진행되는 동안 응답자에게 살짝 다가가는 클로즈업 연출 (BattleManager가 Focus()/Release()를 호출)
///   - 전투 중 마우스로 무대를 둘러보기 (제한된 범위 안에서):
///       오른쪽 버튼 드래그 = 무대 가운데를 축으로 돌기 · 가운데 버튼 드래그 = 옆/위로 밀기
///       휠 = 가까이/멀리 · 휠 클릭 또는 Home = 처음 시점
///     클로즈업이 끝나면 둘러보던 시점으로 돌아간다.
/// Main Camera에 붙인다. (연출 전용 — 이 컴포넌트가 없어도 전투는 정상 동작한다)
/// </summary>
public class BattleCamera : MonoBehaviour
{
    public static BattleCamera Instance { get; private set; }

    [Header("클로즈업")]
    [Tooltip("포커스 타겟 기준 카메라 위치 오프셋 (뒤쪽 -Z가 카메라 쪽)")]
    public Vector3 focusOffset = new Vector3(0f, 1.6f, -4.5f);
    [Tooltip("포커스 타겟의 어느 높이를 바라볼지 (머리 위 칸 표시까지 화면에 들어오게)")]
    public float lookHeight = 1.8f;
    [Tooltip("카메라 이동 속도")]
    public float moveSpeed = 3f;

    [Header("둘러보기 (마우스)")]
    [Tooltip("돌 때 축이 되는 무대 가운데 (플레이어와 적 사이)")]
    public Vector3 orbitPivot = new Vector3(0.8f, 1f, 0.5f);
    [Tooltip("좌우로 돌 수 있는 각도 (±도)")]
    public float maxYaw = 28f;
    [Tooltip("위/아래로 기울일 수 있는 각도 (도) — 아래 = 땅 쪽")]
    public float minPitch = -8f, maxPitch = 18f;
    [Tooltip("옆/위로 밀 수 있는 거리 (월드 단위)")]
    public Vector2 maxPan = new Vector2(3f, 1.2f);
    [Tooltip("가까이(작을수록) ~ 멀리 배율")]
    public float minZoom = 0.55f, maxZoom = 1.2f;
    [Tooltip("드래그 1픽셀당 도는 각도 / 미는 거리, 휠 한 칸당 확대 비율")]
    public float orbitSpeed = 0.15f, panSpeed = 0.01f, zoomSpeed = 0.1f;

    [Header("입체감 (시차)")]
    [Tooltip("마우스가 화면 끝에 있을 때 무대 가운데를 축으로 도는 각도 (좌우, 위아래 도). 0이면 끔")]
    public Vector2 parallaxAngle = new Vector2(2.2f, 1.1f);
    [Tooltip("가만히 있어도 천천히 흔들리는 각도 (도). 0이면 끔")]
    public float swayAngle = 0.8f;

    private Vector3 homePos;
    private Quaternion homeRot;
    private Transform focusTarget;

    private Vector3 smoothPos;
    private float shakeStrength;
    private float shakeDuration;
    private float shakeTimeLeft;

    // 둘러보기 상태 (목표값 → 부드럽게 따라감)
    private float yaw, pitch, zoom = 1f;
    private Vector2 pan;
    private bool dragging;
    private Text hint;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        homePos = transform.position;
        homeRot = transform.rotation;
        smoothPos = homePos;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>타겟을 향해 카메라를 이동시킨다.</summary>
    public void Focus(Unit target)
    {
        focusTarget = target != null ? target.transform : null;
    }

    /// <summary>포커스를 해제하고 둘러보던 시점으로 복귀한다.</summary>
    public void Release()
    {
        focusTarget = null;
    }

    /// <summary>처음 시점으로 (둘러본 각도/확대를 되돌림)</summary>
    public void ResetView()
    {
        yaw = pitch = 0f;
        pan = Vector2.zero;
        zoom = 1f;
    }

    /// <summary>카메라를 strength 세기로 duration초 동안 흔든다 (큰 피해가 들어갈 때).</summary>
    public void Shake(float strength, float duration)
    {
        // 더 센 흔들림이 들어오면 덮어쓰고, 약한 건 무시
        if (shakeTimeLeft > 0f && strength < shakeStrength) return;
        shakeStrength = strength;
        shakeDuration = Mathf.Max(0.01f, duration);
        shakeTimeLeft = shakeDuration;
    }

    void Update()
    {
        bool inBattle = GameManager.Instance == null || GameManager.Instance.State == GameManager.GameState.Battle;
        EnsureHint(inBattle);
        if (!inBattle) { dragging = false; return; }

        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        // 드래그는 UI 밖에서 시작했을 때만 (카드/버튼 조작과 겹치지 않게)
        Vector2 mouse = Input.mousePosition;
        if ((Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2)) && !overUI)
        {
            dragging = true;
            lastMouse = pressMouse = mouse;
        }
        if (!Input.GetMouseButton(1) && !Input.GetMouseButton(2)) dragging = false;

        if (dragging)
        {
            Vector2 d = mouse - lastMouse;   // 픽셀 단위
            lastMouse = mouse;
            if (Input.GetMouseButton(1))
            {
                yaw = Mathf.Clamp(yaw + d.x * orbitSpeed, -maxYaw, maxYaw);
                pitch = Mathf.Clamp(pitch - d.y * orbitSpeed, minPitch, maxPitch);
            }
            else
            {
                pan.x = Mathf.Clamp(pan.x - d.x * panSpeed, -maxPan.x, maxPan.x);
                pan.y = Mathf.Clamp(pan.y - d.y * panSpeed, -maxPan.y * 0.5f, maxPan.y);
            }
        }

        float wheel = Input.mouseScrollDelta.y;
        if (Mathf.Abs(wheel) > 0.01f && !overUI) zoom = Mathf.Clamp(zoom - wheel * zoomSpeed, minZoom, maxZoom);

        // 휠 클릭(거의 안 움직이고 뗌) 또는 Home = 처음 시점
        if (Input.GetKeyDown(KeyCode.Home)) ResetView();
        if (Input.GetMouseButtonUp(2) && (mouse - pressMouse).sqrMagnitude < 16f) ResetView();
    }

    private Vector2 lastMouse, pressMouse;

    /// <summary>둘러보기 값이 적용된 기본 시점 (축을 중심으로 돌리고, 당기고, 민다)</summary>
    private void ViewPose(out Vector3 pos, out Quaternion rot)
    {
        // 시차: 마우스 위치와 느린 흔들림만큼 무대 가운데를 축으로 아주 살짝 돈다 → 앞/뒤 사물이 다르게 움직여 깊이가 보인다
        Vector2 m = new Vector2(Input.mousePosition.x / Mathf.Max(1, Screen.width), Input.mousePosition.y / Mathf.Max(1, Screen.height)) * 2f - Vector2.one;
        m = Vector2.ClampMagnitude(m, 1.2f);
        float t = Time.unscaledTime;
        float driftYaw = dragging ? 0f : m.x * parallaxAngle.x + (Mathf.PerlinNoise(t * 0.13f, 0.5f) - 0.5f) * swayAngle;
        float driftPitch = dragging ? 0f : -m.y * parallaxAngle.y + (Mathf.PerlinNoise(0.5f, t * 0.11f) - 0.5f) * swayAngle * 0.5f;

        Quaternion orbit = Quaternion.AngleAxis(yaw + driftYaw, Vector3.up) * Quaternion.AngleAxis(pitch + driftPitch, homeRot * Vector3.right);
        Vector3 fromPivot = orbit * (homePos - orbitPivot) * zoom;
        rot = orbit * homeRot;
        pos = orbitPivot + fromPivot + rot * new Vector3(pan.x, pan.y, 0f);
        pos.y = Mathf.Max(pos.y, 0.6f);   // 땅 밑으로 내려가지 않게
    }

    void LateUpdate()
    {
        // smoothPos = 흔들림을 뺀 '원래' 카메라 위치. 흔들림이 이동 계산에 섞이지 않게 따로 관리한다.
        if (focusTarget != null)
        {
            Vector3 goal = focusTarget.position + focusOffset;
            smoothPos = Vector3.Lerp(smoothPos, goal, Time.deltaTime * moveSpeed);

            Vector3 lookPoint = focusTarget.position + Vector3.up * lookHeight;
            Quaternion look = Quaternion.LookRotation(lookPoint - smoothPos);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * moveSpeed);
        }
        else
        {
            ViewPose(out Vector3 viewPos, out Quaternion viewRot);
            float k = dragging ? 12f : moveSpeed * 0.7f;   // 직접 움직일 땐 바로 따라오고, 돌아갈 땐 천천히
            smoothPos = Vector3.Lerp(smoothPos, viewPos, Time.unscaledDeltaTime * k);
            transform.rotation = Quaternion.Slerp(transform.rotation, viewRot, Time.unscaledDeltaTime * k);
        }

        Vector3 shakeOffset = Vector3.zero;
        if (shakeTimeLeft > 0f)
        {
            // 히트스톱(시간 정지) 중에도 흔들리도록 unscaledDeltaTime 사용
            shakeTimeLeft -= Time.unscaledDeltaTime;
            float fade = Mathf.Clamp01(shakeTimeLeft / shakeDuration);
            shakeOffset = Random.insideUnitSphere * shakeStrength * fade;
        }

        transform.position = smoothPos + shakeOffset;
    }

    /// <summary>덱 보기 버튼 위에 조작 안내 한 줄 (전투 HUD 안 — 스토리 중엔 같이 숨겨짐)</summary>
    private void EnsureHint(bool show)
    {
        if (hint == null)
        {
            BattleHUD hud = FindAnyObjectByType<BattleHUD>();
            if (hud == null) return;
            hint = UIBuilder.Label("CameraHint", hud.transform, "우클릭 드래그: 둘러보기 · 휠: 확대/축소 · 휠 클릭/Home: 처음 시점", 16, UITheme.TextMuted, TextAnchor.LowerLeft);
            UIBuilder.Place(hint.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(30f, 362f), new Vector2(560f, 24f));
        }
        bool moved = yaw != 0f || pitch != 0f || pan != Vector2.zero || zoom != 1f;
        hint.color = moved ? UITheme.Brass : UITheme.TextMuted;   // 둘러보는 중이면 강조 (Home으로 돌아갈 수 있음을 알림)
    }
}
