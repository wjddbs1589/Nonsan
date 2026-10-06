using System;
using System.Runtime.InteropServices;

namespace UDP_Car
{
    /// <summary>
    /// UDP PC → 메인 PC: 인원수 + 시작 신호 수신용 패킷.
    /// UDPReceiver에서 수신한다.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    [Serializable]
    public struct ToPlayerPacket
    {
        public int playerCount; // 참여 플레이어 인원수
    }

    /// <summary>
    /// 메인 PC → UDP PC → 시뮬레이터: 기울기 + 충돌 데이터 송신용 패킷.
    /// SimulatorUDP에서 송신한다.
    ///
    /// Command 값:
    ///   1=대기, 2=준비, 3=운행, 4=종료
    ///
    /// Crash_Dir 값:
    ///   0=없음, 1=앞, 2=앞우, 3=우, 4=뒤우, 5=뒤, 6=뒤좌, 7=좌, 8=앞좌
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    [Serializable]
    public struct ToServerPacket
    {
        public int FrameCount;  // 0~2999 순환 카운터
        public int Command;     // 시뮬레이터 상태 (1=대기, 2=준비, 3=운행, 4=종료)
        public float Heave_Acc; // 수직 가속도 (현재 미사용)
        public float Roll_Pos;  // 좌우 기울기 (도)
        public float Pitch_Pos; // 앞뒤 기울기 (도)
        public int Crash_Dir;   // 충돌 방향
        public int Crash_Amp;   // 충돌 세기 (0~100)
    }
}
