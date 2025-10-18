using ItAssetTool.Core;
using System.Management; // <--- 添加这一行
using System.Runtime.Versioning; // <--- 添加这一行

namespace ItAssetTool.Plugins;

[SupportedOSPlatform("windows")] // <--- 添加这一行特性来解决 CA1416 警告
public class ScanCpu : IScanPlugin
{
    public string Name => "处理器信息";

    public Task<List<HardwareInfo>> ScanAsync()
    {
        return Task.Run(() =>
        {
            var data = new List<HardwareInfo>();
            try
            {
                ManagementObjectSearcher searcher = new("SELECT * FROM Win32_Processor");
                foreach (var obj in searcher.Get())
                {
                    var managementObject = (ManagementObject)obj;
                    data.Add(new HardwareInfo
                    {
                        Category = "处理器",
                        Brand = managementObject["Manufacturer"]?.ToString() ?? "N/A",
                        Model = managementObject["Name"]?.ToString()?.Trim() ?? "N/A",
                        SerialNumber = managementObject["ProcessorId"]?.ToString() ?? "无法获取",
                        Size = "N/A",
                        ManufactureDate = "N/A",
                        WarrantyLink = "N/A"
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"扫描CPU失败: {ex.Message}");
                data.Add(new HardwareInfo { Category = "处理器", Model = $"扫描失败: {ex.Message}" });
            }
            return data;
        });
    }
}