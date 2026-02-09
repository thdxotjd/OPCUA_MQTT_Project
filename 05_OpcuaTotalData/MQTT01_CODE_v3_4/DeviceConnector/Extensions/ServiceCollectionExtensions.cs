namespace DeviceConnector.Extensions;

using DeviceConnector.Interfaces;
using DeviceConnector.Models;
using DeviceConnector.Services;
using DeviceConnector.Mqtt.Interfaces;
using DeviceConnector.Mqtt.Models;
using DeviceConnector.Mqtt.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// DI 컨테이너 확장 메서드
/// </summary>
public static class ServiceCollectionExtensions
{
    #region OPC UA 서비스

    /// <summary>
    /// DeviceConnector 서비스 등록
    /// </summary>
    public static IServiceCollection AddDeviceConnector(
        this IServiceCollection services,
        OpcUaConnectionInfo connectionInfo)
    {
        services.AddSingleton(connectionInfo);
        services.AddSingleton<UnifiedOpcUaClientService>(sp =>
        {
            var logger = sp.GetService<ILogger<UnifiedOpcUaClientService>>();
            return new UnifiedOpcUaClientService(connectionInfo, logger);
        });

        return services;
    }

    /// <summary>
    /// DeviceConnector 서비스 등록 (설정 액션)
    /// </summary>
    public static IServiceCollection AddDeviceConnector(
        this IServiceCollection services,
        Action<OpcUaConnectionInfo> configure)
    {
        var connectionInfo = new OpcUaConnectionInfo();
        configure(connectionInfo);

        return services.AddDeviceConnector(connectionInfo);
    }

    #endregion

    #region MQTT 서비스

    /// <summary>
    /// MQTT 퍼블리셔 서비스 등록
    /// </summary>
    public static IServiceCollection AddMqttPublisher(
        this IServiceCollection services,
        MqttConnectionInfo connectionInfo,
        MqttTopicConfig? topicConfig = null)
    {
        services.AddSingleton(connectionInfo);
        services.AddSingleton(topicConfig ?? new MqttTopicConfig());
        services.AddSingleton<IMqttPublisherService>(sp =>
        {
            var connInfo = sp.GetRequiredService<MqttConnectionInfo>();
            var topicCfg = sp.GetRequiredService<MqttTopicConfig>();
            return new MqttPublisherService(connInfo, topicCfg);
        });

        return services;
    }

    /// <summary>
    /// MQTT 퍼블리셔 서비스 등록 (설정 액션)
    /// </summary>
    public static IServiceCollection AddMqttPublisher(
        this IServiceCollection services,
        Action<MqttConnectionInfo> configureConnection,
        Action<MqttTopicConfig>? configureTopic = null)
    {
        var connectionInfo = new MqttConnectionInfo();
        configureConnection(connectionInfo);

        var topicConfig = new MqttTopicConfig();
        configureTopic?.Invoke(topicConfig);

        return services.AddMqttPublisher(connectionInfo, topicConfig);
    }

    #endregion

    #region 브릿지 서비스

    /// <summary>
    /// 통합 OPC UA - MQTT 브릿지 서비스 등록
    /// </summary>
    public static IServiceCollection AddUnifiedOpcUaMqttBridge(
        this IServiceCollection services,
        UnifiedMqttTopicConfig? topicConfig = null)
    {
        services.AddSingleton(topicConfig ?? new UnifiedMqttTopicConfig());
        services.AddSingleton<UnifiedOpcUaMqttBridgeService>(sp =>
        {
            var opcUaService = sp.GetRequiredService<UnifiedOpcUaClientService>();
            var mqttConfig = sp.GetRequiredService<MqttConnectionInfo>();
            var topicCfg = sp.GetRequiredService<UnifiedMqttTopicConfig>();
            return new UnifiedOpcUaMqttBridgeService(opcUaService, mqttConfig, topicCfg);
        });

        return services;
    }

    /// <summary>
    /// 전체 브릿지 스택 등록 (OPC UA + MQTT + Bridge)
    /// </summary>
    public static IServiceCollection AddFullBridgeStack(
        this IServiceCollection services,
        OpcUaConnectionInfo opcUaConnectionInfo,
        MqttConnectionInfo mqttConnectionInfo,
        UnifiedMqttTopicConfig? topicConfig = null)
    {
        // OPC UA 서비스 등록
        services.AddDeviceConnector(opcUaConnectionInfo);

        // MQTT 설정 등록
        services.AddSingleton(mqttConnectionInfo);

        // 브릿지 서비스 등록
        services.AddUnifiedOpcUaMqttBridge(topicConfig);

        return services;
    }

    #endregion
}
