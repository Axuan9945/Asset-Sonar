using ItAssetTool.Core;
using Microsoft.Extensions.Logging; // <-- 添加 using
using System;                    // <-- 添加 using for Exception
using System.Collections.Generic; // <-- 添加 using for List
using System.Linq;                // <-- 添加 using for Linq methods
using System.Management;          // <-- 添加 using for Management classes
using System.Runtime.Versioning;  // <-- 添加 using for SupportedOSPlatform
using System.Threading.Tasks;     // <-- 添加 using for Task


namespace ItAssetTool.Plugins;

[SupportedOSPlatform("windows")]
public class ScanOs : IScanPlugin
{
    private readonly ILogger<ScanOs> _logger; // <-- 添加 logger

    public string Name => "操作系统信息";

    // 构造函数注入
    public ScanOs(ILogger<ScanOs> logger)
    {
        _logger = logger;
    }


    public Task<List<HardwareInfo>> ScanAsync()
    {
        return Task.Run(() =>
        {
            _logger.LogInformation("开始扫描操作系统信息...");
            var data = new List<HardwareInfo>();
            try
            {
                // 查询 Win32_OperatingSystem 获取 OS 信息
                using var searcher = new ManagementObjectSearcher("SELECT Caption, SerialNumber FROM Win32_OperatingSystem");
                var os = searcher.Get().OfType<ManagementObject>().FirstOrDefault();

                if (os == null)
                {
                    _logger.LogError("无法获取操作系统信息 (Win32_OperatingSystem 查询返回 null)。");
                    data.Add(new HardwareInfo { Category = "操作系统", Model = "扫描失败: 无法获取 WMI 信息" });
                    return data;
                    // 或者抛出异常: throw new Exception("无法获取操作系统信息。");
                }

                using (os)
                {
                    var caption = os["Caption"]?.ToString()?.Trim() ?? "N/A";
                    var serialNumber = os["SerialNumber"]?.ToString()?.Trim() ?? "N/A";

                    // 清理无效序列号
                    if (serialNumber.Equals("N/A", StringComparison.OrdinalIgnoreCase) ||
                        serialNumber.Contains("o.e.m.", StringComparison.OrdinalIgnoreCase)) // 有些 OEM 版本可能显示奇怪的序列号
                    {
                        _logger.LogWarning("获取到的操作系统序列号无效或非标准: {SerialNumber}", serialNumber);
                        // 可以保留原样，或设置为 "无法获取"
                        // serialNumber = "无法获取";
                    }


                    _logger.LogInformation("操作系统信息: 名称='{Caption}', 序列号='{SerialNumber}'", caption, serialNumber);

                    data.Add(new HardwareInfo
                    {
                        Category = "操作系统",
                        Brand = "Microsoft", // 通常是 Microsoft
                        Model = caption,     // OS 版本名称作为 Model
                        SerialNumber = serialNumber,
                        Size = "N/A",
                        ManufactureDate = "N/A",
                        WarrantyLink = "N/A"
                    });
                }
                _logger.LogInformation("操作系统信息扫描完成。");
            }
            catch (ManagementException wmiEx)
            {
                _logger.LogError(wmiEx, "扫描操作系统失败 (WMI 查询出错)");
                data.Add(new HardwareInfo { Category = "操作系统", Model = $"扫描失败: WMI 错误 ({wmiEx.Message})" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "扫描操作系统时发生意外错误");
                data.Add(new HardwareInfo { Category = "操作系统", Model = $"扫描失败: {ex.Message}" });
            }
            return data;
        });
    }
}