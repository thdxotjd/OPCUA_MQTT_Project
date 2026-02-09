namespace DeviceConnector.Mqtt.Services;

using DeviceConnector.Models;
using DeviceConnector.Mqtt.Models;
using DeviceConnector.Mqtt.Events;
using DeviceConnector.Services;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using Newtonsoft.Json;
using System.Text;

/// <summary>
/// 통합 OPC UA-MQTT 브릿지 서비스
/// 여러 디바이스 타입을 MQTT로 발행하고 제어 명령 수신
/// 
/// ┌─────────────────────────────────────────────────────────────────────┐
/// │ 데이터 흐름                                                         │
/// │                                                                     │
/// │ [Monitoring]                                                        │
/// │ OPC UA → Bridge → MQTT (factory/line1/{type}/{id}/data)            │
/// │                                                                     │
/// │ [Control - SCADA]                                                   │
/// │ MQTT (factory/line1/{type}/{id}/command) → Bridge → OPC UA         │
/// │                                                                     │
/// │ [Response]                                                          │
/// │ OPC UA → Bridge → MQTT (factory/line1/{type}/{id}/response)        │
/// └─────────────────────────────────────────────────────────────────────┘
/// </summary>
public class UnifiedOpcUaMqttBridgeService : IDisposable
{
    #region Private Fields

    private readonly UnifiedOpcUaClientService _opcUaClient;
    private readonly MqttConnectionInfo _mqttConfig;
    private readonly UnifiedMqttTopicConfig _topicConfig;
    
    private IMqttClient? _mqttClient;
    private MqttFactory? _mqttFactory;
    private CancellationTokenSource? _bridgeCts;
    private bool _disposed;

    #endregion

    #region Events

    public event EventHandler<BridgeStatusChangedEventArgs>? StatusChanged;
    public event EventHandler<UnifiedDataBridgedEventArgs>? DataBridged;
    public event EventHandler<MqttCommandReceivedEventArgs>? CommandReceived;

    #endregion

    #region Properties

    public bool IsRunning { get; private set; }
    public bool IsMqttConnected => _mqttClient?.IsConnected ?? false;
    public bool IsOpcUaConnected => _opcUaClient.IsConnected;
    public long BridgedMessageCount { get; private set; }
    public long CommandsProcessedCount { get; private set; }

    #endregion

    #region Constructor

    public UnifiedOpcUaMqttBridgeService(
        UnifiedOpcUaClientService opcUaClient, 
        MqttConnectionInfo mqttConfig,
        UnifiedMqttTopicConfig? topicConfig = null)
    {
        _opcUaClient = opcUaClient ?? throw new ArgumentNullException(nameof(opcUaClient));
        _mqttConfig = mqttConfig ?? throw new ArgumentNullException(nameof(mqttConfig));
        _topicConfig = topicConfig ?? new UnifiedMqttTopicConfig();

        _mqttFactory = new MqttFactory();
    }

    #endregion

    #region 브릿지 시작/중지

