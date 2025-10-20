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
public class ScanGpu : IScanPlugin
{
    private readonly ILogger<ScanGpu> _logger; // <-- 添加 logger

    public string Name => "显卡信息";

    // 构造函数注入
    public ScanGpu(ILogger<ScanGpu> logger)
    {
        _logger = logger;
    }

    public Task<List<HardwareInfo>> ScanAsync()
    {
        return Task.Run(() =>
        {
            _logger.LogInformation("开始扫描显卡信息...");
            var data = new List<HardwareInfo>();
            try
            {
                // 查询 Win32_VideoController 获取显卡信息
                ManagementObjectSearcher searcher = new("SELECT Name, AdapterRAM FROM Win32_VideoController");
                var gpus = searcher.Get().OfType<ManagementObject>().ToList();

                if (!gpus.Any())
                {
                    _logger.LogWarning("WMI 查询未返回任何显卡信息 (Win32_VideoController)。");
                    data.Add(new HardwareInfo { Category = "显卡", Model = "未检测到显卡" });
                    return data;
                }
                _logger.LogDebug("查询到 {GpuCount} 个显卡控制器。", gpus.Count);


                foreach (var obj in gpus)
                {
                    using (obj)
                    {
                        var modelName = obj["Name"]?.ToString()?.Trim() ?? "N/A";
                        var adapterRam = FormatBytes(obj["AdapterRAM"]); // 格式化显存大小

                        // 尝试从型号名称中提取品牌 (简单方式)
                        var brand = modelName.Split(' ')[0];
                        if (brand.Equals("N/A", StringComparison.OrdinalIgnoreCase) || brand.Length < 2 || brand.Contains("Microsoft")) // 排除 Microsoft Basic Display Adapter 等
                        {
                            // 可以尝试更复杂的品牌识别逻辑，或默认为未知
                            brand = InferBrandFromName(modelName);
                        }

                        _logger.LogDebug("  发现显卡: {Brand} {Model}, 显存: {AdapterRam}", brand, modelName, adapterRam);

                        data.Add(new HardwareInfo
                        {
                            Category = "显卡",
                            Brand = brand,
                            Model = modelName,
                            Size = adapterRam, // 使用 Size 字段存储显存大小
                            SerialNumber = "N/A", // WMI 通常不提供显卡序列号
                            ManufactureDate = "N/A",
                            WarrantyLink = "N/A"
                        });
                    }
                }
                _logger.LogInformation("显卡信息扫描完成，共找到 {Count} 个显卡控制器。", data.Count);
            }
            catch (ManagementException wmiEx)
            {
                _logger.LogError(wmiEx, "扫描显卡失败 (WMI 查询出错)");
                data.Add(new HardwareInfo { Category = "显卡", Model = $"扫描失败: WMI 错误 ({wmiEx.Message})" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "扫描显卡时发生意外错误");
                data.Add(new HardwareInfo { Category = "显卡", Model = $"扫描失败: {ex.Message}" });
            }
            return data;
        });
    }

    // 辅助函数：格式化字节大小 (与 ScanDisk 相同)
    private static string FormatBytes(object? sizeObject)
    {
        if (sizeObject == null || !ulong.TryParse(sizeObject.ToString(), out ulong bytes))
        {
            // AdapterRAM 可能为 null 或 0，返回 N/A
            return "N/A";
        }
        if (bytes == 0) return "N/A"; // 显存为0通常无意义

        const int k = 1024;
        string[] sizes = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
        int i = Convert.ToInt32(Math.Floor(Math.Log(bytes, k)));
        if (i < 0 || i >= sizes.Length) i = 0;
        double value = bytes / Math.Pow(k, i);
        return $"{value:F1} {sizes[i]}"; // 显存通常用 F1 即可
    }

    // 辅助函数：从名称推断品牌
    private string InferBrandFromName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "未知品牌";
        var lowerName = name.ToLower();
        if (lowerName.Contains("nvidia") || lowerName.Contains("geforce") || lowerName.Contains("quadro") || lowerName.Contains("rtx")) return "NVIDIA";
        if (lowerName.Contains("amd") || lowerName.Contains("radeon") || lowerName.Contains("firepro")) return "AMD";
        if (lowerName.Contains("intel")) return "Intel";
        // 可以添加更多品牌判断
        return "未知品牌"; // 默认返回未知
    }
}