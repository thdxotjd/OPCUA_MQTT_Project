using System;
using System.Threading.Tasks;
using DeviceConnector.Events;

namespace DeviceConnector.Interfaces
{
    // ============================================================
    // 이 파일은 기존 MODBUS02_CODE의 인터페이스 정의입니다.
    // 실제 프로젝트에서는 MODBUS02_CODE의 인터페이스를 사용하세요.
    // ============================================================

    /// <summary>
    /// ESP32 OPC UA 클라이언트 서비스 인터페이스
    /// (MODBUS02_CODE에서 가져옴)
    /// </summary>
    public interface IOpcUaClientService : IDisposable
    {
        event EventHandler<DataChangedEventArgs>? DataChanged;
        event EventHandler<ConnectionStatusChangedEventArgs>? ConnectionStatusChanged;

        bool IsConnected { get; }

        Task<bool> ConnectAsync();
        Task DisconnectAsync();
        Task<ESP32Data?> ReadDeviceDataAsync();
        Task<bool> WriteTagAsync(string tagName, object value);
        Task StartSubscriptionAsync();
        Task StopSubscriptionAsync();
    }

    /// <summary>
    /// STM_yolo OPC UA 클라이언트 서비스 인터페이스
    /// (MODBUS02_CODE에서 가져옴)
    /// </summary>
    public interface ISTMYoloClientService : IDisposable
    {
        event EventHandler<STMYoloDataChangedEventArgs>? DataChanged;
        event EventHandler<ConnectionStatusChangedEventArgs>? ConnectionStatusChanged;

        bool IsConnected { get; }

        Task<bool> ConnectAsync();
        Task DisconnectAsync();
        Task<STMYoloData?> ReadAllDataAsync();
        Task<bool> WriteTargetStateAsync(long value);
        Task<bool> WriteTargetSpeedMainAsync(long value);
        Task<bool> WriteTargetSpeedSortAsync(long value);
        Task<bool> WriteTargetSpeedLoadAsync(long value);
        Task StartAllSubscriptionsAsync();
        Task StopAllSubscriptionsAsync();
    }
}

namespace DeviceConnector.Events
{
    /// <summary>
    /// ESP32 데이터 변경 이벤트 (MODBUS02_CODE에서 가져옴)
    /// </summary>
    public class DataChangedEventArgs : EventArgs
    {
        public ESP32Data? Data { get; set; }
    }

    /// <summary>
    /// STM_yolo 데이터 변경 이벤트 (MODBUS02_CODE에서 가져옴)
    /// </summary>
    public class STMYoloDataChangedEventArgs : EventArgs
    {
        public STMYoloData? Data { get; set; }
    }

    /// <summary>
    /// 연결 상태 변경 이벤트 (MODBUS02_CODE에서 가져옴)
    /// </summary>
    public class ConnectionStatusChangedEventArgs : EventArgs
    {
        public bool IsConnected { get; set; }
        public string? ErrorMessage { get; set; }
    }
}

namespace DeviceConnector.Interfaces
{
    /// <summary>
    /// ESP32 데이터 모델 (MODBUS02_CODE에서 가져옴)
    /// </summary>
    public class ESP32Data
    {
        public string DeviceId { get; set; } = string.Empty;
        public float PosX { get; set; }
        public float PosY { get; set; }
        public float PosTheta { get; set; }
        public bool TargetA { get; set; }
        public string Control { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public bool IsGoodQuality { get; set; }
    }

    /// <summary>
    /// STM_yolo 데이터 모델 (MODBUS02_CODE에서 가져옴)
    /// </summary>
    public class STMYoloData
    {
        public string DeviceId { get; set; } = string.Empty;

        // Current 값
        public long CurrentState { get; set; }
        public long CurrentSpeedMain { get; set; }
        public long CurrentSpeedSort { get; set; }
        public long CurrentSpeedLoad { get; set; }

        // Target 값
        public long TargetState { get; set; }
        public long TargetSpeedMain { get; set; }
        public long TargetSpeedSort { get; set; }
        public long TargetSpeedLoad { get; set; }

        // AGV 플래그
        public bool AgvSortArrived { get; set; }
        public bool AgvSortDeparted { get; set; }
        public bool AgvLoadArrived { get; set; }
        public bool AgvLoadDeparted { get; set; }

        public DateTime Timestamp { get; set; }
        public bool IsGoodQuality { get; set; }
    }
}

namespace DeviceConnector.Models
{
    /// <summary>
    /// OPC UA 연결 정보 (MODBUS02_CODE에서 가져옴)
    /// </summary>
    public class OpcUaConnectionInfo
    {
        public string ServerUrl { get; set; } = "opc.tcp://localhost:49320";
        public string ApplicationName { get; set; } = "DeviceConnector";
        public bool AutoReconnect { get; set; } = true;
    }
}
