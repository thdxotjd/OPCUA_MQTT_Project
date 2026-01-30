using System;
using System.Threading.Tasks;
using DeviceConnector.Models;

namespace DeviceConnector.Interfaces
{
    /// <summary>
    /// OPC UA → MQTT 브릿지 서비스 인터페이스
    /// OPC UA에서 데이터 변경 시 자동으로 MQTT로 발행
    /// </summary>
    public interface IOpcUaMqttBridgeService : IDisposable
    {
        #region 이벤트

        /// <summary>
        /// 브릿지 상태 변경 이벤트
        /// </summary>
        event EventHandler<BridgeStatusChangedEventArgs>? StatusChanged;

        /// <summary>
        /// 데이터 브릿지 완료 이벤트
        /// </summary>
        event EventHandler<DataBridgedEventArgs>? DataBridged;

        #endregion

        #region 속성

        /// <summary>
        /// 브릿지 실행 중 여부
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// OPC UA 연결 상태
        /// </summary>
        bool IsOpcUaConnected { get; }

        /// <summary>
        /// MQTT 연결 상태
        /// </summary>
        bool IsMqttConnected { get; }

        /// <summary>
        /// 브릿지된 메시지 수
        /// </summary>
        long BridgedMessageCount { get; }

        #endregion

        #region 브릿지 제어

        /// <summary>
        /// 브릿지 시작 (OPC UA 연결 + MQTT 연결 + 구독 시작)
        /// </summary>
        Task<bool> StartAsync();

        /// <summary>
        /// 브릿지 중지
        /// </summary>
        Task StopAsync();

        #endregion

        #region 수동 발행

        /// <summary>
        /// ESP32 데이터 수동 읽기 및 발행
        /// </summary>
        Task<bool> PublishEsp32DataNowAsync(string deviceId);

        /// <summary>
        /// STM_yolo 데이터 수동 읽기 및 발행
        /// </summary>
        Task<bool> PublishStmYoloDataNowAsync(string deviceId);

        /// <summary>
        /// 모든 디바이스 데이터 수동 발행
        /// </summary>
        Task PublishAllDataNowAsync();

        #endregion

        #region MQTT → OPC UA 명령 처리

        /// <summary>
        /// MQTT 명령 수신 처리 활성화
        /// </summary>
        Task<bool> EnableCommandHandlingAsync();

        #endregion
    }

    /// <summary>
    /// 브릿지 상태 변경 이벤트 인자
    /// </summary>
    public class BridgeStatusChangedEventArgs : EventArgs
    {
        public bool IsRunning { get; set; }
        public bool IsOpcUaConnected { get; set; }
        public bool IsMqttConnected { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// 데이터 브릿지 완료 이벤트 인자
    /// </summary>
    public class DataBridgedEventArgs : EventArgs
    {
        public string DeviceType { get; set; } = string.Empty; // "ESP32", "STM_YOLO"
        public string DeviceId { get; set; } = string.Empty;
        public string MqttTopic { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
