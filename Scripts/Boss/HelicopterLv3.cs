using UnityEngine;
using System.Collections;

/// <summary>
/// Lv3 헬기 보스. 외부 트리거(Lv3HeliTrigger)의 신호로 진입과 이탈을 제어한다.
///
/// 페이즈:
///   진입 → 카메라 정면 기준 위치로 enterDuration 동안 이동하고, 완료되면 공격을 시작한다
///   호버 → Perlin Noise로 카메라 정면 영역 안을 떠다니며 레일을 따라 이동하는 카메라를 추적한다
///   이탈 → RequestExit() 호출 시 공격을 멈추고 위로 빠져나간 뒤 비활성화된다
///
/// 호버 영역:
///   현장 스크린 배치에 맞춰 좌우 이동 범위를 hoverAreaLeft / hoverAreaRight로 나눠 비대칭으로 설정할 수 있다.
/// </summary>
public class HelicopterLv3 : MarkerBase
{
    [Header("전투 대상")]
    [Tooltip("플레이어 카메라. 비워두면 Camera.main을 사용한다.")]
    [SerializeField] private Transform playerCamera;

    [Header("진입")]
    [Tooltip("진입 이동 시간 (초)")]
    [SerializeField] private float enterDuration = 3f;

    [Header("이탈")]
    [Tooltip("위로 빠져나가는 시간 (초)")]
    [SerializeField] private float exitDuration = 3f;

    [Tooltip("이탈 이동이 끝난 뒤 비활성화까지 대기 시간 (초)")]
    [SerializeField] private float deactivateDelay = 5f;

    [Tooltip("위로 이동하는 거리 (m)")]
    [SerializeField] private float exitUpDistance = 150f;

    [Header("기준 위치")]
    [Tooltip("카메라 정면 기준 거리 (m)")]
    [SerializeField] private float baseDistance = 20f;

    [Tooltip("기준 위치의 높이 오프셋 (m)")]
    [SerializeField] private float baseHeightOffset = 2.5f;

    [Header("호버 영역 (카메라 정면 기준)")]
    [Tooltip("중앙 기준 왼쪽 최대 이동 거리 (m). 양수로 입력한다.")]
    [SerializeField] private float hoverAreaLeft = 12f;

    [Tooltip("중앙 기준 오른쪽 최대 이동 거리 (m). 왼쪽·중앙 위주로 비행하려면 작게 설정한다.")]
    [SerializeField] private float hoverAreaRight = 2f;

    [Tooltip("위아래 최대 이동 거리 (m). ± 적용")]
    [SerializeField] private float hoverAreaHeight = 5f;

    [Tooltip("앞뒤 최대 이동 거리 (m). ± 적용")]
    [SerializeField] private float hoverAreaDepth = 4f;

    [Header("이동 속도")]
    [Tooltip("이동하는 카메라를 따라가는 추적 속도. 빠른 구간에서는 4 이상을 권장한다.")]
    [SerializeField] private float hoverLerpSpeed = 4.0f;

    [Tooltip("호버 영역 안에서 떠다니는 속도")]
    [SerializeField] private float hoverWanderSpeed = 0.3f;

    [Header("기수 회전")]
    [Tooltip("플레이어를 향해 기수를 돌리는 회전 속도")]
    [SerializeField] private float rotationSpeed = 3f;

    [Header("파괴 추락")]
    [Tooltip("추락 시작 속도")]
    [SerializeField] private float crashInitialSpeed = 12f;
    [Tooltip("추락 가속도")]
    [SerializeField] private float crashAcceleration = 8f;
    [Tooltip("추락 회전 속도 (도/초)")]
    [SerializeField] private float crashRotateSpeed = 120f;
    [Tooltip("이 Y 좌표 아래로 내려가면 비활성화한다.")]
    [SerializeField] private float crashGroundY = -10f;

    private HeliAttackController _attackCtrl;

