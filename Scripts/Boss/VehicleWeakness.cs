using UnityEngine;
using System;

/// <summary>
/// 차량형 보스의 부위 파괴 약점.
/// 체력이 0이 되면 콜라이더를 비활성화하고 partsDestroyEvent를 발생시킨다.
///
/// 점수 구조:
///   피격 보너스(hitBonusScore) — 살아있을 때 피격마다 적립
///   처치 점수(scoreOnKill)     — 파괴 시 BaseEnemy에서 적립
///   파괴되는 마지막 피격에는 피격 보너스를 주지 않아 두 점수가 중복되지 않는다.
/// </summary>
public class VehicleWeakness : BaseEnemy
{
    /// <summary>
    /// 부위가 파괴될 때 발생한다.
    /// MarkerBase(다음 약점 활성화)와 공격 컨트롤러(대응 무기 무력화)가 구독한다.
    /// </summary>
    public event Action<Transform> partsDestroyEvent;

    [Header("파괴 설정")]
    [Tooltip("체크 시 파괴 이펙트 생성 전에 모든 자식 오브젝트를 제거한다.")]
    [SerializeField] private bool destroyChildrenOnDeath = false;

    [Header("파괴 이펙트")]
    [Tooltip("파괴 시 생성할 폭발 이펙트 (부위의 자식으로 생성)")]
    [SerializeField] private GameObject explosionEffectPrefab;
    [Tooltip("파괴 시 함께 생성할 전기 스파크 이펙트")]
    [SerializeField] private GameObject electricEffectPrefab;

    [Header("피격 보너스 점수")]
    [Tooltip("파괴 전 피격 시 공격자에게 추가되는 점수. 파괴 시의 scoreOnKill과 중복되지 않는다.")]
    [SerializeField] private int hitBonusScore = 30;

    /// <summary>체력을 초기화한다.</summary>
    private void Awake()
    {
        InitHealth();
    }

    /// <summary>피격 처리 후 효과음을 재생하고, 아직 파괴되지 않았으면 피격 보너스 점수를 적립한다.</summary>
    public override void TakeDamage(float damage, Vector3 hitDirection, int attackerIndex)
    {
        if (IsDead) return;

        base.TakeDamage(damage, hitDirection, attackerIndex);

        if (SoundManager.Instance != null)
            SoundManager.Instance.PlaySFXAtPosition("SFX_V02", transform.position);

        // 아직 살아있을 때만 피격 보너스 적립 (파괴된 경우 scoreOnKill로 처리됨)
        if (!IsDead && ScoreManager.Instance != null)
            ScoreManager.Instance.AddScore(attackerIndex, hitBonusScore);
    }

    /// <summary>
    /// 콜라이더 비활성화 → 자식 제거(옵션) → 이펙트 생성 → 파괴 이벤트 순서로 처리한다.
    /// 이벤트를 로컬 변수로 옮긴 뒤 필드를 비워, 중복 발생과 구독 해제 누락을 함께 방지한다.
    /// </summary>
    protected override void OnDeath(int killerIndex)
    {
        if (TryGetComponent(out Collider col)) col.enabled = false;

        if (SoundManager.Instance != null)
            SoundManager.Instance.PlaySFXAtPosition("PartsBreak", transform.position);

        if (destroyChildrenOnDeath)
        {
            foreach (Transform child in transform)
                Destroy(child.gameObject);
        }

        // 이펙트는 자식으로 생성해 부위와 함께 이동하도록 한다
        if (explosionEffectPrefab != null)
            Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity, transform);

        if (electricEffectPrefab != null)
            Instantiate(electricEffectPrefab, transform.position, Quaternion.identity, transform);

        var evt = partsDestroyEvent;
        partsDestroyEvent = null;
        evt?.Invoke(transform);
    }
}
