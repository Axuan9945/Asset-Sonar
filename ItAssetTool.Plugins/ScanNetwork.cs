using ItAssetTool.Core;
using System.Management;
using System.Runtime.Versioning;
using System.Linq;

namespace ItAssetTool.Plugins;

[SupportedOSPlatform("windows")]
public class ScanNetwork : IScanPlugin
{
    public string Name => "网卡信息";

    public Task<List<HardwareInfo>> ScanAsync()
    {
        return Task.Run(() =>
        {
            var data = new List<HardwareInfo>();
            try
            {
                // WMI 查询，只选择启用了 IP 的网络适配器配置
                using var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = 'TRUE'");

                foreach (var obj in searcher.Get().OfType<ManagementObject>())
                {
                    // 通过配置找到对应的物理适配器
                    using var adapterSearcher = new ManagementObjectSearcher(
                        $"SELECT * FROM Win32_NetworkAdapter WHERE Index = {obj["Index"]}");
                    var adapter = adapterSearcher.Get().OfType<ManagementObject>().FirstOrDefault();

                    if (adapter == null) continue;

                    data.Add(new HardwareInfo
                    {
                        Category = "网卡",
                        Brand = adapter["Manufacturer"]?.ToString() ?? "N/A",
                        Model = adapter["Description"]?.ToString() ?? "N/A",
                        // MAC地址作为唯一的序列号
                        SerialNumber = obj["MACAddress"]?.ToString() ?? "N/A",
                        Size = "N/A",
                        ManufactureDate = "N/A",
                        WarrantyLink = "N/A"
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"扫描网卡失败: {ex.Message}");
                data.Add(new HardwareInfo { Category = "网卡", Model = $"扫描失败: {ex.Message}" });
            }
            return data;
        });
    }
}