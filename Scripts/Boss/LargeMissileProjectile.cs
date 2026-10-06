using UnityEngine;

/// <summary>
/// 대형 미사일 비행체.
/// 비행 로직은 소형 미사일과 같고, 인스펙터에서 vignetteTriggerDistance와 cameraProximityTrigger를
/// 소형보다 크게 설정해 더 넓은 범위에서 폭발하도록 운용한다.
/// </summary>
public class LargeMissileProjectile : MissileProjectileBase
{
    /// <summary>폭발 조건 확인 → 약한 호밍 → 등속 비행 순서로 처리한다.</summary>
    private void FixedUpdate()
    {
        if (!isLaunched || hasExploded) return;

        // 카메라 근접 또는 수명 초과 시 폭발하고 비행을 멈춘다
        if (CheckExplosionConditions()) return;

        // 카메라 방향으로 비행 방향을 살짝 회전시킨다 (약한 호밍)
        UpdateTrackingDirection();

        // 갱신된 방향으로 등속 비행한다
        rb.linearVelocity = launchDirection * speed;
    }
}