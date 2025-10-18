using ItAssetTool.Core;
using System.Management;
using System.Runtime.Versioning;

namespace ItAssetTool.Plugins;

[SupportedOSPlatform("windows")]
public class ScanMemory : IScanPlugin
{
    public string Name => "内存信息";

    public Task<List<HardwareInfo>> ScanAsync()
    {
        return Task.Run(() =>
        {
            var data = new List<HardwareInfo>();
            try
            {
                var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemory");
                foreach (var obj in searcher.Get().OfType<ManagementObject>())
                {
                    var managementObject = (ManagementObject)obj;

                    // vvvv 核心修正：使用更安全的 TryGetValue 方法来获取属性 vvvv
                    uint memoryType = 0;
                    if (managementObject["SMBIOSMemoryType"] != null)
                    {
                        uint.TryParse(managementObject["SMBIOSMemoryType"].ToString(), out memoryType);
                    }

                    uint speed = 0;
                    if (managementObject["Speed"] != null)
                    {
                        uint.TryParse(managementObject["Speed"].ToString(), out speed);
                    }
                    // ^^^^ 核心修正结束 ^^^^

                    data.Add(new HardwareInfo
                    {
                        Category = "内存",
                        Brand = managementObject["Manufacturer"]?.ToString()?.Trim() ?? "N/A",
                        Model = managementObject["PartNumber"]?.ToString()?.Trim() ?? "N/A",
                        Size = FormatBytes(managementObject["Capacity"]),
                        SerialNumber = managementObject["SerialNumber"]?.ToString()?.Trim() ?? "N/A",
                        MemoryType = memoryType,
                        Speed = speed,
                        ManufactureDate = "N/A",
                        WarrantyLink = "N/A"
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"扫描内存失败: {ex.Message}");
                data.Add(new HardwareInfo { Category = "内存", Model = $"扫描失败: {ex.Message}" });
            }
            return data;
        });
    }

    private static string FormatBytes(object? sizeObject)
    {
        if (sizeObject == null || !ulong.TryParse(sizeObject.ToString(), out ulong bytes)) return "N/A";
        if (bytes == 0) return "0 B";
        const int k = 1024;
        string[] sizes = { "B", "KB", "MB", "GB", "TB", "PB" };
        int i = (int)Math.Floor(Math.Log(bytes) / Math.Log(k));
        return $"{bytes / Math.Pow(k, i):F2} {sizes[i]}";
    }
}