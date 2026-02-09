using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DeviceConnector.Events;
using DeviceConnector.Interfaces;
using DeviceConnector.Mqtt.Events;
using DeviceConnector.Mqtt.Interfaces;
using DeviceConnector.Mqtt.Models;

namespace DeviceConnector.Mqtt.Services
{
    /// <summary>
    /// OPC UA → MQTT 브릿지 서비스 구현
    /// OPC UA 구독 데이터를 실시간으로 MQTT로 발행
    /// </summary>
    public class OpcUaMqttBridgeService : IOpcUaMqttBridgeService
    {
        #region 필드

        private readonly IOpcUaClientService _opcUaService;
        private readonly IMqttPublisherService _mqttService;
        private readonly SemaphoreSlim _bridgeLock = new(1, 1);
        private bool _disposed;
        private long _bridgedMessageCount;

        #endregion

        #region 이벤트

        public event EventHandler<BridgeStatusChangedEventArgs>? StatusChanged;
        public event EventHandler<DataBridgedEventArgs>? DataBridged;

        #endregion

        #region 속성

        public bool IsRunning { get; private set; }
        public bool IsOpcUaConnected => _opcUaService.IsConnected;
        public bool IsMqttConnected => _mqttService.IsConnected;
        public long BridgedMessageCount => Interlocked.Read(ref _bridgedMessageCount);

        #endregion

        #region 생성자

        /// <summary>
        /// 브릿지 서비스 생성자
        /// </summary>
        public OpcUaMqttBridgeService(
            IOpcUaClientService opcUaService,
            IMqttPublisherService mqttService)
        {
            _opcUaService = opcUaService ?? throw new ArgumentNullException(nameof(opcUaService));
            _mqttService = mqttService ?? throw new ArgumentNullException(nameof(mqttService));

            // OPC UA 이벤트 연결
            _opcUaService.DataChanged += OnOpcUaDataChanged;
            _opcUaService.ConnectionChanged += OnOpcUaConnectionChanged;

            // MQTT 이벤트 연결
            _mqttService.ConnectionChanged += OnMqttConnectionChanged;
            _mqttService.MessageReceived += OnMqttCommandReceived;
        }

        #endregion

        #region 브릿지 제어

        public async Task<bool> StartAsync()
        {
            await _bridgeLock.WaitAsync();
            try
            {
                if (IsRunning)
                    return true;

                Console.WriteLine("[Bridge] 브릿지 시작 중...");

                // 1. MQTT 연결
                Console.WriteLine("[Bridge] MQTT 브로커 연결 중...");
                var mqttConnected = await _mqttService.ConnectAsync();
                if (!mqttConnected)
                {
                    Console.WriteLine("[Bridge] MQTT 연결 실패");
                    return false;
                }

                // 2. OPC UA 연결
                Console.WriteLine("[Bridge] OPC UA 서버 연결 중...");
                var opcUaConnected = await _opcUaService.ConnectAsync();
                if (!opcUaConnected)
                {
                    Console.WriteLine("[Bridge] OPC UA 연결 실패");
                    // MQTT는 연결 유지
                }

                // 3. OPC UA 구독 시작
                if (_opcUaService.IsConnected)
                {
                    Console.WriteLine("[Bridge] OPC UA 데이터 구독 시작...");
                    await _opcUaService.StartAllSubscriptionsAsync();
                }

                IsRunning = true;
                OnStatusChanged("브릿지 시작됨");
                Console.WriteLine("[Bridge] 브릿지 시작 완료!");

                return true;
            }
            finally
            {
                _bridgeLock.Release();
            }
        }

        public async Task StopAsync()
        {
            await _bridgeLock.WaitAsync();
            try
            {
                if (!IsRunning)
                    return;

                Console.WriteLine("[Bridge] 브릿지 중지 중...");

                // 구독 중지 및 연결 해제
                await _opcUaService.StopAllSubscriptionsAsync();
                await _opcUaService.DisconnectAsync();
                await _mqttService.DisconnectAsync();

                IsRunning = false;
                OnStatusChanged("브릿지 중지됨");
                Console.WriteLine("[Bridge] 브릿지 중지 완료");
            }
            finally
            {
                _bridgeLock.Release();
            }
        }

        #endregion

        #region OPC UA 데이터 변경 → MQTT 발행

        private async void OnOpcUaDataChanged(object? sender, DataChangedEventArgs e)
        {
            if (!_mqttService.IsConnected || e.Data == null)
                return;

            try
            {
                // ESP32Data → Esp32MqttMessage 변환
                var message = Esp32MqttMessage.FromEsp32Data(e.Data);

                // MQTT 발행
                var success = await _mqttService.PublishEsp32DataAsync(message);

                if (success)
                {
                    Interlocked.Increment(ref _bridgedMessageCount);
                    OnDataBridged(e.Data.DeviceId, success);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Bridge] 데이터 브릿지 오류: {ex.Message}");
            }
        }

