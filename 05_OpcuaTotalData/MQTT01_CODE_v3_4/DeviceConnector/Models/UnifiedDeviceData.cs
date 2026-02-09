namespace DeviceConnector.Models;

/// <summary>
/// 통합 디바이스 데이터 래퍼
/// 여러 디바이스 타입을 하나의 인터페이스로 처리
/// </summary>
public interface IDeviceData
{
    string DeviceId { get; }
    string ChannelName { get; }
    string DeviceName { get; }
    DateTime Timestamp { get; set; }
    bool IsGoodQuality { get; set; }
    Dictionary<string, object> ToDictionary();
}

/// <summary>
/// 디바이스 타입 열거형
/// </summary>
public enum DeviceType
{
    /// <summary>ESP32 ModbusTCP 디바이스</summary>
    ESP32,
    
    /// <summary>STM Yolo 디바이스</summary>
    STMYolo,
    
    /// <summary>KEPServerEX Simulator 디바이스</summary>
    Simulator
}

/// <summary>
/// 통합 디바이스 설정 인터페이스
/// </summary>
public interface IDeviceTagConfig
{
    string DeviceId { get; }
    string ChannelName { get; }
    string DeviceName { get; }
    DeviceType DeviceType { get; }
    int NamespaceIndex { get; }
    string GetNodeId(string tagName);
    IEnumerable<string> GetAllTagNames();
    IEnumerable<string> GetMonitoringTagNames();
    IEnumerable<string> GetControlTagNames();
}

/// <summary>
/// 통합 디바이스 설정 (모든 디바이스 타입 지원)
/// </summary>
public class UnifiedDeviceConfig : IDeviceTagConfig
{
    public string DeviceId { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public DeviceType DeviceType { get; set; }
    public int NamespaceIndex { get; set; } = 2;
    
    /// <summary>태그 그룹 사용 여부 (Simulator는 그룹 사용)</summary>
    public bool UseTagGroups { get; set; } = false;
    
    /// <summary>태그 정의 (TagName -> TagGroup 또는 빈 문자열)</summary>
    public Dictionary<string, TagDefinition> Tags { get; set; } = new();

    public string GetNodeId(string tagName)
    {
        if (Tags.TryGetValue(tagName, out var tagDef) && UseTagGroups && !string.IsNullOrEmpty(tagDef.Group))
        {
            return $"ns={NamespaceIndex};s={ChannelName}.{DeviceName}.{tagDef.Group}.{tagName}";
        }
        return $"ns={NamespaceIndex};s={ChannelName}.{DeviceName}.{tagName}";
    }

    public IEnumerable<string> GetAllTagNames() => Tags.Keys;
    
    public IEnumerable<string> GetMonitoringTagNames() => 
        Tags.Where(t => t.Value.Direction == TagDirection.Read || t.Value.Direction == TagDirection.ReadWrite)
            .Select(t => t.Key);
    
    public IEnumerable<string> GetControlTagNames() =>
        Tags.Where(t => t.Value.Direction == TagDirection.Write || t.Value.Direction == TagDirection.ReadWrite)
            .Select(t => t.Key);
}

/// <summary>
/// 태그 정의
/// </summary>
public class TagDefinition
{
    public string Group { get; set; } = string.Empty;
    public TagDirection Direction { get; set; } = TagDirection.Read;
    public Type DataType { get; set; } = typeof(float);
}

/// <summary>
/// 태그 방향
/// </summary>
public enum TagDirection
{
    Read,
    Write,
    ReadWrite
}

/// <summary>
/// 통합 디바이스 데이터 (동적 태그 지원)
/// </summary>
public class UnifiedDeviceData : IDeviceData
{
    public string DeviceId { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public DeviceType DeviceType { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsGoodQuality { get; set; } = true;
    
    /// <summary>태그 값 저장소</summary>
    public Dictionary<string, object?> TagValues { get; set; } = new();

    public object? GetValue(string tagName) => 
        TagValues.TryGetValue(tagName, out var value) ? value : null;

    public T? GetValue<T>(string tagName)
    {
        if (TagValues.TryGetValue(tagName, out var value) && value is T typedValue)
            return typedValue;
        return default;
    }

    public void SetValue(string tagName, object? value) => TagValues[tagName] = value;

    public Dictionary<string, object> ToDictionary()
    {
        var dict = new Dictionary<string, object>
        {
            ["deviceId"] = DeviceId,
            ["channelName"] = ChannelName,
            ["deviceName"] = DeviceName,
            ["deviceType"] = DeviceType.ToString(),
            ["timestamp"] = Timestamp.ToString("O"),
            ["isGoodQuality"] = IsGoodQuality
        };
        
        foreach (var tag in TagValues)
        {
            if (tag.Value != null)
                dict[tag.Key] = tag.Value;
        }
        
        return dict;
    }

    public UnifiedDeviceData Clone()
    {
        return new UnifiedDeviceData
        {
            DeviceId = DeviceId,
            ChannelName = ChannelName,
            DeviceName = DeviceName,
            DeviceType = DeviceType,
            Timestamp = Timestamp,
            IsGoodQuality = IsGoodQuality,
            TagValues = new Dictionary<string, object?>(TagValues)
        };
    }

    public override string ToString()
    {
        var values = string.Join(", ", TagValues.Select(kv => $"{kv.Key}={kv.Value}"));
        return $"[{DeviceId}:{DeviceType}] {values}";
    }
}
