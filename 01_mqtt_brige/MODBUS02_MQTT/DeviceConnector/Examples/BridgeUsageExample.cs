using System;
using System.Threading.Tasks;
using DeviceConnector.Interfaces;
using DeviceConnector.Models;
using DeviceConnector.Services;

namespace DeviceConnector.Examples
{
    /// <summary>
    /// OPC UA → MQTT 브릿지 사용 예제
    /// </summary>
    public class BridgeUsageExample
    {
        /// <summary>
        /// 기본 사용 예제
        /// </summary>
        public static async Task BasicUsageAsync()
        {
            // ============================================================
            // 1. 설정
            // ============================================================

            // OPC UA 연결 설정 (MODBUS02_CODE 사용)
            var opcUaConfig = new OpcUaConnectionInfo
            {
                ServerUrl = "opc.tcp://localhost:49320",  // KEPServerEX
                ApplicationName = "DeviceConnector",
                AutoReconnect = true
            };

            // MQTT 연결 설정
            var mqttConfig = new MqttConnectionInfo
            {
                BrokerAddress = "localhost",  // Mosquitto
                Port = 1883,
                ClientId = "OpcUaMqttBridge_01",
                AutoReconnect = true
            };

            // MQTT 토픽 설정
            var topicConfig = new MqttTopicConfig
            {
                BaseTopic = "factory/line1"  // factory/line1/esp32/ESP32_01/data
            };

            // ============================================================
            // 2. 서비스 생성
            // ============================================================

            // OPC UA 서비스 (MODBUS02_CODE에서 생성)
            // var opcUaService = new OpcUaClientService(opcUaConfig, deviceTagConfig);
            // var stmYoloService = new STMYoloClientService(opcUaConfig, stmTagConfig);

            // MQTT 퍼블리셔 서비스
            var mqttService = new MqttPublisherService(mqttConfig, topicConfig);

            // 브릿지 서비스
            // var bridgeService = new OpcUaMqttBridgeService(opcUaService, stmYoloService, mqttService);

            // ============================================================
            // 3. 이벤트 핸들러 등록
            // ============================================================

            mqttService.ConnectionChanged += (s, e) =>
            {
                Console.WriteLine($"MQTT 연결 상태: {(e.IsConnected ? "연결됨" : "연결 해제")}");
            };

            mqttService.MessagePublished += (s, e) =>
            {
                if (e.IsSuccess)
                    Console.WriteLine($"발행 성공: {e.Topic}");
            };

            // ============================================================
            // 4. 연결 및 실행
            // ============================================================

            // MQTT만 테스트
            Console.WriteLine("MQTT 브로커 연결 중...");
            var connected = await mqttService.ConnectAsync();

            if (connected)
            {
                Console.WriteLine("MQTT 연결 성공!");

                // 테스트 메시지 발행
                var testMessage = new Esp32MqttMessage
                {
                    DeviceId = "ESP32_01",
                    PosX = 1.5f,
                    PosY = 2.3f,
                    PosTheta = 0.785f,
                    TargetA = true,
                    Control = "AUTO",
                    State = "RUNNING",
                    IsGoodQuality = true
                };

                await mqttService.PublishEsp32DataAsync(testMessage);
                Console.WriteLine("테스트 메시지 발행 완료");
            }

            // 정리
            await mqttService.DisconnectAsync();
            mqttService.Dispose();
        }

        /// <summary>
        /// 브릿지 전체 사용 예제 (OPC UA + MQTT)
        /// </summary>
        public static async Task FullBridgeUsageAsync()
        {
            // ============================================================
            // 실제 사용 시 MODBUS02_CODE와 통합
            // ============================================================

            /*
            // 1. 설정
            var opcUaConfig = new OpcUaConnectionInfo
            {
                ServerUrl = "opc.tcp://localhost:49320",
                ApplicationName = "DeviceConnector"
            };

            var esp32TagConfig = new DeviceTagConfig
            {
                ChannelName = "ModbusTCP",
                DeviceName = "ESP32_01"
            };

            var stmTagConfig = new STMYoloTagConfig
            {
                ChannelName = "ModbusTCP",
                DeviceName = "STM_yolo"
            };

            var mqttConfig = new MqttConnectionInfo
            {
                BrokerAddress = "localhost",
                Port = 1883
            };

            // 2. 서비스 생성
            var opcUaService = new OpcUaClientService(opcUaConfig, esp32TagConfig);
            var stmYoloService = new STMYoloClientService(opcUaConfig, stmTagConfig);
            var mqttService = new MqttPublisherService(mqttConfig);
            var bridgeService = new OpcUaMqttBridgeService(opcUaService, stmYoloService, mqttService);

            // 3. 이벤트 등록
            bridgeService.StatusChanged += (s, e) =>
            {
                Console.WriteLine($"브릿지 상태: {e.Message}");
                Console.WriteLine($"  - OPC UA: {(e.IsOpcUaConnected ? "연결됨" : "연결 해제")}");
                Console.WriteLine($"  - MQTT: {(e.IsMqttConnected ? "연결됨" : "연결 해제")}");
            };

            bridgeService.DataBridged += (s, e) =>
            {
                Console.WriteLine($"데이터 브릿지: {e.DeviceType}/{e.DeviceId} → {(e.IsSuccess ? "성공" : "실패")}");
            };

            // 4. 브릿지 시작
            Console.WriteLine("브릿지 시작 중...");
            var started = await bridgeService.StartAsync();

            if (started)
            {
                Console.WriteLine("브릿지 실행 중. Enter 키를 누르면 종료합니다.");
                Console.ReadLine();
            }

            // 5. 브릿지 중지
            await bridgeService.StopAsync();
            bridgeService.Dispose();
            */
        }
    }
}
