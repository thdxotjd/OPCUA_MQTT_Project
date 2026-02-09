namespace DeviceConnector.Services;

using DeviceConnector.Events;
using DeviceConnector.Interfaces;
using DeviceConnector.Models;
using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Client;
using System.Collections.Concurrent;

/// <summary>
/// 통합 OPC UA 클라이언트 서비스
/// ESP32, STMYolo, Simulator 등 여러 디바이스 타입 지원
/// 
/// ┌─────────────────────────────────────────────────────────────────────┐
/// │ v3.0 변경사항                                                       │
/// │ - 다중 디바이스 타입 지원 (ESP32, STMYolo, Simulator)               │
/// │ - UnifiedDeviceConfig 기반 동적 태그 관리                           │
/// │ - 태그 그룹 지원 (Simulator용)                                      │
/// └─────────────────────────────────────────────────────────────────────┘
/// </summary>
public class UnifiedOpcUaClientService : IDisposable
{
    #region Private Fields

    private readonly OpcUaConnectionInfo _connectionInfo;
    private readonly ILogger<UnifiedOpcUaClientService>? _logger;
    private readonly ConcurrentDictionary<string, UnifiedDeviceConfig> _deviceConfigs = new();
    private readonly ConcurrentDictionary<string, UnifiedDeviceData> _deviceDataCache = new();
    private readonly ConcurrentDictionary<string, Subscription> _subscriptions = new();

    private Session? _session;
    private bool _disposed;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private CancellationTokenSource? _reconnectCts;

    #endregion

    #region Events

    public event EventHandler<UnifiedDataChangedEventArgs>? DataChanged;
    public event EventHandler<ConnectionChangedEventArgs>? ConnectionChanged;
    public event EventHandler<WriteCompletedEventArgs>? WriteCompleted;
    public event EventHandler<ErrorOccurredEventArgs>? ErrorOccurred;

    #endregion

    #region Properties

    public bool IsConnected => _session?.Connected ?? false;
    public ConnectionStatus Status { get; private set; } = new();
    public IReadOnlyDictionary<string, UnifiedDeviceConfig> DeviceConfigs => _deviceConfigs;

    #endregion

    #region Constructor

    public UnifiedOpcUaClientService(OpcUaConnectionInfo connectionInfo, ILogger<UnifiedOpcUaClientService>? logger = null)
    {
        _connectionInfo = connectionInfo ?? throw new ArgumentNullException(nameof(connectionInfo));
        _logger = logger;
    }

    #endregion

    #region 연결 관리

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (IsConnected)
            {
                _logger?.LogInformation("Already connected to OPC UA server");
                return true;
            }

            UpdateConnectionState(ConnectionState.Connecting);

            var config = new ApplicationConfiguration
            {
                ApplicationName = _connectionInfo.ApplicationName,
                ApplicationType = ApplicationType.Client,
                SecurityConfiguration = new SecurityConfiguration
                {
                    ApplicationCertificate = new CertificateIdentifier(),
                    AutoAcceptUntrustedCertificates = true
                },
                ClientConfiguration = new ClientConfiguration
                {
                    DefaultSessionTimeout = _connectionInfo.SessionTimeout
                }
            };

            await config.Validate(ApplicationType.Client);

            var endpoint = CoreClientUtils.SelectEndpoint(
                _connectionInfo.EndpointUrl,
                useSecurity: _connectionInfo.SecurityPolicy != "None");

            var endpointConfig = EndpointConfiguration.Create(config);
            var configuredEndpoint = new ConfiguredEndpoint(null, endpoint, endpointConfig);

            UserIdentity userIdentity;
            if (!string.IsNullOrEmpty(_connectionInfo.Username))
            {
                userIdentity = new UserIdentity(_connectionInfo.Username, _connectionInfo.Password);
            }
            else
            {
                userIdentity = new UserIdentity(new AnonymousIdentityToken());
            }

            _session = await Session.Create(
                config,
                configuredEndpoint,
                false,
                _connectionInfo.ApplicationName,
                (uint)_connectionInfo.SessionTimeout,
                userIdentity,
                null);

            _session.KeepAlive += Session_KeepAlive;

