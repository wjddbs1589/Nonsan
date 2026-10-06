using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 차량형 보스의 약점(HitPoint)과 마커를 관리하는 기반 클래스.
/// 상속받아 Helicopter, Radar, Titan 등 개별 보스를 구현한다.
///
/// 동작 흐름:
///   InitMarkers()          → 약점별 마커 연결, VehicleWeakness 파괴 이벤트 구독, 콜라이더 비활성화
///   OnPathTriggerEntered() → 현재 순서의 약점 마커와 콜라이더 활성화
///   OnWeaknessDestroyed()  → 마커 반환 → 다음 약점 활성화 → 반복
///   모든 약점 파괴         → 최종 폭발 이펙트 생성 후 OnVehicleDestroyed() 호출
///
/// 이펙트 구조:
///   부위 파괴 이펙트는 각 VehicleWeakness에서, 최종 폭발 이펙트는 이 클래스에서 생성한다.
///   자식 클래스는 OnVehicleDestroyed()를 override해 보스별 연출(추락, 정지 등)만 구현한다.
/// </summary>
public abstract class MarkerBase : MonoBehaviour
{
    [Header("약점 설정")]
    [Tooltip("마커를 붙일 약점 부위 배열. 배열 순서가 파괴 순서가 된다.")]
    [SerializeField] protected Transform[] hitPoints;

    /// <summary>HeliAttackController 등 외부에서 약점 목록을 읽기 위해 공개한다.</summary>
    public Transform[] HitPoints => hitPoints;

    [Header("최종 폭발")]
    [Tooltip("마지막 약점이 파괴될 때 한 번 생성하는 최종 폭발 이펙트. 비워두면 생략한다.")]
    [SerializeField] private GameObject finalExplosionPrefab;

    [Tooltip("최종 폭발 이펙트 크기 배율")]
    [SerializeField] private float finalExplosionScale = 2f;

    [Tooltip("최종 폭발 이펙트 자동 제거 시간 (초)")]
    [SerializeField] private float finalExplosionLifetime = 10f;

    /// <summary>약점 Transform → 마커 조회용 딕셔너리</summary>
    protected Dictionary<Transform, TargetMarker> markerMap = new Dictionary<Transform, TargetMarker>();

    protected int destroyedPartsCount = 0;

    /// <summary>현재 공격 대상 약점의 배열 인덱스 (0부터 시작)</summary>
    protected int currentTargetIndex = 0;
    public int CurrentTargetIndex => currentTargetIndex;

    private bool _isInitialized = false;
    private bool _isVehicleDestroyedHandled = false;

    /// <summary>파괴된 약점 수가 전체 약점 수 이상이면 차량 완전 파괴 상태.</summary>
    public bool IsVehicleDestroyed => hitPoints != null && destroyedPartsCount >= hitPoints.Length;

    /// <summary>약점이 지정되어 있으면 마커를 초기화한다.</summary>
    protected virtual void Start()
    {
        if (hitPoints != null && hitPoints.Length > 0)
            InitMarkers();
    }

    /// <summary>
    /// MarkerManager 풀에서 마커를 받아 각 약점에 연결하고, VehicleWeakness 파괴 이벤트를 구독한다.
    /// 순서가 오기 전에는 피격되지 않도록 모든 약점 콜라이더를 비활성화한다.
    /// </summary>
    public void InitMarkers()
    {
        if (_isInitialized) return;
        _isInitialized = true;

        List<TargetMarker> markers = null;
        if (MarkerManager.Instance != null)
            markers = MarkerManager.Instance.GetMarkers(hitPoints);
        else
            Debug.LogWarning("[MarkerBase] MarkerManager가 없어 마커 없이 동작합니다.", this);

        for (int i = 0; i < hitPoints.Length; i++)
        {
            Transform part = hitPoints[i];

            if (markers != null && i < markers.Count)
            {
                markerMap.Add(part, markers[i]);
                markers[i].Hide();
            }

            if (part.TryGetComponent(out VehicleWeakness weakness))
            {
                weakness.partsDestroyEvent += OnWeaknessDestroyed;
                if (part.TryGetComponent(out Collider col)) col.enabled = false;
            }
        }
    }

