using System;
using System.Collections;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Unity.Cinemachine;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using UDP_Car;

/// <summary>
/// [메인 PC] CinemachineSplineCart에 부착하는 모션 시뮬레이터 UDP 송신 컴포넌트.
/// 스플라인 경로의 커브 방향과 세기를 Roll 값으로 변환해 매 프레임 UDP로 전송한다.
/// Pitch는 평상시 0으로 고정하고, 급정지 연출(StopAndDestroy) 시에만 사용한다.
///
/// 송신 버퍼(_sendBuffer)는 Awake에서 한 번만 할당해 재사용한다.
/// </summary>
[RequireComponent(typeof(CinemachineSplineCart))]
public class SimulatorUDP : MonoBehaviour
{
    [Header("네트워크 - UDP PC IP / 포트")]
    [Tooltip("시뮬레이터 제어 PC(UDP PC)의 IP")]
    public string m_Ip = "192.168.0.12";
    [Tooltip("송신 포트")]
    public int m_Port = 61031;

    [Header("커브 감지")]
    [Tooltip("현재 위치에서 이만큼 앞의 접선과 비교해 곡률을 계산한다 (스플라인 정규화 t 기준)")]
    public float lookAheadOffset = 0.02f;

    [Header("출력 범위")]
    [Tooltip("Roll 출력의 최대 절댓값 (도)")]
    public float rollOutputMax = 4f;
    [Tooltip("곡률 감도. 낮을수록 완만한 커브에서도 큰 Roll이 출력된다.")]
    public float curvatureSensitivity = 30f;
    [Header("스무딩")]
    [Range(1f, 20f)]
    [Tooltip("Roll 값이 목표값으로 수렴하는 속도. 높을수록 즉각적으로 반응한다.")]
    public float smoothSpeed = 8f;

    [Header("급정지 연출")]
    [Tooltip("앞으로 기울어지는 Pitch 최대값 (도)")]
    public float brakePitchMax = 2f;
    [Tooltip("앞으로 기울어지는 데 걸리는 시간 (초)")]
    public float brakePitchDuration = 0.3f;
    [Tooltip("원점으로 복귀하는 데 걸리는 시간 (초)")]
    public float brakeReturnDuration = 0.6f;

    // 곡률값 클램프 범위 (각도가 아닌 단위 없는 곡률 지표)
    private const float CURVATURE_CLAMP = 45f;

    // 송신용 UDP 클라이언트와 패킷
    private UdpClient _client;
    private ToServerPacket _packet = new ToServerPacket();
    // 패킷 순번 (0~2999 순환)
    private int _frameCount;

    // Awake에서 한 번만 할당하고 매 프레임 재사용하는 송신 버퍼
    private byte[] _sendBuffer;

    // 현재 송신 중인 Roll 값 (스무딩 적용)
    private float _rollPos;
    // 원점 복귀·급정지 연출 중에는 곡률 기반 송신을 멈춘다
    private bool _isReturningToCenter = false;
    // 스플라인 위치 안정화 대기가 끝났는지 여부
    private bool _isReady = false;

    // 곡률 계산에 사용하는 스플라인 참조
    private CinemachineSplineCart _cart;
    private SplineContainer _splineContainer;
    private float _splineLength;

    /// <summary>스플라인 참조와 길이를 캐싱하고 송신 버퍼를 할당한다.</summary>
    private void Awake()
    {
        _cart = GetComponent<CinemachineSplineCart>();
        _splineContainer = _cart.Spline;

        if (_splineContainer != null && _splineContainer.Spline != null)
            _splineLength = _splineContainer.Spline.GetLength();
        else
            Debug.LogError("[SimulatorUDP] SplineContainer를 찾을 수 없습니다.");

        _sendBuffer = new byte[Marshal.SizeOf(typeof(ToServerPacket))];
    }

