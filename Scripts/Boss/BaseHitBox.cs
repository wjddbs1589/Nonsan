using UnityEngine;

/// <summary>
/// 적 히트박스의 공통 기반 클래스.
/// 피격을 owner(적 본체)에게 전달하며, 태그로 헤드샷을 판별해 데미지 배율을 적용한다. (Head = 헤드샷, Body = 일반)
///
/// 사용 예:
///   public class EnemyHitBox : BaseHitBox&lt;Enemy&gt; { }
///   public class CoverEnemyHitBox : BaseHitBox&lt;CoverEnemy&gt; { }
/// </summary>
public abstract class BaseHitBox<T> : MonoBehaviour, IDamageable where T : BaseEnemy
{
    [Tooltip("데미지를 실제로 받을 적 오브젝트. 비워두면 부모에서 자동으로 찾는다.")]
    [SerializeField] protected T owner;

    [Header("헤드샷 배율")]
    [Tooltip("Head 태그 히트박스에 적용할 데미지 배율")]
    [SerializeField] protected float headshotMultiplier = 2f;

    [Header("피격 표면")]
    [Tooltip("피격 이펙트 종류를 결정한다.")]
    [SerializeField] private HitSurfaceType hitSurfaceType = HitSurfaceType.Human;
    public HitSurfaceType HitSurfaceType => hitSurfaceType;

    /// <summary>owner의 사망 여부</summary>
    public bool IsDead => owner != null && owner.IsDead;

    /// <summary>에디터에서 컴포넌트를 추가하거나 Reset할 때 owner를 미리 채운다.</summary>
    protected virtual void Reset()
    {
        if (owner == null) owner = GetComponentInParent<T>();
    }

    /// <summary>
    /// 인스펙터에서 owner가 비어 있으면 런타임에 부모에서 찾는다.
    /// (Reset은 에디터에서만 호출되므로 런타임 보장을 위해 한 번 더 확인한다)
    /// </summary>
    protected virtual void Awake()
    {
        if (owner == null) owner = GetComponentInParent<T>();
        if (owner == null)
            Debug.LogWarning($"[{GetType().Name}] owner를 찾을 수 없어 피격이 전달되지 않습니다.", this);
    }

    /// <summary>태그가 Head이면 headshotMultiplier를 곱한 뒤 owner에게 데미지를 전달한다.</summary>
    public virtual void TakeDamage(float damage, Vector3 hitDirection, int attackerIndex)
    {
        if (owner == null || owner.IsDead) return;
        if (CompareTag("Head")) damage *= headshotMultiplier;
        owner.TakeDamage(damage, hitDirection, attackerIndex);
    }
}
