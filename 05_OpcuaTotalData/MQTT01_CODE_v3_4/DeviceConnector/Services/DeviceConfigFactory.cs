namespace DeviceConnector.Services;

using DeviceConnector.Models;

/// <summary>
/// 디바이스 설정 팩토리
/// KEPServerEX 채널별 디바이스 설정 생성
/// </summary>
public static class DeviceConfigFactory
{
    /// <summary>
    /// MqttTest 채널 - SimDevice01 설정 생성
    /// KEPServerEX 8-Bit Simulator 디바이스
    /// </summary>
    public static UnifiedDeviceConfig CreateSimulatorConfig(
        string deviceId = "SimDevice01",
        string channelName = "MqttTest",
        string deviceName = "SimDevice01")
    {
        return new UnifiedDeviceConfig
        {
            DeviceId = deviceId,
            ChannelName = channelName,
            DeviceName = deviceName,
            DeviceType = DeviceType.Simulator,
            NamespaceIndex = 2,
            UseTagGroups = true,  // Simulator는 태그 그룹 사용
            Tags = new Dictionary<string, TagDefinition>
            {
                // Monitoring Group (Read)
                ["Temperature"] = new TagDefinition 
                { 
                    Group = "Monitoring", 
                    Direction = TagDirection.Read, 
                    DataType = typeof(float) 
                },
                ["Pressure"] = new TagDefinition 
                { 
                    Group = "Monitoring", 
                    Direction = TagDirection.Read, 
                    DataType = typeof(float) 
                },
                ["MotorRPM"] = new TagDefinition 
                { 
                    Group = "Monitoring", 
                    Direction = TagDirection.Read, 
                    DataType = typeof(float) 
                },

                // Control Group (Read/Write)
                ["MotorStart"] = new TagDefinition 
                { 
                    Group = "Control", 
                    Direction = TagDirection.ReadWrite, 
                    DataType = typeof(bool) 
                },
                ["MotorStop"] = new TagDefinition 
                { 
                    Group = "Control", 
                    Direction = TagDirection.ReadWrite, 
                    DataType = typeof(bool) 
                },
                ["SpeedSetpoint"] = new TagDefinition 
                { 
                    Group = "Control", 
                    Direction = TagDirection.ReadWrite, 
                    DataType = typeof(ushort) 
                },
                ["ModeSelect"] = new TagDefinition 
                { 
                    Group = "Control", 
                    Direction = TagDirection.ReadWrite, 
                    DataType = typeof(ushort) 
                },

                // Status Group (Read/Write)
                ["Alarm01"] = new TagDefinition 
                { 
                    Group = "Status", 
                    Direction = TagDirection.ReadWrite, 
                    DataType = typeof(bool) 
                },
                ["Alarm02"] = new TagDefinition 
                { 
                    Group = "Status", 
                    Direction = TagDirection.ReadWrite, 
                    DataType = typeof(bool) 
                },
                ["RunningFlag"] = new TagDefinition 
                { 
                    Group = "Status", 
                    Direction = TagDirection.ReadWrite, 
                    DataType = typeof(bool) 
                }
            }
        };
    }

