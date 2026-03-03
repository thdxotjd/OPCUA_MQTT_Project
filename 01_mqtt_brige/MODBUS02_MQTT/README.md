# MODBUS02_CODE MQTT 확장

OPC UA 데이터를 MQTT로 발행하는 브릿지 확장 모듈입니다.

## 📁 프로젝트 구조

```
MODBUS02_MQTT/
└── DeviceConnector/
    ├── DeviceConnector.Mqtt.csproj      # 프로젝트 파일
    │
    ├── Models/
    │   ├── MqttConnectionInfo.cs        # MQTT 연결 설정
    │   ├── MqttTopicConfig.cs           # 토픽 설정
    │   └── MqttMessages.cs              # MQTT 메시지 모델
    │
    ├── Events/
    │   └── MqttEventArgs.cs             # MQTT 이벤트 정의
    │
    ├── Interfaces/
    │   ├── IMqttPublisherService.cs     # MQTT 퍼블리셔 인터페이스
    │   ├── IOpcUaMqttBridgeService.cs   # 브릿지 서비스 인터페이스
    │   └── MODBUS02_CODE_Interfaces.cs  # 기존 인터페이스 참조 (스텁)
    │
    ├── Services/
    │   ├── MqttPublisherService.cs      # MQTT 퍼블리셔 구현
    │   └── OpcUaMqttBridgeService.cs    # 브릿지 서비스 구현
    │
    ├── Extensions/
    │   └── MqttServiceCollectionExtensions.cs  # DI 확장
    │
    └── Examples/
        └── BridgeUsageExample.cs        # 사용 예제
```

## 🔧 설치

### 1. NuGet 패키지 설치

```bash
dotnet add package MQTTnet
dotnet add package System.Text.Json
dotnet add package Microsoft.Extensions.DependencyInjection.Abstractions
```

### 2. MODBUS02_CODE 프로젝트 참조

`.csproj` 파일에 추가:
```xml
<ItemGroup>
  <ProjectReference Include="..\MODBUS02_CODE\DeviceConnector\DeviceConnector.csproj" />
</ItemGroup>
```

## 📊 아키텍처

```
┌─────────────────┐     ┌──────────────────┐     ┌─────────────────┐
│   KEPServerEX   │────▶│  OPC UA Service  │────▶│                 │
│   (OPC UA)      │     │  (MODBUS02_CODE) │     │   Bridge        │
└─────────────────┘     └──────────────────┘     │   Service       │
                                                 │                 │
┌─────────────────┐     ┌──────────────────┐     │                 │
│   ESP32/PLC     │────▶│  Modbus TCP      │     │                 │
│   (현장 장비)    │     │                  │     └────────┬────────┘
└─────────────────┘     └──────────────────┘              │
                                                          ▼
                        ┌──────────────────┐     ┌─────────────────┐
                        │   MQTT Broker    │◀────│  MQTT Publisher │
                        │   (Mosquitto)    │     │                 │
                        └────────┬─────────┘     └─────────────────┘
                                 │
              ┌──────────────────┼──────────────────┐
              ▼                  ▼                  ▼
        ┌──────────┐      ┌──────────┐      ┌──────────┐
        │  SCADA   │      │ Dashboard│      │  Cloud   │
        │          │      │ (Grafana)│      │  (AWS)   │
        └──────────┘      └──────────┘      └──────────┘
```

## 🚀 사용법

### 기본 사용 (MQTT만 테스트)

```csharp
// MQTT 설정
var mqttConfig = new MqttConnectionInfo
{
    BrokerAddress = "localhost",
    Port = 1883,
    ClientId = "Bridge_01"
};

var topicConfig = new MqttTopicConfig
{
    BaseTopic = "factory/line1"
};

// MQTT 서비스 생성 및 연결
var mqttService = new MqttPublisherService(mqttConfig, topicConfig);
await mqttService.ConnectAsync();

// 데이터 발행
var message = new Esp32MqttMessage
{
    DeviceId = "ESP32_01",
    PosX = 1.5f,
    PosY = 2.3f,
    PosTheta = 0.785f
};
await mqttService.PublishEsp32DataAsync(message);
```

