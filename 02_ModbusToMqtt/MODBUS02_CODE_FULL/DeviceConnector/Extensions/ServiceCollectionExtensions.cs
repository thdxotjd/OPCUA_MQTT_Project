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
        services.AddSingleton<IOpcUaClientService>(sp =>
        {
            var logger = sp.GetService<ILogger<OpcUaClientService>>();
            return new OpcUaClientService(connectionInfo, logger);
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
    /// OPC UA - MQTT 브릿지 서비스 등록
    /// </summary>
    public static IServiceCollection AddOpcUaMqttBridge(this IServiceCollection services)
    {
        services.AddSingleton<IOpcUaMqttBridgeService>(sp =>
        {
            var opcUaService = sp.GetRequiredService<IOpcUaClientService>();
            var mqttService = sp.GetRequiredService<IMqttPublisherService>();
            return new OpcUaMqttBridgeService(opcUaService, mqttService);
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
        MqttTopicConfig? topicConfig = null)
    {
        // OPC UA 서비스 등록
        services.AddDeviceConnector(opcUaConnectionInfo);

        // MQTT 서비스 등록
        services.AddMqttPublisher(mqttConnectionInfo, topicConfig);

        // 브릿지 서비스 등록
        services.AddOpcUaMqttBridge();

        return services;
    }

    #endregion
}
