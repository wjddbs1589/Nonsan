using UnityEngine;

/// <summary>
/// 모든 보스 공격 컨트롤러의 추상 베이스 클래스.
/// PathTriggerDetector가 이 타입을 통해 StartAttack() / StopAttack()을 호출한다.
/// 구현체: HeliAttackController, TitanAttackController
/// </summary>
public abstract class BossAttackController : MonoBehaviour
{
    /// <summary>PathTriggerDetector 또는 외부에서 공격을 시작시킬 때 호출한다.</summary>
    public abstract void StartAttack();

    /// <summary>PathTriggerDetector 또는 외부에서 공격을 중지시킬 때 호출한다.</summary>
    public abstract void StopAttack();
}
