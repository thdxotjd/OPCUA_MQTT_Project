namespace DeviceConnector.Models;

/// <summary>
/// Simulator 디바이스 데이터 모델
/// KEPServerEX 8-Bit Simulator 태그 매핑
/// 
/// ┌──────────────────────────────────────────────────────────────────────────┐
/// │ 태그 구성 (KEPServerEX 8-Bit Simulator)                                  │
/// │ OPC UA NodeId 형식: ns=2;s=ChannelName.DeviceName.TagGroup.TagName       │
/// │ 예: ns=2;s=MqttTest.SimDevice01.Monitoring.Temperature                   │
/// ├───────────────────┬─────────────────────────────────────┬───────────┬────┤
/// │ Tag Name          │ Address                             │ Data Type │방향│
/// ├───────────────────┼─────────────────────────────────────┼───────────┼────┤
/// │ Monitoring Group                                                         │
/// ├───────────────────┼─────────────────────────────────────┼───────────┼────┤
/// │ Temperature       │ RAMP(1000, 0, 100, 1)               │ Float     │Read│
/// │ Pressure          │ RANDOM(1000, 10, 50)                │ Float     │Read│
/// │ MotorRPM          │ SINE(100, 200, 800, 0.05)           │ Float     │Read│
/// ├───────────────────┼─────────────────────────────────────┼───────────┼────┤
/// │ Control Group                                                            │
/// ├───────────────────┼─────────────────────────────────────┼───────────┼────┤
/// │ MotorStart        │ R00100.0                            │ Boolean   │R/W │
/// │ MotorStop         │ R00100.1                            │ Boolean   │R/W │
/// │ SpeedSetpoint     │ R00200                              │ Word      │R/W │
/// │ ModeSelect        │ R00202                              │ Word      │R/W │
/// ├───────────────────┼─────────────────────────────────────┼───────────┼────┤
/// │ Status Group                                                             │
/// ├───────────────────┼─────────────────────────────────────┼───────────┼────┤
/// │ Alarm01           │ R00300.0                            │ Boolean   │R/W │
/// │ Alarm02           │ R00300.1                            │ Boolean   │R/W │
/// │ RunningFlag       │ R00300.2                            │ Boolean   │R/W │
/// └───────────────────┴─────────────────────────────────────┴───────────┴────┘
/// </summary>
public class SimulatorData
{
    #region 식별 정보

    /// <summary>디바이스 ID (예: "SimDevice01")</summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>KEPServerEX 채널명 (예: "MqttTest")</summary>
    public string ChannelName { get; set; } = string.Empty;

    /// <summary>KEPServerEX 디바이스명 (예: "SimDevice01")</summary>
    public string DeviceName { get; set; } = string.Empty;

    #endregion

    #region Monitoring (Read Only - 시뮬레이션 데이터)

    /// <summary>온도 - RAMP(1000, 0, 100, 1)</summary>
    public float Temperature { get; set; }

    /// <summary>압력 - RANDOM(1000, 10, 50)</summary>
    public float Pressure { get; set; }

    /// <summary>모터 RPM - SINE(100, 200, 800, 0.05)</summary>
    public float MotorRPM { get; set; }

    #endregion

    #region Control (Read/Write - 제어 명령)

    /// <summary>모터 시작 - R00100.0</summary>
    public bool MotorStart { get; set; }

    /// <summary>모터 정지 - R00100.1</summary>
    public bool MotorStop { get; set; }

    /// <summary>속도 설정값 - R00200</summary>
    public ushort SpeedSetpoint { get; set; }

    /// <summary>모드 선택 - R00202</summary>
    public ushort ModeSelect { get; set; }

    #endregion

    #region Status (Read/Write - 상태 정보)

    /// <summary>알람 1 - R00300.0</summary>
    public bool Alarm01 { get; set; }

    /// <summary>알람 2 - R00300.1</summary>
    public bool Alarm02 { get; set; }

    /// <summary>운전 상태 플래그 - R00300.2</summary>
    public bool RunningFlag { get; set; }

    #endregion

    #region 메타 데이터

    /// <summary>데이터 갱신 시간 (UTC)</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>OPC UA Quality 상태 (Good=true)</summary>
    public bool IsGoodQuality { get; set; } = true;

    #endregion

    #region 메서드

    /// <summary>객체 복제</summary>
    public SimulatorData Clone() => new()
    {
        DeviceId = DeviceId,
        ChannelName = ChannelName,
        DeviceName = DeviceName,
        // Monitoring
        Temperature = Temperature,
        Pressure = Pressure,
        MotorRPM = MotorRPM,
        // Control
        MotorStart = MotorStart,
        MotorStop = MotorStop,
        SpeedSetpoint = SpeedSetpoint,
        ModeSelect = ModeSelect,
        // Status
        Alarm01 = Alarm01,
        Alarm02 = Alarm02,
        RunningFlag = RunningFlag,
        // Meta
        Timestamp = Timestamp,
        IsGoodQuality = IsGoodQuality
    };

    public override string ToString() =>
        $"[{DeviceId}] Temp={Temperature:F1}°C Press={Pressure:F1}bar RPM={MotorRPM:F0} " +
        $"Motor(Start={MotorStart},Stop={MotorStop}) Running={RunningFlag}";

    #endregion
}

/// <summary>
/// Simulator 태그 이름 정의
/// </summary>
public class SimulatorTagNames
{
    // Monitoring Tags (Read)
    public string Temperature { get; set; } = "Temperature";
    public string Pressure { get; set; } = "Pressure";
    public string MotorRPM { get; set; } = "MotorRPM";

    // Control Tags (Read/Write)
    public string MotorStart { get; set; } = "MotorStart";
    public string MotorStop { get; set; } = "MotorStop";
    public string SpeedSetpoint { get; set; } = "SpeedSetpoint";
    public string ModeSelect { get; set; } = "ModeSelect";

    // Status Tags (Read/Write)
    public string Alarm01 { get; set; } = "Alarm01";
    public string Alarm02 { get; set; } = "Alarm02";
    public string RunningFlag { get; set; } = "RunningFlag";
}

/// <summary>
/// Simulator 디바이스 태그 설정
/// KEPServerEX Item ID 형식: ChannelName.DeviceName.TagGroup.TagName
/// </summary>
public class SimulatorTagConfig
{
    /// <summary>디바이스 식별자</summary>
    public string DeviceId { get; set; } = "SimDevice01";

    /// <summary>KEPServerEX 채널명</summary>
    public string ChannelName { get; set; } = "MqttTest";

    /// <summary>KEPServerEX 디바이스명</summary>
    public string DeviceName { get; set; } = "SimDevice01";

    /// <summary>태그 이름 설정</summary>
    public SimulatorTagNames Tags { get; set; } = new();

    /// <summary>OPC UA 네임스페이스 인덱스</summary>
    public int NamespaceIndex { get; set; } = 2;

    /// <summary>
    /// 태그명으로 NodeId 생성 (태그 그룹 포함)
    /// KEPServerEX 형식: ns=2;s=ChannelName.DeviceName.TagGroup.TagName
    /// 예: ns=2;s=MqttTest.SimDevice01.Monitoring.Temperature
    /// </summary>
    public string GetNodeId(string tagName, string tagGroup)
    {
        return $"ns={NamespaceIndex};s={ChannelName}.{DeviceName}.{tagGroup}.{tagName}";
    }

    /// <summary>
    /// 태그 그룹 정의
    /// </summary>
    public static class TagGroups
    {
        public const string Monitoring = "Monitoring";
        public const string Control = "Control";
        public const string Status = "Status";
    }
}
