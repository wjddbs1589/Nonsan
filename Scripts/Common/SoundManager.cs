using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 전체의 사운드를 관리하는 싱글톤 매니저.
/// - BGM : 페이드 아웃 → 페이드 인으로 전환한다.
/// - SFX : AudioSource 오브젝트 풀링. 유휴 소스를 꺼내 재생하고, 재생이 끝나면 풀로 반환한다.
///
/// 재생 종료 감시:
///   Play() 직후에는 isPlaying이 아직 true로 반영되지 않을 수 있어, 바로 감시하면
///   재생 중인 소스가 풀로 조기 반환되어 다른 효과음에 재사용(소리 끊김)된다.
///   그래서 한 프레임 기다린 뒤 재생 종료를 감시한다.
/// </summary>
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("BGM")]
    [Range(0f, 1f)] public float bgmVolume = 1.0f;

    [Tooltip("BGM 페이드 인/아웃 시간 (초)")]
    public float bgmFadeDuration = 1.0f;

    [Tooltip("시작 시 재생할 BGM 이름")]
    [SerializeField] private string startBGM = "BGM_001";

    [Header("SFX")]
    [Tooltip("효과음 기본 볼륨. 재생 시 volumeScale과 곱해진다.")]
    [Range(0f, 1f)] public float sfxVolume = 1.0f;

    [Tooltip("true = 3D (거리 감쇠) / false = 2D (거리 무관). 런타임 변경 시 ApplySFXSpatial()을 호출한다.")]
    public bool sfx3D = true;

    [Tooltip("씬 시작 시 미리 생성할 SFX 소스 수")]
    public int initialPoolSize = 50;

    [Tooltip("3D 모드 - 최대 볼륨으로 들리는 거리 (m)")]
    public float sfxMinDistance = 1f;

    [Tooltip("3D 모드 - 소리가 사라지는 거리 (m)")]
    public float sfxMaxDistance = 50f;

    [Tooltip("SFX 음소거 여부")]
    public bool sfxMuted = false;

    [Header("리소스")]
    [Tooltip("BGM/SFX 클립을 이름으로 조회하는 ScriptableObject")]
    public SoundClipCollection soundCollection;

    private AudioSource _bgmSource;
    private Coroutine _bgmCoroutine;

    // 재생 대기 중인 유휴 소스
    private readonly List<AudioSource> _sfxPool = new List<AudioSource>();

    // 풀에서 꺼내 재생 중인 소스 (반환 전까지 유지)
    private readonly HashSet<AudioSource> _activeSources = new HashSet<AudioSource>();

    /// <summary>싱글톤을 등록하고 BGM 소스와 SFX 풀을 생성한 뒤 시작 BGM을 재생한다.</summary>
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _bgmSource = CreateSource("BGM_Source", isBGM: true);
        for (int i = 0; i < initialPoolSize; i++)
            _sfxPool.Add(CreateSource($"SFX_Pooled_{i}", isBGM: false));

        PlayBGM(startBGM);
    }

    /// <summary>BGM 또는 SFX용 AudioSource를 자식 오브젝트로 생성한다.</summary>
    private AudioSource CreateSource(string goName, bool isBGM)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(transform);

        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;

        if (isBGM)
        {
            src.loop = true;
            src.spatialBlend = 0f;
        }
        else
        {
            src.spatialBlend = sfx3D ? 1f : 0f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = sfxMinDistance;
            src.maxDistance = sfxMaxDistance;
        }

        return src;
    }

    // ── BGM ──────────────────────────────────────────────────

    /// <summary>
    /// 이름으로 BGM을 재생한다.
    /// 같은 클립이 이미 재생 중이면 무시하고, 다르면 페이드 아웃 → 페이드 인으로 전환한다.
    /// </summary>
    public void PlayBGM(string soundName)
    {
        AudioClip clip = soundCollection != null ? soundCollection.GetBGMClip(soundName) : null;
        if (clip == null)
        {
            Debug.LogWarning($"[SoundManager] BGM '{soundName}'을 찾을 수 없습니다.");
            return;
        }

        if (_bgmSource.clip == clip && _bgmSource.isPlaying) return;

        if (_bgmCoroutine != null) StopCoroutine(_bgmCoroutine);
        _bgmCoroutine = StartCoroutine(ChangeBGMProcess(clip));
    }

    /// <summary>현재 BGM을 페이드 아웃한 뒤 새 BGM을 페이드 인한다.</summary>
    private IEnumerator ChangeBGMProcess(AudioClip newClip)
    {
        if (_bgmSource.isPlaying)
        {
            float startVol = _bgmSource.volume;
            for (float t = 0f; t < bgmFadeDuration; t += Time.deltaTime)
            {
                _bgmSource.volume = Mathf.Lerp(startVol, 0f, t / bgmFadeDuration);
                yield return null;
            }
            _bgmSource.Stop();
            _bgmSource.volume = 0f;
        }

        _bgmSource.clip = newClip;
        _bgmSource.Play();

        for (float t = 0f; t < bgmFadeDuration; t += Time.deltaTime)
        {
            _bgmSource.volume = Mathf.Lerp(0f, bgmVolume, t / bgmFadeDuration);
            yield return null;
        }

        _bgmSource.volume = bgmVolume;
        _bgmCoroutine = null;
    }

    /// <summary>BGM을 페이드 없이 즉시 정지한다.</summary>
    public void StopBGM()
    {
        if (_bgmCoroutine != null) { StopCoroutine(_bgmCoroutine); _bgmCoroutine = null; }
        _bgmSource.Stop();
        _bgmSource.volume = 0f;
    }

    /// <summary>bgmVolume 변경 후 호출하면 재생 중인 BGM에 즉시 반영된다. (페이드 중에는 페이드 종료 시 반영)</summary>
    public void ApplyBGMVolume()
    {
        if (_bgmCoroutine == null) _bgmSource.volume = bgmVolume;
    }

    // ── SFX ──────────────────────────────────────────────────

    /// <summary>
    /// 지정 위치에서 효과음을 재생한다. (sfx3D가 꺼져 있으면 위치와 무관하게 재생)
    /// 재생이 끝나면 소스는 자동으로 풀에 반환된다.
    /// </summary>
    public void PlaySFXAtPosition(string soundName, Vector3 position, float volumeScale = 1f)
    {
        AudioSource src = PrepareSource(soundName, volumeScale);
        if (src == null) return;

        if (sfx3D) src.transform.position = position;
        src.Play();
    }

    /// <summary>
    /// 위치와 무관하게 2D로 효과음을 재생한다.
    /// 폭발음처럼 어디서든 잘 들려야 하는 사운드에 사용한다.
    /// </summary>
    public void PlaySFX2D(string soundName, float volumeScale = 1f)
    {
        AudioSource src = PrepareSource(soundName, volumeScale);
        if (src == null) return;

        src.spatialBlend = 0f;
        src.Play();
    }

    /// <summary>클립을 찾아 풀에서 소스를 꺼내고, 재생 종료 시 반환되도록 감시를 시작한다.</summary>
    private AudioSource PrepareSource(string soundName, float volumeScale)
    {
        if (sfxMuted) return null;

        AudioClip clip = soundCollection != null ? soundCollection.GetSFXClip(soundName) : null;
        if (clip == null)
        {
            Debug.LogWarning($"[SoundManager] SFX '{soundName}'을 찾을 수 없습니다.");
            return null;
        }

        AudioSource src = GetAvailableSource();
        src.clip = clip;
        src.volume = sfxVolume * volumeScale;

        _activeSources.Add(src);
        StartCoroutine(ReturnWhenFinished(src));
        return src;
    }

    /// <summary>풀에서 유휴 소스를 꺼낸다. 풀이 비어 있으면 새로 생성한다.</summary>
    private AudioSource GetAvailableSource()
    {
        int last = _sfxPool.Count - 1;
        if (last >= 0)
        {
            AudioSource src = _sfxPool[last];
            _sfxPool.RemoveAt(last);
            return src;
        }

        Debug.LogWarning($"[SoundManager] SFX 풀 부족으로 동적 확장 (재생 중 {_activeSources.Count}개). initialPoolSize를 늘리세요.");
        return CreateSource($"SFX_Dynamic_{_activeSources.Count}", isBGM: false);
    }

    /// <summary>재생이 끝나면 소스 설정을 풀 기본값으로 되돌리고 풀에 반환한다.</summary>
    private IEnumerator ReturnWhenFinished(AudioSource src)
    {
        // Play() 직후에는 isPlaying이 아직 반영되지 않을 수 있으므로 한 프레임 뒤부터 감시한다
        yield return null;
        yield return new WaitWhile(() => src.isPlaying);

        src.clip = null;
        src.spatialBlend = sfx3D ? 1f : 0f; // 2D 재생으로 바뀐 설정 복원

        _activeSources.Remove(src);
        _sfxPool.Add(src);
    }

    // ── SFX 설정 ─────────────────────────────────────────────

    /// <summary>
    /// sfx3D 설정을 풀에 있는 소스에 즉시 반영한다.
    /// 재생 중인 소스는 재생이 끝나 풀로 반환될 때 반영된다.
    /// </summary>
    public void ApplySFXSpatial()
    {
        float blend = sfx3D ? 1f : 0f;
        foreach (var src in _sfxPool) src.spatialBlend = blend;
    }

    /// <summary>SFX 음소거 상태를 변경한다. 음소거하면 재생 중인 효과음도 모두 정지한다.</summary>
    public void SetSFXMuted(bool muted)
    {
        sfxMuted = muted;
        if (!muted) return;

        // 정지된 소스는 각자의 감시 코루틴이 풀로 반환한다
        foreach (var src in _activeSources) src.Stop();
    }
}
