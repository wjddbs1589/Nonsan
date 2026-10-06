using Unity.Cinemachine;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>
/// CinemachineSplineCart를 스플라인 위에서 이동시키는 주행 컨트롤러.
///
/// 속도 결정 우선순위 (매 프레임):
///   1. 일시 가속(SpeedUp)  — 지정 시간 동안 목표 속도로 가속
///   2. 일시 감속(SlowDown) — 지정 시간 동안 목표 속도로 감속
///   3. 평상시              — 전방 곡률을 계산해 커브가 급할수록 minCurveSpeed에 가깝게 감속
///
/// 외부 제어: StopMove / ResumeMove / ReleaseSpline(스플라인 추적 해제 후 물리 제어로 전환)
/// </summary>
[RequireComponent(typeof(CinemachineSplineCart))]
public class SplineMovement : MonoBehaviour
{
    [Header("속도")]
    [Tooltip("기본 최고 속도")]
    public float moveSpeed = 30.0f;

    [Tooltip("커브 구간 최저 속도")]
    [SerializeField] private float minCurveSpeed = 10.0f;

    [Header("가속")]
    [Tooltip("초당 속도 증가량")]
    [SerializeField] private float acceleration = 10.0f;

    [Tooltip("초당 속도 감소량")]
    [SerializeField] private float deceleration = 20.0f;

    [Tooltip("true이면 가속/감속 없이 즉시 목표 속도로 이동")]
    [SerializeField] private bool instantSpeed = false;

    [Header("커브 감지")]
    [Tooltip("곡률 민감도 — 값이 클수록 약한 커브에도 감속")]
    [SerializeField] private float curveSensitivity = 2.0f;

    [Tooltip("곡률 샘플링 시 전방 탐색 거리 (스플라인 정규화 t 기준)")]
    [SerializeField] private float lookAheadOffset = 0.02f;

    [Header("일시 속도 변화")]
    [Tooltip("SlowDown(duration) 호출 시 사용하는 기본 목표 속도")]
    [SerializeField] private float defaultSlowDownSpeed = 4f;

    [Tooltip("SpeedUp(duration) 호출 시 사용하는 기본 목표 속도")]
    [SerializeField] private float defaultSpeedUpSpeed = 20f;

    [Header("자동 시작")]
    [Tooltip("체크 시 씬 시작과 동시에 주행을 시작한다.")]
    [SerializeField] private bool isPlayOnAwake = true;

    [Header("디버그")]
    [Tooltip("에디터 또는 개발 빌드에서만 P키로 이동/정지를 토글한다.")]
    [SerializeField] private bool useDebugToggle = true;

    // 이동시킬 카트와 스플라인 정보
    private CinemachineSplineCart _cart;
    private SplineContainer _splineContainer;
    private float _splineLength;
    // 현재 주행 중인지 여부
    private bool _isMove;

    // 일시 감속/가속 상태 (남은 시간, 목표 속도)
    private float _slowDownTimer;
    private float _slowDownTarget;
    private float _speedUpTimer;
    private float _speedUpTarget;

    /// <summary>현재 실제 이동 속도</summary>
    public float CurrentSpeed { get; private set; }

    public bool IsMoving => _isMove;

    /// <summary>전체 스플라인 진행도 (0~1)</summary>
    public float NormalizedProgress
    {
        get
        {
            if (_splineContainer == null || _splineContainer.Spline == null || _splineLength <= 0f || _cart == null)
                return 0f;

            return Mathf.Clamp01((float)_cart.SplinePosition / _splineLength);
        }
    }

    /// <summary>카트와 스플라인 길이를 캐싱하고 자동 시작 여부를 설정한다.</summary>
    private void Awake()
    {
        _cart = GetComponent<CinemachineSplineCart>();
        CurrentSpeed = 0f;

        _splineContainer = _cart.Spline;
        if (_splineContainer != null && _splineContainer.Spline != null)
            _splineLength = _splineContainer.Spline.GetLength();

        _isMove = isPlayOnAwake;
    }

