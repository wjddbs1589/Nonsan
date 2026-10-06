using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 헬기 보스의 공격 타이밍과 무기 선택을 관리하는 컨트롤러.
/// MarkerBase를 가진 게임오브젝트에 부착한다.
///
/// 무기 매핑 방식:
///   각 무기에 대응하는 VehicleWeakness 부위를 인스펙터에서 연결한다.
///   - 약점이 연결된 무기   → 해당 부위 파괴 시 즉시 사격을 멈추고 비활성화
///   - 약점이 비어 있는 무기 → 파괴 불가능한 무기로 간주해 끝까지 동작
///   - 무기가 비어 있는 슬롯 → 처음부터 사용 불가
///
/// 약점 개수나 순서와 무관하게 동작하므로 일반 헬기, Lv3 헬기, 서브 헬기(OtherHelis)에서 재사용한다.
/// </summary>
public class HeliAttackController : BossAttackController
{
    private enum WeaponSlot
    {
        Minigun,
        SmallMissileLeft,
        SmallMissileRight,
        LargeMissileLeft,
        LargeMissileRight
    }

    private static readonly WeaponSlot[] MissileSlots =
    {
        WeaponSlot.SmallMissileLeft,
        WeaponSlot.SmallMissileRight,
        WeaponSlot.LargeMissileLeft,
        WeaponSlot.LargeMissileRight
    };

    // 사용할 수 없는 미사일 슬롯이 이 수 이상이면 한 번에 1발만 발사한다
    private const int REDUCED_ATTACK_THRESHOLD = 3;
    private const int MAX_SIMULTANEOUS_MISSILES = 2;

    [Header("미니건 공격 주기 (초)")]
    [Tooltip("미니건 점사 간격. 미사일 주기와 무관하게 독립적으로 동작한다.")]
    [SerializeField] private float minigunIntervalMin = 1.5f;
    [SerializeField] private float minigunIntervalMax = 3f;

    [Header("미사일 공격 주기 (초)")]
    [Tooltip("미사일 공격 간격. 미니건과 독립적으로 동작한다.")]
    [SerializeField] private float missileIntervalMin = 2f;
    [SerializeField] private float missileIntervalMax = 4f;

    [Header("미니건 (무기 + 약점 부위)")]
    [Tooltip("미니건 무기. 비워두면 이 슬롯은 사용하지 않는다.")]
    [SerializeField] private HelicopterMinigun minigun;
    [Tooltip("이 무기를 비활성화시킬 약점 부위. 비워두면 파괴 불가능한 무기로 간주한다.")]
    [SerializeField] private VehicleWeakness minigunWeakness;

    [Header("소형 미사일 좌")]
    [Tooltip("소형 미사일 발사대 (좌). 비워두면 이 슬롯은 사용하지 않는다.")]
    [SerializeField] private HelicopterSmallMissile smallMissileLeft;
    [Tooltip("이 무기를 비활성화시킬 약점 부위. 비워두면 파괴 불가능한 무기로 간주한다.")]
    [SerializeField] private VehicleWeakness smallMissileLeftWeakness;

    [Header("소형 미사일 우")]
    [Tooltip("소형 미사일 발사대 (우). 비워두면 이 슬롯은 사용하지 않는다.")]
    [SerializeField] private HelicopterSmallMissile smallMissileRight;
    [Tooltip("이 무기를 비활성화시킬 약점 부위. 비워두면 파괴 불가능한 무기로 간주한다.")]
    [SerializeField] private VehicleWeakness smallMissileRightWeakness;

    [Header("대형 미사일 좌")]
    [Tooltip("대형 미사일 발사대 (좌). 비워두면 이 슬롯은 사용하지 않는다.")]
    [SerializeField] private HelicopterLargeMissile largeMissileLeft;
    [Tooltip("이 무기를 비활성화시킬 약점 부위. 비워두면 파괴 불가능한 무기로 간주한다.")]
    [SerializeField] private VehicleWeakness largeMissileLeftWeakness;

    [Header("대형 미사일 우")]
    [Tooltip("대형 미사일 발사대 (우). 비워두면 이 슬롯은 사용하지 않는다.")]
    [SerializeField] private HelicopterLargeMissile largeMissileRight;
    [Tooltip("이 무기를 비활성화시킬 약점 부위. 비워두면 파괴 불가능한 무기로 간주한다.")]
    [SerializeField] private VehicleWeakness largeMissileRightWeakness;

