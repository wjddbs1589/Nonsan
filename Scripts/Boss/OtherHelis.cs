using UnityEngine;
using System.Collections;

/// <summary>
/// 화면 가장자리를 스쳐 지나가는 서브 헬기.
/// 레일을 따라 이동·회전하는 카메라 기준 로컬 좌표로 경로를 매 프레임 다시 계산해,
/// 카메라가 움직여도 화면 안에서 의도한 궤적으로 비행한다.
/// 경유지가 있으면 Catmull-Rom 곡선으로, 없으면 직선으로 비행한다.
/// </summary>
public class OtherHelis : MarkerBase
{
    [Header("참조")]
    [Tooltip("플레이어 카메라. 비워두면 Camera.main을 사용한다.")]
    [SerializeField] private Transform playerCamera;

    [Header("이동 경로 (카메라 로컬 좌표 기준)")]
    [Tooltip("카메라 기준 시작 위치 (예: 우측 멀리 X: 50, Y: 10, Z: 60)")]
    [SerializeField] private Vector3 startLocalOffset = new Vector3(50f, 10f, 60f);

    [Tooltip("카메라 기준 도착 위치 (예: 좌측 뒤편 X: -50, Y: 5, Z: -20)")]
    [SerializeField] private Vector3 endLocalOffset = new Vector3(-50f, 5f, -20f);

    [Tooltip("중간 경유지 (카메라 로컬 좌표 기준).\n비워두면 시작 → 도착 직선 이동, 채우면 시작 → 경유지 → 도착 순서로 Catmull-Rom 곡선 비행한다.")]
    [SerializeField] private Vector3[] waypointLocalOffsets = new Vector3[0];

    [Tooltip("전체 경로를 비행하는 시간 (초)")]
    [SerializeField] private float moveDuration = 4f;

    [Header("이탈")]
    [Tooltip("이탈 방향 (카메라 로컬 기준. 예: 위쪽 Vector3.up)")]
    [SerializeField] private Vector3 exitLocalDirection = Vector3.up;

    [Tooltip("이탈 비행 거리 (m)")]
    [SerializeField] private float exitDistance = 150f;

    [Tooltip("이탈 비행 시간 (초)")]
    [SerializeField] private float exitDuration = 2f;

    [Header("전투 및 회전")]
    [Tooltip("체크 시 비행 시작과 동시에 공격하고, 이동하는 동안 플레이어(카메라)를 바라본다.\n해제 시 공격하지 않고 진행 방향을 바라본다.")]
    [SerializeField] private bool isAttacking = false;

    [Tooltip("기수 회전 속도")]
    [SerializeField] private float rotationSpeed = 5f;

    [Header("파괴 추락")]
    [Tooltip("추락 시작 속도")]
    [SerializeField] private float crashInitialSpeed = 12f;

    [Tooltip("추락 가속도")]
    [SerializeField] private float crashAcceleration = 8f;

    [Tooltip("추락 회전 속도 (도/초)")]
    [SerializeField] private float crashRotateSpeed = 120f;

    [Tooltip("이 Y 좌표 아래로 내려가면 비활성화한다.")]
    [SerializeField] private float crashGroundY = -10f;

    // 이탈 시 기수 회전 속도 배율 (도망갈 때 더 빠르게 방향을 튼다)
    private const float EXIT_ROTATION_MULTIPLIER = 2.5f;

    private HeliAttackController _attackCtrl;
    private bool _isExiting = false;
    private bool _isCrashed = false;

    /// <summary>카메라와 공격 컨트롤러 참조를 준비한다. (트리거가 SetActive 직후 Launch를 호출하므로 Awake에서 처리)</summary>
    private void Awake()
    {
        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main.transform;

        _attackCtrl = GetComponent<HeliAttackController>();
    }

    /// <summary>
    /// 비행을 시작한다. 진행 중인 비행이나 이탈이 있으면 취소하고 처음부터 다시 비행한다.
    /// isAttacking이 켜져 있으면 비행과 동시에 공격을 시작한다.
    /// </summary>
    public void Launch()
    {
        if (_isCrashed) return;

        _isExiting = false;
        StopAllCoroutines();
        StartCoroutine(FlybyRoutine());

        if (isAttacking && _attackCtrl != null)
            _attackCtrl.StartAttack();
    }