    /// <summary>
    /// STM 채널 - Stm_yolo 설정 생성
    /// STM Yolo 컨베이어 제어 디바이스
    /// </summary>
    public static UnifiedDeviceConfig CreateSTMYoloConfig(
        string deviceId = "STM_yolo",
        string channelName = "STM",
        string deviceName = "Stm_yolo")
    {
        return new UnifiedDeviceConfig
        {
            DeviceId = deviceId,
            ChannelName = channelName,
            DeviceName = deviceName,
            DeviceType = DeviceType.STMYolo,
            NamespaceIndex = 2,
            UseTagGroups = false,  // STM은 태그 그룹 없음
            Tags = new Dictionary<string, TagDefinition>
            {
                // Target (Write)
                ["TargetState"] = new TagDefinition 
                { 
                    Direction = TagDirection.Write, 
                    DataType = typeof(long) 
                },
                ["TargetSpeedMain"] = new TagDefinition 
                { 
                    Direction = TagDirection.Write, 
                    DataType = typeof(long) 
                },
                ["TargetSpeedSort"] = new TagDefinition 
                { 
                    Direction = TagDirection.Write, 
                    DataType = typeof(long) 
                },
                ["TargetSpeedLoad"] = new TagDefinition 
                { 
                    Direction = TagDirection.Write, 
                    DataType = typeof(long) 
                },
                ["AgvSortArrived"] = new TagDefinition 
                { 
                    Direction = TagDirection.Write, 
                    DataType = typeof(bool) 
                },
                ["AgvSortDeparted"] = new TagDefinition 
                { 
                    Direction = TagDirection.Write, 
                    DataType = typeof(bool) 
                },
                ["AgvLoadArrived"] = new TagDefinition 
                { 
                    Direction = TagDirection.Write, 
                    DataType = typeof(bool) 
                },
                ["AgvLoadDeparted"] = new TagDefinition 
                { 
                    Direction = TagDirection.Write, 
                    DataType = typeof(bool) 
                },

                // Current (Read)
                ["CurrentState"] = new TagDefinition 
                { 
                    Direction = TagDirection.Read, 
                    DataType = typeof(long) 
                },
                ["CurrentSpeedMain"] = new TagDefinition 
                { 
                    Direction = TagDirection.Read, 
                    DataType = typeof(long) 
                },
                ["CurrentSpeedSort"] = new TagDefinition 
                { 
                    Direction = TagDirection.Read, 
                    DataType = typeof(long) 
                },
                ["CurrentSpeedLoad"] = new TagDefinition 
                { 
                    Direction = TagDirection.Read, 
                    DataType = typeof(long) 
                },
                ["CurrentFloor"] = new TagDefinition 
                { 
                    Direction = TagDirection.Read, 
                    DataType = typeof(long) 
                },
                ["IsLiftMoving"] = new TagDefinition 
                { 
                    Direction = TagDirection.Read, 
                    DataType = typeof(bool) 
                },
                ["IsRobotWorking"] = new TagDefinition 
                { 
                    Direction = TagDirection.Read, 
                    DataType = typeof(bool) 
                },
                ["IsRobotDone"] = new TagDefinition 
                { 
                    Direction = TagDirection.Read, 
                    DataType = typeof(bool) 
                }
            }
        };
    }

    /// <summary>
    /// ESP32 ModbusTCP 설정 생성
    /// </summary>
    public static UnifiedDeviceConfig CreateESP32Config(
        string deviceId = "ESP32_01",
        string channelName = "ModbusTCP",
        string deviceName = "ESP32_01")
    {
        return new UnifiedDeviceConfig
        {
            DeviceId = deviceId,
            ChannelName = channelName,
            DeviceName = deviceName,
            DeviceType = DeviceType.ESP32,
            NamespaceIndex = 2,
            UseTagGroups = false,
            Tags = new Dictionary<string, TagDefinition>
            {
                // Read Tags
                ["POS_X"] = new TagDefinition 
                { 
                    Direction = TagDirection.Read, 
                    DataType = typeof(float) 
                },
                ["POS_Y"] = new TagDefinition 
                { 
                    Direction = TagDirection.Read, 
                    DataType = typeof(float) 
                },
                ["POS_T"] = new TagDefinition 
                { 
                    Direction = TagDirection.Read, 
                    DataType = typeof(float) 
                },

                // Write Tags
                ["TargetA"] = new TagDefinition 
                { 
                    Direction = TagDirection.ReadWrite, 
                    DataType = typeof(bool) 
                },
                ["Control"] = new TagDefinition 
                { 
                    Direction = TagDirection.Write, 
                    DataType = typeof(string) 
                },
                ["State"] = new TagDefinition 
                { 
                    Direction = TagDirection.Write, 
                    DataType = typeof(string) 
                }
            }
        };
    }

    /// <summary>
    /// 모든 디바이스 설정 생성
    /// </summary>
    public static List<UnifiedDeviceConfig> CreateAllConfigs()
    {
        return new List<UnifiedDeviceConfig>
        {
            CreateSimulatorConfig(),
            CreateSTMYoloConfig()
            // ESP32는 현재 디바이스 서버가 없으므로 제외
        };
    }

    /// <summary>
    /// 테스트용 설정 생성 (Simulator만)
    /// </summary>
    public static List<UnifiedDeviceConfig> CreateTestConfigs()
    {
        return new List<UnifiedDeviceConfig>
        {
            CreateSimulatorConfig()
        };
    }
}
