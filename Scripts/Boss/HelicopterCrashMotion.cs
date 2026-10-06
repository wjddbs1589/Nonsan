using System.Collections;
using UnityEngine;

/// <summary>
/// 헬기 파괴 시 추락 연출을 공통 처리하는 유틸리티.
/// HelicopterLv3, OtherHelis의 추락 코루틴에서 사용한다.
/// </summary>
public static class HelicopterCrashMotion
{
    // 추락 방향에 섞을 무작위 편차 (0이면 수직 낙하)
    private const float DIRECTION_JITTER = 0.3f;

    // Z축 회전 속도 비율 (X축 대비)
    private const float ROLL_RATIO = 0.7f;

    /// <summary>
    /// 아래 방향으로 가속하며 회전 추락한다. 대상이 groundY 아래로 내려가면 종료한다.
    /// </summary>
    /// <param name="target">추락시킬 Transform</param>
    /// <param name="initialSpeed">추락 시작 속도</param>
    /// <param name="acceleration">초당 속도 증가량</param>
    /// <param name="rotateSpeed">회전 속도 (도/초)</param>
    /// <param name="groundY">연출을 끝낼 Y 좌표</param>
    public static IEnumerator Fall(Transform target, float initialSpeed, float acceleration, float rotateSpeed, float groundY)
    {
        Vector3 direction = (Vector3.down + Random.insideUnitSphere * DIRECTION_JITTER).normalized;
        float speed = initialSpeed;

        while (target.position.y > groundY)
        {
            float dt = Time.deltaTime;
            target.position += direction * speed * dt;
            target.Rotate(rotateSpeed * dt, 0f, rotateSpeed * ROLL_RATIO * dt, Space.Self);
            speed += acceleration * dt;
            yield return null;
        }
    }
}
