using DeviceConnector.Events;
using DeviceConnector.Models;
using DeviceConnector.Services;
using DeviceConnector.Mqtt.Models;
using DeviceConnector.Mqtt.Services;
using DeviceConnector.Mqtt.Events;

namespace DeviceConnector.Test;

/// <summary>
/// DeviceConnector v2.3 테스트 프로그램
/// OPC UA + MQTT 브릿지 테스트
/// </summary>
class Program
{
    private static OpcUaClientService? _opcUaClient;
    private static MqttPublisherService? _mqttClient;
    private static OpcUaMqttBridgeService? _bridge;
    private static readonly string DefaultDeviceId = "ESP32_01";

    static async Task Main(string[] args)
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║     DeviceConnector v2.3 - OPC UA + MQTT Bridge Test       ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // ═══════════════════════════════════════════════════════════
        // 1. OPC UA 설정
        // ═══════════════════════════════════════════════════════════
        var opcUaConfig = new OpcUaConnectionInfo
        {
            EndpointUrl = "opc.tcp://127.0.0.1:49320",
            AutoReconnect = true,
            ReconnectInterval = 5000,
            PublishingInterval = 500,
            SamplingInterval = 500
        };

        var deviceConfig = new DeviceTagConfig
        {
            DeviceId = DefaultDeviceId,
            ChannelName = "ModbusTCP",
            DeviceName = "ESP32_01",
            Tags = new DeviceTagNames
            {
                PosX = "POS_X",
                PosY = "POS_Y",
                PosTheta = "POS_T",
                TargetA = "TargetA",
                Control = "Control",
                State = "State"
            }
        };

        // ═══════════════════════════════════════════════════════════
        // 2. MQTT 설정
        // ═══════════════════════════════════════════════════════════
        var mqttConfig = new MqttConnectionInfo
        {
            BrokerAddress = "localhost",
            Port = 1883,
            ClientId = $"Bridge_{DateTime.Now:HHmmss}",
            AutoReconnect = true
        };

        var topicConfig = new MqttTopicConfig
        {
            BaseTopic = "factory/line1"
        };

        // ═══════════════════════════════════════════════════════════
        // 3. 설정 정보 출력
        // ═══════════════════════════════════════════════════════════
        Console.WriteLine("┌─────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ 설정 정보                                               │");
        Console.WriteLine("├─────────────────────────────────────────────────────────┤");
        Console.WriteLine($"│ OPC UA Server : {opcUaConfig.EndpointUrl,-39} │");
        Console.WriteLine($"│ MQTT Broker   : {mqttConfig.BrokerAddress}:{mqttConfig.Port,-32} │");
        Console.WriteLine($"│ MQTT Topic    : {topicConfig.BaseTopic,-39} │");
        Console.WriteLine($"│ Device ID     : {deviceConfig.DeviceId,-39} │");
        Console.WriteLine("└─────────────────────────────────────────────────────────┘");
        Console.WriteLine();

        Console.WriteLine("┌─────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ MQTT 토픽 구조                                          │");
        Console.WriteLine("├─────────────────────────────────────────────────────────┤");
        Console.WriteLine("│ factory/line1/                                          │");
        Console.WriteLine("│ ├── esp32/ESP32_01/data   ← ESP32 실시간 데이터         │");
        Console.WriteLine("│ ├── status                ← 연결 상태                   │");
        Console.WriteLine("│ └── command/#             ← SCADA 명령 수신             │");
        Console.WriteLine("└─────────────────────────────────────────────────────────┘");
        Console.WriteLine();

        // ═══════════════════════════════════════════════════════════
        // 4. 서비스 생성
        // ═══════════════════════════════════════════════════════════
        _opcUaClient = new OpcUaClientService(opcUaConfig);
        _opcUaClient.AddDeviceConfig(deviceConfig);

        _mqttClient = new MqttPublisherService(mqttConfig, topicConfig);

        _bridge = new OpcUaMqttBridgeService(_opcUaClient, _mqttClient);

