using UnityEngine;

/// <summary>
/// BaseEnemy를 상속하고 IEnemy를 구현하는 전투형 적의 기반 클래스.
/// EnemyManager 등록·해제, 메인 카메라 참조, 플레이어 피격 알림 등
/// Enemy, CoverEnemy, DroneEnemy, VehicleEnemy의 공통 동작을 담당한다.
/// </summary>
public abstract class CombatEnemy : BaseEnemy, IEnemy
{
    /// <summary>조준 방향 계산과 사거리 판정에 사용하는 메인 카메라</summary>
    protected Camera mainCam;

    /// <summary>StartEnemy 중복 호출 방지 플래그</summary>
    protected bool isStarted = false;

    /// <summary>
    /// 체력을 초기화하고 EnemyManager에 등록한다.
    /// 자식 클래스는 OnStartEnemy()를 override해 추가 초기화를 구현한다.
    /// </summary>
    public virtual void StartEnemy()
    {
        if (isStarted || IsDead) return;
        isStarted = true;

        InitHealth();
        mainCam = Camera.main;

        if (EnemyManager.Instance != null)
            EnemyManager.Instance.Register(this);
        else
            Debug.LogWarning("[CombatEnemy] EnemyManager가 없어 틱 갱신에 등록되지 않습니다.", this);

        OnStartEnemy();
    }

    /// <summary>
    /// StartEnemy 완료 후 호출된다. 애니메이터 참조, 이동 경로 설정 등 클래스별 초기화를 작성한다.
    /// </summary>
    protected virtual void OnStartEnemy() { }

    /// <summary>적의 동작을 멈춘다. 기본 구현은 비어 있으며, 정지 처리가 필요한 자식 클래스에서 override한다.</summary>
    public virtual void StopEnemy() { }

    /// <summary>공격 동작을 수행한다. 이펙트 재생, 사운드 출력 등을 구현한다.</summary>
    public abstract void PlayAttack();

    /// <summary>EnemyManager의 틱마다 호출된다. 이동, 전투 등 상태별 동작을 처리한다.</summary>
    public abstract void OnTick(float dt);

    /// <summary>사망 시 EnemyManager에서 해제하고 콜라이더를 비활성화한 뒤 OnDeathEffect를 호출한다.</summary>
    protected override void OnDeath(int killerIndex)
    {
        if (EnemyManager.Instance != null)
            EnemyManager.Instance.Unregister(this);

        if (TryGetComponent(out Collider col)) col.enabled = false;

        OnDeathEffect(killerIndex);
    }

    /// <summary>사망 연출을 구현한다. 애니메이션, 이펙트, 물리 전환 등 클래스별 처리를 작성한다.</summary>
    protected abstract void OnDeathEffect(int killerIndex);

    /// <summary>
    /// 플레이어(카메라)가 사거리 안에 있으면 플레이어 피격을 발생시킨다.
    /// PlayAttack() 안에서 호출한다.
    /// </summary>
    protected void NotifyPlayerHit(float traceDistance)
    {
        if (mainCam == null) return;
        if (Vector3.Distance(transform.position, mainCam.transform.position) < traceDistance)
            PlayerData.PlayerTakeDamage();
    }
}
