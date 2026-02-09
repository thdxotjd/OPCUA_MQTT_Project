namespace DeviceConnector.Mqtt.Models;

using DeviceConnector.Models;
using Newtonsoft.Json;

/// <summary>
/// 통합 MQTT 메시지
/// 모든 디바이스 타입을 위한 범용 메시지 포맷
/// </summary>
public class UnifiedMqttMessage
{
    [JsonProperty("deviceId")]
    public string DeviceId { get; set; } = string.Empty;

    [JsonProperty("channelName")]
    public string ChannelName { get; set; } = string.Empty;

    [JsonProperty("deviceName")]
    public string DeviceName { get; set; } = string.Empty;

    [JsonProperty("deviceType")]
    public string DeviceType { get; set; } = string.Empty;

    [JsonProperty("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonProperty("isGoodQuality")]
    public bool IsGoodQuality { get; set; } = true;

    [JsonProperty("tags")]
    public Dictionary<string, object?> Tags { get; set; } = new();

    /// <summary>
    /// UnifiedDeviceData에서 메시지 생성
    /// </summary>
    public static UnifiedMqttMessage FromDeviceData(UnifiedDeviceData data)
    {
        return new UnifiedMqttMessage
        {
            DeviceId = data.DeviceId,
            ChannelName = data.ChannelName,
            DeviceName = data.DeviceName,
            DeviceType = data.DeviceType.ToString(),
            Timestamp = data.Timestamp,
            IsGoodQuality = data.IsGoodQuality,
            Tags = new Dictionary<string, object?>(data.TagValues)
        };
    }

    /// <summary>
    /// JSON 직렬화
    /// </summary>
    public string ToJson()
    {
        return JsonConvert.SerializeObject(this, Formatting.None);
    }

    /// <summary>
    /// JSON 역직렬화
    /// </summary>
    public static UnifiedMqttMessage? FromJson(string json)
    {
        return JsonConvert.DeserializeObject<UnifiedMqttMessage>(json);
    }
}

/// <summary>
/// MQTT 제어 명령 메시지
/// Node-RED 등에서 전송하는 제어 명령
/// </summary>
public class MqttCommandMessage
{
    [JsonProperty("deviceId")]
    public string DeviceId { get; set; } = string.Empty;

    [JsonProperty("tagName")]
    public string TagName { get; set; } = string.Empty;

    [JsonProperty("value")]
    public object? Value { get; set; }

    [JsonProperty("qos")]
    public int QoS { get; set; } = 1;

    [JsonProperty("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonProperty("correlationId")]
    public string? CorrelationId { get; set; }

    /// <summary>
    /// JSON 직렬화
    /// </summary>
    public string ToJson()
    {
        return JsonConvert.SerializeObject(this, Formatting.None);
    }

    /// <summary>
    /// JSON 역직렬화
    /// </summary>
    public static MqttCommandMessage? FromJson(string json)
    {
        return JsonConvert.DeserializeObject<MqttCommandMessage>(json);
    }
}

/// <summary>
/// MQTT 명령 응답 메시지
/// </summary>
public class MqttCommandResponse
{
    [JsonProperty("deviceId")]
    public string DeviceId { get; set; } = string.Empty;

    [JsonProperty("tagName")]
    public string TagName { get; set; } = string.Empty;

    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("message")]
    public string? Message { get; set; }

    [JsonProperty("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonProperty("correlationId")]
    public string? CorrelationId { get; set; }

    /// <summary>
    /// JSON 직렬화
    /// </summary>
    public string ToJson()
    {
        return JsonConvert.SerializeObject(this, Formatting.None);
    }
}

/// <summary>
/// MQTT 토픽 설정 (확장)
/// </summary>
public class UnifiedMqttTopicConfig
{
    /// <summary>기본 토픽 (예: "factory/line1")</summary>
    public string BaseTopic { get; set; } = "factory/line1";

    /// <summary>
    /// 디바이스 데이터 토픽 생성
    /// 예: factory/line1/simulator/SimDevice01/data
    /// </summary>
    public string GetDataTopic(string deviceType, string deviceId)
    {
        return $"{BaseTopic}/{deviceType.ToLower()}/{deviceId}/data";
    }

    /// <summary>
    /// 디바이스 명령 토픽 생성
    /// 예: factory/line1/simulator/SimDevice01/command
    /// </summary>
    public string GetCommandTopic(string deviceType, string deviceId)
    {
        return $"{BaseTopic}/{deviceType.ToLower()}/{deviceId}/command";
    }

    /// <summary>
    /// 디바이스 응답 토픽 생성
    /// 예: factory/line1/simulator/SimDevice01/response
    /// </summary>
    public string GetResponseTopic(string deviceType, string deviceId)
    {
        return $"{BaseTopic}/{deviceType.ToLower()}/{deviceId}/response";
    }

    /// <summary>
    /// 상태 토픽
    /// </summary>
    public string StatusTopic => $"{BaseTopic}/status";

    /// <summary>
    /// 전체 명령 구독 토픽 (와일드카드)
    /// </summary>
    public string AllCommandsTopic => $"{BaseTopic}/+/+/command";
}
