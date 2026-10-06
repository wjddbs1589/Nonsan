using UnityEngine;
using System.Collections;

/// <summary>
/// 헬기 소형 미사일 발사대 (로켓 포드).
/// Fire() 호출 시 missilesPerSalvo발을 launchInterval 간격으로 연속 발사한다.
/// 미사일은 씬에 미리 배치된 오브젝트를 순서대로 사용하며, 예측 조준은 HelicopterMissileBase에서 처리한다.
/// </summary>
public class HelicopterSmallMissile : HelicopterMissileBase
{
    [Header("미사일 오브젝트 목록")]
    [Tooltip("씬에 미리 배치된 소형 미사일 오브젝트를 발사 순서대로 연결한다.")]
    [SerializeField] private GameObject[] missiles;

    [Header("발사 설정")]
    [Tooltip("한 번의 공격에서 연속 발사할 미사일 수")]
    [SerializeField] private int missilesPerSalvo = 5;

    [Tooltip("미사일 한 발 사이의 발사 간격 (초)")]
    [SerializeField] private float launchInterval = 0.12f;

    [Header("비행 설정")]
    [Tooltip("미사일 비행 속도 (m/s)")]
    [SerializeField] private float missileSpeed = 400f;

    private int _nextMissileIndex = 0;

    // 진행 중인 일제사격 (null이면 발사 가능)
    private Coroutine _salvoCoroutine;

    /// <summary>발사할 미사일이 남아 있는지 여부</summary>
    public bool HasAmmo => missiles != null && _nextMissileIndex < missiles.Length;
    /// <summary>남은 미사일 수</summary>
    public int RemainingAmmo => missiles != null ? Mathf.Max(0, missiles.Length - _nextMissileIndex) : 0;

    /// <summary>일제사격을 시작한다. 이미 발사 중이거나 잔탄이 없으면 무시한다.</summary>
    public void Fire()
    {
        if (_salvoCoroutine != null || !HasAmmo) return;

        int count = Mathf.Min(missilesPerSalvo, RemainingAmmo);
        if (count <= 0) return;

        _salvoCoroutine = StartCoroutine(FireSalvo(count));
    }

    /// <summary>진행 중인 일제사격을 즉시 중단한다. 이미 발사된 미사일은 계속 비행한다.</summary>
    public void Stop()
    {
        if (_salvoCoroutine != null) StopCoroutine(_salvoCoroutine);
        _salvoCoroutine = null;
    }

    /// <summary>비활성화되면 코루틴이 중단되므로 상태도 초기화한다. (재활성화 후 발사 불가 방지)</summary>
    private void OnDisable()
    {
        _salvoCoroutine = null;
    }

    /// <summary>count발을 launchInterval 간격으로 순차 발사한다.</summary>
    private IEnumerator FireSalvo(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (!HasAmmo) break;
            LaunchNextMissile();
            yield return new WaitForSeconds(launchInterval);
        }

        _salvoCoroutine = null;
    }

    /// <summary>다음 미사일을 예측 위치로 발사한다. 미사일은 부모에서 분리해 독립적으로 비행시킨다.</summary>
    private void LaunchNextMissile()
    {
        GameObject missileObj = missiles[_nextMissileIndex++];
        if (missileObj == null) return;

        Vector3 targetPos = GetPredictedTargetPosition(missileObj.transform.position, missileSpeed);

        missileObj.transform.SetParent(null);
        missileObj.SetActive(true);

        if (!missileObj.TryGetComponent(out SmallMissileProjectile projectile))
            projectile = missileObj.AddComponent<SmallMissileProjectile>();

        projectile.Launch(targetPos, missileSpeed);
    }
}