            UpdateConnectionState(ConnectionState.Connected);
            _logger?.LogInformation("Connected to OPC UA server: {Url}", _connectionInfo.EndpointUrl);

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to connect to OPC UA server");
            UpdateConnectionState(ConnectionState.Error, ex.Message);
            OnErrorOccurred(new ErrorOccurredEventArgs($"Connection failed: {ex.Message}", ex));
            return false;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task DisconnectAsync()
    {
        await _connectionLock.WaitAsync();
        try
        {
            _reconnectCts?.Cancel();

            if (_session != null)
            {
                _session.KeepAlive -= Session_KeepAlive;

                foreach (var sub in _subscriptions.Values)
                {
                    try { _session.RemoveSubscription(sub); } catch { }
                }
                _subscriptions.Clear();

                await _session.CloseAsync();
                _session.Dispose();
                _session = null;
            }

            UpdateConnectionState(ConnectionState.Disconnected);
            _logger?.LogInformation("Disconnected from OPC UA server");
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private void Session_KeepAlive(ISession session, KeepAliveEventArgs e)
    {
        if (e.Status != null && ServiceResult.IsBad(e.Status))
        {
            _logger?.LogWarning("KeepAlive failed: {Status}", e.Status);
            UpdateConnectionState(ConnectionState.Reconnecting);

            if (_connectionInfo.AutoReconnect)
            {
                _ = ReconnectAsync();
            }
        }
    }

    private async Task ReconnectAsync()
    {
        _reconnectCts?.Cancel();
        _reconnectCts = new CancellationTokenSource();
        var token = _reconnectCts.Token;

        while (!token.IsCancellationRequested && !IsConnected)
        {
            Status.ReconnectAttempts++;
            _logger?.LogInformation("Reconnect attempt #{Attempt}", Status.ReconnectAttempts);

            try
            {
                await Task.Delay(_connectionInfo.ReconnectInterval, token);
                if (await ConnectAsync(token))
                {
                    Status.ReconnectAttempts = 0;
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Reconnect attempt failed");
            }
        }
    }

    private void UpdateConnectionState(ConnectionState newState, string? error = null)
    {
        var previousState = Status.State;
        Status.State = newState;
        Status.ServerUrl = _connectionInfo.EndpointUrl;
        Status.LastError = error;

        if (newState == ConnectionState.Connected)
        {
            Status.LastConnectedTime = DateTime.UtcNow;
        }

        ConnectionChanged?.Invoke(this, new ConnectionChangedEventArgs(Status, previousState));
    }

    #endregion

    #region 디바이스 관리

    /// <summary>
    /// 디바이스 설정 추가
    /// </summary>
    public void AddDeviceConfig(UnifiedDeviceConfig config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        _deviceConfigs[config.DeviceId] = config;
        _logger?.LogInformation("Device config added: {DeviceId} ({DeviceType})", config.DeviceId, config.DeviceType);
    }

    /// <summary>
    /// 여러 디바이스 설정 추가
    /// </summary>
    public void AddDeviceConfigs(IEnumerable<UnifiedDeviceConfig> configs)
    {
        foreach (var config in configs)
        {
            AddDeviceConfig(config);
        }
    }

    /// <summary>
    /// 디바이스 설정 제거
    /// </summary>
    public bool RemoveDeviceConfig(string deviceId)
    {
        return _deviceConfigs.TryRemove(deviceId, out _);
    }

    /// <summary>
    /// 디바이스 데이터 읽기
    /// </summary>
    public async Task<UnifiedDeviceData?> ReadDeviceDataAsync(string deviceId)
    {
        if (!IsConnected || _session == null)
        {
            _logger?.LogWarning("Not connected to OPC UA server");
            return null;
        }

        if (!_deviceConfigs.TryGetValue(deviceId, out var config))
        {
            _logger?.LogWarning("Device config not found: {DeviceId}", deviceId);
            return null;
        }

        try
        {
            var data = new UnifiedDeviceData
            {
                DeviceId = config.DeviceId,
                ChannelName = config.ChannelName,
                DeviceName = config.DeviceName,
                DeviceType = config.DeviceType
            };

            var nodesToRead = new ReadValueIdCollection();
            var tagNames = config.GetAllTagNames().ToList();

            foreach (var tagName in tagNames)
            {
                var nodeId = config.GetNodeId(tagName);
                nodesToRead.Add(new ReadValueId
                {
                    NodeId = new NodeId(nodeId),
                    AttributeId = Attributes.Value
                });
            }

            _session.Read(
                null,
                0,
                TimestampsToReturn.Both,
                nodesToRead,
                out DataValueCollection results,
                out DiagnosticInfoCollection diagnostics);

            for (int i = 0; i < tagNames.Count && i < results.Count; i++)
            {
                var tagName = tagNames[i];
                var result = results[i];

                if (StatusCode.IsGood(result.StatusCode))
                {
                    data.TagValues[tagName] = result.Value;
                }
            }

            data.Timestamp = DateTime.UtcNow;
            data.IsGoodQuality = results.All(r => StatusCode.IsGood(r.StatusCode));

            _deviceDataCache[deviceId] = data;
            return data;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to read device data: {DeviceId}", deviceId);
            OnErrorOccurred(new ErrorOccurredEventArgs($"Read failed: {ex.Message}", ex));
            return null;
        }
    }

    /// <summary>
    /// 모든 디바이스 데이터 읽기
    /// </summary>
    public async Task<Dictionary<string, UnifiedDeviceData>> ReadAllDeviceDataAsync()
    {
        var results = new Dictionary<string, UnifiedDeviceData>();

        foreach (var deviceId in _deviceConfigs.Keys)
        {
            var data = await ReadDeviceDataAsync(deviceId);
            if (data != null)
            {
                results[deviceId] = data;
            }
        }

        return results;
    }

    #endregion

    #region 태그 쓰기

    /// <summary>
    /// 태그 값 쓰기
    /// </summary>
    public async Task<bool> WriteTagAsync(string deviceId, string tagName, object value)
    {
        if (!IsConnected || _session == null)
        {
            _logger?.LogWarning("Not connected to OPC UA server");
            return false;
        }

        if (!_deviceConfigs.TryGetValue(deviceId, out var config))
        {
            _logger?.LogWarning("Device config not found: {DeviceId}", deviceId);
            return false;
        }

        try
        {
            var nodeId = config.GetNodeId(tagName);

            var writeValues = new WriteValueCollection
            {
                new WriteValue
                {
                    NodeId = new NodeId(nodeId),
                    AttributeId = Attributes.Value,
                    Value = new DataValue(new Variant(value))
                }
            };

            _session.Write(
                null,
                writeValues,
                out StatusCodeCollection results,
                out DiagnosticInfoCollection diagnostics);

            var success = StatusCode.IsGood(results[0]);

            if (success)
            {
                Console.WriteLine($"[OPC UA] ✅ Write 성공: {deviceId}.{tagName} = {value}");
                _logger?.LogInformation("Write success: {DeviceId}.{Tag} = {Value}", deviceId, tagName, value);
            }
            else
            {
                Console.WriteLine($"[OPC UA] ❌ Write 실패: {deviceId}.{tagName} StatusCode={results[0]}");
                _logger?.LogWarning("Write failed: {DeviceId}.{Tag} StatusCode={StatusCode}",
                    deviceId, tagName, results[0]);
            }

            OnWriteCompleted(deviceId, tagName, value, success,
                success ? null : $"StatusCode: {results[0]}");

            return success;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OPC UA] ❌ Write 예외: {deviceId}.{tagName} - {ex.Message}");
            _logger?.LogError(ex, "Write exception: {DeviceId}.{Tag}", deviceId, tagName);
            OnWriteCompleted(deviceId, tagName, value, false, ex.Message);
            OnErrorOccurred(new ErrorOccurredEventArgs($"Write failed: {ex.Message}", ex));
            return false;
        }
    }

    /// <summary>
    /// 여러 태그 값 쓰기
    /// </summary>
    public async Task<Dictionary<string, bool>> WriteTagsAsync(string deviceId, Dictionary<string, object> tagValues)
    {
        var results = new Dictionary<string, bool>();

        foreach (var kv in tagValues)
        {
            results[kv.Key] = await WriteTagAsync(deviceId, kv.Key, kv.Value);
        }

        return results;
    }

    private void OnWriteCompleted(string deviceId, string tagName, object value, bool success, string? error)
    {
        WriteCompleted?.Invoke(this, new WriteCompletedEventArgs(deviceId, tagName, value, success, error));
    }

    #endregion

    #region 구독 관리

    /// <summary>
    /// 디바이스 구독 시작
    /// </summary>
    public async Task StartSubscriptionAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _session == null)
        {
            _logger?.LogWarning("Not connected to OPC UA server");
            return;
        }

        if (!_deviceConfigs.TryGetValue(deviceId, out var config))
        {
            _logger?.LogWarning("Device config not found: {DeviceId}", deviceId);
            return;
        }

        if (_subscriptions.ContainsKey(deviceId))
        {
            _logger?.LogInformation("Subscription already exists for: {DeviceId}", deviceId);
            return;
        }

        try
        {
            var subscription = new Subscription(_session.DefaultSubscription)
            {
                PublishingInterval = _connectionInfo.PublishingInterval,
                DisplayName = $"Sub_{deviceId}"
            };

            var monitoredItems = new List<MonitoredItem>();
            var monitoringTags = config.GetMonitoringTagNames().ToList();

            foreach (var tagName in monitoringTags)
            {
                var nodeId = config.GetNodeId(tagName);
                monitoredItems.Add(CreateMonitoredItem(nodeId, $"{deviceId}.{tagName}"));
            }

            subscription.AddItems(monitoredItems);
            _session.AddSubscription(subscription);
            subscription.Create();

            foreach (var item in monitoredItems)
            {
                item.Notification += (sender, e) => OnMonitoredItemNotification(deviceId, config, sender, e);
            }

            _subscriptions[deviceId] = subscription;
            _logger?.LogInformation("Subscription started for: {DeviceId} ({Count} tags)", deviceId, monitoringTags.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start subscription: {DeviceId}", deviceId);
            OnErrorOccurred(new ErrorOccurredEventArgs($"Subscription failed: {ex.Message}", ex));
        }
    }

    private MonitoredItem CreateMonitoredItem(string nodeId, string displayName)
    {
        return new MonitoredItem
        {
            StartNodeId = new NodeId(nodeId),
            AttributeId = Attributes.Value,
            DisplayName = displayName,
            SamplingInterval = _connectionInfo.SamplingInterval,
            QueueSize = 1,
            DiscardOldest = true
        };
    }

    private void OnMonitoredItemNotification(string deviceId, UnifiedDeviceConfig config,
        MonitoredItem item, MonitoredItemNotificationEventArgs e)
    {
        try
        {
            if (e.NotificationValue is not MonitoredItemNotification notification)
                return;

            var dataValue = notification.Value;

            if (!_deviceDataCache.TryGetValue(deviceId, out var data))
            {
                data = new UnifiedDeviceData
                {
                    DeviceId = deviceId,
                    ChannelName = config.ChannelName,
                    DeviceName = config.DeviceName,
                    DeviceType = config.DeviceType
                };
            }

            // 태그명 추출 (DisplayName: "DeviceId.TagName")
            var parts = item.DisplayName.Split('.');
            var tagName = parts.Length > 1 ? parts[1] : item.DisplayName;

            data.TagValues[tagName] = dataValue.Value;
            data.Timestamp = DateTime.UtcNow;
            data.IsGoodQuality = StatusCode.IsGood(dataValue.StatusCode);
            _deviceDataCache[deviceId] = data;

            DataChanged?.Invoke(this, new UnifiedDataChangedEventArgs(deviceId, data.Clone()));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing notification: {DeviceId}", deviceId);
        }
    }

    /// <summary>
    /// 디바이스 구독 중지
    /// </summary>
    public async Task StopSubscriptionAsync(string deviceId)
    {
        if (_subscriptions.TryRemove(deviceId, out var subscription))
        {
            try
            {
                _session?.RemoveSubscription(subscription);
                _logger?.LogInformation("Subscription stopped for: {DeviceId}", deviceId);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error stopping subscription: {DeviceId}", deviceId);
            }
        }
    }

    /// <summary>
    /// 모든 디바이스 구독 시작
    /// </summary>
    public async Task StartAllSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        foreach (var deviceId in _deviceConfigs.Keys)
        {
            await StartSubscriptionAsync(deviceId, cancellationToken);
        }
    }

