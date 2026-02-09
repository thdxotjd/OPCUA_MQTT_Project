# OPCUA_MQTT_Project
OPCUA를 통한 MQTT와 SCADA 구현
### MQTT 브릿지 테스트 코드 ###
**ModbusTCP기반으로 AGV의 데이터 값을 읽을 수 있다**

### 현 구조 ###
KEPServerEX → C# → MQTT → Node-RED → InfluxDB → Grafana
**KEPServerEX (OPC UA Server)**
**C# OPC UA Client (데이터 수집)**
**MQTT Broker (데이터 전달)**
**Node-RED (데이터 저장)**
**InfluxDB (데이터 저장소)**
**Grafana (데이터 시각화)**
