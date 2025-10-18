using ItAssetTool.Core;
using System.Management;
using System.Runtime.Versioning;

namespace ItAssetTool.Plugins;

[SupportedOSPlatform("windows")]
public class ScanGpu : IScanPlugin
{
    public string Name => "显卡信息";

    public Task<List<HardwareInfo>> ScanAsync()
    {
        return Task.Run(() =>
        {
            var data = new List<HardwareInfo>();
            try
            {
                ManagementObjectSearcher searcher = new("SELECT * FROM Win32_VideoController");
                foreach (var obj in searcher.Get())
                {
                    var managementObject = (ManagementObject)obj;
                    var modelName = managementObject["Name"]?.ToString() ?? "N/A";

                    data.Add(new HardwareInfo
                    {
                        Category = "显卡",
                        // 尝试从型号名称中提取品牌
                        Brand = modelName.Split(' ')[0],
                        Model = modelName,
                        Size = "N/A",
                        SerialNumber = "N/A", // WMI通常不提供显卡序列号
                        ManufactureDate = "N/A",
                        WarrantyLink = "N/A"
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"扫描显卡失败: {ex.Message}");
                data.Add(new HardwareInfo { Category = "显卡", Model = $"扫描失败: {ex.Message}" });
            }
            return data;
        });
    }
}