# MODBUS02_CODE (DeviceConnector v2.3)

ESP32 ModbusTCP - KEPServerEX OPC UA Client Library with **MQTT Bridge**

## 📁 프로젝트 구조

```
DeviceConnector/
├── DeviceConnector.csproj
│
├── Models/
│   ├── ESP32Data.cs                    # ESP32 데이터 모델
│   ├── ConnectionStatus.cs             # 연결 상태
│   └── OpcUaConnectionInfo.cs          # OPC UA 연결 설정
│
├── Events/
│   └── DataChangedEventArgs.cs         # OPC UA 이벤트
│
├── Interfaces/
│   └── IOpcUaClientService.cs          # OPC UA 서비스 인터페이스
│
├── Services/
│   └── OpcUaClientService.cs           # OPC UA 클라이언트 구현
│
├── Extensions/
│   └── ServiceCollectionExtensions.cs  # DI 확장 (OPC UA + MQTT)
│
└── Mqtt/                               # ★ MQTT 브릿지 (v2.3 추가)
    ├── Models/
    │   ├── MqttConnectionInfo.cs       # MQTT 연결 설정
    │   ├── MqttTopicConfig.cs          # 토픽 설정
    │   └── MqttMessages.cs             # MQTT 메시지 모델
    │
    ├── Events/
    │   └── MqttEventArgs.cs            # MQTT 이벤트
    │
    ├── Interfaces/
    │   ├── IMqttPublisherService.cs    # MQTT 퍼블리셔 인터페이스
    │   └── IOpcUaMqttBridgeService.cs  # 브릿지 인터페이스
    │
    └── Services/
        ├── MqttPublisherService.cs     # MQTT 퍼블리셔 구현
        └── OpcUaMqttBridgeService.cs   # OPC UA→MQTT 브릿지
```

## 🚀 아키텍처

```
┌─────────────────┐     ┌──────────────────┐     ┌─────────────────┐
│   KEPServerEX   │────▶│  OPC UA Service  │────▶│                 │
│   (OPC UA)      │     │                  │     │   Bridge        │
└─────────────────┘     └──────────────────┘     │   Service       │
                                                 │                 │
┌─────────────────┐     ┌──────────────────┐     │                 │
│   ESP32/PLC     │────▶│  Modbus TCP      │     └────────┬────────┘
│   (현장 장비)    │     │                  │              │
└─────────────────┘     └──────────────────┘              ▼
                                                 ┌─────────────────┐
                        ┌──────────────────┐     │  MQTT Publisher │
                        │   MQTT Broker    │◀────│                 │
                        │   (Mosquitto)    │     └─────────────────┘
                        └────────┬─────────┘
                                 │
              ┌──────────────────┼──────────────────┐
              ▼                  ▼                  ▼
        ┌──────────┐      ┌──────────┐      ┌──────────┐
        │  SCADA   │      │ Dashboard│      │  Cloud   │
        │          │      │ (Grafana)│      │  (AWS)   │
        └──────────┘      └──────────┘      └──────────┘
```

## 🔧 설치

### NuGet 패키지 (자동 설치됨)
- OPCFoundation.NetStandard.Opc.Ua.Client
- MQTTnet
- System.Text.Json
- Microsoft.Extensions.DependencyInjection.Abstractions

## 📖 사용법

### 1. 기본 사용 (OPC UA + MQTT 브릿지)

```csharp
using DeviceConnector.Models;
using DeviceConnector.Mqtt.Models;
using DeviceConnector.Mqtt.Services;
using DeviceConnector.Services;

// OPC UA 설정
var opcUaConfig = new OpcUaConnectionInfo
{
    ServerUrl = "opc.tcp://localhost:49320",
    ApplicationName = "DeviceConnector"
};

// 디바이스 태그 설정
var tagConfig = new DeviceTagConfig
{
    DeviceId = "ESP32_01",
    ChannelName = "ModbusTCP",
    DeviceName = "ESP32_01"
};

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

// 서비스 생성
var opcUaService = new OpcUaClientService(opcUaConfig);
opcUaService.AddDeviceConfig(tagConfig);

var mqttService = new MqttPublisherService(mqttConfig, topicConfig);
var bridgeService = new OpcUaMqttBridgeService(opcUaService, mqttService);

// 이벤트 등록
bridgeService.DataBridged += (s, e) =>
{
    Console.WriteLine($"브릿지됨: {e.DeviceId} → {e.MqttTopic}");
};

// 브릿지 시작
await bridgeService.StartAsync();

Console.WriteLine("브릿지 실행 중. Enter 키를 누르면 종료.");
Console.ReadLine();

// 브릿지 중지
await bridgeService.StopAsync();
bridgeService.Dispose();
```

### 2. DI 컨테이너 사용

```csharp
services.AddFullBridgeStack(
    new OpcUaConnectionInfo { ServerUrl = "opc.tcp://localhost:49320" },
    new MqttConnectionInfo { BrokerAddress = "localhost", Port = 1883 },
    new MqttTopicConfig { BaseTopic = "factory/line1" }
);

// 또는 개별 등록
services.AddDeviceConnector(opcUaConfig);
services.AddMqttPublisher(mqttConfig, topicConfig);
services.AddOpcUaMqttBridge();
```

## 📡 MQTT 토픽 구조

```
{BaseTopic}/
├── esp32/
│   └── {DeviceId}/
│       └── data          # ESP32 데이터
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
  "channelName": "ModbusTCP",
  "deviceName": "ESP32_01",
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

**SCADA 명령 (MQTT → OPC UA):**
```json
{
  "timestamp": "2026-01-29T08:30:00Z",
  "deviceId": "ESP32_01",
  "messageType": "COMMAND",
  "commandType": "WRITE_TAG",
  "tagName": "TargetA",
  "value": true
}
```

## 🧪 테스트

### Mosquitto 테스트

```bash
# 구독자 (터미널 1)
mosquitto_sub -h localhost -t "factory/line1/#" -v

# 발행자 (터미널 2) - 명령 전송
mosquitto_pub -h localhost -t "factory/line1/command" -m '{"deviceId":"ESP32_01","tagName":"TargetA","value":true}'
```

## 📌 주의사항

1. **Mosquitto 설치 필요**: MQTT 브로커가 먼저 실행 중이어야 합니다.
2. **KEPServerEX 실행 필요**: OPC UA 서버가 먼저 실행 중이어야 합니다.
3. **디바이스 설정 필수**: `AddDeviceConfig()`로 디바이스를 등록해야 합니다.

## 🔄 버전 히스토리

| 버전 | 내용 |
|------|------|
| v2.3 | MQTT 브릿지 기능 추가 |
| v2.2 | TargetA Coil 주소 변경 (00007) |
| v2.1 | 다중 디바이스 지원, DI 확장 |
| v2.0 | 초기 버전 |
