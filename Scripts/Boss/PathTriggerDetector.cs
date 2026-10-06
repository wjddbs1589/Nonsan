using UnityEngine;

/// <summary>
/// 스플라인 경로 위에 배치하는 전투 구역 트리거.
/// 플레이어(카메라 카트)가 통과하면 지정한 보스들의 전투를 시작하거나 종료한다.
///
/// activateAttack = true  (진입) → 마커 활성화 + StartAttack()
/// activateAttack = false (이탈) → StopAttack() + 남은 마커 정리
/// </summary>
public class PathTriggerDetector : MonoBehaviour
{
    [Tooltip("진입 시 마커를 활성화하고, 이탈 시 마커를 정리할 보스 목록")]
    public MarkerBase[] markerBases;

    [Tooltip("진입 시 StartAttack(), 이탈 시 StopAttack()을 호출할 보스 공격 컨트롤러 목록")]
    public BossAttackController[] bossControllers;

    [Tooltip("트리거를 발동시킬 플레이어 레이어")]
    [SerializeField] private LayerMask playerLayerMask;

    [Tooltip("true면 전투 구역 진입, false면 전투 구역 이탈")]
    [SerializeField] private bool activateAttack = true;

    /// <summary>플레이어가 통과하면 설정에 따라 전투를 시작하거나 종료한다.</summary>
    private void OnTriggerEnter(Collider other)
    {
        // 플레이어 레이어가 아니면 무시한다
        if ((playerLayerMask.value & (1 << other.gameObject.layer)) == 0) return;

        if (activateAttack) EnterCombat();
        else ExitCombat();
    }

    /// <summary>마커를 활성화하고 공격을 시작한다.</summary>
    private void EnterCombat()
    {
        if (markerBases != null)
        {
            foreach (var markerBase in markerBases)
            {
                if (markerBase != null) markerBase.OnPathTriggerEntered();
            }
        }

        if (bossControllers != null)
        {
            foreach (var boss in bossControllers)
            {
                if (boss != null) boss.StartAttack();
            }
        }
    }

    /// <summary>공격을 중지하고 남은 마커를 정리한다.</summary>
    private void ExitCombat()
    {
        if (bossControllers != null)
        {
            foreach (var boss in bossControllers)
            {
                if (boss != null) boss.StopAttack();
            }
        }

        if (markerBases != null)
        {
            foreach (var markerBase in markerBases)
            {
                if (markerBase != null) markerBase.ClearAllMarkers();
            }
        }
    }
}
