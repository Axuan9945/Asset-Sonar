using ItAssetTool.Core;
using Microsoft.Extensions.Logging; // <-- 添加 using
using System;                    // <-- 添加 using for Exception
using System.Collections.Generic; // <-- 添加 using for List
using System.Linq;                // <-- 添加 using for Linq methods
using System.Management;          // <-- 添加 using for Management classes
using System.Runtime.Versioning;  // <-- 添加 using for SupportedOSPlatform
using System.Threading.Tasks;     // <-- 添加 using for Task


namespace ItAssetTool.Plugins;

[SupportedOSPlatform("windows")]
public class ScanNetwork : IScanPlugin
{
    private readonly ILogger<ScanNetwork> _logger; // <-- 添加 logger

    public string Name => "网卡信息";

    // 构造函数注入
    public ScanNetwork(ILogger<ScanNetwork> logger)
    {
        _logger = logger;
    }


    public Task<List<HardwareInfo>> ScanAsync()
    {
        return Task.Run(() =>
        {
            _logger.LogInformation("开始扫描物理网卡信息...");
            var data = new List<HardwareInfo>();
            // 扩展虚拟/不需要的适配器关键字列表 (与 ScanIpAddress 保持一致或更严格)
            var virtualAdapterKeywords = new[] {
                "loopback", "vmware", "virtual", "tap-", "hyper-v", // 注意 "tap-" 匹配 tap-windows 等
                "pnp generic", "bluetooth", "wan miniport", "isatap", "teredo",
                "microsoft wi-fi direct virtual adapter", "vpn", "usb debug", "remote ndis"
            };


            try
            {
                // 1. 查询 Win32_NetworkAdapter 获取所有网络适配器信息
                //    选择必要的字段，并过滤掉非物理适配器（PNPDeviceID 不为 NULL 通常表示物理设备）
                var query = new SelectQuery("Win32_NetworkAdapter", "PNPDeviceID IS NOT NULL", new[] { "Index", "Manufacturer", "Description", "MACAddress", "NetConnectionID", "InterfaceIndex", "AdapterType" });
                using var searcher = new ManagementObjectSearcher(query);
                var adapters = searcher.Get().OfType<ManagementObject>().ToList();

                if (!adapters.Any())
                {
                    _logger.LogWarning("WMI 查询未返回任何物理网络适配器信息 (Win32_NetworkAdapter where PNPDeviceID IS NOT NULL)。");
                    data.Add(new HardwareInfo { Category = "网卡", Model = "未检测到物理网卡" });
                    return data;
                }
                _logger.LogDebug("查询到 {AdapterCount} 个潜在的物理网络适配器。", adapters.Count);

                int physicalAdapterCount = 0;
                foreach (var adapter in adapters)
                {
                    using (adapter)
                    {
                        var description = adapter["Description"]?.ToString()?.ToLower() ?? "";
                        var adapterType = adapter["AdapterType"]?.ToString()?.ToLower() ?? "";
                        var macAddress = adapter["MACAddress"]?.ToString()?.Replace(":", ""); // 获取并清理 MAC 地址

                        // 进一步过滤虚拟/不需要的适配器
                        if (string.IsNullOrEmpty(macAddress) || // 没有 MAC 地址通常不是物理网卡
                            macAddress.StartsWith("000000") || // MAC 地址为全 0
                            virtualAdapterKeywords.Any(keyword => description.Contains(keyword) || adapterType.Contains(keyword)))
                        {
                            _logger.LogDebug("  跳过非物理或不需要的适配器: {Description} (Type: {AdapterType}, MAC: {MAC})",
                                             adapter["Description"]?.ToString() ?? description, adapterType, macAddress ?? "N/A");
                            continue;
                        }

                        // 检查是否有 IP 配置 (可选，但可以增加准确性)
                        bool hasIpConfig = false;
                        try
                        {
                            var index = adapter["Index"]; // Index 是 uint32
                            if (index != null)
                            {
                                using var configSearcher = new ManagementObjectSearcher($"SELECT IPEnabled FROM Win32_NetworkAdapterConfiguration WHERE Index = {index}");
                                var config = configSearcher.Get().OfType<ManagementObject>().FirstOrDefault();
                                if (config != null)
                                {
                                    hasIpConfig = (bool?)config["IPEnabled"] ?? false;
                                    config.Dispose();
                                }
                            }
                        }
                        catch (ManagementException configEx)
                        {
                            _logger.LogWarning(configEx, "查询适配器 {Description} 的 IP 配置时出错。", description);
                        }

                        // 如果要求必须有 IP 配置才添加，可以在这里加判断
                        // if (!hasIpConfig)
                        // {
                        //     _logger.LogDebug("  跳过适配器 {Description}，因为它当前未启用 IP。", description);
                        //     continue;
                        // }


                        var manufacturer = adapter["Manufacturer"]?.ToString() ?? "N/A";
                        var model = adapter["Description"]?.ToString() ?? "N/A";

                        _logger.LogDebug("  发现物理网卡: {Manufacturer} {Model}, MAC: {MACAddress}, IP Enabled: {IPEnabled}", manufacturer, model, macAddress, hasIpConfig);
                        physicalAdapterCount++;

                        data.Add(new HardwareInfo
                        {
                            Category = "网卡",
                            Brand = manufacturer,
                            Model = model,
                            SerialNumber = macAddress, // 使用 MAC 地址作为序列号
                            Size = "N/A",
                            ManufactureDate = "N/A",
                            WarrantyLink = "N/A"
                        });
                    }
                }
                _logger.LogInformation("物理网卡信息扫描完成，共找到 {Count} 个物理网卡。", physicalAdapterCount);
                if (physicalAdapterCount == 0 && adapters.Any())
                {
                    data.Add(new HardwareInfo { Category = "网卡", Model = "未找到符合条件的物理网卡" });
                    _logger.LogWarning("虽然查询到潜在物理适配器，但未能找到有效的物理网卡（可能被过滤）。");
                }
            }
            catch (ManagementException wmiEx)
            {
                _logger.LogError(wmiEx, "扫描网卡失败 (WMI 查询出错)");
                data.Add(new HardwareInfo { Category = "网卡", Model = $"扫描失败: WMI 错误 ({wmiEx.Message})" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "扫描网卡时发生意外错误");
                data.Add(new HardwareInfo { Category = "网卡", Model = $"扫描失败: {ex.Message}" });
            }
            return data;
        });
    }
}