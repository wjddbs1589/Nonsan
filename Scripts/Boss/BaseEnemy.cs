using UnityEngine;

/// <summary>
/// IDamageable을 구현하는 모든 적의 공통 기반 클래스.
/// 체력 관리, 피격 처리, 사망 처리, 점수 적립을 담당한다.
///
/// 상속 구조:
///   BaseEnemy
///     ├─ CombatEnemy
///     │    └─ Enemy, CoverEnemy, DroneEnemy, VehicleEnemy
///     └─ VehicleWeakness (전투 동작이 필요 없는 보스 약점)
/// </summary>
public abstract class BaseEnemy : MonoBehaviour, IDamageable
{
    [Header("체력")]
    [Tooltip("최대 체력")]
    [SerializeField] protected float maxHealth = 100f;
    protected float currentHealth;

    [Header("점수")]
    [Tooltip("처치 시 마지막 공격자에게 부여할 점수")]
    [SerializeField] protected int scoreOnKill = 100;

    [Header("피격 표면")]
    [Tooltip("피격 이펙트 종류를 결정한다.")]
    [SerializeField] private HitSurfaceType hitSurfaceType;
    public HitSurfaceType HitSurfaceType => hitSurfaceType;

    private bool _isDead = false;
    /// <summary>사망 여부</summary>
    public bool IsDead => _isDead;

    /// <summary>마지막으로 피격을 가한 플레이어 인덱스 (0~3). 없으면 -1.</summary>
    protected int lastAttackerIndex = -1;

    /// <summary>
    /// 체력과 상태를 초기값으로 되돌린다.
    /// 자식 클래스의 Awake 또는 StartEnemy에서 반드시 호출해야 한다.
    /// </summary>
    protected void InitHealth()
    {
        currentHealth = maxHealth;
        _isDead = false;
        lastAttackerIndex = -1;
    }

    /// <summary>
    /// 피격 처리. 체력을 감소시키고 0 이하가 되면 사망 처리를 시작한다.
    /// 자식 클래스에서 override할 경우 base.TakeDamage()를 호출해야 체력 감소와 사망 처리가 동작한다.
    /// </summary>
    public virtual void TakeDamage(float damage, Vector3 hitDirection, int attackerIndex)
    {
        if (_isDead) return;
        lastAttackerIndex = attackerIndex;
        OnBeforeDamage(damage, hitDirection, attackerIndex);
        currentHealth -= damage;
        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            TriggerDeath();
        }
    }

    // 사망을 확정하고 점수 적립 후 OnDeath를 호출한다 (중복 호출 방지)
    private void TriggerDeath()
    {
        if (_isDead) return;
        _isDead = true;

        if (lastAttackerIndex >= 0 && ScoreManager.Instance != null)
            ScoreManager.Instance.AddScore(lastAttackerIndex, scoreOnKill);

        OnDeath(lastAttackerIndex);
    }

    /// <summary>
    /// 데미지가 체력에 적용되기 직전에 호출된다. 피격 이펙트, 피격 반응 등 전처리에 활용한다.
    /// damage는 값으로 전달되므로 여기서 바꿔도 실제 데미지에는 반영되지 않는다. (부위별 배율은 BaseHitBox에서 적용)
    /// </summary>
    protected virtual void OnBeforeDamage(float damage, Vector3 hitDirection, int attackerIndex) { }

    /// <summary>
    /// 사망이 확정된 직후 호출된다.
    /// 반드시 override해 사망 연출, 매니저 등록 해제 등 후처리를 구현한다.
    /// </summary>
    protected abstract void OnDeath(int killerIndex);
}