    /// <summary>속도 우선순위(일시 가속 → 일시 감속 → 커브 기반)에 따라 속도를 정하고 카트를 이동시킨다.</summary>
    private void Update()
    {
        // 빌드 배포본에서는 디버그 키가 동작하지 않도록 개발 빌드 여부를 함께 확인한다
        if (useDebugToggle && Debug.isDebugBuild && Input.GetKeyDown(KeyCode.P))
            _isMove = !_isMove;

        // 정지 상태이거나 경로 끝에 도달하면 이동하지 않는다
        if (!_isMove || _cart == null || _cart.SplinePosition >= _splineLength)
        {
            CurrentSpeed = 0f;
            return;
        }

        // 1. 일시 가속
        if (_speedUpTimer > 0f)
        {
            UpdateTemporarySpeed(ref _speedUpTimer, _speedUpTarget);
            return;
        }

        // 2. 일시 감속
        if (_slowDownTimer > 0f)
        {
            UpdateTemporarySpeed(ref _slowDownTimer, _slowDownTarget);
            return;
        }

        // 3. 평상시: 전방 곡률 기반 속도
        ApplySpeed(GetTargetSpeed());
        MoveCart();
    }

    /// <summary>일시 속도 변화 타이머를 줄이면서 목표 속도로 이동한다.</summary>
    private void UpdateTemporarySpeed(ref float timer, float targetSpeed)
    {
        timer = Mathf.Max(0f, timer - Time.deltaTime);
        ApplySpeed(targetSpeed);
        MoveCart();
    }

    /// <summary>현재 속도를 목표 속도까지 가속 또는 감속한다.</summary>
    private void ApplySpeed(float targetSpeed)
    {
        if (instantSpeed)
        {
            CurrentSpeed = targetSpeed;
            return;
        }

        float rate = targetSpeed > CurrentSpeed ? acceleration : deceleration;
        CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, targetSpeed, rate * Time.deltaTime);
    }

    /// <summary>현재 속도만큼 카트를 전진시킨다.</summary>
    private void MoveCart()
    {
        _cart.SplinePosition = Mathf.Min(_cart.SplinePosition + Time.deltaTime * CurrentSpeed, _splineLength);
    }

    /// <summary>
    /// 현재 위치와 전방 지점의 접선 방향 차이로 곡률을 계산해 목표 속도를 반환한다.
    /// 곡률이 클수록 minCurveSpeed에 가까워진다.
    /// </summary>
    private float GetTargetSpeed()
    {
        if (_splineContainer == null || _splineContainer.Spline == null || _splineLength <= 0f)
            return moveSpeed;

        var spline = _splineContainer.Spline;
        float currentT = Mathf.Clamp01((float)_cart.SplinePosition / _splineLength);
        float aheadT = Mathf.Clamp01(currentT + lookAheadOffset);

        float3 tangentCurrent = SplineUtility.EvaluateTangent(spline, currentT);
        float3 tangentAhead = SplineUtility.EvaluateTangent(spline, aheadT);

        // 두 접선이 같은 방향이면 0, 꺾일수록 커진다
        float curvature = 1.0f - math.dot(math.normalize(tangentCurrent), math.normalize(tangentAhead));
        float curveAmount = Mathf.Clamp01(curvature * curveSensitivity);

        return Mathf.Lerp(moveSpeed, minCurveSpeed, curveAmount);
    }

    /// <summary>차량을 정지하고 진행 중인 일시 속도 변화를 취소한다.</summary>
    public void StopMove()
    {
        _isMove = false;
        CurrentSpeed = 0f;
        _slowDownTimer = 0f;
        _speedUpTimer = 0f;
    }

    /// <summary>정지한 차량을 다시 출발시킨다.</summary>
    public void ResumeMove()
    {
        _isMove = true;
    }

    /// <summary>스플라인 추적을 완전히 해제해 물리 제어가 가능하도록 한다.</summary>
    public void ReleaseSpline()
    {
        StopMove();
        if (_cart != null) _cart.enabled = false;
    }

    /// <summary>기본 목표 속도(defaultSlowDownSpeed)로 일정 시간 감속한다.</summary>
    public void SlowDown(float duration) => SlowDown(duration, defaultSlowDownSpeed);

    /// <summary>지정 속도로 일정 시간 감속한다. 진행 중인 가속은 취소된다.</summary>
    public void SlowDown(float duration, float targetSpeed)
    {
        _speedUpTimer = 0f;
        _slowDownTimer = duration;
        _slowDownTarget = targetSpeed;
    }

    /// <summary>기본 목표 속도(defaultSpeedUpSpeed)로 일정 시간 가속한다.</summary>
    public void SpeedUp(float duration) => SpeedUp(duration, defaultSpeedUpSpeed);

    /// <summary>지정 속도로 일정 시간 가속한다. 진행 중인 감속은 취소된다.</summary>
    public void SpeedUp(float duration, float targetSpeed)
    {
        _slowDownTimer = 0f;
        _speedUpTimer = duration;
        _speedUpTarget = targetSpeed;
    }
}