    /// <summary>
    /// 모든 디바이스 구독 중지
    /// </summary>
    public async Task StopAllSubscriptionsAsync()
    {
        foreach (var deviceId in _subscriptions.Keys.ToList())
        {
            await StopSubscriptionAsync(deviceId);
        }
    }

    #endregion

    #region 캐시 데이터 조회

    /// <summary>
    /// 캐시된 디바이스 데이터 조회
    /// </summary>
    public UnifiedDeviceData? GetCachedData(string deviceId)
    {
        return _deviceDataCache.TryGetValue(deviceId, out var data) ? data.Clone() : null;
    }

    /// <summary>
    /// 모든 캐시된 데이터 조회
    /// </summary>
    public Dictionary<string, UnifiedDeviceData> GetAllCachedData()
    {
        return _deviceDataCache.ToDictionary(kv => kv.Key, kv => kv.Value.Clone());
    }

    #endregion

    #region Helper

    private void OnErrorOccurred(ErrorOccurredEventArgs e)
    {
        ErrorOccurred?.Invoke(this, e);
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _reconnectCts?.Cancel();
            _reconnectCts?.Dispose();
            _connectionLock.Dispose();
            DisconnectAsync().GetAwaiter().GetResult();
        }

        _disposed = true;
    }

    #endregion
}

/// <summary>
/// 통합 데이터 변경 이벤트
/// </summary>
public class UnifiedDataChangedEventArgs : EventArgs
{
    public string DeviceId { get; }
    public UnifiedDeviceData Data { get; }

    public UnifiedDataChangedEventArgs(string deviceId, UnifiedDeviceData data)
    {
        DeviceId = deviceId;
        Data = data;
    }
}
