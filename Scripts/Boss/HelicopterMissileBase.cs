using UnityEngine;

/// <summary>
/// 헬기 미사일 발사대(HelicopterLargeMissile / HelicopterSmallMissile)의 공통 베이스 클래스.
/// 카메라 이동 속도 추적과 예측 조준 계산(GetPredictedTargetPosition)을 담당한다.
///
/// 예측 조준 원리:
///   "예측 위치까지의 거리"로 도달 시간을 다시 계산하는 과정을 predictionIterations만큼 반복해
///   도달 시간과 예측 위치를 함께 수렴시킨다.
///
/// 의도적 오프셋:
///   예측이 너무 정확하면 미사일이 화면 정중앙으로 날아와 사라져 위협감이 떨어진다.
///   aimOffsetLocal로 카메라 기준 살짝 빗나가게 조준해 미사일이 화면 옆을 스쳐가도록 만든다.
/// </summary>
public abstract class HelicopterMissileBase : MonoBehaviour
{
    [Header("예측 조준 설정")]
    [Tooltip("예측 수렴 반복 횟수. 2~4면 충분히 수렴한다.")]
    [SerializeField] protected int predictionIterations = 3;

    [Tooltip("목표 위치 주변에 추가할 무작위 오프셋 범위 (m)")]
    [SerializeField] protected float targetOffsetRange = 2f;

    [Tooltip("Y축 오프셋 비율. 위아래로 너무 튀지 않도록 작게 설정한다.")]
    [SerializeField] protected float verticalOffsetRatio = 0.3f;

    [Header("의도적 조준 오프셋 (카메라 로컬 좌표)")]
    [Tooltip("예측 위치에 더할 의도적 오프셋 (카메라 right/up/forward 기준).\n" +
             "X: 좌(-)/우(+), Y: 아래(-)/위(+), Z: 뒤(-)/앞(+).\n" +
             "기본값 (-1.5, 0, 0)은 카메라 좌측 1.5m로 살짝 빗나가게 조준한다.")]
    [SerializeField] protected Vector3 aimOffsetLocal = new Vector3(-1.5f, 0f, 0f);

    [Tooltip("발사마다 X(좌우) 오프셋에 더할 무작위 범위 (m). 0이면 항상 aimOffsetLocal 그대로 조준한다.")]
    [SerializeField] protected float aimOffsetRandomX = 0.5f;

    /// <summary>매 프레임 갱신되는 카메라 이동 속도</summary>
    protected Vector3 cameraVelocity;

    private Vector3 _prevCameraPos;
    private bool _hasPrevCameraPos = false;

    /// <summary>매 프레임 카메라 이동 속도를 갱신한다.</summary>
    protected virtual void Update()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 camPos = cam.transform.position;
        float dt = Time.deltaTime;

        // 일시정지 등으로 deltaTime이 0인 프레임은 속도를 갱신하지 않는다 (0으로 나누기 방지)
        if (_hasPrevCameraPos && dt > 0f)
            cameraVelocity = (camPos - _prevCameraPos) / dt;

        _prevCameraPos = camPos;
        _hasPrevCameraPos = true;
    }

    /// <summary>
    /// 미사일 발사 위치와 속도로 미사일 도달 시점의 카메라 예측 위치를 계산하고,
    /// 카메라 로컬 기준 의도적 오프셋과 무작위 오프셋을 더해 반환한다.
    /// </summary>
    protected Vector3 GetPredictedTargetPosition(Vector3 missileStartPos, float missileSpeed)
    {
        Camera mainCam = Camera.main;
        if (mainCam == null)
            return transform.position + transform.forward * 30f;

        Transform cam = mainCam.transform;
        Vector3 camPos = cam.position;
        Vector3 predictedPos = camPos;

        // 예측 위치까지의 거리로 도달 시간을 다시 계산 → 새 예측 위치 산출을 반복
        for (int i = 0; i < predictionIterations; i++)
        {
            float timeToReach = Vector3.Distance(missileStartPos, predictedPos) / missileSpeed;
            predictedPos = camPos + cameraVelocity * timeToReach;
        }

        // 카메라 로컬 축 기준 의도적 오프셋 (X에는 추가 무작위)
        float randomX = Random.Range(-aimOffsetRandomX, aimOffsetRandomX);
        Vector3 localOffset = aimOffsetLocal + new Vector3(randomX, 0f, 0f);
        predictedPos += cam.right * localOffset.x + cam.up * localOffset.y + cam.forward * localOffset.z;

        // 월드 기준 무작위 흔들림 (Y축은 verticalOffsetRatio만큼 좁게)
        return predictedPos + new Vector3(
            Random.Range(-targetOffsetRange, targetOffsetRange),
            Random.Range(-targetOffsetRange * verticalOffsetRatio, targetOffsetRange * verticalOffsetRatio),
            Random.Range(-targetOffsetRange, targetOffsetRange));
    }
}
