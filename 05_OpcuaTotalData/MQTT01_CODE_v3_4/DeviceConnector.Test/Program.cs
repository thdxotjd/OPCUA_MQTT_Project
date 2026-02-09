using DeviceConnector.Models;
using DeviceConnector.Services;
using DeviceConnector.Mqtt.Models;
using DeviceConnector.Mqtt.Services;
using DeviceConnector.Mqtt.Events;
using DeviceConnector.Events;

namespace DeviceConnector.Test;

/// <summary>
/// DeviceConnector v3.0 테스트 프로그램
/// MqttTest(Simulator) + STM 채널 통합 지원
/// OPC UA → MQTT 브릿지 + SCADA 제어
/// </summary>
class Program
{
    private static UnifiedOpcUaClientService? _opcUaClient;
    private static UnifiedOpcUaMqttBridgeService? _bridge;
    private static ProcessManager? _processManager;

    static async Task Main(string[] args)
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║     DeviceConnector v3.0 - Multi-Device Bridge             ║");
        Console.WriteLine("║     MqttTest(Simulator) + STM 채널 통합                    ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // ═══════════════════════════════════════════════════════════
        // 0. ProcessManager 초기화
        // ═══════════════════════════════════════════════════════════
        _processManager = new ProcessManager();

        Console.Write("외부 서비스(Mosquitto, InfluxDB, Node-RED)를 시작하시겠습니까? (y/n): ");
        if (Console.ReadLine()?.ToLower() == "y")
        {
            await _processManager.StartAllServicesAsync();
            Console.WriteLine("\n서비스 시작 완료. 잠시 대기...");
            await Task.Delay(2000);
        }
        Console.Clear();

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

        // ═══════════════════════════════════════════════════════════
        // 2. 디바이스 설정 (MqttTest + STM + ModbusTCP)
        // ═══════════════════════════════════════════════════════════
        var deviceConfigs = new List<UnifiedDeviceConfig>
        {
            DeviceConfigFactory.CreateSimulatorConfig(),  // MqttTest.SimDevice01
            DeviceConfigFactory.CreateSTMYoloConfig(),    // STM.Stm_yolo
            DeviceConfigFactory.CreateESP32Config()       // ModbusTCP.ESP32_01
        };

        // ═══════════════════════════════════════════════════════════
        // 3. MQTT 설정
        // ═══════════════════════════════════════════════════════════
        var mqttConfig = new MqttConnectionInfo
        {
            BrokerAddress = "localhost",
            Port = 1883,
            ClientId = $"Bridge_{DateTime.Now:HHmmss}",
            AutoReconnect = true
        };

        var topicConfig = new UnifiedMqttTopicConfig
        {
            BaseTopic = "factory/line1"
        };

        // ═══════════════════════════════════════════════════════════
        // 4. 설정 정보 출력
        // ═══════════════════════════════════════════════════════════
        PrintConfiguration(opcUaConfig, mqttConfig, topicConfig, deviceConfigs);

        // ═══════════════════════════════════════════════════════════
        // 5. 서비스 생성
        // ═══════════════════════════════════════════════════════════
        _opcUaClient = new UnifiedOpcUaClientService(opcUaConfig);
        _opcUaClient.AddDeviceConfigs(deviceConfigs);

        _bridge = new UnifiedOpcUaMqttBridgeService(_opcUaClient, mqttConfig, topicConfig);

        // ═══════════════════════════════════════════════════════════
        // 6. 이벤트 핸들러 등록
        // ═══════════════════════════════════════════════════════════
        _opcUaClient.ConnectionChanged += OnOpcUaConnectionChanged;
        _opcUaClient.DataChanged += OnOpcUaDataChanged;
        _opcUaClient.ErrorOccurred += OnOpcUaError;

        _bridge.StatusChanged += OnBridgeStatusChanged;
        _bridge.DataBridged += OnDataBridged;
        _bridge.CommandReceived += OnCommandReceived;

        // ═══════════════════════════════════════════════════════════
        // 7. 메뉴 실행
        // ═══════════════════════════════════════════════════════════
        await RunMenuLoop();

        // ═══════════════════════════════════════════════════════════
        // 8. 정리
        // ═══════════════════════════════════════════════════════════
        _bridge?.Dispose();
        _opcUaClient?.Dispose();

        Console.Write("\n외부 서비스도 종료하시겠습니까? (y/n): ");
        if (Console.ReadLine()?.ToLower() == "y")
        {
            _processManager.KillAllRelatedProcesses();
        }

        _processManager?.Dispose();
        Console.WriteLine("\n프로그램을 종료합니다.");
    }

