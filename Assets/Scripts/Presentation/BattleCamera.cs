using UnityEngine;

/// <summary>
/// 합이 진행되는 동안 카메라가 응답자에게 살짝 다가가는 클로즈업 연출.
/// Main Camera에 붙인다. BattleManager가 Focus()/Release()를 호출한다.
/// (연출 전용 — 이 컴포넌트가 없어도 전투는 정상 동작한다)
/// </summary>
public class BattleCamera : MonoBehaviour
{
    public static BattleCamera Instance { get; private set; }

    [Header("설정")]
    [Tooltip("포커스 타겟 기준 카메라 위치 오프셋 (뒤쪽 -Z가 카메라 쪽)")]
    public Vector3 focusOffset = new Vector3(0f, 1.6f, -4.5f);
    [Tooltip("포커스 타겟의 어느 높이를 바라볼지 (머리 위 주사위 표시까지 화면에 들어오게)")]
    public float lookHeight = 1.8f;
    [Tooltip("카메라 이동 속도")]
    public float moveSpeed = 3f;

    private Vector3 homePos;
    private Quaternion homeRot;
    private Transform focusTarget;

    private Vector3 smoothPos;
    private float shakeStrength;
    private float shakeDuration;
    private float shakeTimeLeft;

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

    /// <summary>포커스를 해제하고 원래 시점으로 복귀한다.</summary>
    public void Release()
    {
        focusTarget = null;
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
            smoothPos = Vector3.Lerp(smoothPos, homePos, Time.deltaTime * moveSpeed * 0.7f);
            transform.rotation = Quaternion.Slerp(transform.rotation, homeRot, Time.deltaTime * moveSpeed * 0.7f);
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
}