    private MarkerBase _helicopter;

    // 슬롯별 사용 가능 여부
    private readonly Dictionary<WeaponSlot, bool> _weaponAlive = new Dictionary<WeaponSlot, bool>();

    // 발사 가능한 미사일 슬롯 목록 (공격마다 새로 할당하지 않도록 재사용)
    private readonly List<WeaponSlot> _availableMissileSlots = new List<WeaponSlot>(4);

    private bool _isAttacking = false;
    private bool _isInitialized = false;

    /// <summary>
    /// SetActive(true) 직후 StartAttack()이 호출되는 경우(OtherHelis.Launch 등)에도
    /// 무기 목록이 준비되어 있도록 Start가 아닌 Awake에서 초기화한다.
    /// </summary>
    private void Awake()
    {
        Initialize();
    }

    /// <summary>MarkerBase 참조를 가져오고 무기 슬롯 5개를 등록한다. 한 번만 실행된다.</summary>
    private void Initialize()
    {
        if (_isInitialized) return;
        _isInitialized = true;

        _helicopter = GetComponent<MarkerBase>();
        if (_helicopter == null)
            Debug.LogError("[HeliAttackController] MarkerBase 컴포넌트를 찾을 수 없습니다.", this);

        RegisterWeapon(WeaponSlot.Minigun, minigun, minigunWeakness);
        RegisterWeapon(WeaponSlot.SmallMissileLeft, smallMissileLeft, smallMissileLeftWeakness);
        RegisterWeapon(WeaponSlot.SmallMissileRight, smallMissileRight, smallMissileRightWeakness);
        RegisterWeapon(WeaponSlot.LargeMissileLeft, largeMissileLeft, largeMissileLeftWeakness);
        RegisterWeapon(WeaponSlot.LargeMissileRight, largeMissileRight, largeMissileRightWeakness);
    }

    /// <summary>
    /// 무기 슬롯 하나를 등록한다.
    /// 무기가 없으면 사용 불가로 등록하고, 약점이 있으면 파괴 시 해당 무기를 멈추고 비활성화한다.
    /// </summary>
    private void RegisterWeapon(WeaponSlot slot, Object weaponRef, VehicleWeakness weaknessRef)
    {
        if (weaponRef == null)
        {
            _weaponAlive[slot] = false;
            return;
        }

        _weaponAlive[slot] = true;

        if (weaknessRef != null)
        {
            // 약점 부위가 파괴되면 해당 무기를 사용 불가로 표시하고 진행 중인 사격을 멈춘다
            weaknessRef.partsDestroyEvent += _ =>
            {
                _weaponAlive[slot] = false;
                StopWeapon(slot);
            };
        }
    }

    /// <summary>공격 루프를 시작한다. 이미 공격 중이거나 본체가 파괴된 상태면 무시한다.</summary>
    public override void StartAttack()
    {
        if (_isAttacking || _helicopter == null || _helicopter.IsVehicleDestroyed) return;
        _isAttacking = true;
        StartCoroutine(MinigunLoop());
        StartCoroutine(MissileLoop());
    }

    /// <summary>공격 루프와 함께 진행 중인 점사·일제사격도 즉시 중단한다.</summary>
    public override void StopAttack()
    {
        StopAllCoroutines();
        _isAttacking = false;

        StopWeapon(WeaponSlot.Minigun);
        StopWeapon(WeaponSlot.SmallMissileLeft);
        StopWeapon(WeaponSlot.SmallMissileRight);
    }

    /// <summary>미니건 루프. 미사일과 독립적으로 동작하며, 미니건이 비활성화되거나 본체가 파괴되면 종료한다.</summary>
    private IEnumerator MinigunLoop()
    {
        while (!_helicopter.IsVehicleDestroyed && IsAlive(WeaponSlot.Minigun))
        {
            yield return new WaitForSeconds(Random.Range(minigunIntervalMin, minigunIntervalMax));
            if (!_helicopter.IsVehicleDestroyed && IsAlive(WeaponSlot.Minigun))
                minigun.Fire();
        }
    }

