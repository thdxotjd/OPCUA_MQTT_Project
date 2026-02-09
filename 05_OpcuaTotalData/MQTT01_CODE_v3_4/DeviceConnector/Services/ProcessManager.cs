using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace DeviceConnector.Services
{
    /// <summary>
    /// 외부 프로세스(Node-RED, InfluxDB, Mosquitto) 자동 실행 및 관리
    /// </summary>
    public class ProcessManager : IDisposable
    {
        private Process? _nodeRedProcess;
        private Process? _influxDbProcess;
        private Process? _mosquittoProcess;

        private readonly string _mosquittoPath;
        private readonly string _influxDbPath;

        private bool _disposed = false;

        public bool IsNodeRedRunning => IsProcessRunning("node");
        public bool IsInfluxDbRunning => IsProcessRunning("influxd");
        public bool IsMosquittoRunning => IsProcessRunning("mosquitto");

        /// <summary>
        /// 기본 경로로 ProcessManager 생성
        /// </summary>
        public ProcessManager() 
            : this(@"C:\Program Files\mosquitto", @"C:\Users\pc\Desktop\InfluxDB\influxdb2-2.7.5-windows")
        {
        }

        /// <summary>
        /// 사용자 지정 경로로 ProcessManager 생성
        /// </summary>
        public ProcessManager(string mosquittoPath, string influxDbPath)
        {
            _mosquittoPath = mosquittoPath;
            _influxDbPath = influxDbPath;
        }

        /// <summary>
        /// 모든 서비스 시작
        /// </summary>
        public async Task StartAllServicesAsync()
        {
            Console.WriteLine();
            Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║     외부 서비스 자동 시작                                   ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            await StartMosquittoAsync();
            await Task.Delay(2000);

            await StartInfluxDbAsync();
            await Task.Delay(3000);

            await StartNodeRedAsync();
            await Task.Delay(2000);

            Console.WriteLine();
            PrintStatus();
        }

        /// <summary>
        /// Mosquitto MQTT Broker 시작
        /// </summary>
        public async Task StartMosquittoAsync()
        {
            try
            {
                if (IsProcessRunning("mosquitto"))
                {
                    Console.WriteLine("[Mosquitto] ✓ 이미 실행 중입니다.");
                    return;
                }

                string exePath = Path.Combine(_mosquittoPath, "mosquitto.exe");
                string configPath = Path.Combine(_mosquittoPath, "mosquitto.conf");

                if (!File.Exists(exePath))
                {
                    Console.WriteLine($"[Mosquitto] ✗ 실행 파일을 찾을 수 없습니다: {exePath}");
                    return;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = File.Exists(configPath) ? $"-c \"{configPath}\" -v" : "-v",
                    WorkingDirectory = _mosquittoPath,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Minimized
                };

                _mosquittoProcess = Process.Start(startInfo);

                if (_mosquittoProcess != null)
                {
                    Console.WriteLine($"[Mosquitto] ✓ 시작됨 (PID: {_mosquittoProcess.Id})");
                    Console.WriteLine($"            포트: 1883");
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Mosquitto] ✗ 시작 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// InfluxDB 시작
        /// </summary>
        public async Task StartInfluxDbAsync()
        {
            try
            {
                if (IsProcessRunning("influxd"))
                {
                    Console.WriteLine("[InfluxDB] ✓ 이미 실행 중입니다.");
                    return;
                }

                string exePath = Path.Combine(_influxDbPath, "influxd.exe");

                if (!File.Exists(exePath))
                {
                    Console.WriteLine($"[InfluxDB] ✗ 실행 파일을 찾을 수 없습니다: {exePath}");
                    return;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    WorkingDirectory = _influxDbPath,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Minimized
                };

                _influxDbProcess = Process.Start(startInfo);

                if (_influxDbProcess != null)
                {
                    Console.WriteLine($"[InfluxDB] ✓ 시작됨 (PID: {_influxDbProcess.Id})");
                    Console.WriteLine($"           URL: http://localhost:8086");
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[InfluxDB] ✗ 시작 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// Node-RED 시작
        /// </summary>
        public async Task StartNodeRedAsync()
        {
            try
            {
                if (IsProcessRunning("node"))
                {
                    Console.WriteLine("[Node-RED] ✓ 이미 실행 중일 수 있습니다.");
                    return;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c node-red",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Minimized
                };

                _nodeRedProcess = Process.Start(startInfo);

                if (_nodeRedProcess != null)
                {
                    Console.WriteLine($"[Node-RED] ✓ 시작됨 (PID: {_nodeRedProcess.Id})");
                    Console.WriteLine($"           URL: http://localhost:1880");
                    Console.WriteLine($"           Dashboard: http://localhost:1880/ui");
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Node-RED] ✗ 시작 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 특정 프로세스가 실행 중인지 확인
        /// </summary>
        private bool IsProcessRunning(string processName)
        {
            return Process.GetProcessesByName(processName).Length > 0;
        }

        /// <summary>
        /// 모든 서비스 상태 출력
        /// </summary>
        public void PrintStatus()
        {
            Console.WriteLine("┌─────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ 외부 서비스 상태                                        │");
            Console.WriteLine("├─────────────────────────────────────────────────────────┤");
            Console.WriteLine($"│ Mosquitto (MQTT)  : {(IsMosquittoRunning ? "✅ 실행 중  (Port: 1883)" : "❌ 중지됨"),-34} │");
            Console.WriteLine($"│ InfluxDB          : {(IsInfluxDbRunning ? "✅ 실행 중  (Port: 8086)" : "❌ 중지됨"),-34} │");
            Console.WriteLine($"│ Node-RED          : {(IsNodeRedRunning ? "✅ 실행 중  (Port: 1880)" : "❌ 중지됨"),-34} │");
            Console.WriteLine("└─────────────────────────────────────────────────────────┘");
        }

        /// <summary>
        /// 관리 중인 프로세스 중지
        /// </summary>
        public void StopManagedServices()
        {
            Console.WriteLine("\n[INFO] 관리 중인 서비스 중지...");

            StopProcess(ref _nodeRedProcess, "Node-RED");
            StopProcess(ref _influxDbProcess, "InfluxDB");
            StopProcess(ref _mosquittoProcess, "Mosquitto");

            Console.WriteLine("[INFO] 완료");
        }

        /// <summary>
        /// 시스템에서 실행 중인 모든 관련 프로세스 강제 종료
        /// </summary>
        public void KillAllRelatedProcesses()
        {
            Console.WriteLine();
            Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║     시스템 프로세스 강제 종료                               ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            KillProcessByName("mosquitto", "Mosquitto");
            KillProcessByName("influxd", "InfluxDB");
            KillProcessByName("node", "Node.js (Node-RED)");

            Console.WriteLine();
            Console.WriteLine("[INFO] 강제 종료 완료");
        }

        private void StopProcess(ref Process? process, string serviceName)
        {
            try
            {
                if (process != null && !process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                    Console.WriteLine($"  [{serviceName}] 중지됨");
                }
                process?.Dispose();
                process = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [{serviceName}] 중지 실패: {ex.Message}");
            }
        }

        private void KillProcessByName(string processName, string displayName)
        {
            try
            {
                var processes = Process.GetProcessesByName(processName);
                if (processes.Length > 0)
                {
                    foreach (var p in processes)
                    {
                        p.Kill();
                        Console.WriteLine($"  [{displayName}] PID {p.Id} 종료됨");
                    }
                }
                else
                {
                    Console.WriteLine($"  [{displayName}] 실행 중인 프로세스 없음");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [{displayName}] 종료 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 브라우저에서 대시보드 열기
        /// </summary>
        public void OpenDashboards()
        {
            Console.WriteLine("\n[INFO] 브라우저에서 대시보드 열기...");

            try
            {
                // Node-RED Editor
                Process.Start(new ProcessStartInfo
                {
                    FileName = "http://localhost:1880",
                    UseShellExecute = true
                });

                // Node-RED Dashboard
                Process.Start(new ProcessStartInfo
                {
                    FileName = "http://localhost:1880/ui",
                    UseShellExecute = true
                });

                // InfluxDB UI
                Process.Start(new ProcessStartInfo
                {
                    FileName = "http://localhost:8086",
                    UseShellExecute = true
                });

                Console.WriteLine("  ✓ Node-RED 에디터   : http://localhost:1880");
                Console.WriteLine("  ✓ Node-RED 대시보드 : http://localhost:1880/ui");
                Console.WriteLine("  ✓ InfluxDB UI       : http://localhost:8086");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 브라우저 열기 실패: {ex.Message}");
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _nodeRedProcess?.Dispose();
                    _influxDbProcess?.Dispose();
                    _mosquittoProcess?.Dispose();
                }
                _disposed = true;
            }
        }
    }
}