    /// <summary>UDP 클라이언트를 생성하고 운행 상태로 송신을 준비한다.</summary>
    private void Start()
    {
        Application.targetFrameRate = 60;
        _client = new UdpClient();
        _client.Client.Blocking = false;
        _packet.Command = 3; // 운행 상태로 시작

        // 스플라인 위치가 안정화될 때까지 1프레임 대기한다
        StartCoroutine(DelayedInit());
    }

    /// <summary>스플라인 카트 위치가 안정화될 때까지 1프레임 기다린 뒤 송신을 시작한다.</summary>
    private IEnumerator DelayedInit()
    {
        yield return null;
        _isReady = true;
    }

    /// <summary>매 프레임 Roll을 계산해 송신한다. 원점 복귀·급정지 연출 중에는 건너뛴다.</summary>
    private void Update()
    {
        if (_isReturningToCenter || !_isReady) return;
        CalculateRoll();
        _packet.Roll_Pos = _rollPos;
        _packet.Pitch_Pos = 0f;
        SendPacket();
    }

    /// <summary>종료 시 UDP 연결을 닫는다.</summary>
    private void OnApplicationQuit()
    {
        _client?.Close();
        _client = null;
    }

    /// <summary>
    /// 스플라인 현재 위치와 lookAheadOffset 앞 지점의 접선 차이로 횡방향 곡률을 계산하고,
    /// 커브 방향에 맞춘 Roll 목표값으로 변환한다.
    /// </summary>
    private void CalculateRoll()
    {
        if (_splineContainer == null || _splineLength <= 0f) return;

        float normalizedT = Mathf.Clamp01((float)_cart.SplinePosition / _splineLength);
        float aheadT = Mathf.Clamp01(normalizedT + lookAheadOffset);
        var spline = _splineContainer.Spline;

        float3 tangentCurrent = SplineUtility.EvaluateTangent(spline, normalizedT);
        float3 tangentAhead = SplineUtility.EvaluateTangent(spline, aheadT);

        Vector3 tangent = _splineContainer.transform.TransformDirection((Vector3)math.normalize(tangentCurrent));
        Vector3 tangentAheadW = _splineContainer.transform.TransformDirection((Vector3)math.normalize(tangentAhead));

        // 접선 변화량 = 진행 방향이 꺾이는 방향과 세기
        Vector3 curvatureVec = (tangentAheadW - tangent) / lookAheadOffset;

        // Unity(왼손 좌표계)에서 Cross(진행 방향, 위쪽)는 진행 방향 기준 왼쪽 벡터
        Vector3 left = Vector3.Cross(tangent, Vector3.up).normalized;

        // 양수 = 좌회전, 음수 = 우회전
        float curvatureLateral = Vector3.Dot(curvatureVec, left);

        // 원심력 반대 방향으로 기울이기 위해 부호를 반전하고, 곡률값을 제한한 뒤 Roll 출력 범위로 매핑한다
        float clampedLateral = Mathf.Clamp(-curvatureLateral, -CURVATURE_CLAMP, CURVATURE_CLAMP);
        float targetRoll = clampedLateral / curvatureSensitivity * rollOutputMax;
        _rollPos = Mathf.Lerp(_rollPos, targetRoll, Time.deltaTime * smoothSpeed);
    }

    /// <summary>
    /// 충돌 이벤트를 송신한다.
    /// dir: 1~8 (1=앞, 2=앞우, 3=우, 4=뒤우, 5=뒤, 6=뒤좌, 7=좌, 8=앞좌)
    /// 3초 후 자동으로 충돌 값을 초기화한다.
    /// </summary>
    public void CrashEvent(int dir)
    {
        if (dir < 1 || dir > 8)
        {
            Debug.LogWarning($"[SimulatorUDP] 잘못된 충돌 방향: {dir}");
            return;
        }

        _packet.Crash_Dir = dir;
        _packet.Crash_Amp = 100;
        SendPacket();

        StopCoroutine("ClearCrash");
        StartCoroutine("ClearCrash");
    }

    /// <summary>3초 후 충돌 값을 초기화해 송신한다.</summary>
    private IEnumerator ClearCrash()
    {
        yield return new WaitForSeconds(3f);
        _packet.Crash_Dir = 0;
        _packet.Crash_Amp = 0;
        SendPacket();
    }

