using UnityEngine;

/// <summary>
/// TrackerAimBoard의 BoxCollider 크기를 카메라 FOV와 Canvas 해상도 비율에 맞게 자동 계산한다.
/// 보드 크기는 PLANE_DISTANCE 거리에서의 시야 크기로 계산되므로, 보드가 카메라 시야를 정확히 채운다.
///
/// 동작 원리:
///   FOV → PLANE_DISTANCE 거리에서의 실제 월드 높이 계산 → Canvas 비율로 너비 계산 → BoxCollider 크기 설정
/// 이렇게 하면 TrackerRay가 AimBoard에 충돌할 때의 UV 좌표가 Canvas 해상도와 정확히 일치한다.
/// </summary>
public class AimBoardFitter : MonoBehaviour
{
    [Header("연결 대상")]
    [Tooltip("AimCanvas의 RectTransform. 해상도 비율(width/height) 계산에 사용한다.")]
    [SerializeField] private RectTransform canvasRect;

    private Camera _mainCamera;
    private BoxCollider _aimBoardCollider;

    // 카메라로부터 보드까지의 거리 (보드 크기는 이 거리 기준으로 계산되므로 Canvas 거리와 같을 필요는 없다)
    private const float PLANE_DISTANCE = 50f;

    /// <summary>보드 콜라이더를 캐싱한다.</summary>
    private void Awake()
    {
        _aimBoardCollider = GetComponent<BoxCollider>();
    }

    /// <summary>활성화될 때마다 카메라를 다시 찾아 보드 크기를 맞춘다.</summary>
    private void OnEnable()
    {
        // 씬 재활성화 시 카메라 참조를 다시 확인한다
        _mainCamera = null;
        Fit();
    }

    /// <summary>시작 시 보드 크기를 한 번 더 맞춘다. (OnEnable 시점에는 Canvas 크기가 확정되지 않았을 수 있음)</summary>
    private void Start()
    {
        Fit();
    }

    /// <summary>보드 위치와 콜라이더 크기를 현재 카메라 FOV와 Canvas 비율에 맞춘다.</summary>
    private void Fit()
    {
        if (_mainCamera == null) _mainCamera = Camera.main;
        if (_mainCamera == null || _aimBoardCollider == null || canvasRect == null) return;

        // Z 위치를 보드 크기 계산에 사용한 거리와 일치시킨다
        Vector3 pos = transform.localPosition;
        transform.localPosition = new Vector3(pos.x, pos.y, PLANE_DISTANCE);

        // FOV 기반 실제 월드 높이: 거리 D에서의 시야 높이 = 2 * D * tan(halfFOV)
        float halfFOV = _mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float worldHeight = 2f * PLANE_DISTANCE * Mathf.Tan(halfFOV);

        // Canvas 비율(3840:1080 등)로 너비를 계산한다
        float worldWidth = worldHeight * (canvasRect.rect.width / canvasRect.rect.height);

        // Size.x = 가로, Size.y = 두께(얇게), Size.z = 세로로 설정한다
        _aimBoardCollider.size = new Vector3(worldWidth, 0.01f, worldHeight);
        transform.localScale = Vector3.one;
    }

#if UNITY_EDITOR
    /// <summary>에디터에서 선택 시 보드 범위와 크기를 기즈모로 표시한다.</summary>
    private void OnDrawGizmosSelected()
    {
        if (_mainCamera == null || canvasRect == null) return;

        float halfFOV = _mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float worldHeight = 2f * PLANE_DISTANCE * Mathf.Tan(halfFOV);
        float worldWidth = worldHeight * (canvasRect.rect.width / canvasRect.rect.height);

        Gizmos.color = Color.cyan;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(worldWidth, 0.01f, worldHeight));

        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 0.5f,
            $"AimBoard: {worldWidth:F2} x {worldHeight:F2}  FOV: {_mainCamera.fieldOfView}°");
    }
#endif
}
