using System;

namespace DeviceConnector.Models
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
        /// ESP32 데이터 토픽 (BaseTopic/ESP32/{DeviceId}/data)
        /// </summary>
        public string Esp32DataTopic => $"{BaseTopic}/esp32";

        /// <summary>
        /// STM_yolo 데이터 토픽 (BaseTopic/STM_yolo/{DeviceId}/data)
        /// </summary>
        public string StmYoloDataTopic => $"{BaseTopic}/stm_yolo";

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
        public string GetDeviceDataTopic(string deviceType, string deviceId)
        {
            return $"{BaseTopic}/{deviceType}/{deviceId}/data";
        }

        /// <summary>
        /// 디바이스별 상태 토픽 생성
        /// </summary>
        public string GetDeviceStatusTopic(string deviceType, string deviceId)
        {
            return $"{BaseTopic}/{deviceType}/{deviceId}/status";
        }
    }
}
