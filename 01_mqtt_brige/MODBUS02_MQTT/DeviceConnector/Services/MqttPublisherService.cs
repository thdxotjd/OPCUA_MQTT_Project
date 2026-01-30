using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DeviceConnector.Events;
using DeviceConnector.Interfaces;
using DeviceConnector.Models;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace DeviceConnector.Services
{
    /// <summary>
    /// MQTT 발행 서비스 구현
    /// MQTTnet 라이브러리 사용
    /// </summary>
    public class MqttPublisherService : IMqttPublisherService
    {
        #region 필드

        private readonly IMqttClient _mqttClient;
        private readonly MqttClientOptions _mqttOptions;
        private readonly SemaphoreSlim _connectionLock = new(1, 1);
        private readonly JsonSerializerOptions _jsonOptions;
        private bool _disposed;
        private CancellationTokenSource? _reconnectCts;

        #endregion

        #region 이벤트

        public event EventHandler<MqttConnectionChangedEventArgs>? ConnectionChanged;
        public event EventHandler<MqttMessagePublishedEventArgs>? MessagePublished;
        public event EventHandler<MqttMessageReceivedEventArgs>? MessageReceived;

        #endregion

        #region 속성

        public bool IsConnected => _mqttClient?.IsConnected ?? false;
        public MqttConnectionInfo ConnectionInfo { get; }
        public MqttTopicConfig TopicConfig { get; }

        #endregion

        #region 생성자

        public MqttPublisherService(MqttConnectionInfo connectionInfo, MqttTopicConfig? topicConfig = null)
        {
            ConnectionInfo = connectionInfo ?? throw new ArgumentNullException(nameof(connectionInfo));
            TopicConfig = topicConfig ?? new MqttTopicConfig();

            // JSON 직렬화 옵션
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            // MQTT 클라이언트 생성
            var factory = new MqttFactory();
            _mqttClient = factory.CreateMqttClient();

            // MQTT 옵션 설정
            var optionsBuilder = new MqttClientOptionsBuilder()
                .WithTcpServer(ConnectionInfo.BrokerAddress, ConnectionInfo.Port)
                .WithClientId(ConnectionInfo.ClientId)
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(ConnectionInfo.KeepAliveSeconds))
                .WithCleanSession(ConnectionInfo.CleanSession);

            // 인증 설정 (옵션)
            if (!string.IsNullOrEmpty(ConnectionInfo.Username))
            {
                optionsBuilder.WithCredentials(ConnectionInfo.Username, ConnectionInfo.Password);
            }

            _mqttOptions = optionsBuilder.Build();

            // 이벤트 핸들러 등록
            _mqttClient.ConnectedAsync += OnConnectedAsync;
            _mqttClient.DisconnectedAsync += OnDisconnectedAsync;
            _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;
        }

        #endregion

        #region 연결 관리

        public async Task<bool> ConnectAsync()
        {
            await _connectionLock.WaitAsync();
            try
            {
                if (IsConnected)
                    return true;

                _reconnectCts = new CancellationTokenSource();
                var result = await _mqttClient.ConnectAsync(_mqttOptions, _reconnectCts.Token);

                return result.ResultCode == MqttClientConnectResultCode.Success;
            }
            catch (Exception ex)
            {
                OnConnectionChanged(false, ex.Message);
                return false;
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        public async Task DisconnectAsync()
        {
            await _connectionLock.WaitAsync();
            try
            {
                _reconnectCts?.Cancel();

                if (IsConnected)
                {
                    await _mqttClient.DisconnectAsync();
                }
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        private async Task OnConnectedAsync(MqttClientConnectedEventArgs args)
        {
            OnConnectionChanged(true);
            Console.WriteLine($"[MQTT] 브로커 연결 성공: {ConnectionInfo.BrokerAddress}:{ConnectionInfo.Port}");

            // Last Will 메시지로 연결 상태 발행
            await PublishConnectionStatusAsync(new ConnectionStatusMqttMessage
            {
                DeviceId = ConnectionInfo.ClientId,
                IsConnected = true,
                ConnectionType = "MQTT",
                ServerEndpoint = $"{ConnectionInfo.BrokerAddress}:{ConnectionInfo.Port}"
            });
        }

        private async Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs args)
        {
            OnConnectionChanged(false, args.Exception?.Message);
            Console.WriteLine($"[MQTT] 브로커 연결 해제: {args.Reason}");

            // 자동 재연결
            if (ConnectionInfo.AutoReconnect && !(_reconnectCts?.IsCancellationRequested ?? true))
            {
                await Task.Delay(TimeSpan.FromSeconds(ConnectionInfo.ReconnectDelaySeconds));

                try
                {
                    Console.WriteLine("[MQTT] 재연결 시도 중...");
                    await _mqttClient.ConnectAsync(_mqttOptions, _reconnectCts!.Token);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MQTT] 재연결 실패: {ex.Message}");
                }
            }
        }

        private Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
        {
            var topic = args.ApplicationMessage.Topic;
            var payload = Encoding.UTF8.GetString(args.ApplicationMessage.PayloadSegment);

            Console.WriteLine($"[MQTT] 메시지 수신 - Topic: {topic}");

            MessageReceived?.Invoke(this, new MqttMessageReceivedEventArgs
            {
                Topic = topic,
                Payload = payload,
                Timestamp = DateTime.Now
            });

            return Task.CompletedTask;
        }

        private void OnConnectionChanged(bool isConnected, string? errorMessage = null)
        {
            ConnectionChanged?.Invoke(this, new MqttConnectionChangedEventArgs
            {
                IsConnected = isConnected,
                BrokerAddress = $"{ConnectionInfo.BrokerAddress}:{ConnectionInfo.Port}",
                ErrorMessage = errorMessage
            });
        }

        #endregion

        #region 데이터 발행

        public async Task<bool> PublishEsp32DataAsync(Esp32MqttMessage message)
        {
            var topic = TopicConfig.GetDeviceDataTopic("esp32", message.DeviceId);
            return await PublishJsonAsync(topic, message);
        }

        public async Task<bool> PublishStmYoloDataAsync(StmYoloMqttMessage message)
        {
            var topic = TopicConfig.GetDeviceDataTopic("stm_yolo", message.DeviceId);
            return await PublishJsonAsync(topic, message);
        }

        public async Task<bool> PublishConnectionStatusAsync(ConnectionStatusMqttMessage message)
        {
            var topic = TopicConfig.StatusTopic;
            return await PublishJsonAsync(topic, message, retain: true);
        }

        public async Task<bool> PublishJsonAsync(string topic, object message, bool retain = false)
        {
            try
            {
                var json = JsonSerializer.Serialize(message, _jsonOptions);
                return await PublishRawAsync(topic, json, retain);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MQTT] JSON 직렬화 오류: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> PublishRawAsync(string topic, string payload, bool retain = false)
        {
            if (!IsConnected)
            {
                Console.WriteLine("[MQTT] 연결되지 않음 - 발행 실패");
                return false;
            }

            try
            {
                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(payload)
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                    .WithRetainFlag(retain)
                    .Build();

                var result = await _mqttClient.PublishAsync(message);
                var isSuccess = result.IsSuccess;

                MessagePublished?.Invoke(this, new MqttMessagePublishedEventArgs
                {
                    Topic = topic,
                    Payload = payload,
                    IsSuccess = isSuccess
                });

                if (isSuccess)
                {
                    Console.WriteLine($"[MQTT] 발행 성공 - Topic: {topic}");
                }

                return isSuccess;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MQTT] 발행 오류: {ex.Message}");

                MessagePublished?.Invoke(this, new MqttMessagePublishedEventArgs
                {
                    Topic = topic,
                    Payload = payload,
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                });

                return false;
            }
        }

        #endregion

        #region 구독 (명령 수신용)

        public async Task<bool> SubscribeAsync(string topic)
        {
            if (!IsConnected)
                return false;

            try
            {
                var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
                    .WithTopicFilter(topic, MqttQualityOfServiceLevel.AtLeastOnce)
                    .Build();

                await _mqttClient.SubscribeAsync(subscribeOptions);
                Console.WriteLine($"[MQTT] 구독 성공 - Topic: {topic}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MQTT] 구독 오류: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UnsubscribeAsync(string topic)
        {
            if (!IsConnected)
                return false;

            try
            {
                var unsubscribeOptions = new MqttClientUnsubscribeOptionsBuilder()
                    .WithTopicFilter(topic)
                    .Build();

                await _mqttClient.UnsubscribeAsync(unsubscribeOptions);
                Console.WriteLine($"[MQTT] 구독 해제 - Topic: {topic}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MQTT] 구독 해제 오류: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> StartCommandSubscriptionAsync()
        {
            // 명령 토픽 구독 (와일드카드 사용)
            var commandTopic = $"{TopicConfig.CommandTopic}/#";
            return await SubscribeAsync(commandTopic);
        }

        #endregion

        #region Dispose

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _reconnectCts?.Cancel();
            _reconnectCts?.Dispose();

            _mqttClient.DisconnectAsync().GetAwaiter().GetResult();
            _mqttClient.Dispose();
            _connectionLock.Dispose();

            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
