using CommunityToolkit.Mvvm.ComponentModel;
using ItAssetTool.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ItAssetTool.ViewModels;

public partial class HardwareSummaryViewModel : ObservableObject
{
    [ObservableProperty] private string? cpuModel;
    [ObservableProperty] private string? cpuDetails;
    [ObservableProperty] private string? ramCapacity;
    [ObservableProperty] private string? ramDetails;
    [ObservableProperty] private string? gpuInfo;
    [ObservableProperty] private string? gpuDetails;
    [ObservableProperty] private string? storageCapacity;
    [ObservableProperty] private string? storageDetails;

    public void Update(IEnumerable<HardwareInfo> scannedData)
    {
        var cpu = scannedData.FirstOrDefault(d => d.Category == "处理器");
        CpuModel = cpu?.Model ?? "N/A";
        CpuDetails = "主处理器";

        var ramModules = scannedData.Where(d => d.Category == "内存").ToList();
        if (ramModules.Any())
        {
            var totalRamBytes = ramModules.Select(m => ParseSizeToBytes(m.Size)).Sum();
            RamCapacity = $"{Math.Round(totalRamBytes / (1024.0 * 1024 * 1024), 1)} GB";

            var firstModule = ramModules.First();
            var ramType = GetMemoryTypeString(firstModule.MemoryType);
            var speed = firstModule.Speed > 0 ? $" @ {firstModule.Speed} MHz" : "";
            RamDetails = $"{ramModules.Count} 个插槽已使用, {ramType}{speed}";
        }

        var gpus = scannedData.Where(d => d.Category == "显卡").ToList();
        GpuInfo = gpus.FirstOrDefault()?.Model ?? "N/A";
        GpuDetails = gpus.Count > 1 ? $"已安装 {gpus.Count} 个 GPU" : "独立或集成显卡";

        var disks = scannedData.Where(d => d.Category == "硬盘").ToList();
        var totalDiskBytes = disks.Select(d => ParseSizeToBytes(d.Size)).Sum();
        StorageCapacity = totalDiskBytes > 0 ? $"{totalDiskBytes / Math.Pow(1024, 4):F2} TB" : "N/A";
        StorageDetails = disks.Count > 1 ? $"{disks.Count} 个硬盘驱动器" : "主存储设备";
    }

    // vvvv 核心修正：使用正确的 SMBIOS 标准值 vvvv
    private string GetMemoryTypeString(uint memoryType)
    {
        return memoryType switch
        {
            0x14 => "DDR",
            0x15 => "DDR2",
            0x18 => "DDR3", // 24
            0x1A => "DDR4", // 26
            0x22 => "DDR5", // 34
            _ => "未知类型"
        };
    }
    // ^^^^ 核心修正结束 ^^^^

    private static long ParseSizeToBytes(string? sizeString)
    {
        if (string.IsNullOrEmpty(sizeString)) return 0;
        var parts = sizeString.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !double.TryParse(parts[0], out double value)) return 0;
        return parts[1].ToUpper() switch
        {
            "TB" => (long)(value * Math.Pow(1024, 4)),
            "GB" => (long)(value * Math.Pow(1024, 3)),
            "MB" => (long)(value * Math.Pow(1024, 2)),
            _ => (long)value
        };
    }
}