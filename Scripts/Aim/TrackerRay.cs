using UnityEngine;

/// <summary>
/// rayCube의 전방 방향으로 레이를 쏴서 AimBoard(BoxCollider)의 UV 좌표를 구하고,
/// Canvas 크기에 매핑해 에임 UI 위치를 갱신한다.
///
/// AimBoard의 UV(-0.5~0.5)를 Canvas 픽셀 좌표로 변환하므로
/// 카메라 WorldToViewport 변환 없이 화면 해상도에 무관하게 동작한다.
/// </summary>
public class TrackerRay : MonoBehaviour
{
    [Header("레이어 설정")]
    [Tooltip("레이가 충돌을 감지할 AimBoard 레이어 마스크")]
    public LayerMask aimPlaneLayer;

    [Header("UI 설정")]
    [Tooltip("위치를 갱신할 에임 RectTransform")]
    public RectTransform aim;

    [Header("레이 설정")]
    [Tooltip("레이 최대 거리 (m)")]
    public float maxDistance = 100f;

    [Tooltip("에임 이동 보간 속도. 높을수록 즉각적으로 따라온다.")]
    public float aimLerpSpeed = 20f;

    // 에임의 부모 Canvas (UV를 Canvas 좌표로 변환할 때 크기 기준)
    private RectTransform _canvasRect;

    /// <summary>에임의 부모 Canvas를 캐싱한다.</summary>
    private void Start()
    {
        _canvasRect = aim.parent as RectTransform;
    }

    /// <summary>트래커 회전이 반영된 뒤 에임 위치를 갱신한다.</summary>
    private void LateUpdate()
    {
        UpdateAimUI();
    }

    /// <summary>rayCube 전방으로 레이를 쏴 AimBoard에 맞으면 에임 위치를 갱신하고, 벗어나면 에임을 숨긴다.</summary>
    private void UpdateAimUI()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        bool aimActive = false;

        if (Physics.Raycast(ray, out RaycastHit planeHit, maxDistance, aimPlaneLayer))
        {
            BoxCollider hitCol = planeHit.collider as BoxCollider;
            if (hitCol != null)
            {
                // AimBoard 로컬 좌표로 변환 후 UV 정규화 (-0.5 ~ 0.5)
                Vector3 localHit = planeHit.transform.InverseTransformPoint(planeHit.point);
                float u = localHit.x / hitCol.size.x;
                float v = localHit.z / hitCol.size.z;

                // Canvas 크기에 직접 매핑 (카메라 변환 불필요)
                Vector2 anchoredPos = new Vector2(u * _canvasRect.rect.width, v * _canvasRect.rect.height);
                aim.anchoredPosition = Vector2.Lerp(aim.anchoredPosition, anchoredPos, Time.deltaTime * aimLerpSpeed);
                aimActive = true;
            }
        }

        // AimBoard 밖을 향하면 에임 UI를 숨긴다
        if (aim != null)
            aim.gameObject.SetActive(aimActive);
    }
}
