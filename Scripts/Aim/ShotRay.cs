using UnityEngine;
using System.Collections;

/// <summary>
/// 에임 UI 위치에서 메인 카메라 레이를 쏴 피격 판정을 처리한다.
/// 트래커 레이가 아닌 "화면에 보이는 에임 위치"를 기준으로 판정하므로,
/// 플레이어가 보는 에임 위치와 실제 피격 위치가 항상 일치한다.
///
/// Fire()는 명중 여부를 bool로 반환하며, Shot에서 명중률 집계에 사용한다.
///
/// targetLayer 설정:
///   Enemy 레이어  — 인간형 적 히트박스, 차량, 배럴, 보스 약점
///   Bomb 레이어   — 폭발물 계열
///   EnemyCollision 레이어(인간형 적 본체)는 포함하지 않는다.
///
/// 태그 설정:
///   Head — 헤드샷 판정 오브젝트
///   Body — 일반 피격 오브젝트
/// </summary>
public class ShotRay : MonoBehaviour
{
    [Header("플레이어")]
    [Tooltip("이 ShotRay를 소유한 플레이어 인덱스 (0~3). Shot의 값으로 덮어쓴다.")]
    public int playerIndex = 0;

    [Header("레이어 설정")]
    [Tooltip("Enemy와 Bomb 레이어만 체크한다. EnemyCollision은 포함하지 않는다.")]
    public LayerMask targetLayer;

    [Header("UI 설정")]
    [Tooltip("판정 기준이 되는 에임 RectTransform. 발사 시 반동 연출에도 사용한다.")]
    public RectTransform aim;

    [Tooltip("에임 Canvas를 렌더링하는 카메라. Screen Space - Overlay Canvas라면 비워둔다.")]
    [SerializeField] private Camera ui_camera;

    [Header("레이 설정")]
    [Tooltip("레이 최대 거리 (m)")]
    public float maxDistance = 100f;

    [Header("데미지")]
    [Tooltip("한 발당 데미지. 헤드샷 배율은 히트박스에서 적용한다.")]
    [SerializeField] private float damage = 10f;

    [Header("에임 피드백")]
    [Tooltip("적중/헤드샷 에임 피드백 컴포넌트")]
    [SerializeField] private AimFeedback aimFeedback;

    [Header("피격 이펙트")]
    [Tooltip("금속 표면 피격 이펙트")]
    [SerializeField] private GameObject hitMetalFX;
    [Tooltip("기본 표면(흙, 벽 등) 피격 이펙트")]
    [SerializeField] private GameObject hitDirtFX;
    [Tooltip("인체 피격 이펙트")]
    [SerializeField] private GameObject hitHumanFX;

    [Tooltip("피격 이펙트 자동 제거 시간 (초). 씬에 누적되지 않도록 한다.")]
    [SerializeField] private float hitFxLifetime = 2f;

    // 에임 반동 연출 값
    private readonly Vector2 _aimDefaultSize = new Vector2(85f, 85f);
    private readonly Vector2 _aimFireSize = new Vector2(150f, 150f);
    private const float AIM_RETURN_TIME = 0.2f;

    private Camera _mainCamera;
    private Coroutine _aimSpreadCoroutine;

    /// <summary>메인 카메라를 캐싱한다.</summary>
    private void Awake()
    {
        _mainCamera = Camera.main;
    }

    /// <summary>
    /// 사격 실행. 에임 위치의 타겟에 데미지를 주고 피격 결과에 따라 에임 피드백을 재생한다.
    /// </summary>
    /// <returns>데미지를 줄 수 있는 대상에 명중했으면 true, 빗나갔으면 false</returns>
    public bool Fire()
    {
        if (_mainCamera == null) _mainCamera = Camera.main;
        if (_mainCamera == null)
        {
            Debug.LogWarning("[ShotRay] 메인 카메라를 찾을 수 없습니다.");
            return false;
        }

        PlayFireFeedback();

        if (!DetectTarget(out RaycastHit hit)) return false;

        // 피격 판정과 이펙트 선택에 같은 컴포넌트를 쓰도록 한 번만 조회한다
        hit.collider.TryGetComponent(out IDamageable damageable);
        SpawnHitFX(hit, damageable);

        if (damageable == null) return false;

        Vector3 hitDir = (hit.collider.transform.position - _mainCamera.transform.position).normalized;
        damageable.TakeDamage(damage, hitDir, playerIndex);

        NotifyAimFeedback(hit.collider);
        return true;
    }

    /// <summary>에임 UI의 화면 좌표에서 메인 카메라 레이를 쏴 타겟을 찾는다.</summary>
    private bool DetectTarget(out RaycastHit hit)
    {
        hit = default;
        if (aim == null) return false;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(ui_camera, aim.position);
        Ray ray = _mainCamera.ScreenPointToRay(screenPoint);

        return Physics.Raycast(ray, out hit, maxDistance, targetLayer, QueryTriggerInteraction.Ignore);
    }

    /// <summary>발사음 재생과 에임 반동 연출 (즉시 확대 후 AIM_RETURN_TIME 동안 복귀).</summary>
    private void PlayFireFeedback()
    {
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlaySFXAtPosition("PlayerShot", _mainCamera.transform.position, 0.5f);

        if (aim == null) return;

        if (_aimSpreadCoroutine != null) StopCoroutine(_aimSpreadCoroutine);
        aim.sizeDelta = _aimFireSize;
        _aimSpreadCoroutine = StartCoroutine(ReturnAimSize());
    }

    /// <summary>확대된 에임을 AIM_RETURN_TIME 동안 기본 크기로 되돌린다.</summary>
    private IEnumerator ReturnAimSize()
    {
        float elapsed = 0f;
        while (elapsed < AIM_RETURN_TIME)
        {
            elapsed += Time.deltaTime;
            aim.sizeDelta = Vector2.Lerp(_aimFireSize, _aimDefaultSize, elapsed / AIM_RETURN_TIME);
            yield return null;
        }
        aim.sizeDelta = _aimDefaultSize;
        _aimSpreadCoroutine = null;
    }

    /// <summary>피격 콜라이더의 태그로 헤드샷 여부를 판별해 에임 피드백을 재생한다.</summary>
    private void NotifyAimFeedback(Collider hitCollider)
    {
        if (aimFeedback == null) return;

        if (hitCollider.CompareTag("Head")) aimFeedback.ShowHeadshot(playerIndex);
        else aimFeedback.ShowHit(playerIndex);
    }

    /// <summary>
    /// 피격 대상의 HitSurfaceType에 맞는 이펙트를 생성하고 hitFxLifetime 후 제거한다.
    /// IDamageable이 없는 오브젝트(벽 등)는 기본(Dirt) 이펙트를 사용한다.
    /// </summary>
    private void SpawnHitFX(RaycastHit hit, IDamageable damageable)
    {
        HitSurfaceType surfaceType = damageable != null ? damageable.HitSurfaceType : HitSurfaceType.Default;

        GameObject prefab = surfaceType switch
        {
            HitSurfaceType.Metal => hitMetalFX,
            HitSurfaceType.Human => hitHumanFX,
            _ => hitDirtFX
        };

        if (prefab == null) return;

        // 피격 대상의 자식으로 생성해 움직이는 적에도 이펙트가 붙어 있도록 한다
        GameObject fx = Instantiate(prefab, hit.point, Quaternion.LookRotation(hit.normal), hit.transform);
        Destroy(fx, hitFxLifetime);
    }
}
