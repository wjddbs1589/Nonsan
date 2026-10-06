using UnityEngine;

/// <summary>
/// 보스 약점 부위에 붙는 UI 마커.
/// LateUpdate마다 대상 부위의 화면 좌표와 BoxCollider 크기를 계산해 마커 위치와 크기를 갱신한다.
/// 대상이 카메라 뒤에 있으면 숨긴다.
///
/// 최적화:
///   BoxCollider의 8개 꼭짓점을 Setup 시 로컬 좌표로 한 번만 계산해 캐싱하고,
///   매 프레임은 TransformPoint()와 화면 좌표 변환만 수행한다.
/// </summary>
public class TargetMarker : MonoBehaviour
{
    /// <summary>마커가 추적하는 대상 부위 Transform.</summary>
    public Transform TargetPart { get; private set; }

    private RectTransform _rectTransform;
    private BoxCollider _boxCollider;
    private bool _isVisible = false;

    // Setup 시 로컬 좌표로 한 번만 계산한 BoxCollider 8개 꼭짓점
    private readonly Vector3[] _localCorners = new Vector3[8];

    // 마커가 콜라이더보다 약간 크게 보이도록 주는 여백 (픽셀)
    private const float MARKER_PADDING = 10f;

    /// <summary>RectTransform을 캐싱한다.</summary>
    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
    }

    /// <summary>
    /// 풀에서 꺼낼 때 MarkerManager에서 호출한다.
    /// 대상 부위를 연결하고 BoxCollider 꼭짓점을 캐싱한다. 표시는 Show() 호출 전까지 보류된다.
    /// </summary>
    public void Setup(Transform target)
    {
        TargetPart = target;
        gameObject.SetActive(true);
        _isVisible = false;
        _rectTransform.localScale = Vector3.zero;

        if (TargetPart.TryGetComponent(out _boxCollider))
            CacheLocalCorners(_boxCollider);
    }

    /// <summary>BoxCollider의 로컬 공간 8개 꼭짓점을 계산해 저장한다. (center, size는 로컬 기준이라 한 번만 계산)</summary>
    private void CacheLocalCorners(BoxCollider box)
    {
        Vector3 c = box.center;
        Vector3 e = box.size * 0.5f;
        _localCorners[0] = c + new Vector3(-e.x, -e.y, -e.z);
        _localCorners[1] = c + new Vector3(e.x, -e.y, -e.z);
        _localCorners[2] = c + new Vector3(-e.x, e.y, -e.z);
        _localCorners[3] = c + new Vector3(e.x, e.y, -e.z);
        _localCorners[4] = c + new Vector3(-e.x, -e.y, e.z);
        _localCorners[5] = c + new Vector3(e.x, -e.y, e.z);
        _localCorners[6] = c + new Vector3(-e.x, e.y, e.z);
        _localCorners[7] = c + new Vector3(e.x, e.y, e.z);
    }

    /// <summary>마커를 화면에 표시한다. Hide() 또는 ReturnToPool() 호출 전까지 유지된다.</summary>
    public void Show() => _isVisible = true;

    /// <summary>마커를 즉시 숨긴다.</summary>
    public void Hide()
    {
        _isVisible = false;
        _rectTransform.localScale = Vector3.zero;
    }

    /// <summary>대상 부위의 화면 좌표를 따라 마커 위치와 크기를 갱신한다.</summary>
    private void LateUpdate()
    {
        if (TargetPart == null || !_isVisible) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 screenPos = cam.WorldToScreenPoint(TargetPart.position);

        // 카메라 뒤에 있으면 숨김
        if (screenPos.z < 0)
        {
            _rectTransform.localScale = Vector3.zero;
            return;
        }

        _rectTransform.localScale = Vector3.one;
        transform.position = screenPos;
        UpdateMarkerSize(cam);
    }

    /// <summary>
    /// 캐싱한 8개 꼭짓점을 화면 좌표로 변환해 감싸는 사각형 크기로 마커 크기를 갱신한다.
    /// TransformPoint()가 대상의 현재 회전과 스케일을 반영한다.
    /// </summary>
    private void UpdateMarkerSize(Camera cam)
    {
        if (_boxCollider == null) return;

        float screenMinX = float.MaxValue, screenMinY = float.MaxValue;
        float screenMaxX = float.MinValue, screenMaxY = float.MinValue;

        foreach (Vector3 localCorner in _localCorners)
        {
            Vector3 sp = cam.WorldToScreenPoint(TargetPart.TransformPoint(localCorner));
            if (sp.z < 0) continue; // 카메라 뒤 꼭짓점 제외

            screenMinX = Mathf.Min(screenMinX, sp.x);
            screenMinY = Mathf.Min(screenMinY, sp.y);
            screenMaxX = Mathf.Max(screenMaxX, sp.x);
            screenMaxY = Mathf.Max(screenMaxY, sp.y);
        }

        float width = screenMaxX - screenMinX;
        float height = screenMaxY - screenMinY;
        if (width <= 0 || height <= 0) return;

        _rectTransform.sizeDelta = new Vector2(width + MARKER_PADDING, height + MARKER_PADDING);
    }

    /// <summary>풀 반환 시 호출한다. 숨기고 참조를 초기화한 뒤 비활성화한다.</summary>
    public void ReturnToPool()
    {
        Hide();
        TargetPart = null;
        _boxCollider = null;
        gameObject.SetActive(false);
    }
}
