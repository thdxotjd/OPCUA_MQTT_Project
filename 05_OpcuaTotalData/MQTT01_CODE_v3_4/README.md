# DeviceConnector v3.0

## 개요

OPC UA + MQTT 브릿지 라이브러리로, 여러 디바이스 타입을 통합 지원합니다.

- **MqttTest 채널**: KEPServerEX 8-Bit Simulator 디바이스
- **STM 채널**: STM Yolo 컨베이어 제어 디바이스
- **ModbusTCP 채널**: ESP32 Modbus TCP 디바이스 (옵션)

## 아키텍처

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        DeviceConnector v3.0                             │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  [KEPServerEX]                                                          │
│  ├── MqttTest (Simulator)                                               │
│  │   └── SimDevice01                                                    │
│  │       ├── Monitoring (Temperature, Pressure, MotorRPM)               │
│  │       ├── Control (MotorStart, MotorStop, SpeedSetpoint)             │
│  │       └── Status (Alarm01, Alarm02, RunningFlag)                     │
│  │                                                                      │
│  └── STM                                                                │
│      └── Stm_yolo                                                       │
│          ├── Target (TargetState, TargetSpeed*, Agv*Arrived)            │
│          └── Current (CurrentState, CurrentSpeed*, IsLift*, IsRobot*)   │
│                                                                         │
│           ↓ OPC UA                                                      │
│                                                                         │
│  [UnifiedOpcUaClientService]                                            │
│  ├── 다중 디바이스 설정 지원                                            │
│  ├── 동적 태그 관리 (UnifiedDeviceConfig)                               │
│  └── 구독/읽기/쓰기                                                     │
│                                                                         │
│           ↓ Data Changed Events                                         │
│                                                                         │
│  [UnifiedOpcUaMqttBridgeService]                                        │
│  ├── OPC UA → MQTT (데이터 발행)                                        │
│  ├── MQTT → OPC UA (명령 수신)                                          │
│  └── 명령 응답 발행                                                     │
│                                                                         │
│           ↓ MQTT                                                        │
│                                                                         │
│  [Mosquitto MQTT Broker]                                                │
│  ├── factory/line1/simulator/SimDevice01/data                           │
│  ├── factory/line1/simulator/SimDevice01/command                        │
│  ├── factory/line1/stmyolo/STM_yolo/data                                │
│  ├── factory/line1/stmyolo/STM_yolo/command                             │
│  └── factory/line1/status                                               │
│                                                                         │
│           ↓                                                             │
│                                                                         │
│  [Node-RED Dashboard / InfluxDB / SCADA]                                │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

## MQTT 토픽 구조

| 토픽 | 방향 | QoS | 설명 |
|------|------|-----|------|
| `factory/line1/{type}/{id}/data` | Publish | 0 | 디바이스 데이터 |
| `factory/line1/{type}/{id}/command` | Subscribe | 1 | 제어 명령 |
| `factory/line1/{type}/{id}/response` | Publish | 1 | 명령 응답 |
| `factory/line1/status` | Publish | 1 | 연결 상태 (Retained) |

## 명령 메시지 포맷

### 명령 전송 (MQTT → OPC UA)

```json
{
  "deviceId": "SimDevice01",
  "tagName": "MotorStart",
  "value": true,
  "qos": 1,
  "correlationId": "cmd-001"
}
```

### 명령 응답 (OPC UA → MQTT)

```json
{
  "deviceId": "SimDevice01",
  "tagName": "MotorStart",
  "success": true,
  "message": "OK",
  "correlationId": "cmd-001",
  "timestamp": "2025-02-05T12:00:00Z"
}
```

## KEPServerEX 설정

### MqttTest 채널 (8-Bit Simulator)

| Tag Group | Tag Name | Address | Data Type |
|-----------|----------|---------|-----------|
| Monitoring | Temperature | RAMP(1000, 0, 100, 1) | Float |
| Monitoring | Pressure | RANDOM(1000, 10, 50) | Float |
| Monitoring | MotorRPM | SINE(100, 200, 800, 0.05) | Float |
| Control | MotorStart | R00100.0 | Boolean |
| Control | MotorStop | R00100.1 | Boolean |
| Control | SpeedSetpoint | R00200 | Word |
| Status | Alarm01 | R00300.0 | Boolean |
| Status | Alarm02 | R00300.1 | Boolean |
| Status | RunningFlag | R00300.2 | Boolean |

### STM 채널

| Tag Name | Data Type | Direction |
|----------|-----------|-----------|
| TargetState | LLong | Write |
| TargetSpeedMain | LLong | Write |
| TargetSpeedSort | LLong | Write |
| TargetSpeedLoad | LLong | Write |
| AgvSortArrived | Boolean | Write |
| AgvSortDeparted | Boolean | Write |
| AgvLoadArrived | Boolean | Write |
| AgvLoadDeparted | Boolean | Write |
| CurrentState | LLong | Read |
| CurrentSpeedMain | LLong | Read |
| CurrentSpeedSort | LLong | Read |
| CurrentSpeedLoad | LLong | Read |
| CurrentFloor | LLong | Read |
| IsLiftMoving | Boolean | Read |
| IsRobotWorking | Boolean | Read |
| IsRobotDone | Boolean | Read |

## 사용 방법

### 1. 빌드

```bash
dotnet build
```

### 2. 실행

```bash
cd DeviceConnector.Test
dotnet run
```

### 3. 테스트

```bash
# MQTT 데이터 확인
mosquitto_sub -h localhost -t "factory/line1/#" -v

# Simulator 명령 전송
mosquitto_pub -h localhost \
  -t "factory/line1/simulator/SimDevice01/command" \
  -m '{"deviceId":"SimDevice01","tagName":"MotorStart","value":true}'

# STM 명령 전송
mosquitto_pub -h localhost \
  -t "factory/line1/stmyolo/STM_yolo/command" \
  -m '{"deviceId":"STM_yolo","tagName":"TargetSpeedMain","value":500}'
```

## 다음 단계

1. **InfluxDB 연동**: 시계열 데이터 저장
2. **Node-RED 대시보드**: 실시간 모니터링 및 제어 UI
3. **QoS 최적화**: 명령 전달 신뢰성 향상

## 버전 히스토리

- **v3.0**: 다중 디바이스 타입 통합 지원 (Simulator, STMYolo, ESP32)
- **v2.4**: OPC UA-MQTT 브릿지 + ProcessManager
- **v2.2**: TargetA Coil 주소 변경