    /// <summary>
    /// 진행 중인 비행을 멈추고 exitLocalDirection 방향으로 이탈한다.
    /// 이탈이 끝나면 공격을 멈추고 비활성화된다.
    /// </summary>
    public void RequestExit()
    {
        if (_isExiting || _isCrashed) return;

        _isExiting = true;
        StopAllCoroutines();
        StartCoroutine(ExitRoutine());
    }

    /// <summary>경로를 따라 moveDuration 동안 비행한 뒤 공격을 멈추고 비활성화한다.</summary>
    private IEnumerator FlybyRoutine()
    {
        if (playerCamera == null) yield break;

        float elapsed = 0f;
        Vector3 prevPosition = transform.position;

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / moveDuration);

            // 카메라의 현재 위치·회전 기준으로 경로 위 목표 지점을 계산한다
            Vector3 targetPosition = EvaluatePathWorld(t);
            Vector3 moveDirection = targetPosition - prevPosition;
            transform.position = targetPosition;
            prevPosition = targetPosition;

            if (isAttacking)
            {
                transform.LookAt(playerCamera);
            }
            else if (moveDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDirection.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }

            yield return null;
        }

        if (_attackCtrl != null) _attackCtrl.StopAttack();
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 전체 경로(시작 → 경유지 → 도착)에서 t(0~1) 위치의 월드 좌표를 반환한다.
    /// 카메라 좌표계 기준으로 매 프레임 다시 계산하므로 카메라가 회전해도 경로가 화면 기준으로 유지된다.
    /// </summary>
    private Vector3 EvaluatePathWorld(float t)
    {
        if (waypointLocalOffsets == null || waypointLocalOffsets.Length == 0)
        {
            Vector3 a = playerCamera.TransformPoint(startLocalOffset);
            Vector3 b = playerCamera.TransformPoint(endLocalOffset);
            return Vector3.Lerp(a, b, t);
        }

        // 제어점: [start, wp0, wp1, ..., end]
        int totalPoints = waypointLocalOffsets.Length + 2;
        int segmentCount = totalPoints - 1;

        // 전체 t를 구간 인덱스와 구간 내 t로 변환 (구간은 시간 기준으로 균등 분할)
        float scaled = Mathf.Clamp01(t) * segmentCount;
        int seg = Mathf.Min(Mathf.FloorToInt(scaled), segmentCount - 1);
        float localT = scaled - seg;

        // P1 → P2 구간을 P0, P3의 영향을 받아 보간한다
        Vector3 p0 = GetControlPointWorld(seg - 1, totalPoints);
        Vector3 p1 = GetControlPointWorld(seg, totalPoints);
        Vector3 p2 = GetControlPointWorld(seg + 1, totalPoints);
        Vector3 p3 = GetControlPointWorld(seg + 2, totalPoints);

        return CatmullRom(p0, p1, p2, p3, localT);
    }

    /// <summary>
    /// 제어점 인덱스를 월드 좌표로 변환한다.
    /// 양 끝 바깥 인덱스는 끝점 기준으로 외삽해, 시작과 도착 부근에서도 자연스러운 곡률을 만든다.
    /// </summary>
    private Vector3 GetControlPointWorld(int index, int totalPoints)
    {
        if (index >= 0 && index < totalPoints)
        {
            Vector3 local;
            if (index == 0) local = startLocalOffset;
            else if (index == totalPoints - 1) local = endLocalOffset;
            else local = waypointLocalOffsets[index - 1];
            return playerCamera.TransformPoint(local);
        }

        // 시작 이전: P(-1) = 2 * P0 - P1
        if (index < 0)
        {
            Vector3 first = GetControlPointWorld(0, totalPoints);
            Vector3 second = GetControlPointWorld(1, totalPoints);
            return 2f * first - second;
        }

        // 도착 이후: P(n) = 2 * P(n-1) - P(n-2)
        Vector3 last = GetControlPointWorld(totalPoints - 1, totalPoints);
        Vector3 prev = GetControlPointWorld(totalPoints - 2, totalPoints);
        return 2f * last - prev;
    }

    /// <summary>Catmull-Rom 스플라인: P1과 P2 사이를 P0, P3의 영향을 받아 보간한다.</summary>
    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    /// <summary>카메라 기준 이탈 방향으로 exitDuration 동안 이동한 뒤 비활성화한다.</summary>
    private IEnumerator ExitRoutine()
    {
        if (playerCamera == null) yield break;

        float elapsed = 0f;
        Vector3 startPos = transform.position;

        while (elapsed < exitDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / exitDuration;

            // 이탈 방향도 카메라 기준으로 매 프레임 계산해, 카메라가 회전 중이어도 화면 기준 방향으로 빠져나간다
            Vector3 worldExitDir = playerCamera.TransformDirection(exitLocalDirection.normalized);
            Vector3 targetPosition = startPos + worldExitDir * exitDistance;

            Vector3 moveDirection = targetPosition - transform.position;
            transform.position = Vector3.Lerp(startPos, targetPosition, t);

            if (moveDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDirection.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * EXIT_ROTATION_MULTIPLIER * Time.deltaTime);
            }

            yield return null;
        }

        if (_attackCtrl != null) _attackCtrl.StopAttack();
        gameObject.SetActive(false);
    }

    // ── 파괴 → 추락 ──────────────────────────────────────

    /// <summary>모든 약점이 파괴되면 진행 중인 비행을 취소하고 추락 연출을 시작한다.</summary>
    protected override void OnVehicleDestroyed()
    {
        // 이미 추락 중이면 무시한다. 이탈 중이었더라도 파괴되면 추락이 우선한다.
        if (_isCrashed) return;

        base.OnVehicleDestroyed();

        _isCrashed = true;
        _isExiting = false;
        StopAllCoroutines();
        if (_attackCtrl != null) _attackCtrl.StopAttack();
        StartCoroutine(CrashRoutine());
    }

    /// <summary>추락 연출이 끝나면 비활성화한다.</summary>
    private IEnumerator CrashRoutine()
    {
        yield return HelicopterCrashMotion.Fall(transform, crashInitialSpeed, crashAcceleration, crashRotateSpeed, crashGroundY);
        gameObject.SetActive(false);
    }

