using System;

namespace DeviceConnector.Mqtt.Models
{
    /// <summary>
    /// MQTT 브로커 연결 설정
    /// </summary>
    public class MqttConnectionInfo
    {
        /// <summary>
        /// MQTT 브로커 주소 (예: "localhost", "192.168.1.100")
        /// </summary>
        public string BrokerAddress { get; set; } = "localhost";

        /// <summary>
        /// MQTT 브로커 포트 (기본값: 1883)
        /// </summary>
        public int Port { get; set; } = 1883;

        /// <summary>
        /// 클라이언트 ID (고유해야 함)
        /// </summary>
        public string ClientId { get; set; } = $"DeviceConnector_{Guid.NewGuid():N}";

        /// <summary>
        /// 인증 사용자명 (옵션)
        /// </summary>
        public string? Username { get; set; }

        /// <summary>
        /// 인증 비밀번호 (옵션)
        /// </summary>
        public string? Password { get; set; }

        /// <summary>
        /// Keep Alive 간격 (초)
        /// </summary>
        public int KeepAliveSeconds { get; set; } = 60;

        /// <summary>
        /// 자동 재연결 여부
        /// </summary>
        public bool AutoReconnect { get; set; } = true;

        /// <summary>
        /// 재연결 지연 시간 (초)
        /// </summary>
        public int ReconnectDelaySeconds { get; set; } = 5;

        /// <summary>
        /// Clean Session 여부
        /// </summary>
        public bool CleanSession { get; set; } = true;
    }
}
