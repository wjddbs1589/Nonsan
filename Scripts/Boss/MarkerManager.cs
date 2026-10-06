using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 약점 마커 UI 오브젝트 풀링 매니저.
/// 씬 시작 시 poolCount만큼 마커를 미리 생성해 두고, MarkerBase의 요청에 따라 대여하고 반환받는다.
/// 풀이 부족하면 경고를 남기고 추가 생성한다.
/// </summary>
public class MarkerManager : MonoBehaviour
{
    public static MarkerManager Instance { get; private set; }

    [Header("마커 풀 설정")]
    [Tooltip("마커 UI 프리팹 (TargetMarker 포함)")]
    [SerializeField] private GameObject markerPrefab;

    [Tooltip("마커가 생성될 Canvas Transform")]
    [SerializeField] private Transform canvasTransform;

    [Tooltip("미리 생성해 둘 마커 수. 씬에 동시에 등장하는 최대 약점 수 이상으로 설정한다.")]
    [SerializeField] private int poolCount = 100;

    private readonly Queue<TargetMarker> _poolingQueue = new Queue<TargetMarker>();
    private int _createdCount = 0;

    /// <summary>싱글톤을 등록하고 마커 풀을 생성한다.</summary>
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        InitializePool();
    }

    /// <summary>poolCount만큼 마커를 미리 생성해 비활성 상태로 큐에 넣는다.</summary>
    private void InitializePool()
    {
        for (int i = 0; i < poolCount; i++)
            _poolingQueue.Enqueue(CreateMarker());
    }

    /// <summary>마커를 하나 생성해 비활성 상태로 반환한다.</summary>
    private TargetMarker CreateMarker()
    {
        TargetMarker marker = Instantiate(markerPrefab, canvasTransform).GetComponent<TargetMarker>();
        marker.gameObject.SetActive(false);
        _createdCount++;
        return marker;
    }

    /// <summary>
    /// 약점 배열 크기만큼 마커를 대여해 각 부위에 연결(Setup)하고 반환한다.
    /// 풀이 부족하면 경고를 남기고 추가 생성한다.
    /// </summary>
    public List<TargetMarker> GetMarkers(Transform[] parts)
    {
        var borrowed = new List<TargetMarker>(parts.Length);

        foreach (var part in parts)
        {
            TargetMarker marker;
            if (_poolingQueue.Count > 0)
            {
                marker = _poolingQueue.Dequeue();
            }
            else
            {
                Debug.LogWarning($"[MarkerManager] 마커 풀이 부족해 추가 생성합니다. (현재 {_createdCount}개) poolCount를 늘리세요.");
                marker = CreateMarker();
            }

            marker.Setup(part);
            borrowed.Add(marker);
        }

        return borrowed;
    }

    /// <summary>마커 한 개를 풀에 반환한다.</summary>
    public void ReturnSingleMarker(TargetMarker marker)
    {
        if (marker == null) return;
        marker.ReturnToPool();
        _poolingQueue.Enqueue(marker);
    }
}
