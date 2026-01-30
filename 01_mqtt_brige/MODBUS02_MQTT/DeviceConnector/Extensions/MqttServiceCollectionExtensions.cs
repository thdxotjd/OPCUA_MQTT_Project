using System;
using DeviceConnector.Interfaces;
using DeviceConnector.Models;
using DeviceConnector.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DeviceConnector.Extensions
{
    /// <summary>
    /// 서비스 컬렉션 확장 메서드
    /// </summary>
    public static class ServiceCollectionExtensions
    {
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
        /// MQTT 퍼블리셔 서비스 등록 (설정 액션 사용)
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

        /// <summary>
        /// OPC UA - MQTT 브릿지 서비스 등록
        /// </summary>
        public static IServiceCollection AddOpcUaMqttBridge(this IServiceCollection services)
        {
            services.AddSingleton<IOpcUaMqttBridgeService>(sp =>
            {
                var opcUaService = sp.GetRequiredService<IOpcUaClientService>();
                var mqttService = sp.GetRequiredService<IMqttPublisherService>();

                // STM_yolo 서비스가 등록되어 있으면 포함
                var stmYoloService = sp.GetService<ISTMYoloClientService>();

                if (stmYoloService != null)
                {
                    return new OpcUaMqttBridgeService(opcUaService, stmYoloService, mqttService);
                }

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
            // OPC UA 서비스는 기존 MODBUS02_CODE에서 등록한다고 가정
            // services.AddDeviceConnector(opcUaConnectionInfo);

            // MQTT 서비스 등록
            services.AddMqttPublisher(mqttConnectionInfo, topicConfig);

            // 브릿지 서비스 등록
            services.AddOpcUaMqttBridge();

            return services;
        }
    }
}
