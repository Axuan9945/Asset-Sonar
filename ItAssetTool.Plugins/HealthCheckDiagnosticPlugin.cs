// In Project: ItAssetTool.Plugins
// File: HealthCheckDiagnosticPlugin.cs
using ItAssetTool.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace ItAssetTool.Plugins;

[SupportedOSPlatform("windows")]
public class HealthCheckDiagnosticPlugin : IDiagnosticPlugin
{
    public string Name => "系统综合诊断";

    public async Task<List<DiagnosticResult>> RunDiagnosticAsync()
    {
        var results = new List<DiagnosticResult>();

        await Task.Run(() =>
        {
            CheckSystemBasics(results);
            CheckPerformance(results);
            CheckWindowsHealth(results);
            CheckHardwareStatus(results);
        });

        await CheckNetworkAsync(results);

        return results;
    }

    private void CheckSystemBasics(List<DiagnosticResult> results)
    {
        results.Add(new DiagnosticResult { Task = "权限检查", Status = "信息", Message = "请确保以管理员身份运行以获得完整信息。", Value = 1 });

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT LastBootUpTime FROM Win32_OperatingSystem");
            var os = searcher.Get().OfType<ManagementObject>().FirstOrDefault();
            if (os != null)
            {
                var lastBootUpTime = ManagementDateTimeConverter.ToDateTime(os["LastBootUpTime"].ToString());
                var uptime = DateTime.Now - lastBootUpTime;
                results.Add(new DiagnosticResult { Task = "系统运行时长", Status = "信息", Message = $"{uptime.Days}天 {uptime.Hours}小时 {uptime.Minutes}分钟", Value = uptime.TotalHours });
            }
        }
        catch (Exception ex)
        {
            results.Add(new DiagnosticResult { Task = "系统运行时长", Status = "错误", Message = ex.Message });
        }
    }

    private void CheckPerformance(List<DiagnosticResult> results)
    {
        try
        {
#pragma warning disable CA1416 // vvvv 核心修改：禁用过时警告 vvvv
            var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            cpuCounter.NextValue();
            System.Threading.Thread.Sleep(1000);
            float cpuUsage = cpuCounter.NextValue();
            results.Add(new DiagnosticResult { Task = "CPU 总体使用率", Status = "正常", Message = $"{cpuUsage:F1}%", Value = cpuUsage });
#pragma warning restore CA1416 // ^^^^ 修改结束 ^^^^

            using var searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
            var os = searcher.Get().OfType<ManagementObject>().FirstOrDefault();
            if (os != null)
            {
                var totalMem = Convert.ToDouble(os["TotalVisibleMemorySize"]);
                var freeMem = Convert.ToDouble(os["FreePhysicalMemory"]);
                var usedMem = totalMem - freeMem;
                var memPercent = (usedMem / totalMem) * 100;
                results.Add(new DiagnosticResult { Task = "内存使用率", Status = "正常", Message = $"{memPercent:F1}%", Value = memPercent });
            }
        }
        catch (Exception ex)
        {
            results.Add(new DiagnosticResult { Task = "性能诊断", Status = "错误", Message = $"无法获取性能数据: {ex.Message}" });
        }
    }

    private void CheckWindowsHealth(List<DiagnosticResult> results)
    {
        var criticalServices = new Dictionary<string, string> { { "Spooler", "打印服务" }, { "wuauserv", "更新服务" }, { "BFE", "防火墙服务" } };
        foreach (var service in criticalServices)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher($"SELECT State FROM Win32_Service WHERE Name = '{service.Key}'");
                var serviceObj = searcher.Get().OfType<ManagementObject>().FirstOrDefault();
                if (serviceObj != null)
                {
                    var state = serviceObj["State"].ToString();
                    bool isRunning = state.Equals("Running", StringComparison.OrdinalIgnoreCase);
                    results.Add(new DiagnosticResult { Task = $"服务 ({service.Value})", Status = isRunning ? "正常" : "警告", Message = $"状态: {state}", Value = isRunning ? 1 : 0 });
                }
                else
                {
                    results.Add(new DiagnosticResult { Task = $"服务 ({service.Value})", Status = "失败", Message = "未找到该服务。", Value = -1 });
                }
            }
            catch { /* 忽略单个服务查询失败 */ }
        }
    }

    private void CheckHardwareStatus(List<DiagnosticResult> results)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Caption, Status FROM Win32_DiskDrive");
            foreach (var drive in searcher.Get().OfType<ManagementObject>())
            {
                bool isOk = (drive["Status"]?.ToString() ?? "").Equals("OK", StringComparison.OrdinalIgnoreCase);
                results.Add(new DiagnosticResult { Task = $"硬盘健康 ({drive["Caption"]})", Status = isOk ? "正常" : "警告", Message = $"S.M.A.R.T. 状态: {drive["Status"]}", Value = isOk ? 1 : 0 });
            }
        }
        catch { /* 忽略 */ }
    }

    private async Task CheckNetworkAsync(List<DiagnosticResult> results)
    {
        var gateway = GetDefaultGateway();
        if (gateway != null)
        {
            await PerformPingAsync($"内网网关 ({gateway})", gateway.ToString(), results);
        }
        else
        {
            results.Add(new DiagnosticResult { Task = "内网网关", Status = "警告", Message = "未能自动找到内网网关地址。" });
        }

        await PerformPingAsync("外网连接 (baidu.com)", "www.baidu.com", results);
    }

    private IPAddress? GetDefaultGateway()
    {
        return NetworkInterface
            .GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up)
            .SelectMany(n => n.GetIPProperties()?.GatewayAddresses)
            .FirstOrDefault(g => g?.Address != null)?.Address;
    }

    private async Task PerformPingAsync(string taskName, string target, List<DiagnosticResult> results)
    {
        try
        {
            using var pinger = new Ping();
            var reply = await pinger.SendPingAsync(target, 2000);
            if (reply.Status == IPStatus.Success)
            {
                results.Add(new DiagnosticResult { Task = taskName, Status = "正常", Message = $"连接成功，延迟: {reply.RoundtripTime} ms", Value = reply.RoundtripTime });
            }
            else
            {
                results.Add(new DiagnosticResult { Task = taskName, Status = "失败", Message = $"连接失败: {reply.Status}" });
            }
        }
        catch (Exception ex)
        {
            results.Add(new DiagnosticResult { Task = taskName, Status = "错误", Message = ex.Message });
        }
    }
}