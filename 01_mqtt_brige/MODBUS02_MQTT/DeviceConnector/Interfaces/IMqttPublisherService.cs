using System;
using System.Threading.Tasks;
using DeviceConnector.Events;
using DeviceConnector.Models;

namespace DeviceConnector.Interfaces
{
    /// <summary>
    /// MQTT 발행 서비스 인터페이스
    /// OPC UA에서 읽은 데이터를 MQTT 브로커로 발행
    /// </summary>
    public interface IMqttPublisherService : IDisposable
    {
        #region 이벤트

        /// <summary>
        /// MQTT 연결 상태 변경 이벤트
        /// </summary>
        event EventHandler<MqttConnectionChangedEventArgs>? ConnectionChanged;

        /// <summary>
        /// 메시지 발행 완료 이벤트
        /// </summary>
        event EventHandler<MqttMessagePublishedEventArgs>? MessagePublished;

        /// <summary>
        /// 메시지 수신 이벤트 (명령 수신용)
        /// </summary>
        event EventHandler<MqttMessageReceivedEventArgs>? MessageReceived;

        #endregion

        #region 속성

        /// <summary>
        /// 연결 상태
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// MQTT 연결 설정
        /// </summary>
        MqttConnectionInfo ConnectionInfo { get; }

        /// <summary>
        /// MQTT 토픽 설정
        /// </summary>
        MqttTopicConfig TopicConfig { get; }

        #endregion

        #region 연결 관리

        /// <summary>
        /// MQTT 브로커에 연결
        /// </summary>
        Task<bool> ConnectAsync();

        /// <summary>
        /// MQTT 브로커 연결 해제
        /// </summary>
        Task DisconnectAsync();

        #endregion

        #region 데이터 발행

        /// <summary>
        /// ESP32 데이터 발행
        /// </summary>
        Task<bool> PublishEsp32DataAsync(Esp32MqttMessage message);

        /// <summary>
        /// STM_yolo 데이터 발행
        /// </summary>
        Task<bool> PublishStmYoloDataAsync(StmYoloMqttMessage message);

        /// <summary>
        /// 연결 상태 발행
        /// </summary>
        Task<bool> PublishConnectionStatusAsync(ConnectionStatusMqttMessage message);

        /// <summary>
        /// 커스텀 JSON 메시지 발행
        /// </summary>
        Task<bool> PublishJsonAsync(string topic, object message, bool retain = false);

        /// <summary>
        /// 원시 문자열 메시지 발행
        /// </summary>
        Task<bool> PublishRawAsync(string topic, string payload, bool retain = false);

        #endregion

        #region 구독 (명령 수신용)

        /// <summary>
        /// 토픽 구독 (SCADA로부터 명령 수신)
        /// </summary>
        Task<bool> SubscribeAsync(string topic);

        /// <summary>
        /// 토픽 구독 해제
        /// </summary>
        Task<bool> UnsubscribeAsync(string topic);

        /// <summary>
        /// 명령 토픽 구독 시작
        /// </summary>
        Task<bool> StartCommandSubscriptionAsync();

        #endregion
    }
}
