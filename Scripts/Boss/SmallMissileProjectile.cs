using UnityEngine;

/// <summary>
/// 소형 미사일 비행체.
/// 발사, 호밍, 폭발 판정, 폭발 처리는 MissileProjectileBase에서 상속받는다.
///
/// 비행 로직:
///   매 FixedUpdate마다 폭발 조건을 확인하고, 발사 방향을 카메라 쪽으로 살짝 회전시킨 뒤 등속 비행한다.
/// </summary>
public class SmallMissileProjectile : MissileProjectileBase
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