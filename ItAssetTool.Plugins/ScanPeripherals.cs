using ItAssetTool.Core;
using Microsoft.Extensions.Logging; // <-- 添加 using
using System;                    // <-- 添加 using for Exception, StringComparison
using System.Collections.Generic; // <-- 添加 using for List, HashSet
using System.Diagnostics;         // <-- 添加 using for Debug (如果不再需要可以移除)
using System.Linq;                // <-- 添加 using for Linq methods
using System.Management;          // <-- 添加 using for Management classes
using System.Runtime.Versioning;  // <-- 添加 using for SupportedOSPlatform
using System.Threading.Tasks;     // <-- 添加 using for Task

namespace ItAssetTool.Plugins;

[SupportedOSPlatform("windows")]
public class ScanPeripherals : IScanPlugin
{
    private readonly ILogger<ScanPeripherals> _logger; // <-- 添加 logger

    public string Name => "键盘和鼠标";

    // 内部记录结构，保持不变
    private record PeripheralInfo(string Category, string Description, string Name, string Manufacturer, string PnpDeviceId);

    // 构造函数注入
    public ScanPeripherals(ILogger<ScanPeripherals> logger)
    {
        _logger = logger;
    }


    public Task<List<HardwareInfo>> ScanAsync()
    {
        return Task.Run(() =>
        {
            _logger.LogInformation("开始扫描键盘和鼠标信息...");
            var data = new List<HardwareInfo>();
            // 黑名单，用于过滤通用设备描述
            var keyboardBlacklist = new[] { "hid keyboard device", "usb 输入设备", "ps/2", "standard keyboard" };
            var mouseBlacklist = new[] { "hid-compliant mouse", "usb 输入设备", "compliant mouse" };
            var combinedBlacklist = keyboardBlacklist.Concat(mouseBlacklist).ToArray();

            var allCandidates = new List<PeripheralInfo>();

            try
            {
                // 1. 安全地收集所有候选设备信息
                _logger.LogDebug("查询 Win32_Keyboard...");
                try
                {
                    // 查询更具体的字段
                    using var keyboardSearcher = new ManagementObjectSearcher("SELECT Description, Name, PNPDeviceID FROM Win32_Keyboard");
                    foreach (var item in keyboardSearcher.Get())
                    {
                        var mo = (ManagementObject)item;
                        using (mo)
                        {
                            var description = mo["Description"]?.ToString()?.Trim() ?? "";
                            var name = mo["Name"]?.ToString()?.Trim() ?? "";
                            var pnpId = mo["PNPDeviceID"]?.ToString()?.Trim() ?? "";
                            allCandidates.Add(new PeripheralInfo("键盘", description, name, "", pnpId));
                            _logger.LogTrace("  发现键盘候选: Desc='{Description}', Name='{Name}', PnpID='{PnpID}'", description, name, pnpId);
                        }
                    }
                }
                catch (ManagementException wmiEx)
                {
                    _logger.LogWarning(wmiEx, "查询 Win32_Keyboard 时发生 WMI 错误。");
                    /* 可以选择不中断，继续查询鼠标 */
                }

                _logger.LogDebug("查询 Win32_PointingDevice...");
                try
                {
                    // 查询更具体的字段
                    using var mouseSearcher = new ManagementObjectSearcher("SELECT Description, Name, Manufacturer, PNPDeviceID FROM Win32_PointingDevice");
                    foreach (var item in mouseSearcher.Get())
                    {
                        var mo = (ManagementObject)item;
                        using (mo)
                        {
                            var description = mo["Description"]?.ToString()?.Trim() ?? "";
                            var name = mo["Name"]?.ToString()?.Trim() ?? "";
                            var manufacturer = mo["Manufacturer"]?.ToString()?.Trim() ?? "";
                            var pnpId = mo["PNPDeviceID"]?.ToString()?.Trim() ?? "";
                            allCandidates.Add(new PeripheralInfo("鼠标", description, name, manufacturer, pnpId));
                            _logger.LogTrace("  发现鼠标候选: Desc='{Description}', Name='{Name}', Manuf='{Manufacturer}', PnpID='{PnpID}'", description, name, manufacturer, pnpId);
                        }
                    }
                }
                catch (ManagementException wmiEx)
                {
                    _logger.LogWarning(wmiEx, "查询 Win32_PointingDevice 时发生 WMI 错误。");
                    /* 可以选择不中断 */
                }
                _logger.LogDebug("共找到 {CandidateCount} 个外设候选。", allCandidates.Count);


                // 2. 智能识别和分配
                _logger.LogDebug("开始筛选通用外设...");
                // 找出明确的、非通用的设备
                var specificDevices = allCandidates.Where(d => !IsGeneric(d, combinedBlacklist)).ToList();
                _logger.LogDebug("筛选出 {SpecificCount} 个非通用设备。", specificDevices.Count);

                PeripheralInfo? finalMouse = null;
                // 优先选择描述中包含 "mouse" 且非通用的
                finalMouse = specificDevices.FirstOrDefault(d => d.Category == "鼠标"); // 简化逻辑，优先选非通用的鼠标

                PeripheralInfo? finalKeyboard = null;
                // 优先选择描述中非通用的键盘
                finalKeyboard = specificDevices.FirstOrDefault(d => d.Category == "键盘");


                // 如果没有找到非通用的，则从所有候选者中选择第一个作为备选
                if (finalKeyboard == null)
                {
                    finalKeyboard = allCandidates.FirstOrDefault(d => d.Category == "键盘");
                    if (finalKeyboard != null) _logger.LogDebug("未找到特定键盘，选择第一个键盘候选: {Description}", finalKeyboard.Description);
                }
                if (finalMouse == null)
                {
                    finalMouse = allCandidates.FirstOrDefault(d => d.Category == "鼠标");
                    if (finalMouse != null) _logger.LogDebug("未找到特定鼠标，选择第一个鼠标候选: {Description}", finalMouse.Description);
                }

                // 3. 添加到最终结果 (确保不添加重复的设备，基于 PnpDeviceId)
                var addedPnpIds = new HashSet<string>();
                if (finalKeyboard != null && !string.IsNullOrEmpty(finalKeyboard.PnpDeviceId) && addedPnpIds.Add(finalKeyboard.PnpDeviceId))
                {
                    var brand = InferBrandFromName(finalKeyboard.Name, finalKeyboard.Description); // 从 Name 或 Description 推断品牌
                    var model = finalKeyboard.Description; // 使用 Description 作为型号
                    _logger.LogInformation("确定键盘: {Brand} {Model}", brand, model);
                    data.Add(new HardwareInfo
                    {
                        Category = "键盘",
                        Brand = brand,
                        Model = model,
                        SerialNumber = "N/A"
                    });
                }
                else if (finalKeyboard != null)
                {
                    _logger.LogWarning("最终选择的键盘 PnpDeviceId 为空或重复，未添加。Desc='{Desc}' PnpID='{PnpID}'", finalKeyboard.Description, finalKeyboard.PnpDeviceId);
                }
                else
                {
                    _logger.LogWarning("未找到任何键盘设备。");
                }


                if (finalMouse != null && !string.IsNullOrEmpty(finalMouse.PnpDeviceId) && addedPnpIds.Add(finalMouse.PnpDeviceId))
                {
                    // 鼠标优先使用 Manufacturer 字段作为品牌
                    var brand = !string.IsNullOrWhiteSpace(finalMouse.Manufacturer) ? finalMouse.Manufacturer : InferBrandFromName(finalMouse.Name, finalMouse.Description);
                    var model = finalMouse.Description;
                    _logger.LogInformation("确定鼠标: {Brand} {Model}", brand, model);
                    data.Add(new HardwareInfo
                    {
                        Category = "鼠标",
                        Brand = brand,
                        Model = model,
                        SerialNumber = "N/A"
                    });
                }
                else if (finalMouse != null)
                {
                    _logger.LogWarning("最终选择的鼠标 PnpDeviceId 为空或重复，未添加。Desc='{Desc}' PnpID='{PnpID}'", finalMouse.Description, finalMouse.PnpDeviceId);
                }
                else
                {
                    _logger.LogWarning("未找到任何鼠标设备。");
                }
                _logger.LogInformation("键盘和鼠标扫描完成。");
            }
            catch (Exception ex) // 捕获其他意外错误
            {
                _logger.LogError(ex, "扫描外设时发生意外错误");
                data.Add(new HardwareInfo { Category = "外设", Model = $"扫描失败: {ex.Message}", SerialNumber = "N/A" });
            }
            return data;
        });
    }