    static void PrintConfiguration(OpcUaConnectionInfo opcUa, MqttConnectionInfo mqtt,
                                   UnifiedMqttTopicConfig topic, List<UnifiedDeviceConfig> devices)
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║     DeviceConnector v3.0 - Multi-Device Bridge             ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        Console.WriteLine("┌─────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ 연결 설정                                               │");
        Console.WriteLine("├─────────────────────────────────────────────────────────┤");
        Console.WriteLine($"│ OPC UA Server : {opcUa.EndpointUrl,-39} │");
        Console.WriteLine($"│ MQTT Broker   : {mqtt.BrokerAddress}:{mqtt.Port,-32} │");
        Console.WriteLine($"│ Base Topic    : {topic.BaseTopic,-39} │");
        Console.WriteLine("└─────────────────────────────────────────────────────────┘");
        Console.WriteLine();

        Console.WriteLine("┌─────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ 등록된 디바이스                                         │");
        Console.WriteLine("├─────────────────────────────────────────────────────────┤");
        foreach (var device in devices)
        {
            Console.WriteLine($"│ {device.DeviceId,-15} │ {device.DeviceType,-10} │ {device.ChannelName}.{device.DeviceName,-15} │");
        }
        Console.WriteLine("└─────────────────────────────────────────────────────────┘");
        Console.WriteLine();

