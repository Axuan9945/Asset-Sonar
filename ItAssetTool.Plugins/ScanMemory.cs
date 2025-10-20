using ItAssetTool.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace ItAssetTool.Plugins;

[SupportedOSPlatform("windows")]
public class ScanMemory : IScanPlugin
{
    private readonly ILogger<ScanMemory> _logger;

    public string Name => "内存信息";

    public ScanMemory(ILogger<ScanMemory> logger)
    {
        _logger = logger;
    }

    public Task<List<HardwareInfo>> ScanAsync()
    {
        return Task.Run(() =>
        {
            _logger.LogInformation("开始扫描内存信息...");
            var data = new List<HardwareInfo>();
            try
            {
                var searcher = new ManagementObjectSearcher("SELECT Manufacturer, PartNumber, Capacity, SerialNumber, SMBIOSMemoryType, Speed, DeviceLocator FROM Win32_PhysicalMemory");
                using var memoryModulesCollection = searcher.Get();
                var memoryModules = memoryModulesCollection.OfType<ManagementObject>().ToList();

                if (!memoryModules.Any()) { _logger.LogWarning("WMI 查询未返回任何物理内存信息 (Win32_PhysicalMemory)。"); data.Add(new HardwareInfo { Category = "内存", Model = "未检测到内存条" }); return data; }
                _logger.LogDebug("查询到 {ModuleCount} 个物理内存条。", memoryModules.Count);

                foreach (var obj in memoryModules)
                {
                    using (obj)
                    {
                        var brand = GetWmiProperty(obj, "Manufacturer")?.Trim() ?? "N/A";
                        var model = GetWmiProperty(obj, "PartNumber")?.Trim() ?? "N/A";
                        var size = FormatBytes(obj["Capacity"]);
                        var serialNumber = GetWmiProperty(obj, "SerialNumber")?.Trim() ?? "N/A";
                        var deviceLocator = GetWmiProperty(obj, "DeviceLocator")?.Trim() ?? "未知插槽";
                        uint memoryType = 0;
                        if (obj["SMBIOSMemoryType"] != null && uint.TryParse(obj["SMBIOSMemoryType"].ToString(), out uint parsedType)) memoryType = parsedType;
                        else _logger.LogDebug("  无法解析内存类型 (SMBIOSMemoryType) for {DeviceLocator}", deviceLocator);
                        uint speed = 0;
                        if (obj["Speed"] != null && uint.TryParse(obj["Speed"].ToString(), out uint parsedSpeed)) speed = parsedSpeed;
                        else _logger.LogDebug("  无法解析内存速度 (Speed) for {DeviceLocator}", deviceLocator);

                        _logger.LogDebug("  发现内存: {Brand} {Model} ({Size}), 速度: {Speed}MHz, 类型代码: {MemoryType}, 插槽: {DeviceLocator}, SN: {SerialNumber}", brand, model, size, speed, memoryType, deviceLocator, serialNumber);

                        data.Add(new HardwareInfo
                        {
                            Category = "内存",
                            Brand = brand,
                            Model = model,
                            Size = size,
                            SerialNumber = serialNumber,
                            MemoryType = memoryType,
                            Speed = speed,
                            ManufactureDate = "N/A",
                            WarrantyLink = "N/A"
                        });
                    }
                }
                memoryModules.Clear();
                _logger.LogInformation("内存信息扫描完成，共找到 {Count} 个内存条。", data.Count);
            }
            catch (ManagementException wmiEx) { _logger.LogError(wmiEx, "扫描内存失败 (WMI 查询出错)"); data.Add(new HardwareInfo { Category = "内存", Model = $"扫描失败: WMI 错误 ({wmiEx.Message})" }); }
            // --- vvvv CS0168 修正：将 ex 传递给 LogError vvvv ---
            catch (Exception ex)
            {
                _logger.LogError(ex, "扫描内存时发生意外错误"); // 修正：传递 ex
                data.Add(new HardwareInfo { Category = "内存", Model = $"扫描失败: {ex.Message}" });
            }
            // --- ^^^^ 修正结束 ^^^^
            return data;
        });
    }

    private static string? GetWmiProperty(ManagementObject obj, string propertyName) { try { return obj[propertyName]?.ToString(); } catch { return null; } }
    private static string FormatBytes(object? sizeObject)
    {
        if (sizeObject == null || !ulong.TryParse(sizeObject.ToString(), out ulong bytes)) { return "N/A"; }
        if (bytes == 0) return "0 B";
        const int k = 1024;
        string[] sizes = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
        int i = Convert.ToInt32(Math.Floor(Math.Log(bytes, k)));
        if (i < 0 || i >= sizes.Length) i = 0;
        double value = bytes / Math.Pow(k, i);
        return $"{value:F2} {sizes[i]}";
    }
}