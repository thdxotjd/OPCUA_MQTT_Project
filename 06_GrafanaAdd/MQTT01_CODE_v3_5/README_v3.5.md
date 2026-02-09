# MQTT01_CODE v3.5 - Industrial IoT Integration Platform

## 📌 개요
**MQTT01_CODE**는 산업용 IoT 장비들을 통합 관리하는 OPC UA - MQTT 브릿지 시스템입니다.

### ✨ v3.5 주요 업데이트
- ✅ **Grafana 자동 시작 기능 추가**
- ✅ **Node-RED 자동 실행 개선**
- ✅ **4개 외부 서비스 통합 관리** (Mosquitto, InfluxDB, Grafana, Node-RED)
- ✅ **브라우저 자동 열기 기능 향상**

---

## 🏗️ 시스템 아키텍처

```
┌─────────────────────────────────────────────────────────────┐
│                    MQTT01_CODE v3.5                         │
│                                                             │
│  ┌──────────────┐   ┌──────────────┐   ┌──────────────┐  │
│  │ ProcessManager│   │  OPC UA      │   │    MQTT      │  │
│  │              │   │  Client      │   │   Bridge     │  │
│  │ - Mosquitto  │   │              │   │              │  │
│  │ - InfluxDB   │   │  Unified     │   │   Unified    │  │
│  │ - Grafana ✨ │   │  Device      │   │   Topic      │  │
│  │ - Node-RED   │   │  Management  │   │   Config     │  │
│  └──────────────┘   └──────────────┘   └──────────────┘  │
│                                                             │
└─────────────────────────────────────────────────────────────┘
           │                    │                    │
           ▼                    ▼                    ▼
    ┌──────────┐        ┌──────────┐        ┌──────────┐
    │ External │        │ KEPServer│        │  MQTT    │
    │ Services │        │   EX     │        │ Clients  │
    └──────────┘        └──────────┘        └──────────┘
```

---

## 🚀 기능

### 1. ProcessManager (자동 서비스 관리)
외부 서비스를 자동으로 시작하고 관리합니다.

#### 지원 서비스
| 서비스 | 포트 | 용도 | 비고 |
|--------|------|------|------|
| **Mosquitto** | 1883 | MQTT Broker | 메시지 브로커 |
| **InfluxDB** | 8086 | Time-Series DB | 시계열 데이터 저장 |
| **Grafana** ✨ | 3000 | Dashboard | 데이터 시각화 |
| **Node-RED** | 1880 | Flow Programming | 자동화 워크플로우 |

#### 특징
- ✅ 중복 실행 방지 (이미 실행 중이면 스킵)
- ✅ 순차적 시작 (의존성 고려)
- ✅ 자동 상태 모니터링
- ✅ 브라우저 자동 열기
- ✅ 안전한 종료 처리

### 2. OPC UA Client Service
KEPServerEX와 연결하여 산업 장비 데이터를 읽고 제어합니다.

#### 지원 디바이스
- **ESP32 ModbusTCP** - ModbusTCP.ESP32_01
- **STM Yolo Conveyor** - STM.Stm_yolo
- **Simulator Device** - MqttTest.SimDevice01

### 3. MQTT Bridge Service
OPC UA 데이터를 MQTT로 변환하여 발행합니다.

#### MQTT 토픽 구조
```
factory/line1/
├── simulator/SimDevice01/data        ← Simulator 데이터
├── simulator/SimDevice01/command     ← Simulator 제어
├── stmyolo/STM_yolo/data            ← STM Conveyor 데이터
├── stmyolo/STM_yolo/command         ← STM Conveyor 제어
├── esp32/ESP32_01/data              ← ESP32 센서 데이터
├── esp32/ESP32_01/command           ← ESP32 제어
└── status                           ← 연결 상태
```

---

## 📦 설치 및 설정

### 사전 요구사항

1. **Mosquitto MQTT Broker**
   - 설치 경로: `C:\Program Files\mosquitto`
   - 다운로드: https://mosquitto.org/download/

2. **InfluxDB 2.x**
   - 설치 경로: `C:\Users\pc\Desktop\InfluxDB\influxdb2-2.7.5-windows`
   - 다운로드: https://portal.influxdata.com/downloads/

3. **Grafana** ✨
   - 설치 경로: `C:\Program Files\GrafanaLabs\grafana`
   - 다운로드: https://grafana.com/grafana/download?platform=windows
   - 기본 로그인: `admin` / `admin`

4. **Node-RED**
   - NPM 전역 설치: `npm install -g node-red`
   - 실행 명령어: `node-red`

5. **KEPServerEX**
   - OPC UA Server 역할
   - 엔드포인트: `opc.tcp://127.0.0.1:49320`

### 경로 커스터마이징

기본 경로가 다른 경우 `ProcessManager` 생성 시 수정:

