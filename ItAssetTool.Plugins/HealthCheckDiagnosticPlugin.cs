// In Project: ItAssetTool.Plugins
// File: HealthCheckDiagnosticPlugin.cs
using ItAssetTool.Core;
using Microsoft.Extensions.Logging; // <-- 添加 using
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.Versioning;
using System.Threading; // <-- 添加 using for Thread
using System.Threading.Tasks;

namespace ItAssetTool.Plugins;

[SupportedOSPlatform("windows")]
public class HealthCheckDiagnosticPlugin : IDiagnosticPlugin
{
    private readonly ILogger<HealthCheckDiagnosticPlugin> _logger; // <-- 添加 logger

    public string Name => "系统综合诊断";

    // 构造函数注入
    public HealthCheckDiagnosticPlugin(ILogger<HealthCheckDiagnosticPlugin> logger)
    {
        _logger = logger;
    }

    public async Task<List<DiagnosticResult>> RunDiagnosticAsync()
    {
        _logger.LogInformation("开始执行系统综合诊断...");
        var results = new List<DiagnosticResult>();

        // 将同步操作包装在 Task.Run 中以避免阻塞
        await Task.Run(() =>
        {
            _logger.LogDebug("检查系统基本信息...");
            CheckSystemBasics(results);
            _logger.LogDebug("检查系统性能...");
            CheckPerformance(results);
            _logger.LogDebug("检查 Windows 健康状况...");
            CheckWindowsHealth(results);
            _logger.LogDebug("检查硬件状态...");
            CheckHardwareStatus(results);
        });

        _logger.LogDebug("检查网络连接...");
        await CheckNetworkAsync(results);

        _logger.LogInformation("系统综合诊断完成，共产生 {ResultCount} 条结果。", results.Count);
        return results;
    }

    private void CheckSystemBasics(List<DiagnosticResult> results)
    {
        results.Add(new DiagnosticResult { Task = "权限检查", Status = "信息", Message = "请确保以管理员身份运行以获得完整信息。", Value = 1 });

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT LastBootUpTime FROM Win32_OperatingSystem");
            using var os = searcher.Get().OfType<ManagementObject>().FirstOrDefault(); // 使用 using
            if (os != null)
            {
                var lastBootUpTime = ManagementDateTimeConverter.ToDateTime(os["LastBootUpTime"]?.ToString() ?? string.Empty);
                var uptime = DateTime.Now - lastBootUpTime;
                var uptimeMessage = $"{uptime.Days}天 {uptime.Hours}小时 {uptime.Minutes}分钟";
                results.Add(new DiagnosticResult { Task = "系统运行时长", Status = "信息", Message = uptimeMessage, Value = uptime.TotalHours });
                _logger.LogDebug("系统运行时长: {Uptime}", uptimeMessage);
            }
            else
            {
                _logger.LogWarning("无法获取 Win32_OperatingSystem 信息。");
                results.Add(new DiagnosticResult { Task = "系统运行时长", Status = "失败", Message = "无法获取操作系统信息。" });
            }
        }
        catch (ManagementException wmiEx)
        {
            _logger.LogError(wmiEx, "查询系统运行时长时发生 WMI 错误");
            results.Add(new DiagnosticResult { Task = "系统运行时长", Status = "错误", Message = $"WMI 查询失败: {wmiEx.Message}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查系统运行时长时发生错误");
            results.Add(new DiagnosticResult { Task = "系统运行时长", Status = "错误", Message = ex.Message });
        }
    }

