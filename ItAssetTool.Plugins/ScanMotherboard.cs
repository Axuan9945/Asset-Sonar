using ItAssetTool.Core;
using System.Management;
using System.Runtime.Versioning;
using System.Linq;

namespace ItAssetTool.Plugins;

[SupportedOSPlatform("windows")]
public class ScanMotherboard : IScanPlugin
{
    public string Name => "主板/整机信息";

    public Task<List<HardwareInfo>> ScanAsync()
    {
        return Task.Run(() =>
        {
            var data = new List<HardwareInfo>();
            try
            {
                // 首先尝试获取整机信息，这对于品牌机更准确
                using var computerSystemSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem");
                var computerSystem = computerSystemSearcher.Get().OfType<ManagementObject>().FirstOrDefault();

                // 然后获取主板信息
                using var baseBoardSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_BaseBoard");
                var baseBoard = baseBoardSearcher.Get().OfType<ManagementObject>().FirstOrDefault();

                if (computerSystem == null && baseBoard == null)
                {
                    throw new Exception("无法获取任何主板或整机信息。");
                }

                // 优先使用整机信息，如果获取不到再用主板信息
                var manufacturer = computerSystem?["Manufacturer"]?.ToString()?.Trim() ?? baseBoard?["Manufacturer"]?.ToString()?.Trim() ?? "N/A";
                var model = computerSystem?["Model"]?.ToString()?.Trim() ?? baseBoard?["Product"]?.ToString()?.Trim() ?? "N/A";

                // 序列号通常在主板(BaseBoard)信息里更准确
                var serialNumber = baseBoard?["SerialNumber"]?.ToString()?.Trim() ?? "N/A";

                data.Add(new HardwareInfo
                {
                    Category = "主板/整机",
                    Brand = manufacturer,
                    Model = model,
                    SerialNumber = serialNumber,
                    Size = "N/A",
                    ManufactureDate = "N/A",
                    WarrantyLink = GenerateWarrantyLink(manufacturer, serialNumber)
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"扫描主板/整机失败: {ex.Message}");
                data.Add(new HardwareInfo { Category = "主板/整机", Model = $"扫描失败: {ex.Message}" });
            }
            return data;
        });
    }

    // 从 Python 版本翻译过来的保修链接生成函数
    private static string GenerateWarrantyLink(string manufacturer, string serialNumber)
    {
        if (string.IsNullOrWhiteSpace(serialNumber) || serialNumber.Contains("serial", StringComparison.OrdinalIgnoreCase))
        {
            return "N/A";
        }

        var mfgLower = manufacturer.ToLower();
        if (mfgLower.Contains("dell"))
        {
            return $"https://www.dell.com/support/home/en-sg/product-support/servicetag/{serialNumber}/overview";
        }
        if (mfgLower.Contains("hewlett-packard") || mfgLower.Contains("hp"))
        {
            return $"https://support.hp.com/sg-en/checkwarranty/search?q={serialNumber}";
        }
        if (mfgLower.Contains("lenovo"))
        {
            return $"https://pcsupport.lenovo.com/sg-en/search?query={serialNumber}";
        }

        return "N/A";
    }
}