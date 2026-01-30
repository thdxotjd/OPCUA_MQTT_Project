namespace DeviceConnector.Mqtt.Models
{
    /// <summary>
    /// MQTT 토픽 설정
    /// </summary>
    public class MqttTopicConfig
    {
        /// <summary>
        /// 기본 토픽 접두사 (예: "factory/line1")
        /// </summary>
        public string BaseTopic { get; set; } = "opcua/devices";

        /// <summary>
        /// ESP32 데이터 토픽
        /// </summary>
        public string Esp32DataTopic => $"{BaseTopic}/esp32";

        /// <summary>
        /// 상태 토픽 (연결 상태 등)
        /// </summary>
        public string StatusTopic => $"{BaseTopic}/status";

        /// <summary>
        /// 명령 수신 토픽 (SCADA → 장비)
        /// </summary>
        public string CommandTopic => $"{BaseTopic}/command";

        /// <summary>
        /// 디바이스별 데이터 토픽 생성
        /// </summary>
        public string GetDeviceDataTopic(string deviceId)
        {
            return $"{BaseTopic}/esp32/{deviceId}/data";
        }

        /// <summary>
        /// 디바이스별 상태 토픽 생성
        /// </summary>
        public string GetDeviceStatusTopic(string deviceId)
        {
            return $"{BaseTopic}/esp32/{deviceId}/status";
        }
    }
}
