using UnityEngine;

/// <summary>
/// 헬기 대형 미사일 발사대.
/// Fire() 호출 시 미리 배치된 미사일을 한 발씩 순서대로 발사한다.
/// 예측 조준은 HelicopterMissileBase에서 처리한다.
/// </summary>
public class HelicopterLargeMissile : HelicopterMissileBase
{
    [Header("미사일 오브젝트 목록")]
    [Tooltip("씬에 미리 배치된 대형 미사일 오브젝트를 발사 순서대로 연결한다.")]
    [SerializeField] private GameObject[] missiles;

    [Header("비행 설정")]
    [Tooltip("미사일 비행 속도 (m/s)")]
    [SerializeField] private float missileSpeed = 300f;

    private int _nextMissileIndex = 0;

    /// <summary>발사할 미사일이 남아 있는지 여부</summary>
    public bool HasAmmo => missiles != null && _nextMissileIndex < missiles.Length;
    /// <summary>남은 미사일 수</summary>
    public int RemainingAmmo => missiles != null ? Mathf.Max(0, missiles.Length - _nextMissileIndex) : 0;

    /// <summary>다음 미사일 한 발을 발사한다. 잔탄이 없으면 무시한다.</summary>
    public void Fire()
    {
        if (!HasAmmo) return;
        LaunchNextMissile();
    }

    /// <summary>다음 미사일을 예측 위치로 발사한다. 미사일은 부모에서 분리해 독립적으로 비행시킨다.</summary>
    private void LaunchNextMissile()
    {
        GameObject missileObj = missiles[_nextMissileIndex++];
        if (missileObj == null) return;

        Vector3 targetPos = GetPredictedTargetPosition(missileObj.transform.position, missileSpeed);

        missileObj.transform.SetParent(null);
        missileObj.SetActive(true);

        if (!missileObj.TryGetComponent(out LargeMissileProjectile projectile))
            projectile = missileObj.AddComponent<LargeMissileProjectile>();

        projectile.Launch(targetPos, missileSpeed);
    }
}
