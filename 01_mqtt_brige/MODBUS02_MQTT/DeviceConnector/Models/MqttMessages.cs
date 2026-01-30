using System;
using System.Text.Json.Serialization;

namespace DeviceConnector.Models
{
    /// <summary>
    /// MQTT로 발행되는 메시지 기본 클래스
    /// </summary>
    public abstract class MqttMessageBase
    {
        /// <summary>
        /// 메시지 타임스탬프 (UTC)
        /// </summary>
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 디바이스 ID
        /// </summary>
        [JsonPropertyName("deviceId")]
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// 메시지 타입
        /// </summary>
        [JsonPropertyName("messageType")]
        public string MessageType { get; set; } = string.Empty;
    }

    /// <summary>
    /// ESP32 데이터 MQTT 메시지
    /// </summary>
    public class Esp32MqttMessage : MqttMessageBase
    {
        public Esp32MqttMessage()
        {
            MessageType = "ESP32_DATA";
        }

        [JsonPropertyName("posX")]
        public float PosX { get; set; }

        [JsonPropertyName("posY")]
        public float PosY { get; set; }

        [JsonPropertyName("posTheta")]
        public float PosTheta { get; set; }

        [JsonPropertyName("targetA")]
        public bool TargetA { get; set; }

        [JsonPropertyName("control")]
        public string Control { get; set; } = string.Empty;

        [JsonPropertyName("state")]
        public string State { get; set; } = string.Empty;

        [JsonPropertyName("isGoodQuality")]
        public bool IsGoodQuality { get; set; }
    }

    /// <summary>
    /// STM_yolo 데이터 MQTT 메시지
    /// </summary>
    public class StmYoloMqttMessage : MqttMessageBase
    {
        public StmYoloMqttMessage()
        {
            MessageType = "STM_YOLO_DATA";
        }

        // Current 값 (읽기)
        [JsonPropertyName("currentState")]
        public long CurrentState { get; set; }

        [JsonPropertyName("currentSpeedMain")]
        public long CurrentSpeedMain { get; set; }

        [JsonPropertyName("currentSpeedSort")]
        public long CurrentSpeedSort { get; set; }

        [JsonPropertyName("currentSpeedLoad")]
        public long CurrentSpeedLoad { get; set; }

        // Target 값 (쓰기)
        [JsonPropertyName("targetState")]
        public long TargetState { get; set; }

        [JsonPropertyName("targetSpeedMain")]
        public long TargetSpeedMain { get; set; }

        [JsonPropertyName("targetSpeedSort")]
        public long TargetSpeedSort { get; set; }

        [JsonPropertyName("targetSpeedLoad")]
        public long TargetSpeedLoad { get; set; }

        // AGV 플래그
        [JsonPropertyName("agvSortArrived")]
        public bool AgvSortArrived { get; set; }

        [JsonPropertyName("agvSortDeparted")]
        public bool AgvSortDeparted { get; set; }

        [JsonPropertyName("agvLoadArrived")]
        public bool AgvLoadArrived { get; set; }

        [JsonPropertyName("agvLoadDeparted")]
        public bool AgvLoadDeparted { get; set; }

        [JsonPropertyName("isGoodQuality")]
        public bool IsGoodQuality { get; set; }
    }

    /// <summary>
    /// 연결 상태 MQTT 메시지
    /// </summary>
    public class ConnectionStatusMqttMessage : MqttMessageBase
    {
        public ConnectionStatusMqttMessage()
        {
            MessageType = "CONNECTION_STATUS";
        }

        [JsonPropertyName("isConnected")]
        public bool IsConnected { get; set; }

        [JsonPropertyName("connectionType")]
        public string ConnectionType { get; set; } = string.Empty; // "OPC_UA", "MQTT"

        [JsonPropertyName("serverEndpoint")]
        public string ServerEndpoint { get; set; } = string.Empty;

        [JsonPropertyName("errorMessage")]
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// 명령 MQTT 메시지 (SCADA → 장비)
    /// </summary>
    public class CommandMqttMessage : MqttMessageBase
    {
        public CommandMqttMessage()
        {
            MessageType = "COMMAND";
        }

        [JsonPropertyName("commandType")]
        public string CommandType { get; set; } = string.Empty;

        [JsonPropertyName("tagName")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public object? Value { get; set; }
    }
}