    // 辅助函数：判断是否为通用设备
    private bool IsGeneric(PeripheralInfo device, IEnumerable<string> blacklist)
    {
        // Description 通常比 Name 更具体，优先判断 Description
        var description = device.Description?.ToLowerInvariant() ?? "";
        if (blacklist.Any(keyword => description.Contains(keyword)))
        {
            return true;
        }
        // 如果 Description 不通用，再检查 Name
        var name = device.Name?.ToLowerInvariant() ?? "";
        if (blacklist.Any(keyword => name.Contains(keyword)))
        {
            return true;
        }
        return false;
    }

    // 辅助函数：尝试从 Name 或 Description 推断品牌
    private string InferBrandFromName(string name, string description)
    {
        // 优先从 Name 中提取第一个单词作为品牌
        if (!string.IsNullOrWhiteSpace(name))
        {
            var parts = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0 && parts[0].Length > 1) // 避免单个字母或数字
            {
                // 排除常见的通用词
                var commonWords = new[] { "microsoft", "standard", "logitech", "dell", "lenovo", "hp" }; // 可以扩展
                if (!commonWords.Contains(parts[0].ToLowerInvariant()))
                {
                    // return parts[0]; // 可能不够准确
                }
            }
        }
        // 如果 Name 中没找到，尝试从 Description 中提取
        if (!string.IsNullOrWhiteSpace(description))
        {
            var lowerDesc = description.ToLowerInvariant();
            if (lowerDesc.Contains("logitech")) return "Logitech";
            if (lowerDesc.Contains("microsoft")) return "Microsoft";
            if (lowerDesc.Contains("razer")) return "Razer";
            if (lowerDesc.Contains("dell")) return "Dell";
            if (lowerDesc.Contains("lenovo")) return "Lenovo";
            if (lowerDesc.Contains("hp")) return "HP";
            // 可以添加更多品牌...
        }

        return "未知品牌"; // 默认返回未知
    }
}