    /// <summary>
    /// 스플라인 경로의 트리거를 통과했을 때 PathTriggerDetector에서 호출한다.
    /// 현재 순서의 약점 마커와 콜라이더를 활성화한다.
    /// </summary>
    public void OnPathTriggerEntered()
    {
        if (IsVehicleDestroyed) return;
        ShowCurrentMarker();
    }

    /// <summary>현재 순서의 약점 마커와 콜라이더를 활성화한다.</summary>
    private void ShowCurrentMarker()
    {
        if (currentTargetIndex >= hitPoints.Length) return;

        Transform currentTarget = hitPoints[currentTargetIndex];
        if (currentTarget.TryGetComponent(out Collider col)) col.enabled = true;

        if (markerMap.TryGetValue(currentTarget, out TargetMarker marker))
            marker.Show();
    }

    /// <summary>
    /// VehicleWeakness.partsDestroyEvent 구독 핸들러.
    /// 파괴된 약점의 마커를 반환하고 다음 약점을 활성화한다.
    /// 모든 약점이 파괴되면 최종 폭발을 생성하고 OnVehicleDestroyed()를 호출한다.
    /// </summary>
    protected virtual void OnWeaknessDestroyed(Transform brokenPart)
    {
        destroyedPartsCount++;

        if (SoundManager.Instance != null)
            SoundManager.Instance.PlaySFXAtPosition("EnemyShot", transform.position);

        // 파괴된 약점의 마커를 풀에 반환한다
        DestroyMarker(brokenPart);

        // 모든 약점이 파괴되면 최종 폭발 후 보스별 파괴 연출을 실행한다
        if (IsVehicleDestroyed)
        {
            if (_isVehicleDestroyedHandled) return;
            _isVehicleDestroyedHandled = true;

            SpawnFinalExplosion(brokenPart);
            OnVehicleDestroyed();
            return;
        }

        // 다음 약점의 마커와 콜라이더를 활성화한다
        currentTargetIndex++;
        ShowCurrentMarker();
    }

    /// <summary>
    /// 차량 중심 위치에 최종 폭발 이펙트를 생성한다.
    /// 마지막 약점의 자식으로 붙여 차량과 함께 움직이도록 하고, finalExplosionLifetime 후 제거한다.
    /// </summary>
    private void SpawnFinalExplosion(Transform parent)
    {
        if (finalExplosionPrefab == null) return;

        GameObject fx = Instantiate(finalExplosionPrefab, transform.position, Quaternion.identity, parent);
        fx.transform.localScale *= finalExplosionScale;
        Destroy(fx, finalExplosionLifetime);
    }

    /// <summary>
    /// 모든 약점이 파괴됐을 때 호출된다. 기본 구현은 남은 마커 정리만 수행한다.
    /// 자식 클래스에서 override해 추락, 정지 등 보스별 연출을 추가한다.
    /// </summary>
    protected virtual void OnVehicleDestroyed()
    {
        ClearAllMarkers();
    }

    /// <summary>특정 약점의 마커를 풀에 반환하고 딕셔너리에서 제거한다.</summary>
    public void DestroyMarker(Transform brokenPart)
    {
        if (!markerMap.TryGetValue(brokenPart, out TargetMarker marker)) return;

        markerMap.Remove(brokenPart);
        if (MarkerManager.Instance != null)
            MarkerManager.Instance.ReturnSingleMarker(marker);
    }

    /// <summary>남아있는 모든 마커를 풀에 반환한다. 차량 완전 파괴 또는 비활성화 시 호출된다.</summary>
    public void ClearAllMarkers()
    {
        // 씬 종료 시 MarkerManager가 먼저 파괴될 수 있으므로 확인 후 반환한다
        if (MarkerManager.Instance != null)
        {
            foreach (var marker in markerMap.Values)
                MarkerManager.Instance.ReturnSingleMarker(marker);
        }
        markerMap.Clear();
    }

    /// <summary>비활성화 시 마커를 모두 반환해 풀 누수를 방지한다.</summary>
    protected virtual void OnDisable()
    {
        ClearAllMarkers();
    }
}