    /// <summary>
    /// 브릿지 시작
    /// </summary>
    public async Task<bool> StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            OnStatusChanged("브릿지가 이미 실행 중입니다.");
            return true;
        }

        try
        {
            _bridgeCts = new CancellationTokenSource();

            // 1. OPC UA 연결
            OnStatusChanged("OPC UA 서버에 연결 중...");
            if (!await _opcUaClient.ConnectAsync(cancellationToken))
            {
                OnStatusChanged("OPC UA 연결 실패");
                return false;
            }

            // 2. MQTT 연결
            OnStatusChanged("MQTT 브로커에 연결 중...");
            if (!await ConnectMqttAsync(cancellationToken))
            {
                OnStatusChanged("MQTT 연결 실패");
                return false;
            }

            // 3. 명령 토픽 구독
            await SubscribeCommandTopicsAsync();

            // 4. OPC UA 구독 시작
            _opcUaClient.DataChanged += OnOpcUaDataChanged;
            await _opcUaClient.StartAllSubscriptionsAsync(cancellationToken);

            IsRunning = true;
            OnStatusChanged("브릿지 시작 완료");
            
            // 연결 상태 발행
            await PublishStatusAsync(true);

            return true;
        }
        catch (Exception ex)
        {
            OnStatusChanged($"브릿지 시작 실패: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 브릿지 중지
    /// </summary>
    public async Task StopAsync()
    {
        if (!IsRunning) return;

        try
        {
            _bridgeCts?.Cancel();
            _opcUaClient.DataChanged -= OnOpcUaDataChanged;

            await _opcUaClient.StopAllSubscriptionsAsync();
            
            // 연결 상태 발행
            await PublishStatusAsync(false);

            if (_mqttClient?.IsConnected == true)
            {
                await _mqttClient.DisconnectAsync();
            }

            IsRunning = false;
            OnStatusChanged("브릿지 중지 완료");
        }
        catch (Exception ex)
        {
            OnStatusChanged($"브릿지 중지 중 오류: {ex.Message}");
        }
    }

    #endregion

    #region MQTT 연결

    private async Task<bool> ConnectMqttAsync(CancellationToken cancellationToken)
    {
        try
        {
            _mqttClient = _mqttFactory!.CreateMqttClient();
            _mqttClient.ApplicationMessageReceivedAsync += OnMqttMessageReceivedAsync;
            _mqttClient.DisconnectedAsync += OnMqttDisconnectedAsync;

            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(_mqttConfig.BrokerAddress, _mqttConfig.Port)
                .WithClientId(_mqttConfig.ClientId ?? $"Bridge_{DateTime.Now:HHmmss}")
                .WithCleanSession()
                .Build();

            var result = await _mqttClient.ConnectAsync(options, cancellationToken);
            return result.ResultCode == MqttClientConnectResultCode.Success;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MQTT] 연결 실패: {ex.Message}");
            return false;
        }
    }

    private async Task OnMqttDisconnectedAsync(MqttClientDisconnectedEventArgs e)
    {
        if (!IsRunning) return;

        Console.WriteLine($"[MQTT] 연결 끊김. 재연결 시도...");

        if (_mqttConfig.AutoReconnect)
        {
            await Task.Delay(5000);
            try
            {
                await ConnectMqttAsync(CancellationToken.None);
                await SubscribeCommandTopicsAsync();
            }
            catch { }
        }
    }

    #endregion

    #region 데이터 발행 (OPC UA → MQTT)

    private async void OnOpcUaDataChanged(object? sender, UnifiedDataChangedEventArgs e)
    {
        if (!IsMqttConnected) return;

        try
        {
            await PublishDeviceDataAsync(e.Data);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Bridge] 데이터 발행 오류: {ex.Message}");
        }
    }

    /// <summary>
    /// 디바이스 데이터 MQTT 발행
    /// </summary>
    public async Task<bool> PublishDeviceDataAsync(UnifiedDeviceData data, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce)
    {
        if (!IsMqttConnected || _mqttClient == null) return false;

        try
        {
            var message = UnifiedMqttMessage.FromDeviceData(data);
            var topic = _topicConfig.GetDataTopic(data.DeviceType.ToString(), data.DeviceId);
            var payload = message.ToJson();

            var mqttMessage = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(qos)
                .WithRetainFlag(false)
                .Build();

            await _mqttClient.PublishAsync(mqttMessage);
            BridgedMessageCount++;

            DataBridged?.Invoke(this, new UnifiedDataBridgedEventArgs(
                data.DeviceId, data.DeviceType.ToString(), topic, payload));

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MQTT] 발행 실패: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 모든 디바이스 데이터 즉시 발행
    /// </summary>
    public async Task<bool> PublishAllDataNowAsync()
    {
        if (!IsRunning) return false;

        var allData = await _opcUaClient.ReadAllDeviceDataAsync();
        var success = true;

        foreach (var data in allData.Values)
        {
            if (!await PublishDeviceDataAsync(data))
            {
                success = false;
            }
        }

        return success;
    }

    /// <summary>
    /// 상태 발행
    /// </summary>
    private async Task PublishStatusAsync(bool isOnline)
    {
        if (!IsMqttConnected || _mqttClient == null) return;

        var status = new
        {
            online = isOnline,
            timestamp = DateTime.UtcNow,
            devices = _opcUaClient.DeviceConfigs.Keys.ToList()
        };

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(_topicConfig.StatusTopic)
            .WithPayload(JsonConvert.SerializeObject(status))
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .WithRetainFlag(true)
            .Build();

        await _mqttClient.PublishAsync(message);
    }

    #endregion

    #region 명령 수신 (MQTT → OPC UA)

    private async Task SubscribeCommandTopicsAsync()
    {
        if (!IsMqttConnected || _mqttClient == null) return;

        // 모든 디바이스의 명령 토픽 구독
        foreach (var config in _opcUaClient.DeviceConfigs.Values)
        {
            var commandTopic = _topicConfig.GetCommandTopic(config.DeviceType.ToString(), config.DeviceId);
            
            await _mqttClient.SubscribeAsync(new MqttTopicFilterBuilder()
                .WithTopic(commandTopic)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build());

            Console.WriteLine($"[MQTT] 구독: {commandTopic}");
        }
    }

    private async Task OnMqttMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        try
        {
            var topic = e.ApplicationMessage.Topic;
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);

            Console.WriteLine($"[MQTT] 메시지 수신: {topic}");
            Console.WriteLine($"       Payload: {payload}");

            // 명령 토픽인지 확인
            if (topic.EndsWith("/command"))
            {
                var command = MqttCommandMessage.FromJson(payload);
                if (command != null)
                {
                    await ProcessCommandAsync(command, topic);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MQTT] 메시지 처리 오류: {ex.Message}");
        }
    }

    private async Task ProcessCommandAsync(MqttCommandMessage command, string topic)
    {
        Console.WriteLine($"[Bridge] 명령 처리: {command.DeviceId}.{command.TagName} = {command.Value}");

        // 이벤트 발생
        CommandReceived?.Invoke(this, new MqttCommandReceivedEventArgs(
            command.DeviceId, command.TagName, command.Value, topic));

        // OPC UA에 쓰기
        bool success = false;
        string? errorMessage = null;

        try
        {
            if (command.Value != null)
            {
                success = await _opcUaClient.WriteTagAsync(command.DeviceId, command.TagName, command.Value);
            }
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
        }

        CommandsProcessedCount++;

        // 응답 발행
        await PublishCommandResponseAsync(command, success, errorMessage);
    }

    private async Task PublishCommandResponseAsync(MqttCommandMessage command, bool success, string? errorMessage)
    {
        if (!IsMqttConnected || _mqttClient == null) return;

        // 디바이스 타입 찾기
        var deviceType = "unknown";
        if (_opcUaClient.DeviceConfigs.TryGetValue(command.DeviceId, out var config))
        {
            deviceType = config.DeviceType.ToString();
        }

        var response = new MqttCommandResponse
        {
            DeviceId = command.DeviceId,
            TagName = command.TagName,
            Success = success,
            Message = success ? "OK" : errorMessage,
            CorrelationId = command.CorrelationId,
            Timestamp = DateTime.UtcNow
        };

        var topic = _topicConfig.GetResponseTopic(deviceType, command.DeviceId);

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(response.ToJson())
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await _mqttClient.PublishAsync(message);
        Console.WriteLine($"[MQTT] 응답 발행: {topic} → {(success ? "성공" : "실패")}");
    }

    #endregion

    #region 수동 명령

    /// <summary>
    /// 수동으로 명령 전송 (테스트용)
    /// </summary>
    public async Task<bool> SendCommandAsync(string deviceId, string tagName, object value)
    {
        return await _opcUaClient.WriteTagAsync(deviceId, tagName, value);
    }

    #endregion

    #region Helper

    private void OnStatusChanged(string message)
    {
        Console.WriteLine($"[Bridge] {message}");
        StatusChanged?.Invoke(this, new BridgeStatusChangedEventArgs(message, IsRunning));
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;

        StopAsync().GetAwaiter().GetResult();
        _mqttClient?.Dispose();
        _bridgeCts?.Dispose();
        
        _disposed = true;
    }

    #endregion
}

#region Event Args

public class UnifiedDataBridgedEventArgs : EventArgs
{
    public string DeviceId { get; }
    public string DeviceType { get; }
    public string MqttTopic { get; }
    public string Payload { get; }

    public UnifiedDataBridgedEventArgs(string deviceId, string deviceType, string mqttTopic, string payload)
    {
        DeviceId = deviceId;
        DeviceType = deviceType;
        MqttTopic = mqttTopic;
        Payload = payload;
    }
}

public class MqttCommandReceivedEventArgs : EventArgs
{
    public string DeviceId { get; }
    public string TagName { get; }
    public object? Value { get; }
    public string Topic { get; }

    public MqttCommandReceivedEventArgs(string deviceId, string tagName, object? value, string topic)
    {
        DeviceId = deviceId;
        TagName = tagName;
        Value = value;
        Topic = topic;
    }
}

#endregion