    private void CheckPerformance(List<DiagnosticResult> results)
    {
        try
        {
            // 获取 CPU 使用率
            try
            {
                // 使用 using 确保 PerformanceCounter 被释放
                using var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                cpuCounter.NextValue(); // Call NextValue once to initialize the counter
                Thread.Sleep(1000); // Wait for a second to get a valid reading
                float cpuUsage = cpuCounter.NextValue();
                results.Add(new DiagnosticResult { Task = "CPU 总体使用率", Status = cpuUsage > 90 ? "警告" : "正常", Message = $"{cpuUsage:F1}%", Value = cpuUsage });
                _logger.LogDebug("CPU 使用率: {CpuUsage}%", cpuUsage);
            }
            catch (InvalidOperationException ioEx) // PerformanceCounter 可能因权限或类别不存在抛出
            {
                _logger.LogError(ioEx, "获取 CPU 性能计数器失败 (InvalidOperationException)。请确保以管理员身份运行，并且性能计数器未损坏。");
                results.Add(new DiagnosticResult { Task = "CPU 总体使用率", Status = "错误", Message = $"计数器无效: {ioEx.Message}" });
            }
            catch (Exception cpuEx)
            {
                _logger.LogError(cpuEx, "获取 CPU 性能计数器时发生未知错误");
                results.Add(new DiagnosticResult { Task = "CPU 总体使用率", Status = "错误", Message = $"无法获取: {cpuEx.Message}" });
            }


            // 获取内存使用率
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
                using var os = searcher.Get().OfType<ManagementObject>().FirstOrDefault(); // 使用 using
                if (os != null)
                {
                    if (ulong.TryParse(os["TotalVisibleMemorySize"]?.ToString(), out ulong totalMemKB) &&
                        ulong.TryParse(os["FreePhysicalMemory"]?.ToString(), out ulong freeMemKB))
                    {
                        var usedMemKB = totalMemKB > freeMemKB ? totalMemKB - freeMemKB : 0; // 防止 free > total 的罕见情况
                        var memPercent = totalMemKB > 0 ? (double)usedMemKB / totalMemKB * 100 : 0;
                        results.Add(new DiagnosticResult { Task = "内存使用率", Status = memPercent > 90 ? "警告" : "正常", Message = $"{memPercent:F1}% ({usedMemKB / 1024.0 / 1024.0:F2} GB / {totalMemKB / 1024.0 / 1024.0:F2} GB)", Value = memPercent });
                        _logger.LogDebug("内存使用率: {MemPercent}%", memPercent);
                    }
                    else
                    {
                        _logger.LogWarning("无法解析 Win32_OperatingSystem 中的内存大小。Total={TotalMem}, Free={FreeMem}", os["TotalVisibleMemorySize"], os["FreePhysicalMemory"]);
                        results.Add(new DiagnosticResult { Task = "内存使用率", Status = "失败", Message = "无法解析内存大小。" });
                    }
                }
                else
                {
                    _logger.LogWarning("无法获取 Win32_OperatingSystem 信息以计算内存使用率。");
                    results.Add(new DiagnosticResult { Task = "内存使用率", Status = "失败", Message = "无法获取操作系统信息。" });
                }
            }
            catch (ManagementException wmiEx)
            {
                _logger.LogError(wmiEx, "查询内存使用率时发生 WMI 错误");
                results.Add(new DiagnosticResult { Task = "内存使用率", Status = "错误", Message = $"WMI 查询失败: {wmiEx.Message}" });
            }
            catch (Exception memEx)
            {
                _logger.LogError(memEx, "获取内存使用率时发生错误");
                results.Add(new DiagnosticResult { Task = "内存使用率", Status = "错误", Message = $"无法获取: {memEx.Message}" });
            }
        }
        catch (Exception ex) // 捕获 PerformanceCounter 初始化等可能发生的其他错误
        {
            _logger.LogError(ex, "检查性能时发生意外错误");
            results.Add(new DiagnosticResult { Task = "性能诊断", Status = "错误", Message = $"无法获取性能数据: {ex.Message}" });
        }
    }

    private void CheckWindowsHealth(List<DiagnosticResult> results)
    {
        var criticalServices = new Dictionary<string, string> { { "Spooler", "打印服务" }, { "wuauserv", "更新服务" }, { "BFE", "防火墙服务" } };
        _logger.LogDebug("开始检查关键 Windows 服务状态...");
        foreach (var service in criticalServices)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher($"SELECT State FROM Win32_Service WHERE Name = '{service.Key}'");
                using var serviceObj = searcher.Get().OfType<ManagementObject>().FirstOrDefault(); // 使用 using
                if (serviceObj != null)
                {
                    var state = serviceObj["State"]?.ToString() ?? "未知";
                    bool isRunning = state.Equals("Running", StringComparison.OrdinalIgnoreCase);
                    results.Add(new DiagnosticResult { Task = $"服务 ({service.Value})", Status = isRunning ? "正常" : "警告", Message = $"状态: {state}", Value = isRunning ? 1 : 0 });
                    _logger.LogDebug("服务 {ServiceName} ({ServiceKey}) 状态: {State}", service.Value, service.Key, state);
                }
                else
                {
                    _logger.LogWarning("未找到服务 {ServiceKey} ({ServiceName})。", service.Key, service.Value);
                    results.Add(new DiagnosticResult { Task = $"服务 ({service.Value})", Status = "失败", Message = "未找到该服务。", Value = -1 });
                }
            }
            catch (ManagementException wmiEx)
            {
                _logger.LogError(wmiEx, "查询服务 {ServiceKey} ({ServiceName}) 状态时发生 WMI 错误", service.Key, service.Value);
                results.Add(new DiagnosticResult { Task = $"服务 ({service.Value})", Status = "错误", Message = $"WMI 查询失败: {wmiEx.Message}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检查服务 {ServiceKey} ({ServiceName}) 状态时发生错误", service.Key, service.Value);
                results.Add(new DiagnosticResult { Task = $"服务 ({service.Value})", Status = "错误", Message = $"查询失败: {ex.Message}" });
            }
        }
        _logger.LogDebug("关键 Windows 服务状态检查完成。");
    }

    private void CheckHardwareStatus(List<DiagnosticResult> results)
    {
        _logger.LogDebug("开始检查硬盘 SMART 状态...");
        try
        {
            using var searcher = new ManagementObjectSearcher(@"\\.\root\wmi", "SELECT InstanceName, PredictFailure FROM MSStorageDriver_FailurePredictStatus");
            var drives = searcher.Get().OfType<ManagementObject>().ToList();

            if (!drives.Any())
            {
                _logger.LogWarning("在 root\\wmi 下未找到 MSStorageDriver_FailurePredictStatus 信息。尝试 Win32_DiskDrive。");
                using var fallbackSearcher = new ManagementObjectSearcher("SELECT Caption, Status FROM Win32_DiskDrive");
                var fallbackDrives = fallbackSearcher.Get().OfType<ManagementObject>().ToList(); // 避免变量重用

                foreach (var drive in fallbackDrives) using (drive) // 使用 using
                    {
                        var caption = drive["Caption"]?.ToString() ?? "未知硬盘";
                        var status = drive["Status"]?.ToString() ?? "未知";
                        bool isOk = status.Equals("OK", StringComparison.OrdinalIgnoreCase);
                        results.Add(new DiagnosticResult { Task = $"硬盘健康 ({caption})", Status = isOk ? "正常" : "警告", Message = $"Win32_DiskDrive 状态: {status}", Value = isOk ? 1 : 0 });
                        _logger.LogDebug("硬盘 {Caption} Win32_DiskDrive 状态: {Status}", caption, status);
                    }
                fallbackDrives.Clear(); // 释放列表内存
                return;
            }

            foreach (var drive in drives) using (drive) // 使用 using
                {
                    var instanceName = drive["InstanceName"]?.ToString() ?? "未知硬盘";
                    // PredictFailure 是布尔类型
                    var predictFailure = drive["PredictFailure"] != null && (bool)drive["PredictFailure"];
                    var status = predictFailure ? "警告" : "正常";
                    var message = $"SMART 预测失败: {(predictFailure ? "是" : "否")}";

                    results.Add(new DiagnosticResult { Task = $"硬盘健康 ({instanceName})", Status = status, Message = message, Value = predictFailure ? 0 : 1 });
                    _logger.LogDebug("硬盘 {InstanceName} SMART 状态: {Message}", instanceName, message);
                }
            drives.Clear(); // 释放列表内存
        }
        catch (ManagementException wmiEx)
        {
            // 特别处理权限不足
            if (wmiEx.ErrorCode == ManagementStatus.AccessDenied)
            {
                _logger.LogError(wmiEx, "查询硬盘 SMART 状态失败 - 权限不足 (需要管理员权限访问 root\\wmi)。");
                results.Add(new DiagnosticResult { Task = "硬盘健康", Status = "错误", Message = "权限不足" });
            }
            else
            {
                _logger.LogError(wmiEx, "查询硬盘 SMART 状态时发生 WMI 错误");
                results.Add(new DiagnosticResult { Task = "硬盘健康", Status = "错误", Message = $"WMI 查询失败: {wmiEx.Message}" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查硬盘 SMART 状态时发生错误");
            results.Add(new DiagnosticResult { Task = "硬盘健康", Status = "错误", Message = $"查询失败: {ex.Message}" });
        }
        _logger.LogDebug("硬盘 SMART 状态检查完成。");
    }

    private async Task CheckNetworkAsync(List<DiagnosticResult> results)
    {
        IPAddress? gateway = null;
        try
        {
            gateway = GetDefaultGateway();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取默认网关时出错");
            results.Add(new DiagnosticResult { Task = "内网网关", Status = "错误", Message = $"获取网关失败: {ex.Message}" });
        }

        if (gateway != null)
        {
            await PerformPingAsync($"内网网关 ({gateway})", gateway.ToString(), results);
        }
        else if (!results.Any(r => r.Task == "内网网关" && r.Status == "错误"))
        {
            _logger.LogWarning("未能自动找到内网网关地址。");
            results.Add(new DiagnosticResult { Task = "内网网关", Status = "警告", Message = "未能自动找到内网网关地址。" });
        }

        // Ping 公共 DNS 和网站
        await PerformPingAsync("外网连接 (114 DNS)", "114.114.114.114", results);
        await PerformPingAsync("外网连接 (Baidu)", "www.baidu.com", results);
    }

    private IPAddress? GetDefaultGateway()
    {
        _logger.LogDebug("正在查找默认网关...");
        try
        {
            var gateway = NetworkInterface
                .GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                            (n.NetworkInterfaceType == NetworkInterfaceType.Ethernet || n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211))
                .Select(n => n.GetIPProperties()?.GatewayAddresses.FirstOrDefault()?.Address)
                .FirstOrDefault(g => g != null && g.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

            if (gateway != null) { _logger.LogDebug("找到默认网关: {Gateway}", gateway); }
            else { _logger.LogWarning("未找到有效的默认网关。"); }
            return gateway;
        }
        catch (NetworkInformationException netEx)
        {
            _logger.LogError(netEx, "获取网络接口信息时出错");
            return null;
        }
    }

    private async Task PerformPingAsync(string taskName, string target, List<DiagnosticResult> results)
    {
        _logger.LogDebug("正在 Ping 目标: {Target} ({TaskName})", target, taskName);
        PingReply? reply = null; // 声明在 try 外部
        try
        {
            using var pinger = new Ping();
            reply = await pinger.SendPingAsync(target, 2000); // 超时设置为 2 秒

            if (reply.Status == IPStatus.Success)
            {
                var message = $"连接成功，延迟: {reply.RoundtripTime} ms";
                results.Add(new DiagnosticResult { Task = taskName, Status = "正常", Message = message, Value = reply.RoundtripTime });
                _logger.LogDebug("Ping {Target} 成功: {Message}", target, message);
            }
            else
            {
                var message = $"连接失败: {reply.Status}";
                results.Add(new DiagnosticResult { Task = taskName, Status = "失败", Message = message });
                _logger.LogWarning("Ping {Target} 失败: {Status}", target, reply.Status);
            }
        }
        catch (PingException pingEx)
        {
            _logger.LogError(pingEx, "Ping {Target} 时发生 PingException (状态: {Status})", target, reply?.Status); // 包含 Ping 状态
            results.Add(new DiagnosticResult { Task = taskName, Status = "错误", Message = $"Ping 失败: {pingEx.Message}" });
        }
        catch (ArgumentNullException argNullEx) // SendPingAsync 可能因 target 为 null 抛出
        {
            _logger.LogError(argNullEx, "Ping 目标 '{Target}' 无效 (ArgumentNullException)", target);
            results.Add(new DiagnosticResult { Task = taskName, Status = "错误", Message = "目标地址无效" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ping {Target} 时发生意外错误", target);
            results.Add(new DiagnosticResult { Task = taskName, Status = "错误", Message = $"Ping 异常: {ex.Message}" });
        }
    }
}