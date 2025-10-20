using ItAssetTool.Core;
using Microsoft.Extensions.Logging; // <-- 添加 using
using System;                    // <-- 添加 using for Exception
using System.Collections.Generic; // <-- 添加 using for List
using System.Linq;                // <-- 添加 using for Linq methods
using System.Management;          // <-- 添加 using for Management classes
using System.Runtime.Versioning;  // <-- 添加 using for SupportedOSPlatform
using System.Text;                // <-- 添加 using for StringBuilder
using System.Threading.Tasks;     // <-- 添加 using for Task


namespace ItAssetTool.Plugins;

[SupportedOSPlatform("windows")]
public class ScanMonitor : IScanPlugin
{
    private readonly ILogger<ScanMonitor> _logger; // <-- 添加 logger

    public string Name => "显示器信息";

    // 构造函数注入
    public ScanMonitor(ILogger<ScanMonitor> logger)
    {
        _logger = logger;
    }


    public Task<List<HardwareInfo>> ScanAsync()
    {
        return Task.Run(() =>
        {
            _logger.LogInformation("开始扫描显示器信息 (root\\wmi)...");
            var data = new List<HardwareInfo>();
            try
            {
                // 连接到 WMI 的 root\wmi 命名空间
                var scope = new ManagementScope(@"\\.\root\wmi");
                scope.Connect(); // 尝试连接

                if (!scope.IsConnected)
                {
                    _logger.LogError("无法连接到 WMI 的 root\\wmi 命名空间。");
                    data.Add(new HardwareInfo { Category = "显示器", Model = "扫描失败: 无法连接 WMI (root\\wmi)" });
                    return data;
                }

                _logger.LogDebug("已连接到 root\\wmi 命名空间。");

                // 查询 WmiMonitorID 类获取 EDID 信息
                var query = new ObjectQuery("SELECT ManufacturerName, UserFriendlyName, SerialNumberID FROM WmiMonitorID");
                using var searcher = new ManagementObjectSearcher(scope, query);
                var monitors = searcher.Get().OfType<ManagementObject>().ToList();

                if (!monitors.Any())
                {
                    _logger.LogWarning("WMI 查询未返回任何显示器信息 (WmiMonitorID)。可能是没有连接外部显示器。");
                    data.Add(new HardwareInfo { Category = "显示器", Model = "未检测到外部显示器" });
                    return data;
                }
                _logger.LogDebug("查询到 {MonitorCount} 个显示器信息记录。", monitors.Count);

                foreach (var monitor in monitors)
                {
                    using (monitor)
                    {
                        // WMI 返回的是 ASCII 码数组 (ushort[])，需要解码
                        var manufacturer = DecodeMonitorString(monitor["ManufacturerName"] as ushort[]);
                        var model = DecodeMonitorString(monitor["UserFriendlyName"] as ushort[]);
                        var serial = DecodeMonitorString(monitor["SerialNumberID"] as ushort[]);

                        _logger.LogDebug("  发现显示器: {Manufacturer} {Model}, SN: {SerialNumber}", manufacturer, model, serial);

                        data.Add(new HardwareInfo
                        {
                            Category = "显示器",
                            Brand = manufacturer,
                            Model = model,
                            SerialNumber = serial,
                            Size = "N/A", // WMI 不直接提供尺寸
                            ManufactureDate = "N/A", // WMI 不直接提供生产日期
                            WarrantyLink = "N/A"
                        });
                    }
                }
                _logger.LogInformation("显示器信息扫描完成，共找到 {Count} 个显示器。", data.Count);
            }
            catch (ManagementException wmiEx)
            {
                // 特别处理权限不足的错误
                if (wmiEx.ErrorCode == ManagementStatus.AccessDenied)
                {
                    _logger.LogError(wmiEx, "扫描显示器失败 - 权限不足 (需要管理员权限访问 root\\wmi)。");
                    data.Add(new HardwareInfo { Category = "显示器", Model = "扫描失败: 权限不足" });
                }
                else
                {
                    _logger.LogError(wmiEx, "扫描显示器失败 (WMI 查询出错)");
                    data.Add(new HardwareInfo { Category = "显示器", Model = $"扫描失败: WMI 错误 ({wmiEx.Message})" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "扫描显示器时发生意外错误");
                data.Add(new HardwareInfo { Category = "显示器", Model = $"扫描失败: {ex.Message}" });
            }
            return data;
        });
    }

    // 辅助函数：解码 WMI 返回的 ushort[]
    private string DecodeMonitorString(ushort[]? value)
    {
        if (value == null || value.Length == 0) return "N/A";

        try
        {
            // 将 ushort 数组转换为 byte 数组，然后使用 ASCII 解码
            // 跳过可能存在的 null 终止符
            byte[] bytes = value.TakeWhile(c => c != 0).Select(c => (byte)c).ToArray();
            // 使用 Default encoding (通常是系统 ANSI codepage) 或 ASCII
            // ASCII 更安全，因为它只包含 0-127
            string decoded = Encoding.ASCII.GetString(bytes).Trim();
            return string.IsNullOrWhiteSpace(decoded) ? "N/A" : decoded;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "解码显示器字符串时出错。原始值: {RawValue}", string.Join(",", value));
            return "解码错误";
        }
    }
}