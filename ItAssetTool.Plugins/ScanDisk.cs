using ItAssetTool.Core;
using Microsoft.Extensions.Logging; // <-- 添加 using
using System;                    // <-- 添加 using for Exception, Math
using System.Collections.Generic; // <-- 添加 using for List
using System.Linq;                // <-- 添加 using for Linq methods
using System.Management;          // <-- 添加 using for Management classes
using System.Runtime.Versioning;  // <-- 添加 using for SupportedOSPlatform
using System.Threading.Tasks;     // <-- 添加 using for Task


namespace ItAssetTool.Plugins;

[SupportedOSPlatform("windows")]
public class ScanDisk : IScanPlugin
{
    private readonly ILogger<ScanDisk> _logger; // <-- 添加 logger

    public string Name => "硬盘信息";

    // 构造函数注入
    public ScanDisk(ILogger<ScanDisk> logger)
    {
        _logger = logger;
    }

    public Task<List<HardwareInfo>> ScanAsync()
    {
        return Task.Run(() =>
        {
            _logger.LogInformation("开始扫描硬盘信息...");
            var data = new List<HardwareInfo>();
            try
            {
                // 查询 Win32_DiskDrive 获取物理磁盘信息
                ManagementObjectSearcher searcher = new("SELECT Model, Size, SerialNumber FROM Win32_DiskDrive");
                var disks = searcher.Get().OfType<ManagementObject>().ToList();

                if (!disks.Any())
                {
                    _logger.LogWarning("WMI 查询未返回任何硬盘信息 (Win32_DiskDrive)。");
                    data.Add(new HardwareInfo { Category = "硬盘", Model = "未检测到硬盘" });
                    return data;
                }

                _logger.LogDebug("查询到 {DiskCount} 个物理硬盘。", disks.Count);

                foreach (var obj in disks)
                {
                    using (obj)
                    {
                        var model = obj["Model"]?.ToString()?.Trim() ?? "N/A";
                        var size = FormatBytes(obj["Size"]); // 调用辅助函数格式化大小
                        // SerialNumber 可能需要特殊处理或权限，有时返回不准确或为空
                        var serialNumber = obj["SerialNumber"]?.ToString()?.Trim();
                        if (string.IsNullOrEmpty(serialNumber) || serialNumber.Equals("N/A", StringComparison.OrdinalIgnoreCase))
                        {
                            serialNumber = "无法获取"; // 统一设置为无法获取
                            _logger.LogWarning("无法获取硬盘 '{Model}' 的序列号。", model);
                        }
                        // 尝试从型号中提取品牌 (简单方式)
                        var brand = model.Split(' ')[0];
                        if (brand.Equals("N/A", StringComparison.OrdinalIgnoreCase) || brand.Length < 2) // 避免无意义的品牌
                        {
                            brand = "未知品牌";
                        }

                        _logger.LogDebug("  发现硬盘: {Brand} {Model}, 容量: {Size}, 序列号: {SerialNumber}", brand, model, size, serialNumber);

                        data.Add(new HardwareInfo
                        {
                            Category = "硬盘",
                            Brand = brand,
                            Model = model,
                            Size = size,
                            SerialNumber = serialNumber,
                            ManufactureDate = "N/A", // WMI 通常不提供生产日期
                            WarrantyLink = "N/A"
                        });
                    }
                }
                _logger.LogInformation("硬盘信息扫描完成，共找到 {Count} 个硬盘。", data.Count);
            }
            catch (ManagementException wmiEx)
            {
                _logger.LogError(wmiEx, "扫描硬盘失败 (WMI 查询出错)");
                data.Add(new HardwareInfo { Category = "硬盘", Model = $"扫描失败: WMI 错误 ({wmiEx.Message})" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "扫描硬盘时发生意外错误");
                data.Add(new HardwareInfo { Category = "硬盘", Model = $"扫描失败: {ex.Message}" });
            }
            return data;
        });
    }

    // 辅助函数：格式化字节大小
    private static string FormatBytes(object? sizeObject)
    {
        if (sizeObject == null || !ulong.TryParse(sizeObject.ToString(), out ulong bytes))
        {
            return "N/A";
        }

        if (bytes == 0) return "0 B";

        const int k = 1024;
        string[] sizes = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; // 添加更多单位
        int i = Convert.ToInt32(Math.Floor(Math.Log(bytes, k))); // 使用 Math.Log(bytes, k)

        // 确保索引在有效范围内
        if (i < 0 || i >= sizes.Length) i = 0; // 如果计算出错，默认为 B

        // 使用 F1 或 F2 控制小数位数，对于 GB/TB 通常 F2 较好
        double value = bytes / Math.Pow(k, i);
        return $"{value:F2} {sizes[i]}";
    }
}