    /// <summary>미사일 루프. 발사 가능한 발사대 중 무작위로 골라 발사한다.</summary>
    private IEnumerator MissileLoop()
    {
        while (!_helicopter.IsVehicleDestroyed)
        {
            yield return new WaitForSeconds(Random.Range(missileIntervalMin, missileIntervalMax));
            if (!_helicopter.IsVehicleDestroyed)
                PerformMissileAttack();
        }
    }

    /// <summary>
    /// 발사 가능한 미사일 발사대 중 무작위로 골라 발사한다.
    /// 사용할 수 없는 슬롯이 REDUCED_ATTACK_THRESHOLD개 이상이면 1발, 그 미만이면 최대 2발 동시 발사한다.
    /// 처음부터 장착되지 않은 슬롯도 사용 불가로 계산하므로, 발사대가 적은 헬기는 처음부터 공격량이 적다.
    /// </summary>
    private void PerformMissileAttack()
    {
        CollectAvailableMissileSlots(_availableMissileSlots);
        if (_availableMissileSlots.Count == 0) return;

        int attackCount = GetUnavailableMissileSlotCount() >= REDUCED_ATTACK_THRESHOLD
            ? 1
            : Mathf.Min(MAX_SIMULTANEOUS_MISSILES, _availableMissileSlots.Count);

        Shuffle(_availableMissileSlots);
        for (int i = 0; i < attackCount; i++)
            FireMissile(_availableMissileSlots[i]);
    }

    /// <summary>사용 가능하고 잔탄이 있는 미사일 슬롯을 result에 채운다.</summary>
    private void CollectAvailableMissileSlots(List<WeaponSlot> result)
    {
        result.Clear();
        foreach (WeaponSlot slot in MissileSlots)
        {
            if (IsAlive(slot) && HasMissileAmmo(slot))
                result.Add(slot);
        }
    }

    /// <summary>미사일 슬롯 4개 중 사용할 수 없는 슬롯 수 (파괴 + 미장착).</summary>
    private int GetUnavailableMissileSlotCount()
    {
        int count = 0;
        foreach (WeaponSlot slot in MissileSlots)
        {
            if (!IsAlive(slot)) count++;
        }
        return count;
    }

    /// <summary>슬롯에 연결된 미사일 발사대에 잔탄이 있는지 확인한다.</summary>
    private bool HasMissileAmmo(WeaponSlot slot)
    {
        switch (slot)
        {
            case WeaponSlot.SmallMissileLeft: return smallMissileLeft != null && smallMissileLeft.HasAmmo;
            case WeaponSlot.SmallMissileRight: return smallMissileRight != null && smallMissileRight.HasAmmo;
            case WeaponSlot.LargeMissileLeft: return largeMissileLeft != null && largeMissileLeft.HasAmmo;
            case WeaponSlot.LargeMissileRight: return largeMissileRight != null && largeMissileRight.HasAmmo;
            default: return false;
        }
    }

    /// <summary>슬롯의 미사일을 발사한다. 발사 가능 여부는 CollectAvailableMissileSlots에서 확인된 상태다.</summary>
    private void FireMissile(WeaponSlot slot)
    {
        switch (slot)
        {
            case WeaponSlot.SmallMissileLeft: smallMissileLeft.Fire(); break;
            case WeaponSlot.SmallMissileRight: smallMissileRight.Fire(); break;
            case WeaponSlot.LargeMissileLeft: largeMissileLeft.Fire(); break;
            case WeaponSlot.LargeMissileRight: largeMissileRight.Fire(); break;
        }
    }

    /// <summary>진행 중인 점사·일제사격을 중단한다. 대형 미사일은 단발이라 중단할 진행 상태가 없다.</summary>
    private void StopWeapon(WeaponSlot slot)
    {
        switch (slot)
        {
            case WeaponSlot.Minigun:
                if (minigun != null) minigun.Stop();
                break;
            case WeaponSlot.SmallMissileLeft:
                if (smallMissileLeft != null) smallMissileLeft.Stop();
                break;
            case WeaponSlot.SmallMissileRight:
                if (smallMissileRight != null) smallMissileRight.Stop();
                break;
        }
    }

    /// <summary>슬롯이 사용 가능한 상태인지 확인한다.</summary>
    private bool IsAlive(WeaponSlot slot)
    {
        return _weaponAlive.TryGetValue(slot, out bool alive) && alive;
    }

    /// <summary>Fisher-Yates 알고리즘으로 리스트를 무작위 순서로 섞는다.</summary>
    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