```csharp
var processManager = new ProcessManager(
    grafanaPath: @"C:\Your\Custom\Path\grafana",
    mosquittoPath: @"C:\Your\Custom\Path\mosquitto",
    influxDbPath: @"C:\Your\Custom\Path\influxdb"
);
```

---

## 🎮 사용법

### 1. 프로그램 시작

```bash
dotnet run
```

### 2. 외부 서비스 자동 시작

프로그램 시작 시 프롬프트에서 `y` 입력:

```
외부 서비스(Mosquitto, InfluxDB, Grafana, Node-RED)를 시작하시겠습니까? (y/n): y
```

실행 순서:
1. Mosquitto (2초 대기)
2. InfluxDB (3초 대기)
3. Grafana ✨ (3초 대기)
4. Node-RED (2초 대기)

### 3. 상태 확인

```
┌─────────────────────────────────────────────────────────┐
│ 외부 서비스 상태                                        │
├─────────────────────────────────────────────────────────┤
│ Mosquitto (MQTT)  : ✅ 실행 중  (Port: 1883)            │
│ InfluxDB          : ✅ 실행 중  (Port: 8086)            │
│ Grafana           : ✅ 실행 중  (Port: 3000)   ✨       │
│ Node-RED          : ✅ 실행 중  (Port: 1880)            │
└─────────────────────────────────────────────────────────┘
```

### 4. 브라우저 자동 열기

메뉴에서 `D` 선택 또는 코드에서:

```csharp
_processManager.OpenDashboards();
```

자동으로 열리는 페이지:
- http://localhost:3000 - **Grafana Dashboard** ✨
- http://localhost:1880 - Node-RED Editor
- http://localhost:1880/ui - Node-RED Dashboard
- http://localhost:8086 - InfluxDB UI

---

## 📊 Grafana 대시보드 설정

### 1. 초기 로그인
- URL: http://localhost:3000
- 기본 계정: `admin` / `admin`
- 첫 로그인 시 비밀번호 변경 요구됨

### 2. InfluxDB 데이터 소스 추가

**Configuration → Data Sources → Add data source → InfluxDB**

```yaml
Name: InfluxDB-MQTT
Query Language: Flux
URL: http://localhost:8086
Organization: your-org
Token: your-influxdb-token
Default Bucket: mqtt_data
```

### 3. 대시보드 생성 예제

#### Panel 1: ESP32 온도 모니터링
```flux
from(bucket: "mqtt_data")
  |> range(start: -1h)
  |> filter(fn: (r) => r["_measurement"] == "esp32_data")
  |> filter(fn: (r) => r["_field"] == "temperature")
```

#### Panel 2: STM Conveyor 속도
```flux
from(bucket: "mqtt_data")
  |> range(start: -1h)
  |> filter(fn: (r) => r["_measurement"] == "stm_data")
  |> filter(fn: (r) => r["_field"] == "speed_main")
```

#### Panel 3: 실시간 상태
```flux
from(bucket: "mqtt_data")
  |> range(start: -5m)
  |> filter(fn: (r) => r["_measurement"] == "device_status")
  |> last()
```

### 4. Alert 설정
**Alerting → Notification channels → Add channel**

```yaml
Type: Email / Slack / Webhook
Alert Rule: temperature > 80
```

---

## 🛠️ 개발 가이드

### ProcessManager API

```csharp
// 개별 서비스 시작
await _processManager.StartMosquittoAsync();
await _processManager.StartInfluxDbAsync();
await _processManager.StartGrafanaAsync();      // ✨ New
await _processManager.StartNodeRedAsync();

// 모든 서비스 일괄 시작
await _processManager.StartAllServicesAsync();

// 상태 확인
bool isGrafanaRunning = _processManager.IsGrafanaRunning;  // ✨ New
bool isNodeRedRunning = _processManager.IsNodeRedRunning;
bool isInfluxDbRunning = _processManager.IsInfluxDbRunning;
bool isMosquittoRunning = _processManager.IsMosquittoRunning;

// 상태 출력
_processManager.PrintStatus();

// 대시보드 열기
_processManager.OpenDashboards();

// 관리 중인 프로세스 정리
_processManager.StopManagedServices();

// 모든 관련 프로세스 강제 종료
_processManager.KillAllRelatedProcesses();
```

### 커스텀 경로 설정

```csharp
var processManager = new ProcessManager(
    grafanaPath: @"D:\Tools\Grafana",
    mosquittoPath: @"D:\Tools\Mosquitto", 
    influxDbPath: @"D:\Tools\InfluxDB"
);
```

---

## 🔧 문제 해결

### 1. Grafana가 시작되지 않는 경우

**문제**: `grafana-server.exe`를 찾을 수 없습니다

**해결**:
```csharp
// ProcessManager 생성 시 올바른 경로 지정
var processManager = new ProcessManager(
    grafanaPath: @"C:\Program Files\GrafanaLabs\grafana",  // 실제 설치 경로
    mosquittoPath: @"C:\Program Files\mosquitto",
    influxDbPath: @"C:\Users\pc\Desktop\InfluxDB\influxdb2-2.7.5-windows"
);
```

