# OPCUA_MQTT_Project
OPCUA를 통한 MQTT와 SCADA 구현
### MQTT 브릿지 테스트 코드 ###
**OPCUA Data로 AGV,Conveyor의 데이터 값을 읽을 수 있다**
<h4>지원 통신 방식</h4>
- AGV: Modbus TCP/IP Ethernet<br>
- Conveyor: OPC UA Client Driver<br>

### 현 구조 ###
**KEPServerEX → C# → MQTT → Node-RED → InfluxDB → Grafana**<p>
**KEPServerEX       (OPC UA Server)**<br>
**C# OPC UA Client  (데이터 수집)**<br>
**MQTT Broker       (데이터 전달)**<br>
**Node-RED          (데이터 저장)**<br>
**InfluxDB          (데이터 저장소)**<br>
**Grafana           (데이터 시각화)**<br>
<h3>데이터 시각화</h3>
<img width="600" height="500" alt="데이터 시각화(Grafana and InfluxDB)" src="https://github.com/user-attachments/assets/6b6a0190-ad83-420b-afce-395611f4d944" />
<h3>시계열 DB</h3>
<img width="600" height="500" alt="시계열DB_ESP32andSimul" src="https://github.com/user-attachments/assets/da77e4d5-36f1-44e3-a225-10757e5fa93b" />
<h3>Node-Red</h3>
<img width="500" height="300" alt="nodeRed_ESP" src="https://github.com/user-attachments/assets/675af8af-7094-4725-a143-d29876da24d7" />
<img width="500" height="300" alt="nodeRed_Sim" src="https://github.com/user-attachments/assets/b843def9-6df0-4722-9ee2-e788beea400f" />
<img width="500" height="300" alt="nodeRed_ui" src="https://github.com/user-attachments/assets/3fcf4a5c-ef03-48fa-a396-80955f3ccf45" />

