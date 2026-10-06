using UnityEngine;
using Valve.VR;

/// <summary>
/// TrackerInput의 RelativeRotation을 받아 rayCube의 로컬 회전에 적용한다.
/// TrackerInput 값은 SteamVR 포즈 이벤트에서 갱신되므로, LateUpdate에서 최신값을 rayCube에 반영한다.
///
/// 축 변환:
///   오일러 각으로 분해한 뒤 Yaw(y)와 Roll(z)을 교환하므로 Pitch가 ±90°에 가까우면 축 간섭이 생긴다.
///   rotationLimits로 실사용 조준 범위 안에서만 회전하도록 제한한다.
/// </summary>
public class Tracker : MonoBehaviour
{
    [Header("연결 대상")]
    [Tooltip("회전값을 적용할 대상 Transform. 보통 레이 방향을 결정하는 큐브 오브젝트.")]
    public Transform rayCube;

    [Header("회전 설정")]
    [Tooltip("각 축별 최대 허용 회전 범위 (도). 이 범위를 넘으면 클램핑된다.")]
    public Vector3 rotationLimits = new Vector3(90, 90, 90);

    [Tooltip("회전 감도 배율. 1이면 그대로, 1보다 크면 더 민감하게 반응한다.")]
    public float sensitivity = 1f;
    
    // 상대 회전값을 제공하는 같은 오브젝트의 TrackerInput
    private TrackerInput _trackerInput;

    /// <summary>같은 오브젝트의 TrackerInput을 캐싱한다.</summary>
    private void Awake()
    {
        _trackerInput = GetComponent<TrackerInput>();
    }

    /// <summary>트래커 포즈가 유효할 때 상대 회전값을 rayCube에 적용한다.</summary>
    private void LateUpdate()
    {
        if(rayCube == null || _trackerInput == null) return;
        
        if(_trackerInput.isValid)
        {
            ApplyRotation(_trackerInput.RelativeRotation);
        }
    }

    /// <summary>회전값을 축 교환 → 감도 → 클램핑 순서로 처리해 rayCube에 적용한다.</summary>
    private void ApplyRotation(Quaternion rot)
    {
        Vector3 euler = rot.eulerAngles;

        // 0~360 범위를 -180~180 범위로 변환한다
        if (euler.x > 180) euler.x -= 360;
        if (euler.y > 180) euler.y -= 360;
        if (euler.z > 180) euler.z -= 360;

        // 축 리매핑: 트래커 Roll(z) → 큐브 Yaw(y), 트래커 Yaw(y) → 큐브 Roll(z)
        Vector3 remapped = new Vector3(
            euler.x,
            euler.z,
            euler.y
        );

        // 감도 적용 후 축별 허용 범위로 제한한다
        remapped *= sensitivity;

        remapped.x = Mathf.Clamp(remapped.x, -rotationLimits.x, rotationLimits.x);
        remapped.y = Mathf.Clamp(remapped.y, -rotationLimits.y, rotationLimits.y);
        remapped.z = Mathf.Clamp(remapped.z, -rotationLimits.z, rotationLimits.z);

        rayCube.localRotation = Quaternion.Euler(remapped);
    }
}