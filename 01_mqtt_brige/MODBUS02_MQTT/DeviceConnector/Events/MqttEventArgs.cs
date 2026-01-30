using System;

namespace DeviceConnector.Events
{
    /// <summary>
    /// MQTT 연결 상태 변경 이벤트
    /// </summary>
    public class MqttConnectionChangedEventArgs : EventArgs
    {
        public bool IsConnected { get; set; }
        public string BrokerAddress { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// MQTT 메시지 발행 완료 이벤트
    /// </summary>
    public class MqttMessagePublishedEventArgs : EventArgs
    {
        public string Topic { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// MQTT 메시지 수신 이벤트 (명령 수신용)
    /// </summary>
    public class MqttMessageReceivedEventArgs : EventArgs
    {
        public string Topic { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