        // ═══════════════════════════════════════════════════════════
        // 5. 이벤트 핸들러 등록
        // ═══════════════════════════════════════════════════════════
        _opcUaClient.ConnectionChanged += OnOpcUaConnectionChanged;
        _opcUaClient.DataChanged += OnOpcUaDataChanged;
        _opcUaClient.ErrorOccurred += OnOpcUaError;

        _mqttClient.ConnectionChanged += OnMqttConnectionChanged;
        _mqttClient.MessagePublished += OnMqttMessagePublished;
        _mqttClient.MessageReceived += OnMqttMessageReceived;

        _bridge.StatusChanged += OnBridgeStatusChanged;
        _bridge.DataBridged += OnDataBridged;

        // ═══════════════════════════════════════════════════════════
        // 6. 메뉴 실행
        // ═══════════════════════════════════════════════════════════
        await RunMenuLoop();

        // ═══════════════════════════════════════════════════════════
        // 7. 정리
        // ═══════════════════════════════════════════════════════════
        _bridge?.Dispose();
        _mqttClient?.Dispose();
        _opcUaClient?.Dispose();

        Console.WriteLine("\n프로그램을 종료합니다.");
    }

    static async Task RunMenuLoop()
    {
        while (true)
        {
            Console.WriteLine("\n═══════════════════════════════════════════════════════════");
            Console.WriteLine("  메뉴 선택");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("  [1] 브릿지 시작 (OPC UA + MQTT 연결 + 자동 발행)");
            Console.WriteLine("  [2] 브릿지 중지");
            Console.WriteLine("  [3] 수동 데이터 발행 (1회)");
            Console.WriteLine("  ───────────────────────────────────────────────────────");
            Console.WriteLine("  [4] OPC UA만 연결");
            Console.WriteLine("  [5] MQTT만 연결");
            Console.WriteLine("  [6] OPC UA 데이터 읽기");
            Console.WriteLine("  ───────────────────────────────────────────────────────");
            Console.WriteLine("  [7] MQTT 테스트 메시지 발행");
            Console.WriteLine("  [8] MQTT 명령 토픽 구독 시작");
            Console.WriteLine("  ───────────────────────────────────────────────────────");
            Console.WriteLine("  [9] 상태 확인");
            Console.WriteLine("  [0] 종료");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.Write("선택: ");

            var input = Console.ReadLine();

            try
            {
                switch (input)
                {
                    case "1":
                        await StartBridgeAsync();
                        break;
                    case "2":
                        await StopBridgeAsync();
                        break;
                    case "3":
                        await PublishDataNowAsync();
                        break;
                    case "4":
                        await ConnectOpcUaOnlyAsync();
                        break;
                    case "5":
                        await ConnectMqttOnlyAsync();
                        break;
                    case "6":
                        await ReadOpcUaDataAsync();
                        break;
                    case "7":
                        await PublishTestMessageAsync();
                        break;
                    case "8":
                        await StartCommandSubscriptionAsync();
                        break;
                    case "9":
                        ShowStatus();
                        break;
                    case "0":
                        return;
                    default:
                        Console.WriteLine("잘못된 선택입니다.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {ex.Message}");
            }
        }
    }

    #region 브릿지 기능

    static async Task StartBridgeAsync()
    {
        Console.WriteLine("\n[INFO] 브릿지 시작 중...");
        Console.WriteLine("[INFO] 1. MQTT 브로커 연결");
        Console.WriteLine("[INFO] 2. OPC UA 서버 연결");
        Console.WriteLine("[INFO] 3. 데이터 구독 시작");
        Console.WriteLine();

        var result = await _bridge!.StartAsync();

        if (result)
        {
            Console.WriteLine("\n╔════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  ✅ 브릿지 시작 성공!                                      ║");
            Console.WriteLine("║                                                            ║");
            Console.WriteLine("║  Mosquitto 구독자에서 데이터를 확인하세요:                 ║");
            Console.WriteLine("║  > mosquitto_sub -h localhost -t \"factory/line1/#\" -v     ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        }
        else
        {
            Console.WriteLine("[FAILED] 브릿지 시작 실패");
        }
    }

    static async Task StopBridgeAsync()
    {
        Console.WriteLine("\n[INFO] 브릿지 중지 중...");
        await _bridge!.StopAsync();
        Console.WriteLine("[SUCCESS] 브릿지 중지 완료");
    }

    static async Task PublishDataNowAsync()
    {
        if (!_bridge!.IsRunning)
        {
            Console.WriteLine("[WARN] 브릿지가 실행 중이 아닙니다. 먼저 [1]번으로 시작하세요.");
            return;
        }

        Console.WriteLine("\n[INFO] 수동 데이터 발행 중...");
        var result = await _bridge.PublishDataNowAsync();
        Console.WriteLine(result ? "[SUCCESS] 발행 완료" : "[FAILED] 발행 실패");
    }

    #endregion

    #region 개별 연결 기능

    static async Task ConnectOpcUaOnlyAsync()
    {
        Console.WriteLine("\n[INFO] OPC UA 서버에 연결 중...");
        var result = await _opcUaClient!.ConnectAsync();
        Console.WriteLine(result ? "[SUCCESS] OPC UA 연결 성공!" : "[FAILED] OPC UA 연결 실패");
    }

    static async Task ConnectMqttOnlyAsync()
    {
        Console.WriteLine("\n[INFO] MQTT 브로커에 연결 중...");
        var result = await _mqttClient!.ConnectAsync();
        Console.WriteLine(result ? "[SUCCESS] MQTT 연결 성공!" : "[FAILED] MQTT 연결 실패");
    }

    static async Task ReadOpcUaDataAsync()
    {
        if (!_opcUaClient!.IsConnected)
        {
            Console.WriteLine("[WARN] OPC UA가 연결되지 않았습니다.");
            return;
        }

        Console.WriteLine("\n[INFO] OPC UA 데이터 읽기 중...");
        var data = await _opcUaClient.ReadDeviceDataAsync(DefaultDeviceId);

        if (data != null)
        {
            Console.WriteLine("┌─────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ ESP32 Data (OPC UA에서 읽음)                            │");
            Console.WriteLine("├─────────────────────────────────────────────────────────┤");
            Console.WriteLine($"│ DeviceId : {data.DeviceId,-44} │");
            Console.WriteLine($"│ PosX     : {data.PosX,-44:F4} │");
            Console.WriteLine($"│ PosY     : {data.PosY,-44:F4} │");
            Console.WriteLine($"│ PosTheta : {data.PosTheta,-44:F4} │");
            Console.WriteLine($"│ TargetA  : {data.TargetA,-44} │");
            Console.WriteLine($"│ Control  : {data.Control,-44} │");
            Console.WriteLine($"│ State    : {data.State,-44} │");
            Console.WriteLine($"│ Quality  : {(data.IsGoodQuality ? "Good" : "Bad"),-44} │");
            Console.WriteLine("└─────────────────────────────────────────────────────────┘");
        }
        else
        {
            Console.WriteLine("[WARN] 데이터를 읽을 수 없습니다.");
        }
    }

    #endregion

    #region MQTT 기능

    static async Task PublishTestMessageAsync()
    {
        if (!_mqttClient!.IsConnected)
        {
            Console.WriteLine("[WARN] MQTT가 연결되지 않았습니다. [5]번으로 먼저 연결하세요.");
            return;
        }

        Console.WriteLine("\n[INFO] 테스트 메시지 발행 중...");

        var testMessage = new Esp32MqttMessage
        {
            DeviceId = DefaultDeviceId,
            ChannelName = "ModbusTCP",
            DeviceName = "ESP32_01",
            PosX = 1.23f,
            PosY = 4.56f,
            PosTheta = 0.789f,
            TargetA = true,
            Control = "TEST",
            State = "RUNNING",
            IsGoodQuality = true,
            Timestamp = DateTime.UtcNow
        };

        var result = await _mqttClient.PublishEsp32DataAsync(testMessage);
        Console.WriteLine(result ? "[SUCCESS] 테스트 메시지 발행 완료!" : "[FAILED] 발행 실패");
        
        if (result)
        {
            Console.WriteLine("\n[TIP] Mosquitto 구독자에서 확인:");
            Console.WriteLine("      mosquitto_sub -h localhost -t \"factory/line1/#\" -v");
        }
    }

    static async Task StartCommandSubscriptionAsync()
    {
        if (!_mqttClient!.IsConnected)
        {
            Console.WriteLine("[WARN] MQTT가 연결되지 않았습니다.");
            return;
        }

        Console.WriteLine("\n[INFO] 명령 토픽 구독 시작...");
        var result = await _mqttClient.StartCommandSubscriptionAsync();
        
        if (result)
        {
            Console.WriteLine("[SUCCESS] 명령 토픽 구독 시작!");
            Console.WriteLine("\n[TIP] 다른 터미널에서 명령 전송 테스트:");
            Console.WriteLine("      mosquitto_pub -h localhost -t \"factory/line1/command\" -m \"{\\\"deviceId\\\":\\\"ESP32_01\\\",\\\"tagName\\\":\\\"TargetA\\\",\\\"value\\\":true}\"");
        }
        else
        {
            Console.WriteLine("[FAILED] 구독 실패");
        }
    }

    #endregion

    #region 상태 확인

    static void ShowStatus()
    {
        Console.WriteLine("\n┌─────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ 현재 상태                                               │");
        Console.WriteLine("├─────────────────────────────────────────────────────────┤");
        Console.WriteLine($"│ Bridge Running  : {(_bridge?.IsRunning == true ? "✅ Yes" : "❌ No"),-36} │");
        Console.WriteLine($"│ OPC UA Connected: {(_opcUaClient?.IsConnected == true ? "✅ Yes" : "❌ No"),-36} │");
        Console.WriteLine($"│ MQTT Connected  : {(_mqttClient?.IsConnected == true ? "✅ Yes" : "❌ No"),-36} │");
        Console.WriteLine($"│ Bridged Messages: {_bridge?.BridgedMessageCount ?? 0,-36} │");
        Console.WriteLine("└─────────────────────────────────────────────────────────┘");
    }

    #endregion

    #region Event Handlers

    static void OnOpcUaConnectionChanged(object? sender, ConnectionChangedEventArgs e)
    {
        Console.WriteLine($"\n[OPC UA] 연결 상태: {e.Status.State}");
    }

    static void OnOpcUaDataChanged(object? sender, DataChangedEventArgs e)
    {
        Console.WriteLine($"\n[OPC UA] 데이터 변경: {e.DeviceId}");
        Console.WriteLine($"         Pos({e.Data.PosX:F3}, {e.Data.PosY:F3}, {e.Data.PosTheta:F3})");
    }

    static void OnOpcUaError(object? sender, ErrorOccurredEventArgs e)
    {
        Console.WriteLine($"\n[OPC UA ERROR] {e.Message}");
    }

    static void OnMqttConnectionChanged(object? sender, MqttConnectionChangedEventArgs e)
    {
        Console.WriteLine($"\n[MQTT] 연결 상태: {(e.IsConnected ? "연결됨" : "연결 해제")} - {e.BrokerAddress}");
    }

    static void OnMqttMessagePublished(object? sender, MqttMessagePublishedEventArgs e)
    {
        if (e.IsSuccess)
        {
            Console.WriteLine($"\n[MQTT] 발행 성공 → {e.Topic}");
        }
    }

    static void OnMqttMessageReceived(object? sender, MqttMessageReceivedEventArgs e)
    {
        Console.WriteLine($"\n[MQTT] 메시지 수신 ← {e.Topic}");
        Console.WriteLine($"       Payload: {e.Payload}");
    }

    static void OnBridgeStatusChanged(object? sender, BridgeStatusChangedEventArgs e)
    {
        Console.WriteLine($"\n[Bridge] {e.Message}");
    }

    static void OnDataBridged(object? sender, DataBridgedEventArgs e)
    {
        Console.WriteLine($"\n[Bridge] 데이터 브릿지 완료: {e.DeviceId} → {e.MqttTopic}");
    }

    #endregion
}
