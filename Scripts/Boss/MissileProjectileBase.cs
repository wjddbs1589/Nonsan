using UnityEngine;

/// <summary>
/// 미사일 비행체(LargeMissileProjectile / SmallMissileProjectile)의 공통 베이스 클래스.
/// Rigidbody 초기화, 발사 방향 설정, 약한 호밍, 폭발 조건 판정, 폭발 처리를 담당한다.
///
/// 폭발 조건:
///   - 카메라 근접: 직전 위치 → 현재 위치 이동 구간에서 카메라와의 최단 거리로 판정 (고속 통과 누락 방지)
///   - 수명 초과: maxLifetime이 지나면 강제 폭발
///   - 충돌: 트리거가 아닌 콜라이더와 접촉
///
/// 자식 클래스는 FixedUpdate에서 CheckExplosionConditions() → UpdateTrackingDirection() 순으로 호출한 뒤
/// linearVelocity를 launchDirection * speed로 설정한다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public abstract class MissileProjectileBase : MonoBehaviour
{
    [Header("폭발 이펙트")]
    [Tooltip("폭발 시 생성할 이펙트 프리팹")]
    [SerializeField] private GameObject explosionEffectPrefab;

    [Tooltip("폭발 이펙트 자동 제거 시간 (초)")]
    [SerializeField] private float explosionEffectLifetime = 3f;

    [Header("축 보정")]
    [Tooltip("모델의 앞 방향을 비행 방향에 맞추는 회전 보정값.\n" +
             "Z축이 앞이면 (0, 0, 0), X축이 앞이면 (0, -90, 0), -Y축(아래)이 앞이면 (-90, 0, 0).")]
    [SerializeField] protected Vector3 rotationOffset = new Vector3(-90f, 0f, 0f);

    [Header("비네트 설정")]
    [Tooltip("이 거리 이내에서 폭발하면 플레이어 피격(비네트)이 발생한다.")]
    [SerializeField] protected float vignetteTriggerDistance = 5f;

    [Header("비행 중 추적 보정 (약한 호밍)")]
    [Tooltip("초당 회전 가능한 최대 각도 (도). 너무 크면 모든 미사일이 명중해 회피감이 사라진다. 30~60 권장.")]
    [SerializeField] protected float trackingDegreesPerSecond = 40f;

    [Tooltip("카메라가 이 거리 이내로 들어오면 호밍을 멈추고 직진한다. 근거리에서 카메라 주위를 맴도는 현상을 막는다.")]
    [SerializeField] protected float homingCutoffDistance = 8f;

    [Header("수명 / 근접 폭발")]
    [Tooltip("발사 후 이 시간이 지나면 강제 폭발한다.")]
    [SerializeField] protected float maxLifetime = 4f;

    [Tooltip("카메라와 이 거리 이내를 지나가면 폭발한다 (스쳐 지나가는 효과). vignetteTriggerDistance보다 약간 크게 설정한다.")]
    [SerializeField] protected float cameraProximityTrigger = 6f;

    protected float speed;
    protected bool hasExploded = false;
    protected bool isLaunched = false;
    protected Vector3 launchDirection;
    protected Rigidbody rb;
    protected float launchTime;

    // 근접 판정용 직전 물리 스텝 위치
    private Vector3 _prevPosition;

    /// <summary>Rigidbody를 중력 없이 회전 고정 상태로 초기화한다.</summary>
    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.freezeRotation = true;
    }

    /// <summary>
    /// 발사대(HelicopterLargeMissile 등)에서 호출한다.
    /// 목표 위치 방향으로 회전을 맞추고 발사음을 재생한다.
    /// </summary>
    public virtual void Launch(Vector3 targetPosition, float missileSpeed)
    {
        speed = missileSpeed;
        isLaunched = true;
        launchTime = Time.time;
        _prevPosition = transform.position;

        launchDirection = (targetPosition - transform.position).normalized;
        if (launchDirection != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(launchDirection) * Quaternion.Euler(rotationOffset);

        if (SoundManager.Instance != null)
            SoundManager.Instance.PlaySFXAtPosition("Missile", transform.position, 0.5f);
    }

    /// <summary>
    /// 비행 방향을 카메라 쪽으로 trackingDegreesPerSecond 이내에서 회전시킨다 (약한 호밍).
    /// 예측이 빗나가도 미사일이 카메라 근처를 지나가도록 보정한다.
    /// </summary>
    protected void UpdateTrackingDirection()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 camPos = cam.transform.position;
        if (Vector3.Distance(transform.position, camPos) < homingCutoffDistance) return;

        Vector3 toCamera = (camPos - transform.position).normalized;
        float maxRadians = trackingDegreesPerSecond * Time.fixedDeltaTime * Mathf.Deg2Rad;
        launchDirection = Vector3.RotateTowards(launchDirection, toCamera, maxRadians, 0f).normalized;

        transform.rotation = Quaternion.LookRotation(launchDirection) * Quaternion.Euler(rotationOffset);
    }

    /// <summary>
    /// 수명 초과와 카메라 근접을 검사해 조건을 만족하면 폭발한다.
    /// 폭발했으면 true를 반환하며, 자식 클래스는 비행 처리를 중단한다.
    /// </summary>
    protected bool CheckExplosionConditions()
    {
        if (Time.time - launchTime >= maxLifetime)
        {
            Explode();
            return true;
        }

        Camera cam = Camera.main;
        if (cam != null)
        {
            // 고속 비행 시 한 물리 스텝에 근접 범위를 통과할 수 있으므로,
            // 현재 위치가 아니라 직전 위치 → 현재 위치 이동 구간 전체에서 카메라와의 최단 거리를 검사한다
            Vector3 camPos = cam.transform.position;
            Vector3 closest = ClosestPointOnSegment(_prevPosition, transform.position, camPos);

            if (Vector3.Distance(closest, camPos) <= cameraProximityTrigger)
            {
                // 실제로 카메라에 가장 가까웠던 지점에서 폭발시킨다
                transform.position = closest;
                Explode();
                return true;
            }
        }

        _prevPosition = transform.position;
        return false;
    }

    /// <summary>선분 a-b 위에서 점 p와 가장 가까운 점을 반환한다.</summary>
    private static Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 p)
    {
        Vector3 ab = b - a;
        float lengthSq = ab.sqrMagnitude;
        if (lengthSq < 1e-6f) return a;

        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / lengthSq);
        return a + ab * t;
    }

    /// <summary>트리거가 아닌 콜라이더에 닿으면 폭발한다.</summary>
    private void OnTriggerEnter(Collider other)
    {
        if (!isLaunched || hasExploded) return;
        if (!other.isTrigger) Explode();
    }

    /// <summary>비네트 판정 → 폭발 이펙트 생성 → 오브젝트 파괴 순서로 처리한다. 한 번만 실행된다.</summary>
    protected void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;

        Camera cam = Camera.main;
        if (cam != null && Vector3.Distance(transform.position, cam.transform.position) <= vignetteTriggerDistance)
            PlayerData.PlayerTakeDamageForced();

        if (explosionEffectPrefab != null)
        {
            GameObject fx = Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
            Destroy(fx, explosionEffectLifetime);
        }

        Destroy(gameObject);
    }
}