    /// <summary>시뮬레이터를 수평 원점으로 복귀시킨다 (UDP 연결 유지).</summary>
    public void ReturnToCenter()
    {
        _isReturningToCenter = true;
        StartCoroutine(ReturnToCenterRoutine());
    }

    /// <summary>원점 복귀가 끝나면 곡률 기반 송신을 재개한다.</summary>
    private IEnumerator ReturnToCenterRoutine()
    {
        yield return ReturnToCenterSequence();
        _isReturningToCenter = false;
    }

    /// <summary>
    /// 급정지 연출 후 원점 복귀, UDP 연결 종료, 컴포넌트 파괴.
    /// 앞으로 살짝 기울었다가 수평으로 돌아온 뒤 종료한다.
    /// </summary>
    public void StopAndDestroy()
    {
        _isReturningToCenter = true;
        StartCoroutine(BrakeAndDestroyRoutine());
    }

    /// <summary>급정지 Pitch 연출 → 원점 복귀 → 연결 종료 순서로 처리한다.</summary>
    private IEnumerator BrakeAndDestroyRoutine()
    {
        float startRoll = _rollPos;
        float elapsed = 0f;

        // 1단계: Roll을 0으로 줄이면서 Pitch를 앞으로 기울인다 (급정지 느낌)
        while (elapsed < brakePitchDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / brakePitchDuration;
            _packet.Roll_Pos = Mathf.Lerp(startRoll, 0f, t);
            _packet.Pitch_Pos = Mathf.Lerp(0f, brakePitchMax, t);
            SendPacket();
            yield return null;
        }

        // 2단계: Roll/Pitch 모두 원점으로 복귀한다
        elapsed = 0f;
        while (elapsed < brakeReturnDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / brakeReturnDuration;
            _packet.Roll_Pos = 0f;
            _packet.Pitch_Pos = Mathf.Lerp(brakePitchMax, 0f, t);
            SendPacket();
            yield return null;
        }

        _rollPos = 0f;
        _packet.Roll_Pos = 0f;
        _packet.Pitch_Pos = 0f;
        SendPacket();

        _client?.Close();
        _client = null;
        Destroy(this);
    }

    /// <summary>Roll을 1.5초 동안 0으로 줄이며 송신한다.</summary>
    private IEnumerator ReturnToCenterSequence()
    {
        float elapsed = 0f;
        const float duration = 1.5f;
        float startRoll = _rollPos;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _rollPos = Mathf.Lerp(startRoll, 0f, elapsed / duration);
            _packet.Roll_Pos = _rollPos;
            _packet.Pitch_Pos = 0f;
            SendPacket();
            yield return null;
        }

        _rollPos = 0f;
        _packet.Roll_Pos = 0f;
        _packet.Pitch_Pos = 0f;
        SendPacket();
    }

    /// <summary>프레임 카운터를 갱신하고 패킷을 송신 버퍼에 복사해 전송한다.</summary>
    private void SendPacket()
    {
        if (_client == null) return;

        _frameCount = _frameCount < 2999 ? _frameCount + 1 : 0;
        _packet.FrameCount = _frameCount;

        try
        {
            StructToBuffer(_packet, _sendBuffer);
            _client.Send(_sendBuffer, _sendBuffer.Length, m_Ip, m_Port);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SimulatorUDP] 전송 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 구조체를 송신 버퍼에 복사한다.
    /// 버퍼는 재사용하며, 마샬링 과정의 임시 비관리 메모리는 호출마다 할당 후 해제한다.
    /// </summary>
    private static void StructToBuffer(object obj, byte[] buffer)
    {
        IntPtr ptr = Marshal.AllocHGlobal(buffer.Length);
        Marshal.StructureToPtr(obj, ptr, true);
        Marshal.Copy(ptr, buffer, 0, buffer.Length);
        Marshal.FreeHGlobal(ptr);
    }
}