        #endregion

        #region 수동 발행

        public async Task<bool> PublishDataNowAsync()
        {
            if (!_opcUaService.IsConnected || !_mqttService.IsConnected)
                return false;

            try
            {
                // 모든 디바이스 데이터 읽기
                var allData = await _opcUaService.ReadAllDevicesDataAsync();

                foreach (var kvp in allData)
                {
                    var data = kvp.Value;
                    var message = Esp32MqttMessage.FromEsp32Data(data);
                    await _mqttService.PublishEsp32DataAsync(message);
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Bridge] 수동 발행 오류: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region MQTT → OPC UA 명령 처리

        public async Task<bool> EnableCommandHandlingAsync()
        {
            return await _mqttService.StartCommandSubscriptionAsync();
        }

        private async void OnMqttCommandReceived(object? sender, MqttMessageReceivedEventArgs e)
        {
            Console.WriteLine($"[Bridge] 명령 수신 - Topic: {e.Topic}");

            try
            {
                var command = JsonSerializer.Deserialize<CommandMqttMessage>(e.Payload);
                if (command == null)
                    return;

                await HandleWriteCommand(command);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Bridge] 명령 처리 오류: {ex.Message}");
            }
        }

        private async Task HandleWriteCommand(CommandMqttMessage command)
        {
            if (!_opcUaService.IsConnected || command.Value == null)
                return;

            var deviceId = command.DeviceId;
            if (string.IsNullOrEmpty(deviceId))
            {
                Console.WriteLine("[Bridge] 명령에 DeviceId가 없습니다.");
                return;
            }

            Console.WriteLine($"[Bridge] 쓰기 명령 - Device: {deviceId}, Tag: {command.TagName}, Value: {command.Value}");

            // 태그 이름에 따라 적절한 쓰기 메서드 호출
            switch (command.TagName?.ToUpperInvariant())
            {
                case "TARGETA":
                case "TARGET_A":
                    if (bool.TryParse(command.Value.ToString(), out var boolValue))
                        await _opcUaService.WriteTargetAAsync(deviceId, boolValue);
                    break;

                case "CONTROL":
                    await _opcUaService.WriteControlAsync(deviceId, command.Value.ToString() ?? "");
                    break;

                case "STATE":
                    await _opcUaService.WriteStateAsync(deviceId, command.Value.ToString() ?? "");
                    break;

                default:
                    // 범용 태그 쓰기
                    await _opcUaService.WriteTagAsync(deviceId, command.TagName ?? "", command.Value);
                    break;
            }
        }

        #endregion

        #region 연결 상태 이벤트

        private void OnOpcUaConnectionChanged(object? sender, ConnectionChangedEventArgs e)
        {
            var isConnected = e.Status.IsConnected;
            Console.WriteLine($"[Bridge] OPC UA 연결 상태: {(isConnected ? "연결됨" : "연결 해제")}");
            OnStatusChanged($"OPC UA {(isConnected ? "연결됨" : "연결 해제")}");
        }

        private void OnMqttConnectionChanged(object? sender, MqttConnectionChangedEventArgs e)
        {
            Console.WriteLine($"[Bridge] MQTT 연결 상태: {(e.IsConnected ? "연결됨" : "연결 해제")}");
            OnStatusChanged($"MQTT {(e.IsConnected ? "연결됨" : "연결 해제")}");
        }

        private void OnStatusChanged(string message)
        {
            StatusChanged?.Invoke(this, new BridgeStatusChangedEventArgs
            {
                IsRunning = IsRunning,
                IsOpcUaConnected = IsOpcUaConnected,
                IsMqttConnected = IsMqttConnected,
                Message = message
            });
        }

        private void OnDataBridged(string deviceId, bool success)
        {
            DataBridged?.Invoke(this, new DataBridgedEventArgs
            {
                DeviceId = deviceId,
                MqttTopic = _mqttService.TopicConfig.GetDeviceDataTopic(deviceId),
                IsSuccess = success
            });
        }

        #endregion

        #region Dispose

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            // 이벤트 해제
            _opcUaService.DataChanged -= OnOpcUaDataChanged;
            _opcUaService.ConnectionChanged -= OnOpcUaConnectionChanged;
            _mqttService.ConnectionChanged -= OnMqttConnectionChanged;
            _mqttService.MessageReceived -= OnMqttCommandReceived;

            _bridgeLock.Dispose();

            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
