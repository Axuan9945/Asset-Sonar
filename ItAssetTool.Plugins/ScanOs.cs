using ItAssetTool.Core;
using System.Management;
using System.Runtime.Versioning;
using System.Linq;

namespace ItAssetTool.Plugins;

[SupportedOSPlatform("windows")]
public class ScanOs : IScanPlugin
{
    public string Name => "操作系统信息";

    public Task<List<HardwareInfo>> ScanAsync()
    {
        return Task.Run(() =>
        {
            var data = new List<HardwareInfo>();
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem");
                var os = searcher.Get().OfType<ManagementObject>().FirstOrDefault();

                if (os == null)
                {
                    throw new Exception("无法获取操作系统信息。");
                }

                data.Add(new HardwareInfo
                {
                    Category = "操作系统",
                    Brand = "Microsoft",
                    Model = os["Caption"]?.ToString() ?? "N/A",
                    SerialNumber = os["SerialNumber"]?.ToString() ?? "N/A",
                    Size = "N/A",
                    ManufactureDate = "N/A",
                    WarrantyLink = "N/A"
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"扫描操作系统失败: {ex.Message}");
                data.Add(new HardwareInfo { Category = "操作系统", Model = $"扫描失败: {ex.Message}" });
            }
            return data;
        });
    }
}