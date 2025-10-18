using ItAssetTool.Core;
using System.Management;
using System.Runtime.Versioning;
using System.Text;

namespace ItAssetTool.Plugins;

[SupportedOSPlatform("windows")]
public class ScanMonitor : IScanPlugin
{
    public string Name => "显示器信息";

    public Task<List<HardwareInfo>> ScanAsync()
    {
        return Task.Run(() =>
        {
            var data = new List<HardwareInfo>();
            try
            {
                // 显示器信息在特殊的 "wmi" 命名空间下
                var scope = new ManagementScope(@"\\.\root\wmi");
                var query = new ObjectQuery("SELECT * FROM WmiMonitorID");
                using var searcher = new ManagementObjectSearcher(scope, query);

                var monitors = searcher.Get().OfType<ManagementObject>().ToList();

                if (!monitors.Any())
                {
                    data.Add(new HardwareInfo { Category = "显示器", Model = "未检测到外部显示器" });
                    return data;
                }

                foreach (var monitor in monitors)
                {
                    var manufacturer = DecodeMonitorString(monitor["ManufacturerName"] as ushort[]);
                    var model = DecodeMonitorString(monitor["UserFriendlyName"] as ushort[]);
                    var serial = DecodeMonitorString(monitor["SerialNumberID"] as ushort[]);

                    data.Add(new HardwareInfo
                    {
                        Category = "显示器",
                        Brand = manufacturer,
                        Model = model,
                        SerialNumber = serial,
                        Size = "N/A", // WMI不直接提供尺寸
                        ManufactureDate = "N/A",
                        WarrantyLink = "N/A"
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"扫描显示器失败: {ex.Message}");
                data.Add(new HardwareInfo { Category = "显示器", Model = $"扫描失败: {ex.Message}" });
            }
            return data;
        });
    }

    // WMI返回的是ASCII码数组，需要一个辅助函数来解码
    private string DecodeMonitorString(ushort[]? value)
    {
        if (value == null) return "N/A";
        var sb = new StringBuilder();
        foreach (var c in value)
        {
            if (c > 0)
            {
                sb.Append((char)c);
            }
        }
        return sb.ToString().Trim();
    }
}