        Console.WriteLine("┌─────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ MQTT 토픽 구조                                          │");
        Console.WriteLine("├─────────────────────────────────────────────────────────┤");
        Console.WriteLine("│ factory/line1/                                          │");
        Console.WriteLine("│ ├── simulator/SimDevice01/data    ← 시뮬레이터 데이터   │");
        Console.WriteLine("│ ├── simulator/SimDevice01/command ← 시뮬레이터 명령     │");
        Console.WriteLine("│ ├── stmyolo/STM_yolo/data         ← STM 데이터          │");
        Console.WriteLine("│ ├── stmyolo/STM_yolo/command      ← STM 명령            │");
        Console.WriteLine("│ └── status                        ← 연결 상태           │");
        Console.WriteLine("└─────────────────────────────────────────────────────────┘");
        Console.WriteLine();
    }

    static async Task RunMenuLoop()
    {
        while (true)
        {
            PrintMenu();
            Console.Write("선택: ");

            var input = Console.ReadLine();

            try
            {
                switch (input?.ToUpper())
                {
                    case "1":
                        await StartBridgeAsync();
                        break;
                    case "2":
                        await StopBridgeAsync();
                        break;
                    case "3":
                        await PublishAllDataAsync();
                        break;
                    case "4":
                        await ConnectOpcUaOnlyAsync();
                        break;
                    case "5":
                        await ReadAllDeviceDataAsync();
                        break;
                    case "6":
                        await TestSimulatorControlAsync();
                        break;
                    case "7":
                        await TestSTMControlAsync();
                        break;
                    case "S":
                        ShowAllStatus();
                        break;
                    case "Q":
                        return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[ERROR] {ex.Message}");
            }

            Console.WriteLine("\n아무 키나 누르면 계속...");
            Console.ReadKey();
            Console.Clear();
        }
    }

    static void PrintMenu()
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                    메뉴 선택                               ║");
        Console.WriteLine("╠════════════════════════════════════════════════════════════╣");
        Console.WriteLine("║  [브릿지]                                                  ║");
        Console.WriteLine("║    1. 브릿지 시작 (OPC UA + MQTT 연결)                     ║");
        Console.WriteLine("║    2. 브릿지 중지                                          ║");
        Console.WriteLine("║    3. 모든 데이터 즉시 발행                                ║");
        Console.WriteLine("╠════════════════════════════════════════════════════════════╣");
        Console.WriteLine("║  [개별 연결]                                               ║");
        Console.WriteLine("║    4. OPC UA만 연결                                        ║");
        Console.WriteLine("║    5. 모든 디바이스 데이터 읽기                            ║");
        Console.WriteLine("╠════════════════════════════════════════════════════════════╣");
        Console.WriteLine("║  [제어 테스트]                                             ║");
        Console.WriteLine("║    6. Simulator 제어 테스트 (MotorStart/Stop)              ║");
        Console.WriteLine("║    7. STM 제어 테스트 (TargetState/Speed)                  ║");
        Console.WriteLine("╠════════════════════════════════════════════════════════════╣");
        Console.WriteLine("║    S. 상태 확인    Q. 종료                                 ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
    }

    #region 브릿지 기능

    static async Task StartBridgeAsync()
    {
        Console.WriteLine("\n[INFO] 브릿지 시작 중...");
        var result = await _bridge!.StartAsync();

        if (result)
        {
            Console.WriteLine();
            Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  ✅ 브릿지 시작 완료!                                      ║");
            Console.WriteLine("║                                                            ║");
            Console.WriteLine("║  MQTT 구독 확인:                                           ║");
            Console.WriteLine("║  mosquitto_sub -h localhost -t \"factory/line1/#\" -v        ║");
            Console.WriteLine("║                                                            ║");
            Console.WriteLine("║  Simulator 명령 테스트:                                    ║");
            Console.WriteLine("║  mosquitto_pub -h localhost \\                              ║");
            Console.WriteLine("║    -t \"factory/line1/simulator/SimDevice01/command\" \\      ║");
            Console.WriteLine("║    -m '{\"deviceId\":\"SimDevice01\",\"tagName\":\"MotorStart\",\"value\":true}'  ║");
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

    static async Task PublishAllDataAsync()
    {
        if (!_bridge!.IsRunning)
        {
            Console.WriteLine("[WARN] 브릿지가 실행 중이 아닙니다.");
            return;
        }

        Console.WriteLine("\n[INFO] 모든 디바이스 데이터 발행 중...");
        var result = await _bridge.PublishAllDataNowAsync();
        Console.WriteLine(result ? "[SUCCESS] 발행 완료" : "[FAILED] 발행 실패");
    }

    #endregion

    #region 개별 연결

    static async Task ConnectOpcUaOnlyAsync()
    {
        Console.WriteLine("\n[INFO] OPC UA 서버에 연결 중...");
        var result = await _opcUaClient!.ConnectAsync();
        Console.WriteLine(result ? "[SUCCESS] OPC UA 연결 성공!" : "[FAILED] OPC UA 연결 실패");
    }

    static async Task ReadAllDeviceDataAsync()
    {
        if (!_opcUaClient!.IsConnected)
        {
            Console.WriteLine("[WARN] OPC UA가 연결되지 않았습니다. [4]번으로 먼저 연결하세요.");
            return;
        }

        Console.WriteLine("\n[INFO] 모든 디바이스 데이터 읽기 중...");
        var allData = await _opcUaClient.ReadAllDeviceDataAsync();

        foreach (var kv in allData)
        {
            var data = kv.Value;
            Console.WriteLine();
            Console.WriteLine($"┌─────────────────────────────────────────────────────────┐");
            Console.WriteLine($"│ {data.DeviceId} ({data.DeviceType})");
            Console.WriteLine($"├─────────────────────────────────────────────────────────┤");
            Console.WriteLine($"│ Channel: {data.ChannelName}, Device: {data.DeviceName}");
            Console.WriteLine($"│ Quality: {(data.IsGoodQuality ? "Good ✅" : "Bad ❌")}");
            Console.WriteLine($"├─────────────────────────────────────────────────────────┤");
            
            foreach (var tag in data.TagValues)
            {
                Console.WriteLine($"│ {tag.Key,-20} : {tag.Value,-30} │");
            }
            Console.WriteLine($"└─────────────────────────────────────────────────────────┘");
        }
    }

    #endregion

    #region 제어 테스트

    static async Task TestSimulatorControlAsync()
    {
        if (!_opcUaClient!.IsConnected)
        {
            Console.WriteLine("[WARN] OPC UA가 연결되지 않았습니다.");
            return;
        }

        Console.WriteLine("\n┌─────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ Simulator 제어 테스트                                   │");
        Console.WriteLine("├─────────────────────────────────────────────────────────┤");
        Console.WriteLine("│ 1. MotorStart = true                                    │");
        Console.WriteLine("│ 2. MotorStart = false                                   │");
        Console.WriteLine("│ 3. MotorStop = true                                     │");
        Console.WriteLine("│ 4. SpeedSetpoint 설정                                   │");
        Console.WriteLine("│ 0. 취소                                                 │");
        Console.WriteLine("└─────────────────────────────────────────────────────────┘");
        Console.Write("선택: ");

        var choice = Console.ReadLine();
        bool result = false;

        switch (choice)
        {
            case "1":
                result = await _opcUaClient.WriteTagAsync("SimDevice01", "MotorStart", true);
                break;
            case "2":
                result = await _opcUaClient.WriteTagAsync("SimDevice01", "MotorStart", false);
                break;
            case "3":
                result = await _opcUaClient.WriteTagAsync("SimDevice01", "MotorStop", true);
                break;
            case "4":
                Console.Write("SpeedSetpoint 값 입력 (0-65535): ");
                if (ushort.TryParse(Console.ReadLine(), out var speed))
                {
                    result = await _opcUaClient.WriteTagAsync("SimDevice01", "SpeedSetpoint", speed);
                }
                break;
            case "0":
                return;
        }

        Console.WriteLine(result ? "[SUCCESS] 쓰기 완료!" : "[FAILED] 쓰기 실패");
    }

    static async Task TestSTMControlAsync()
    {
        if (!_opcUaClient!.IsConnected)
        {
            Console.WriteLine("[WARN] OPC UA가 연결되지 않았습니다.");
            return;
        }

        Console.WriteLine("\n┌─────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ STM Yolo 제어 테스트                                    │");
        Console.WriteLine("├─────────────────────────────────────────────────────────┤");
        Console.WriteLine("│ 1. TargetState 설정                                     │");
        Console.WriteLine("│ 2. TargetSpeedMain 설정                                 │");
        Console.WriteLine("│ 3. TargetSpeedSort 설정                                 │");
        Console.WriteLine("│ 4. TargetSpeedLoad 설정                                 │");
        Console.WriteLine("│ 5. AgvSortArrived = true                                │");
        Console.WriteLine("│ 6. AgvLoadArrived = true                                │");
        Console.WriteLine("│ 0. 취소                                                 │");
        Console.WriteLine("└─────────────────────────────────────────────────────────┘");
        Console.Write("선택: ");

        var choice = Console.ReadLine();
        bool result = false;

        switch (choice)
        {
            case "1":
                Console.Write("TargetState 값 입력: ");
                if (long.TryParse(Console.ReadLine(), out var state))
                {
                    result = await _opcUaClient.WriteTagAsync("STM_yolo", "TargetState", state);
                }
                break;
            case "2":
                Console.Write("TargetSpeedMain 값 입력: ");
                if (long.TryParse(Console.ReadLine(), out var speedMain))
                {
                    result = await _opcUaClient.WriteTagAsync("STM_yolo", "TargetSpeedMain", speedMain);
                }
                break;
            case "3":
                Console.Write("TargetSpeedSort 값 입력: ");
                if (long.TryParse(Console.ReadLine(), out var speedSort))
                {
                    result = await _opcUaClient.WriteTagAsync("STM_yolo", "TargetSpeedSort", speedSort);
                }
                break;
            case "4":
                Console.Write("TargetSpeedLoad 값 입력: ");
                if (long.TryParse(Console.ReadLine(), out var speedLoad))
                {
                    result = await _opcUaClient.WriteTagAsync("STM_yolo", "TargetSpeedLoad", speedLoad);
                }
                break;
            case "5":
                result = await _opcUaClient.WriteTagAsync("STM_yolo", "AgvSortArrived", true);
                break;
            case "6":
                result = await _opcUaClient.WriteTagAsync("STM_yolo", "AgvLoadArrived", true);
                break;
            case "0":
                return;
        }

        Console.WriteLine(result ? "[SUCCESS] 쓰기 완료!" : "[FAILED] 쓰기 실패");
    }

    #endregion

    #region 상태 확인

    static void ShowAllStatus()
    {
        Console.WriteLine();

        // 외부 서비스 상태
        _processManager!.PrintStatus();

        Console.WriteLine();

        // 브릿지 상태
        Console.WriteLine("┌─────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ 브릿지 상태                                             │");
        Console.WriteLine("├─────────────────────────────────────────────────────────┤");
        Console.WriteLine($"│ Bridge Running  : {(_bridge?.IsRunning == true ? "✅ Yes" : "❌ No"),-36} │");
        Console.WriteLine($"│ OPC UA Connected: {(_opcUaClient?.IsConnected == true ? "✅ Yes" : "❌ No"),-36} │");
        Console.WriteLine($"│ MQTT Connected  : {(_bridge?.IsMqttConnected == true ? "✅ Yes" : "❌ No"),-36} │");
        Console.WriteLine($"│ Bridged Messages: {_bridge?.BridgedMessageCount ?? 0,-36} │");
        Console.WriteLine($"│ Commands Processed: {_bridge?.CommandsProcessedCount ?? 0,-34} │");
        Console.WriteLine("└─────────────────────────────────────────────────────────┘");

        // 등록된 디바이스
        Console.WriteLine();
        Console.WriteLine("┌─────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ 등록된 디바이스                                         │");
        Console.WriteLine("├─────────────────────────────────────────────────────────┤");
        if (_opcUaClient != null)
        {
            foreach (var config in _opcUaClient.DeviceConfigs.Values)
            {
                var tagCount = config.Tags.Count;
                Console.WriteLine($"│ {config.DeviceId,-15} │ {config.DeviceType,-10} │ Tags: {tagCount,-5} │");
            }
        }
        Console.WriteLine("└─────────────────────────────────────────────────────────┘");
    }

    #endregion

    #region Event Handlers

    static void OnOpcUaConnectionChanged(object? sender, ConnectionChangedEventArgs e)
    {
        Console.WriteLine($"\n[OPC UA] 연결 상태: {e.Status.State}");
    }

    static void OnOpcUaDataChanged(object? sender, UnifiedDataChangedEventArgs e)
    {
        // 데이터 변경 시 간략 출력 (너무 많으면 주석 처리)
        // Console.WriteLine($"\n[OPC UA] 데이터 변경: {e.DeviceId}");
    }

    static void OnOpcUaError(object? sender, ErrorOccurredEventArgs e)
    {
        Console.WriteLine($"\n[OPC UA ERROR] {e.Message}");
    }

    static void OnBridgeStatusChanged(object? sender, BridgeStatusChangedEventArgs e)
    {
        Console.WriteLine($"\n[Bridge] {e.Message}");
    }

    static void OnDataBridged(object? sender, UnifiedDataBridgedEventArgs e)
    {
        // 화면 가림 방지 - 로그 출력 제거
        // Console.WriteLine($"\n[Bridge] 데이터 브릿지: {e.DeviceId} → {e.MqttTopic}");
    }

    static void OnCommandReceived(object? sender, MqttCommandReceivedEventArgs e)
    {
        Console.WriteLine($"\n[Bridge] 명령 수신: {e.DeviceId}.{e.TagName} = {e.Value}");
    }

    #endregion
}
