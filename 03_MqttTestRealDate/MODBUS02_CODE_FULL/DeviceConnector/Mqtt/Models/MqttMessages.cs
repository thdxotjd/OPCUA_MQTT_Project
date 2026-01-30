using System;
using System.Text.Json.Serialization;

namespace DeviceConnector.Mqtt.Models
{
    /// <summary>
    /// MQTT로 발행되는 메시지 기본 클래스
    /// </summary>
    public abstract class MqttMessageBase
    {
        /// <summary>메시지 타임스탬프 (UTC)</summary>
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>디바이스 ID</summary>
        [JsonPropertyName("deviceId")]
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>채널명</summary>
        [JsonPropertyName("channelName")]
        public string ChannelName { get; set; } = string.Empty;

        /// <summary>디바이스명</summary>
        [JsonPropertyName("deviceName")]
        public string DeviceName { get; set; } = string.Empty;

        /// <summary>메시지 타입</summary>
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

        /// <summary>X 좌표 (m)</summary>
        [JsonPropertyName("posX")]
        public float PosX { get; set; }

        /// <summary>Y 좌표 (m)</summary>
        [JsonPropertyName("posY")]
        public float PosY { get; set; }

        /// <summary>각도 Theta (rad)</summary>
        [JsonPropertyName("posTheta")]
        public float PosTheta { get; set; }

        /// <summary>목표 A 플래그</summary>
        [JsonPropertyName("targetA")]
        public bool TargetA { get; set; }

        /// <summary>제어 명령</summary>
        [JsonPropertyName("control")]
        public string Control { get; set; } = string.Empty;

        /// <summary>상태 정보</summary>
        [JsonPropertyName("state")]
        public string State { get; set; } = string.Empty;

        /// <summary>OPC UA Quality 상태</summary>
        [JsonPropertyName("isGoodQuality")]
        public bool IsGoodQuality { get; set; }

        /// <summary>
        /// ESP32Data에서 MQTT 메시지 생성
        /// </summary>
        public static Esp32MqttMessage FromEsp32Data(DeviceConnector.Models.ESP32Data data)
        {
            return new Esp32MqttMessage
            {
                DeviceId = data.DeviceId,
                ChannelName = data.ChannelName,
                DeviceName = data.DeviceName,
                PosX = data.PosX,
                PosY = data.PosY,
                PosTheta = data.PosTheta,
                TargetA = data.TargetA,
                Control = data.Control,
                State = data.State,
                IsGoodQuality = data.IsGoodQuality,
                Timestamp = data.Timestamp
            };
        }
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

        /// <summary>연결 상태</summary>
        [JsonPropertyName("isConnected")]
        public bool IsConnected { get; set; }

        /// <summary>연결 타입 ("OPC_UA", "MQTT")</summary>
        [JsonPropertyName("connectionType")]
        public string ConnectionType { get; set; } = string.Empty;

        /// <summary>서버 엔드포인트</summary>
        [JsonPropertyName("serverEndpoint")]
        public string ServerEndpoint { get; set; } = string.Empty;

        /// <summary>에러 메시지</summary>
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

        /// <summary>명령 타입 ("WRITE_TAG", "READ_TAG")</summary>
        [JsonPropertyName("commandType")]
        public string CommandType { get; set; } = string.Empty;

        /// <summary>태그 이름</summary>
        [JsonPropertyName("tagName")]
        public string TagName { get; set; } = string.Empty;

        /// <summary>값</summary>
        [JsonPropertyName("value")]
        public object? Value { get; set; }
    }
}
