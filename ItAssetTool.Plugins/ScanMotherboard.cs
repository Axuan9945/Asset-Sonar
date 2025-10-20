using ItAssetTool.Core;
using Microsoft.Extensions.Logging; // <-- 添加 using
using System;                    // <-- 添加 using for Exception, StringComparison
using System.Collections.Generic; // <-- 添加 using for List
using System.Linq;                // <-- 添加 using for Linq methods
using System.Management;          // <-- 添加 using for Management classes
using System.Runtime.Versioning;  // <-- 添加 using for SupportedOSPlatform
using System.Threading.Tasks;     // <-- 添加 using for Task

namespace ItAssetTool.Plugins;

[SupportedOSPlatform("windows")]
public class ScanMotherboard : IScanPlugin
{
    private readonly ILogger<ScanMotherboard> _logger; // <-- 添加 logger

    public string Name => "主板/整机信息";

    // 构造函数注入
    public ScanMotherboard(ILogger<ScanMotherboard> logger)
    {
        _logger = logger;
    }

    public Task<List<HardwareInfo>> ScanAsync()
    {
        return Task.Run(() =>
        {
            _logger.LogInformation("开始扫描主板/整机信息...");
            var data = new List<HardwareInfo>();
            ManagementObject? computerSystem = null;
            ManagementObject? baseBoard = null;

            try
            {
                // 尝试获取整机信息 (Win32_ComputerSystem)
                try
                {
                    using var computerSystemSearcher = new ManagementObjectSearcher("SELECT Manufacturer, Model FROM Win32_ComputerSystem");
                    computerSystem = computerSystemSearcher.Get().OfType<ManagementObject>().FirstOrDefault();
                    if (computerSystem != null)
                    {
                        _logger.LogDebug("获取到 Win32_ComputerSystem 信息: Manufacturer='{Manufacturer}', Model='{Model}'",
                                         computerSystem["Manufacturer"]?.ToString(), computerSystem["Model"]?.ToString());
                    }
                    else
                    {
                        _logger.LogDebug("未获取到 Win32_ComputerSystem 信息。");
                    }
                }
                catch (ManagementException wmiEx)
                {
                    _logger.LogWarning(wmiEx, "查询 Win32_ComputerSystem 时发生 WMI 错误。");
                }


                // 尝试获取主板信息 (Win32_BaseBoard)
                try
                {
                    using var baseBoardSearcher = new ManagementObjectSearcher("SELECT Manufacturer, Product, SerialNumber FROM Win32_BaseBoard");
                    baseBoard = baseBoardSearcher.Get().OfType<ManagementObject>().FirstOrDefault();
                    if (baseBoard != null)
                    {
                        _logger.LogDebug("获取到 Win32_BaseBoard 信息: Manufacturer='{Manufacturer}', Product='{Product}', SerialNumber='{SerialNumber}'",
                                         baseBoard["Manufacturer"]?.ToString(), baseBoard["Product"]?.ToString(), baseBoard["SerialNumber"]?.ToString());
                    }
                    else
                    {
                        _logger.LogDebug("未获取到 Win32_BaseBoard 信息。");
                    }
                }
                catch (ManagementException wmiEx)
                {
                    _logger.LogWarning(wmiEx, "查询 Win32_BaseBoard 时发生 WMI 错误。");
                }


                if (computerSystem == null && baseBoard == null)
                {
                    _logger.LogError("无法获取任何主板或整机信息。");
                    data.Add(new HardwareInfo { Category = "主板/整机", Model = "扫描失败: 无法获取 WMI 信息" });
                    return data;
                }

                // 优先使用整机信息，如果获取不到再用主板信息
                var manufacturer = computerSystem?["Manufacturer"]?.ToString()?.Trim() ?? baseBoard?["Manufacturer"]?.ToString()?.Trim() ?? "N/A";
                var model = computerSystem?["Model"]?.ToString()?.Trim() ?? baseBoard?["Product"]?.ToString()?.Trim() ?? "N/A";
                // 序列号通常在主板(BaseBoard)信息里更准确
                var serialNumber = baseBoard?["SerialNumber"]?.ToString()?.Trim() ?? "N/A";

                // 清理常见的无效序列号
                if (serialNumber.Equals("N/A", StringComparison.OrdinalIgnoreCase) ||
                    serialNumber.Equals("None", StringComparison.OrdinalIgnoreCase) ||
                    serialNumber.Contains("serial number", StringComparison.OrdinalIgnoreCase) ||
                    serialNumber.Contains("to be filled", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("获取到的主板序列号无效: {SerialNumber}，将设置为 '无法获取'", serialNumber);
                    serialNumber = "无法获取";
                }

                _logger.LogInformation("主板/整机信息: 制造商='{Manufacturer}', 型号='{Model}', 序列号='{SerialNumber}'", manufacturer, model, serialNumber);

                data.Add(new HardwareInfo
                {
                    Category = "主板/整机",
                    Brand = manufacturer,
                    Model = model,
                    SerialNumber = serialNumber,
                    Size = "N/A",
                    ManufactureDate = "N/A",
                    WarrantyLink = GenerateWarrantyLink(manufacturer, serialNumber) // 生成保修链接
                });
                _logger.LogInformation("主板/整机信息扫描完成。");
            }
            catch (Exception ex) // 捕获除 WMI 之外的其他可能错误
            {
                _logger.LogError(ex, "扫描主板/整机时发生意外错误");
                data.Add(new HardwareInfo { Category = "主板/整机", Model = $"扫描失败: {ex.Message}" });
            }
            finally // 确保 WMI 对象被释放
            {
                computerSystem?.Dispose();
                baseBoard?.Dispose();
            }
            return data;
        });
    }

    // 生成保修链接的辅助函数
    private string GenerateWarrantyLink(string manufacturer, string serialNumber)
    {
        if (string.IsNullOrWhiteSpace(serialNumber) || serialNumber.Equals("无法获取", StringComparison.OrdinalIgnoreCase) || serialNumber.Equals("N/A", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("序列号无效或无法获取，不生成保修链接。");
            return "N/A";
        }

        var mfgLower = manufacturer?.ToLower() ?? ""; // 处理 manufacturer 可能为 null 的情况
        string link = "N/A";

        if (mfgLower.Contains("dell"))
        {
            link = $"https://www.dell.com/support/home/en-sg/product-support/servicetag/{serialNumber}/overview";
        }
        else if (mfgLower.Contains("hewlett-packard") || mfgLower.Contains("hp"))
        {
            link = $"https://support.hp.com/sg-en/checkwarranty/search?q={serialNumber}";
        }
        else if (mfgLower.Contains("lenovo"))
        {
            link = $"https://pcsupport.lenovo.com/sg-en/search?query={serialNumber}";
        }
        // 可以添加更多品牌的链接

        if (link != "N/A")
        {
            _logger.LogDebug("为制造商 '{Manufacturer}' 和序列号 '{SerialNumber}' 生成保修链接: {Link}", manufacturer, serialNumber, link);
        }
        else
        {
            _logger.LogDebug("未找到制造商 '{Manufacturer}' 对应的保修链接模板。", manufacturer);
        }

        return link;
    }
}