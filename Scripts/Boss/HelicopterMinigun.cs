using UnityEngine;
using System.Collections;

/// <summary>
/// 헬기 기관총.
/// Fire() 호출 시 bulletsPerBurst발을 bulletInterval 간격으로 점사하고,
/// 발사마다 TrailPool에서 탄 궤적을 꺼내 재생한다.
///
/// 조준 방식:
///   aimAtPlayerChance 확률로 카메라 방향을, 나머지는 총구 정면 방향을 조준한다.
/// </summary>
public class HelicopterMinigun : MonoBehaviour
{
    [Header("탄 궤적")]
    [Tooltip("탄 궤적의 최대 거리 (m)")]
    [SerializeField] private float traceDistance = 40f;

    [Tooltip("탄 궤적 이동 속도")]
    [SerializeField] private float traceSpeed = 120f;

    [Header("탄퍼짐")]
    [Tooltip("탄퍼짐 각도 (도). 클수록 넓게 퍼진다.")]
    [SerializeField] private float spreadAngle = 3f;

    [Header("발사 설정")]
    [Tooltip("한 번 공격 시 연사할 탄 수")]
    [SerializeField] private int bulletsPerBurst = 15;

    [Tooltip("탄 한 발 사이의 간격 (초)")]
    [SerializeField] private float bulletInterval = 0.1f;

    [Tooltip("카메라 방향으로 조준할 확률 (0 = 항상 정면, 1 = 항상 플레이어 조준)")]
    [SerializeField] private float aimAtPlayerChance = 0.5f;

    [Tooltip("총구 위치. 비워두면 이 오브젝트의 위치를 사용한다.")]
    [SerializeField] private Transform muzzlePoint;

    [Tooltip("발사마다 총구에 생성할 머즐 이펙트")]
    [SerializeField] private GameObject muzzleEffect;

    [Tooltip("머즐 이펙트 자동 제거 시간 (초)")]
    [SerializeField] private float muzzleEffectDuration = 0.1f;

    // 진행 중인 점사 (null이면 발사 가능)
    private Coroutine _burstCoroutine;

    /// <summary>점사 중인지 여부</summary>
    public bool IsFiring => _burstCoroutine != null;

    /// <summary>
    /// 점사를 시작한다. 이미 점사 중이면 무시한다.
    /// overrideBulletCount를 지정하면 bulletsPerBurst 대신 해당 발수로 연사한다.
    /// </summary>
    public void Fire(int overrideBulletCount = -1)
    {
        if (_burstCoroutine != null) return;

        int count = overrideBulletCount > 0 ? overrideBulletCount : bulletsPerBurst;
        if (count <= 0) return;

        _burstCoroutine = StartCoroutine(FireBurst(count));
    }

    /// <summary>진행 중인 점사를 즉시 중단한다. 공격 중지나 무기 파괴 시 호출된다.</summary>
    public void Stop()
    {
        if (_burstCoroutine != null) StopCoroutine(_burstCoroutine);
        _burstCoroutine = null;
    }

    /// <summary>비활성화되면 코루틴이 중단되므로 상태도 초기화한다. (재활성화 후 발사 불가 방지)</summary>
    private void OnDisable()
    {
        _burstCoroutine = null;
    }

    /// <summary>count발을 bulletInterval 간격으로 연사한다.</summary>
    private IEnumerator FireBurst(int count)
    {
        // 점사 시작 시 플레이어 피격 연출(비네트)을 발생시킨다
        PlayerData.PlayerTakeDamageForced();

        for (int i = 0; i < count; i++)
        {
            if (SoundManager.Instance != null)
                SoundManager.Instance.PlaySFXAtPosition("Minigun", transform.position, 0.5f);

            if (muzzleEffect != null && muzzlePoint != null)
            {
                GameObject effect = Instantiate(muzzleEffect, muzzlePoint.position, muzzlePoint.rotation, muzzlePoint);
                Destroy(effect, muzzleEffectDuration);
            }

            PlayTrace();
            yield return new WaitForSeconds(bulletInterval);
        }

        _burstCoroutine = null;
    }

    /// <summary>TrailPool에서 궤적을 꺼내 발사 방향으로 재생한다.</summary>
    private void PlayTrace()
    {
        if (TrailPool.Instance == null)
        {
            Debug.LogWarning("[HelicopterMinigun] TrailPool 인스턴스가 없습니다.");
            return;
        }

        Transform origin = muzzlePoint != null ? muzzlePoint : transform;
        Vector3 startPoint = origin.position;

        Camera cam = Camera.main;
        Vector3 dir = (cam != null && Random.value < aimAtPlayerChance)
            ? (cam.transform.position - startPoint).normalized
            : origin.forward;

        dir = ApplySpread(dir);

        TrailRenderer trailRenderer = TrailPool.Instance.Get();
        if (!trailRenderer.TryGetComponent(out PooledBulletTrail pooledTrail))
            pooledTrail = trailRenderer.gameObject.AddComponent<PooledBulletTrail>();

        pooledTrail.Play(startPoint, startPoint + dir * traceDistance, traceSpeed);
    }

    /// <summary>방향 벡터에 spreadAngle 범위의 무작위 탄퍼짐을 적용한다.</summary>
    private Vector3 ApplySpread(Vector3 direction)
    {
        float half = spreadAngle * 0.5f;
        Quaternion spread = Quaternion.Euler(Random.Range(-half, half), Random.Range(-half, half), 0f);
        return (Quaternion.LookRotation(direction) * spread) * Vector3.forward;
    }
}