#if UNITY_EDITOR
    /// <summary>에디터에서 시작·도착·경유지 위치와 비행 경로를 표시한다.</summary>
    private void OnDrawGizmos()
    {
        Transform refCam = playerCamera;
        if (refCam == null)
        {
            Camera cam = Camera.main;
            if (cam == null) cam = FindAnyObjectByType<Camera>(); // MainCamera 태그가 없을 때
            if (cam != null) refCam = cam.transform;
        }
        if (refCam == null) return;

        Vector3 startWorld = refCam.TransformPoint(startLocalOffset);
        Vector3 endWorld = refCam.TransformPoint(endLocalOffset);

        // 시작점 (초록) / 도착점 (빨강)
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(startWorld, 2f);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(endWorld, 2f);

        bool hasWaypoints = waypointLocalOffsets != null && waypointLocalOffsets.Length > 0;

        // 경유지 (주황)
        if (hasWaypoints)
        {
            Gizmos.color = new Color(1f, 0.6f, 0f);
            for (int i = 0; i < waypointLocalOffsets.Length; i++)
            {
                Vector3 wpWorld = refCam.TransformPoint(waypointLocalOffsets[i]);
                Gizmos.DrawWireSphere(wpWorld, 1.5f);
                UnityEditor.Handles.Label(wpWorld, $"WP{i} {waypointLocalOffsets[i]}");
            }
        }

        // 경로 (노랑): 플레이 중에는 실제 곡선을 샘플링하고, 정지 상태에서는 제어점을 직선으로 잇는다
        Gizmos.color = Color.yellow;
        if (Application.isPlaying && playerCamera != null && hasWaypoints)
        {
            const int samples = 48;
            Vector3 prev = EvaluatePathWorld(0f);
            for (int i = 1; i <= samples; i++)
            {
                Vector3 cur = EvaluatePathWorld(i / (float)samples);
                Gizmos.DrawLine(prev, cur);
                prev = cur;
            }
        }
        else if (hasWaypoints)
        {
            Vector3 prev = startWorld;
            foreach (Vector3 wp in waypointLocalOffsets)
            {
                Vector3 cur = refCam.TransformPoint(wp);
                Gizmos.DrawLine(prev, cur);
                prev = cur;
            }
            Gizmos.DrawLine(prev, endWorld);
        }
        else
        {
            Gizmos.DrawLine(startWorld, endWorld);
        }

        // 카메라 정면 방향 (청록)
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(refCam.position, refCam.position + refCam.forward * 10f);

        UnityEditor.Handles.Label(startWorld, $"Start {startLocalOffset}");
        UnityEditor.Handles.Label(endWorld, $"End {endLocalOffset}");
    }
#endif
}
