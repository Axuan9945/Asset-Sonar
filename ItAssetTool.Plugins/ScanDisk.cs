using ItAssetTool.Core;
using System.Management;
using System.Runtime.Versioning;

namespace ItAssetTool.Plugins;

[SupportedOSPlatform("windows")]
public class ScanDisk : IScanPlugin
{
    public string Name => "硬盘信息";

    public Task<List<HardwareInfo>> ScanAsync()
    {
        return Task.Run(() =>
        {
            var data = new List<HardwareInfo>();
            try
            {
                ManagementObjectSearcher searcher = new("SELECT * FROM Win32_DiskDrive");
                foreach (var obj in searcher.Get())
                {
                    var managementObject = (ManagementObject)obj;
                    data.Add(new HardwareInfo
                    {
                        Category = "硬盘",
                        Brand = managementObject["Model"]?.ToString()?.Split(' ')[0] ?? "N/A",
                        Model = managementObject["Model"]?.ToString() ?? "N/A",
                        // 调用辅助函数来格式化硬盘大小
                        Size = FormatBytes(managementObject["Size"]),
                        SerialNumber = managementObject["SerialNumber"]?.ToString()?.Trim() ?? "无法获取",
                        ManufactureDate = "N/A",
                        WarrantyLink = "N/A"
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"扫描硬盘失败: {ex.Message}");
                data.Add(new HardwareInfo { Category = "硬盘", Model = $"扫描失败: {ex.Message}" });
            }
            return data;
        });
    }

    // 从 Python 的 format_bytes 翻译过来的辅助函数
    private static string FormatBytes(object? sizeObject)
    {
        if (sizeObject == null || !ulong.TryParse(sizeObject.ToString(), out ulong bytes))
        {
            return "N/A";
        }

        if (bytes == 0) return "0 B";

        const int k = 1024;
        string[] sizes = { "B", "KB", "MB", "GB", "TB", "PB" };
        int i = (int)Math.Floor(Math.Log(bytes) / Math.Log(k));
        return $"{bytes / Math.Pow(k, i):F2} {sizes[i]}";
    }
}