### 브릿지 사용 (OPC UA + MQTT)

```csharp
// MODBUS02_CODE 서비스
var opcUaService = new OpcUaClientService(opcUaConfig, tagConfig);
var stmYoloService = new STMYoloClientService(opcUaConfig, stmTagConfig);

// MQTT 서비스
var mqttService = new MqttPublisherService(mqttConfig, topicConfig);

// 브릿지 서비스 생성
var bridge = new OpcUaMqttBridgeService(opcUaService, stmYoloService, mqttService);

// 이벤트 등록
bridge.DataBridged += (s, e) =>
{
    Console.WriteLine($"브릿지됨: {e.DeviceType}/{e.DeviceId}");
};

// 브릿지 시작 (OPC UA 연결 + MQTT 연결 + 구독 시작)
await bridge.StartAsync();

// ... 실행 중 ...

// 브릿지 중지
await bridge.StopAsync();
```

### DI 컨테이너 사용

```csharp
services.AddMqttPublisher(config =>
{
    config.BrokerAddress = "localhost";
    config.Port = 1883;
}, topic =>
{
    topic.BaseTopic = "factory/line1";
});

services.AddOpcUaMqttBridge();
```

## 📡 MQTT 토픽 구조

```
{BaseTopic}/
├── esp32/
│   └── {DeviceId}/
│       └── data          # ESP32 데이터
├── stm_yolo/
│   └── {DeviceId}/
│       └── data          # STM_yolo 데이터
├── status                # 연결 상태
└── command/              # SCADA → 장비 명령
    └── #
```

### 메시지 예시 (JSON)

**ESP32 데이터:**
```json
{
  "timestamp": "2026-01-29T08:30:00Z",
  "deviceId": "ESP32_01",
  "messageType": "ESP32_DATA",
  "posX": 1.5,
  "posY": 2.3,
  "posTheta": 0.785,
  "targetA": true,
  "control": "AUTO",
  "state": "RUNNING",
  "isGoodQuality": true
}
```

**STM_yolo 데이터:**
```json
{
  "timestamp": "2026-01-29T08:30:00Z",
  "deviceId": "STM_yolo_01",
  "messageType": "STM_YOLO_DATA",
  "currentState": 1,
  "currentSpeedMain": 100,
  "currentSpeedSort": 50,
  "currentSpeedLoad": 30,
  "targetState": 1,
  "targetSpeedMain": 100,
  "targetSpeedSort": 50,
  "targetSpeedLoad": 30,
  "agvSortArrived": false,
  "agvSortDeparted": false,
  "agvLoadArrived": false,
  "agvLoadDeparted": false,
  "isGoodQuality": true
}
```

## 🔄 SCADA → 장비 명령

MQTT 명령 토픽으로 메시지를 발행하면 OPC UA를 통해 장비에 쓰기:

```json
{
  "timestamp": "2026-01-29T08:30:00Z",
  "deviceId": "STM_yolo_01",
  "messageType": "COMMAND",
  "commandType": "WRITE_STM_YOLO",
  "tagName": "TARGET_SPEED_MAIN",
  "value": 150
}
```

## 🧪 Mosquitto 테스트

```bash
# 구독자 (터미널 1)
mosquitto_sub -h localhost -t "factory/line1/#" -v

# 발행자 (터미널 2)
mosquitto_pub -h localhost -t "factory/line1/esp32/ESP32_01/data" -m '{"deviceId":"ESP32_01","posX":1.5}'
```

## 📌 주의사항

1. **MODBUS02_CODE 필수**: 이 확장은 MODBUS02_CODE의 OPC UA 서비스에 의존합니다.
2. **Mosquitto 설치 필요**: MQTT 브로커가 먼저 실행 중이어야 합니다.
3. **KEPServerEX 실행 필요**: OPC UA 서버가 먼저 실행 중이어야 합니다.

