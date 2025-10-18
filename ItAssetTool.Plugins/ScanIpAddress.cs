using ItAssetTool.Core;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace ItAssetTool.Plugins;

[SupportedOSPlatform("windows")]
public class ScanIpAddress : IScanPlugin
{
    public string Name => "IP 地址信息";

    public Task<List<HardwareInfo>> ScanAsync()
    {
        return Task.Run(() =>
        {
            var data = new List<HardwareInfo>();
            var virtualAdapterKeywords = new[] { "loopback", "vmware", "virtual", "tap-windows", "hyper-v" };

            try
            {
                var query = new SelectQuery("Win32_NetworkAdapterConfiguration");
                using var searcher = new ManagementObjectSearcher(query);
                foreach (var obj in searcher.Get().OfType<ManagementObject>())
                {
                    // 过滤掉未启用IP的或虚拟网卡
                    if (!(bool)obj["IPEnabled"]) continue;
                    var description = obj["Description"]?.ToString()?.ToLower() ?? "";
                    if (virtualAdapterKeywords.Any(keyword => description.Contains(keyword))) continue;

                    var ipAddresses = obj["IPAddress"] as string[];
                    var subnets = obj["IPSubnet"] as string[];
                    var gateways = obj["DefaultIPGateway"] as string[];

                    // 只选择 IPv4 地址
                    var ip = ipAddresses?.FirstOrDefault(i => i.Contains('.'));
                    if (string.IsNullOrEmpty(ip)) continue;

                    data.Add(new HardwareInfo
                    {
                        Category = "IP 地址",
                        Brand = obj["Description"]?.ToString() ?? "N/A",
                        Model = $"IP: {ip}",
                        SerialNumber = $"网关: {gateways?.FirstOrDefault() ?? "N/A"}",
                        ManufactureDate = $"子网掩码: {subnets?.FirstOrDefault() ?? "N/A"}",
                        Size = "N/A",
                        WarrantyLink = "N/A"
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"扫描IP地址失败: {ex.Message}");
                data.Add(new HardwareInfo { Category = "IP 地址", Model = $"扫描失败: {ex.Message}" });
            }
            return data;
        });
    }
}