    private bool _moveStarted = false;
    private bool _hasEntered = false;
    private bool _exitRequested = false;
    private bool _isCrashed = false;
    private bool _isAttackActive = false;

    // 헬기마다 다른 호버 궤적을 만들기 위한 Perlin Noise 시드
    private float _seedX, _seedY, _seedZ;

    /// <summary>
    /// Lv3HeliTrigger가 SetActive(true) 직후 StartMove()를 호출하므로,
    /// 이동과 공격에 필요한 참조는 Start가 아닌 Awake에서 준비한다.
    /// </summary>
    private void Awake()
    {
        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main.transform;

        _attackCtrl = GetComponent<HeliAttackController>();

        _seedX = Random.Range(0f, 100f);
        _seedY = Random.Range(0f, 100f);
        _seedZ = Random.Range(0f, 100f);
    }

    /// <summary>추락 전까지 플레이어를 바라보고 공격 상태를 갱신한다.</summary>
    private void Update()
    {
        if (_isCrashed || playerCamera == null) return;

        FacePlayer();
        UpdateAttackState();
    }

    /// <summary>진입 이동을 시작한다. 한 번만 동작한다.</summary>
    public void StartMove()
    {
        if (_moveStarted || _isCrashed) return;

        if (playerCamera == null)
        {
            Debug.LogWarning("[HelicopterLv3] 플레이어 카메라를 찾을 수 없어 이동을 시작하지 않습니다.", this);
            return;
        }

        _moveStarted = true;
        StartCoroutine(MainRoutine());
    }

    /// <summary>호버를 끝내고 이탈하도록 요청한다. 공격은 즉시 중단된다.</summary>
    public void RequestExit()
    {
        if (_exitRequested || _isCrashed) return;
        _exitRequested = true;
        ForceStopAttack();
    }

    /// <summary>진입 → 호버 → 이탈 페이즈를 순서대로 실행한다.</summary>
    private IEnumerator MainRoutine()
    {
        yield return PhaseEnter();
        _hasEntered = true;

        yield return PhaseContinuousHover();

        if (!_isCrashed)
            yield return PhaseExit();
    }

    // ── 진입 ─────────────────────────────────────────────