**또는 수동 확인**:
```bash
# Grafana 실행 파일 확인
cd "C:\Program Files\GrafanaLabs\grafana\bin"
.\grafana-server.exe
```

### 2. Port 3000이 이미 사용 중인 경우

**Grafana 기본 포트 변경**:

1. `grafana\conf\defaults.ini` 복사 → `custom.ini`
2. `custom.ini` 수정:
```ini
[server]
http_port = 8080
```

3. ProcessManager 수정:
```csharp
// StartGrafanaAsync()에서
startInfo.Arguments = "--config custom.ini";
```

### 3. Node-RED가 시작되지 않는 경우

**NPM 전역 설치 확인**:
```bash
npm install -g node-red
node-red --version
```

**환경 변수 PATH 확인**:
- `C:\Users\[사용자]\AppData\Roaming\npm` 포함 여부 확인

### 4. InfluxDB 연결 실패

**토큰 확인**:
```bash
# InfluxDB UI에서 토큰 생성
http://localhost:8086 → Load Data → API Tokens
```

**연결 테스트**:
```bash
curl http://localhost:8086/health
```

---

## 📈 성능 최적화

### 1. 시작 시간 단축

대기 시간 조정:
```csharp
await StartMosquittoAsync();
await Task.Delay(1000);  // 2000 → 1000으로 단축

await StartInfluxDbAsync();
await Task.Delay(2000);  // 3000 → 2000으로 단축
```

### 2. 메모리 사용량 최적화

**Grafana** (`custom.ini`):
```ini
[database]
cache_mode = shared
```

**InfluxDB** (설정 파일):
```toml
[data]
cache-max-memory-size = "1g"
cache-snapshot-memory-size = "256m"
```

### 3. Node-RED 성능 향상

**settings.js** 수정:
```javascript
module.exports = {
    uiPort: 1880,
    apiMaxLength: '5mb',
    httpNodeMiddleware: function(req, res, next) { next(); },
}
```

---

## 🔄 업데이트 히스토리

### v3.5 (2026-02-09) ✨
- **Grafana 자동 시작 기능 추가**
- **브라우저 자동 열기 기능 개선** (Grafana 포함)
- **프로세스 상태 확인 강화** (grafana-server 프로세스 감지)
- **강제 종료 로직 업데이트** (Grafana 포함)
- **문서 업데이트** (Grafana 설정 가이드 추가)

### v3.4
- Node-RED 자동 실행 기능 추가
- 브라우저 자동 열기 기능 추가
- 외부 서비스 상태 모니터링 개선

### v3.0
- Unified Device Config 시스템
- Multi-Device 지원 (ESP32, STM, Simulator)
- MQTT Topic 구조화

---

## 📚 참고 자료

### 공식 문서
- **Grafana**: https://grafana.com/docs/
- **InfluxDB**: https://docs.influxdata.com/
- **Node-RED**: https://nodered.org/docs/
- **Mosquitto**: https://mosquitto.org/documentation/

### 튜토리얼
- **Grafana + InfluxDB 연동**: https://grafana.com/docs/grafana/latest/datasources/influxdb/
- **MQTT 기초**: https://www.hivemq.com/mqtt-essentials/
- **OPC UA 소개**: https://opcfoundation.org/about/opc-technologies/opc-ua/

---

## 💡 향후 계획

### v3.6 예정
- [ ] Docker 컨테이너 지원
- [ ] Grafana Provisioning (자동 대시보드 배포)
- [ ] Alerting 시스템 통합
- [ ] 웹 기반 설정 UI

### v4.0 예정
- [ ] Kubernetes 배포 지원
- [ ] 클라우드 연동 (AWS IoT, Azure IoT Hub)
- [ ] AI/ML 기반 이상 감지
- [ ] 멀티 테넌트 지원

---

## 📞 지원

### 문의
- Email: your-email@example.com
- GitHub Issues: https://github.com/your-repo/issues

### 기여
Pull Request는 언제나 환영합니다!

---

## 📄 라이선스

MIT License - 자유롭게 사용 가능

---

## ⭐ 주요 기능 요약

| 기능 | v3.4 | v3.5 |
|------|------|------|
| Mosquitto 자동 시작 | ✅ | ✅ |
| InfluxDB 자동 시작 | ✅ | ✅ |
| Node-RED 자동 시작 | ✅ | ✅ |
| **Grafana 자동 시작** | ❌ | ✅ ✨ |
| OPC UA 브릿지 | ✅ | ✅ |
| MQTT 퍼블리싱 | ✅ | ✅ |
| Multi-Device 지원 | ✅ | ✅ |
| 상태 모니터링 | ✅ | ✅ |
| 브라우저 자동 열기 | ✅ | ✅ (개선) |

---

**MQTT01_CODE v3.5** - Complete Industrial IoT Integration Platform with Grafana Support ✨
