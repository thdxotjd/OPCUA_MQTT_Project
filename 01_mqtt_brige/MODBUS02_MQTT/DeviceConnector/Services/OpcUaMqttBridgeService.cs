using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DeviceConnector.Events;
using DeviceConnector.Interfaces;
using DeviceConnector.Models;

namespace DeviceConnector.Services
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
        private readonly ISTMYoloClientService? _stmYoloService;
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
        /// ESP32 전용 브릿지 생성자
        /// </summary>
        public OpcUaMqttBridgeService(
            IOpcUaClientService opcUaService,
            IMqttPublisherService mqttService)
        {
            _opcUaService = opcUaService ?? throw new ArgumentNullException(nameof(opcUaService));
            _mqttService = mqttService ?? throw new ArgumentNullException(nameof(mqttService));

            // OPC UA 이벤트 연결
            _opcUaService.DataChanged += OnEsp32DataChanged;
            _opcUaService.ConnectionStatusChanged += OnOpcUaConnectionChanged;

            // MQTT 이벤트 연결
            _mqttService.ConnectionChanged += OnMqttConnectionChanged;
            _mqttService.MessageReceived += OnMqttCommandReceived;
        }

        /// <summary>
        /// ESP32 + STM_yolo 브릿지 생성자
        /// </summary>
        public OpcUaMqttBridgeService(
            IOpcUaClientService opcUaService,
            ISTMYoloClientService stmYoloService,
            IMqttPublisherService mqttService)
            : this(opcUaService, mqttService)
        {
            _stmYoloService = stmYoloService ?? throw new ArgumentNullException(nameof(stmYoloService));

            // STM_yolo 이벤트 연결
            _stmYoloService.DataChanged += OnStmYoloDataChanged;
            _stmYoloService.ConnectionStatusChanged += OnOpcUaConnectionChanged;
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

                // 2. OPC UA 연결 (ESP32)
                Console.WriteLine("[Bridge] OPC UA 서버 연결 중 (ESP32)...");
                var opcUaConnected = await _opcUaService.ConnectAsync();
                if (!opcUaConnected)
                {
                    Console.WriteLine("[Bridge] OPC UA 연결 실패 (ESP32)");
                    // MQTT는 연결 유지, OPC UA만 실패
                }

                // 3. OPC UA 연결 (STM_yolo - 옵션)
                if (_stmYoloService != null)
                {
                    Console.WriteLine("[Bridge] OPC UA 서버 연결 중 (STM_yolo)...");
                    var stmConnected = await _stmYoloService.ConnectAsync();
                    if (!stmConnected)
                    {
                        Console.WriteLine("[Bridge] OPC UA 연결 실패 (STM_yolo)");
                    }
                }

                // 4. OPC UA 구독 시작 (ESP32)
                if (_opcUaService.IsConnected)
                {
                    Console.WriteLine("[Bridge] ESP32 데이터 구독 시작...");
                    await _opcUaService.StartSubscriptionAsync();
                }

                // 5. OPC UA 구독 시작 (STM_yolo)
                if (_stmYoloService?.IsConnected == true)
                {
                    Console.WriteLine("[Bridge] STM_yolo 데이터 구독 시작...");
                    await _stmYoloService.StartAllSubscriptionsAsync();
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
                if (_stmYoloService != null)
                {
                    await _stmYoloService.StopAllSubscriptionsAsync();
                    await _stmYoloService.DisconnectAsync();
                }

                await _opcUaService.StopSubscriptionAsync();
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

        private async void OnEsp32DataChanged(object? sender, DataChangedEventArgs e)
        {
            if (!_mqttService.IsConnected || e.Data == null)
                return;

            try
            {
                var esp32Data = e.Data;
                var message = new Esp32MqttMessage
                {
                    DeviceId = esp32Data.DeviceId,
                    PosX = esp32Data.PosX,
                    PosY = esp32Data.PosY,
                    PosTheta = esp32Data.PosTheta,
                    TargetA = esp32Data.TargetA,
                    Control = esp32Data.Control,
                    State = esp32Data.State,
                    IsGoodQuality = esp32Data.IsGoodQuality,
                    Timestamp = esp32Data.Timestamp
                };

                var success = await _mqttService.PublishEsp32DataAsync(message);

                if (success)
                {
                    Interlocked.Increment(ref _bridgedMessageCount);
                    OnDataBridged("ESP32", esp32Data.DeviceId, success);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Bridge] ESP32 데이터 브릿지 오류: {ex.Message}");
            }
        }

        private async void OnStmYoloDataChanged(object? sender, STMYoloDataChangedEventArgs e)
        {
            if (!_mqttService.IsConnected || e.Data == null)
                return;

            try
            {
                var stmData = e.Data;
                var message = new StmYoloMqttMessage
                {
                    DeviceId = stmData.DeviceId,
                    CurrentState = stmData.CurrentState,
                    CurrentSpeedMain = stmData.CurrentSpeedMain,
                    CurrentSpeedSort = stmData.CurrentSpeedSort,
                    CurrentSpeedLoad = stmData.CurrentSpeedLoad,
                    TargetState = stmData.TargetState,
                    TargetSpeedMain = stmData.TargetSpeedMain,
                    TargetSpeedSort = stmData.TargetSpeedSort,
                    TargetSpeedLoad = stmData.TargetSpeedLoad,
                    AgvSortArrived = stmData.AgvSortArrived,
                    AgvSortDeparted = stmData.AgvSortDeparted,
                    AgvLoadArrived = stmData.AgvLoadArrived,
                    AgvLoadDeparted = stmData.AgvLoadDeparted,
                    IsGoodQuality = stmData.IsGoodQuality,
                    Timestamp = stmData.Timestamp
                };

                var success = await _mqttService.PublishStmYoloDataAsync(message);

                if (success)
                {
                    Interlocked.Increment(ref _bridgedMessageCount);
                    OnDataBridged("STM_YOLO", stmData.DeviceId, success);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Bridge] STM_yolo 데이터 브릿지 오류: {ex.Message}");
            }
        }

        #endregion

        #region 수동 발행

        public async Task<bool> PublishEsp32DataNowAsync(string deviceId)
        {
            if (!_opcUaService.IsConnected || !_mqttService.IsConnected)
                return false;

            try
            {
                var data = await _opcUaService.ReadDeviceDataAsync();
                if (data == null)
                    return false;

                var message = new Esp32MqttMessage
                {
                    DeviceId = deviceId,
                    PosX = data.PosX,
                    PosY = data.PosY,
                    PosTheta = data.PosTheta,
                    TargetA = data.TargetA,
                    Control = data.Control,
                    State = data.State,
                    IsGoodQuality = data.IsGoodQuality,
                    Timestamp = DateTime.UtcNow
                };

                return await _mqttService.PublishEsp32DataAsync(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Bridge] ESP32 수동 발행 오류: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> PublishStmYoloDataNowAsync(string deviceId)
        {
            if (_stmYoloService == null || !_stmYoloService.IsConnected || !_mqttService.IsConnected)
                return false;

            try
            {
                var data = await _stmYoloService.ReadAllDataAsync();
                if (data == null)
                    return false;

                var message = new StmYoloMqttMessage
                {
                    DeviceId = deviceId,
                    CurrentState = data.CurrentState,
                    CurrentSpeedMain = data.CurrentSpeedMain,
                    CurrentSpeedSort = data.CurrentSpeedSort,
                    CurrentSpeedLoad = data.CurrentSpeedLoad,
                    TargetState = data.TargetState,
                    TargetSpeedMain = data.TargetSpeedMain,
                    TargetSpeedSort = data.TargetSpeedSort,
                    TargetSpeedLoad = data.TargetSpeedLoad,
                    AgvSortArrived = data.AgvSortArrived,
                    AgvSortDeparted = data.AgvSortDeparted,
                    AgvLoadArrived = data.AgvLoadArrived,
                    AgvLoadDeparted = data.AgvLoadDeparted,
                    IsGoodQuality = data.IsGoodQuality,
                    Timestamp = DateTime.UtcNow
                };

                return await _mqttService.PublishStmYoloDataAsync(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Bridge] STM_yolo 수동 발행 오류: {ex.Message}");
                return false;
            }
        }

        public async Task PublishAllDataNowAsync()
        {
            await PublishEsp32DataNowAsync("ESP32_01");

            if (_stmYoloService != null)
            {
                await PublishStmYoloDataNowAsync("STM_yolo_01");
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

                // 명령 타입에 따라 OPC UA 쓰기 수행
                switch (command.CommandType?.ToUpperInvariant())
                {
                    case "WRITE_ESP32":
                        await HandleEsp32WriteCommand(command);
                        break;

                    case "WRITE_STM_YOLO":
                        await HandleStmYoloWriteCommand(command);
                        break;

                    default:
                        Console.WriteLine($"[Bridge] 알 수 없는 명령 타입: {command.CommandType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Bridge] 명령 처리 오류: {ex.Message}");
            }
        }

        private async Task HandleEsp32WriteCommand(CommandMqttMessage command)
        {
            if (!_opcUaService.IsConnected || command.Value == null)
                return;

            Console.WriteLine($"[Bridge] ESP32 쓰기 명령 - Tag: {command.TagName}, Value: {command.Value}");
            await _opcUaService.WriteTagAsync(command.TagName, command.Value);
        }

        private async Task HandleStmYoloWriteCommand(CommandMqttMessage command)
        {
            if (_stmYoloService == null || !_stmYoloService.IsConnected || command.Value == null)
                return;

            Console.WriteLine($"[Bridge] STM_yolo 쓰기 명령 - Tag: {command.TagName}, Value: {command.Value}");

            // 태그 이름에 따라 적절한 쓰기 메서드 호출
            switch (command.TagName?.ToUpperInvariant())
            {
                case "TARGET_STATE":
                    if (long.TryParse(command.Value.ToString(), out var state))
                        await _stmYoloService.WriteTargetStateAsync(state);
                    break;

                case "TARGET_SPEED_MAIN":
                    if (long.TryParse(command.Value.ToString(), out var speedMain))
                        await _stmYoloService.WriteTargetSpeedMainAsync(speedMain);
                    break;

                case "TARGET_SPEED_SORT":
                    if (long.TryParse(command.Value.ToString(), out var speedSort))
                        await _stmYoloService.WriteTargetSpeedSortAsync(speedSort);
                    break;

                case "TARGET_SPEED_LOAD":
                    if (long.TryParse(command.Value.ToString(), out var speedLoad))
                        await _stmYoloService.WriteTargetSpeedLoadAsync(speedLoad);
                    break;

                default:
                    Console.WriteLine($"[Bridge] 알 수 없는 STM_yolo 태그: {command.TagName}");
                    break;
            }
        }

        #endregion

        #region 연결 상태 이벤트

        private void OnOpcUaConnectionChanged(object? sender, ConnectionStatusChangedEventArgs e)
        {
            Console.WriteLine($"[Bridge] OPC UA 연결 상태: {(e.IsConnected ? "연결됨" : "연결 해제")}");
            OnStatusChanged($"OPC UA {(e.IsConnected ? "연결됨" : "연결 해제")}");
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

        private void OnDataBridged(string deviceType, string deviceId, bool success)
        {
            DataBridged?.Invoke(this, new DataBridgedEventArgs
            {
                DeviceType = deviceType,
                DeviceId = deviceId,
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
            _opcUaService.DataChanged -= OnEsp32DataChanged;
            _opcUaService.ConnectionStatusChanged -= OnOpcUaConnectionChanged;

            if (_stmYoloService != null)
            {
                _stmYoloService.DataChanged -= OnStmYoloDataChanged;
                _stmYoloService.ConnectionStatusChanged -= OnOpcUaConnectionChanged;
            }

            _mqttService.ConnectionChanged -= OnMqttConnectionChanged;
            _mqttService.MessageReceived -= OnMqttCommandReceived;

            _bridgeLock.Dispose();

            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