    /// <summary>현재 위치에서 카메라 정면 기준 위치까지 enterDuration 동안 이동한다.</summary>
    private IEnumerator PhaseEnter()
    {
        // 시작 위치도 카메라 기준 상대 위치로 유지해, 카메라가 이동해도 자연스럽게 따라오며 진입한다
        Vector3 startOffset = transform.position - playerCamera.position;

        Vector3 initialDir = (FrontAnchor() - (playerCamera.position + startOffset)).normalized;
        if (initialDir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(initialDir);

        float elapsed = 0f;
        while (elapsed < enterDuration)
        {
            if (_isCrashed) yield break;
            elapsed += Time.deltaTime;

            Vector3 start = playerCamera.position + startOffset;
            transform.position = Vector3.Lerp(start, FrontAnchor(), elapsed / enterDuration);
            yield return null;
        }
    }

    // ── 호버 ─────────────────────────────────────────────

    /// <summary>이탈 요청이나 추락 전까지 호버 영역 안을 떠다니며 카메라를 따라간다.</summary>
    private IEnumerator PhaseContinuousHover()
    {
        while (!_exitRequested && !_isCrashed)
        {
            // 축마다 다른 시드로 0~1 범위의 Perlin Noise 값을 얻는다
            float nx = Mathf.PerlinNoise(Time.time * hoverWanderSpeed + _seedX, 0f);
            float ny = Mathf.PerlinNoise(0f, Time.time * hoverWanderSpeed + _seedY);
            float nz = Mathf.PerlinNoise(Time.time * hoverWanderSpeed + _seedZ, 100f);

            // X는 왼쪽(-hoverAreaLeft) ~ 오른쪽(+hoverAreaRight)의 비대칭 범위로,
            // Y와 Z는 중앙 기준 ± 범위로 매핑한다
            Vector3 localOffset = new Vector3(
                Mathf.Lerp(-hoverAreaLeft, hoverAreaRight, nx),
                (ny * 2f - 1f) * hoverAreaHeight,
                (nz * 2f - 1f) * hoverAreaDepth);

            // 카메라는 빠르게 추적하고, 영역 안의 움직임은 노이즈로 천천히 변하게 한다
            transform.position = Vector3.Lerp(
                transform.position,
                GetWorldTargetFromOffset(localOffset),
                hoverLerpSpeed * Time.deltaTime);

            yield return null;
        }
    }

    /// <summary>카메라 정면 기준 로컬 오프셋을 월드 좌표로 변환한다. 높이는 카메라 기울기와 무관하게 월드 위쪽 기준이다.</summary>
    private Vector3 GetWorldTargetFromOffset(Vector3 localOffset)
    {
        return FrontAnchor()
               + playerCamera.right * localOffset.x
               + Vector3.up * localOffset.y
               + playerCamera.forward * localOffset.z;
    }

    // ── 이탈 ─────────────────────────────────────────────

    /// <summary>공격을 멈추고 위로 이동한 뒤 deactivateDelay 후 비활성화한다.</summary>
    private IEnumerator PhaseExit()
    {
        ForceStopAttack();

        Vector3 startPos = transform.position;
        Vector3 exitTarget = startPos + Vector3.up * exitUpDistance;

        float elapsed = 0f;
        while (elapsed < exitDuration)
        {
            if (_isCrashed) yield break;
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(startPos, exitTarget, elapsed / exitDuration);
            yield return null;
        }

        yield return new WaitForSeconds(deactivateDelay);
        gameObject.SetActive(false);
    }

    // ── 파괴 → 추락 ──────────────────────────────────────

    /// <summary>모든 약점이 파괴되면 공격을 멈추고 추락 연출을 시작한다.</summary>
    protected override void OnVehicleDestroyed()
    {
        // 이탈 요청 이후에 파괴되면 추락 연출 없이 이탈을 이어간다
        if (_exitRequested || _isCrashed) return;

        base.OnVehicleDestroyed();
        ForceStopAttack();
        StopAllCoroutines();
        _isCrashed = true;
        StartCoroutine(CrashRoutine());
    }

    /// <summary>추락 연출이 끝나면 비활성화한다.</summary>
    private IEnumerator CrashRoutine()
    {
        yield return HelicopterCrashMotion.Fall(transform, crashInitialSpeed, crashAcceleration, crashRotateSpeed, crashGroundY);
        gameObject.SetActive(false);
    }

    // ── 공통 ─────────────────────────────────────────────

    /// <summary>진입 완료 후, 이탈·추락 전까지만 공격하도록 공격 상태를 맞춘다.</summary>
    private void UpdateAttackState()
    {
        if (_attackCtrl == null) return;

        bool canAttack = _hasEntered && !_exitRequested && !_isCrashed;

        if (canAttack && !_isAttackActive)
        {
            _isAttackActive = true;
            _attackCtrl.StartAttack();
        }
        else if (!canAttack && _isAttackActive)
        {
            ForceStopAttack();
        }
    }

    /// <summary>공격 중이면 공격 컨트롤러를 중지한다.</summary>
    private void ForceStopAttack()
    {
        if (_attackCtrl != null && _isAttackActive)
            _attackCtrl.StopAttack();
        _isAttackActive = false;
    }

    /// <summary>카메라 정면 baseDistance 지점 (호버 영역의 중심)</summary>
    private Vector3 FrontAnchor() =>
        playerCamera.position
        + playerCamera.forward * baseDistance
        + Vector3.up * baseHeightOffset;

    /// <summary>기수를 플레이어 쪽으로 부드럽게 회전시킨다.</summary>
    private void FacePlayer()
    {
        Vector3 dir = playerCamera.position - transform.position;
        if (dir.sqrMagnitude < 0.001f) return;

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(dir),
            rotationSpeed * Time.deltaTime);
    }
}
