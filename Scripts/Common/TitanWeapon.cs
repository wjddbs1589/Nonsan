using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;

/// <summary>
/// 타이탄의 주포. 발사 시 시각·사운드·플레이어 피격 연출을 처리한다.
///
/// Fire() 처리 순서:
///   1. 발사음 재생 + 총구에 머즐 플래시 생성
///   2. PlayerData.PlayerTakeDamageForced()로 플레이어 비네트 발동
///   3. Chromatic Aberration 강도를 1로 올린 뒤 chromaticFadeDuration 동안 0으로 감쇠
///
/// 포스트 프로세싱 값 수정:
///   Volume.sharedProfile은 프로젝트의 프로필 에셋 자체라서, 런타임에 값을 바꾸면 에셋 파일에 그대로 저장된다.
///   Volume.profile은 접근 시 sharedProfile의 런타임 사본을 만들어 반환하므로, 이 사본만 수정해 원본 에셋을 보호한다.
/// </summary>
public class TitanWeapon : MonoBehaviour
{
    [Header("총구 설정")]
    [Tooltip("머즐 플래시가 생성될 위치")]
    [SerializeField] private Transform muzzlePoint;

    [Tooltip("발사 시 생성할 머즐 플래시 프리팹")]
    [SerializeField] private GameObject muzzleFlashPrefab;

    [Tooltip("머즐 플래시 자동 제거 시간 (초)")]
    [SerializeField] private float muzzleFlashDuration = 0.2f;

    [Header("화면 연출")]
    [Tooltip("Chromatic Aberration이 1에서 0까지 감쇠하는 시간 (초)")]
    [SerializeField] private float chromaticFadeDuration = 0.3f;

    private ChromaticAberration _chromaticAberration;
    private Coroutine _chromaticCoroutine;

    /// <summary>Volume의 런타임 프로필에서 Chromatic Aberration 설정을 가져온다.</summary>
    private void Awake()
    {
        if (!TryGetComponent(out Volume vol))
        {
            Debug.LogWarning("[TitanWeapon] Volume 컴포넌트가 없습니다.", this);
            return;
        }

        if (vol.sharedProfile == null)
        {
            Debug.LogWarning("[TitanWeapon] Volume에 프로필이 연결되어 있지 않습니다.", this);
            return;
        }

        // profile은 런타임 사본을 반환하므로 원본 프로필 에셋은 변경되지 않는다
        vol.profile.TryGet(out _chromaticAberration);
    }

    /// <summary>TitanAttackController에서 호출하는 발사 메서드.</summary>
    public void Fire()
    {
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlaySFXAtPosition("Tank", transform.position);

        SpawnMuzzleFlash();
        PlayerData.PlayerTakeDamageForced();
        TriggerChromaticAberration();
    }

    /// <summary>총구 위치에 플레이어를 향하는 머즐 플래시를 생성한다.</summary>
    private void SpawnMuzzleFlash()
    {
        if (muzzleFlashPrefab == null || muzzlePoint == null) return;

        GameObject flash = Instantiate(muzzleFlashPrefab, muzzlePoint.position, Quaternion.identity);

        Camera cam = Camera.main;
        if (cam != null) flash.transform.LookAt(cam.transform);

        Destroy(flash, muzzleFlashDuration);
    }

    /// <summary>
    /// Chromatic Aberration 페이드를 시작한다.
    /// 진행 중인 페이드가 있으면 취소하고 다시 1부터 감쇠시킨다.
    /// </summary>
    private void TriggerChromaticAberration()
    {
        if (_chromaticAberration == null) return;
        if (_chromaticCoroutine != null) StopCoroutine(_chromaticCoroutine);
        _chromaticCoroutine = StartCoroutine(FadeChromaticAberration());
    }

    /// <summary>Chromatic Aberration 강도를 1로 올린 뒤 chromaticFadeDuration 동안 0까지 선형 감쇠시킨다.</summary>
    private IEnumerator FadeChromaticAberration()
    {
        _chromaticAberration.intensity.Override(1f);

        float elapsed = 0f;
        while (elapsed < chromaticFadeDuration)
        {
            elapsed += Time.deltaTime;
            _chromaticAberration.intensity.Override(1f - Mathf.Clamp01(elapsed / chromaticFadeDuration));
            yield return null;
        }

        _chromaticAberration.intensity.Override(0f);
        _chromaticCoroutine = null;
    }
}
