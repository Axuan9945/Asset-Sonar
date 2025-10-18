using ItAssetTool.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace ItAssetTool.Plugins;

[SupportedOSPlatform("windows")]
public class ScanPeripherals : IScanPlugin
{
    public string Name => "键盘和鼠标";

    private record PeripheralInfo(string Category, string Description, string Name, string Manufacturer, string PnpDeviceId);

    public Task<List<HardwareInfo>> ScanAsync()
    {
        return Task.Run(() =>
        {
            var data = new List<HardwareInfo>();
            var keyboardBlacklist = new[] { "hid keyboard device", "usb 输入设备", "ps/2" };
            var mouseBlacklist = new[] { "hid-compliant mouse", "usb 输入设备" };

            try
            {
                // 1. 安全地收集所有设备信息
                var allCandidates = new List<PeripheralInfo>();
                try
                {
                    using var keyboardSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_Keyboard");
                    foreach (var item in keyboardSearcher.Get())
                    {
                        var mo = (ManagementObject)item;
                        allCandidates.Add(new PeripheralInfo("键盘", mo["Description"]?.ToString() ?? "", mo["Name"]?.ToString() ?? "", "", mo["PNPDeviceID"]?.ToString() ?? ""));
                    }
                }
                catch (ManagementException) { /* 静默失败 */ }

                try
                {
                    using var mouseSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_PointingDevice");
                    foreach (var item in mouseSearcher.Get())
                    {
                        var mo = (ManagementObject)item;
                        allCandidates.Add(new PeripheralInfo("鼠标", mo["Description"]?.ToString() ?? "", mo["Name"]?.ToString() ?? "", mo["Manufacturer"]?.ToString() ?? "", mo["PNPDeviceID"]?.ToString() ?? ""));
                    }
                }
                catch (ManagementException) { /* 静默失败 */ }

                // 2. 智能识别和分配
                var specificDevices = allCandidates.Where(d => !IsGeneric(d, keyboardBlacklist.Concat(mouseBlacklist))).ToList();

                PeripheralInfo? finalMouse = null;
                finalMouse = specificDevices.FirstOrDefault(d => d.Description.ToLower().Contains("mouse") || d.Description.ToLower().Contains("basilisk"));
                if (finalMouse == null)
                {
                    finalMouse = allCandidates.FirstOrDefault(d => d.Category == "鼠标" && !IsGeneric(d, mouseBlacklist));
                }

                var keyboardCandidates = allCandidates.Where(c => c.Category == "键盘");
                if (finalMouse != null)
                {
                    keyboardCandidates = keyboardCandidates.Where(k => k.PnpDeviceId != finalMouse.PnpDeviceId);
                }
                var finalKeyboard = keyboardCandidates.FirstOrDefault(d => !IsGeneric(d, keyboardBlacklist));

                if (finalKeyboard == null) finalKeyboard = keyboardCandidates.FirstOrDefault();
                if (finalMouse == null) finalMouse = allCandidates.FirstOrDefault(d => d.Category == "鼠标");

                // 3. 添加到最终结果
                var addedDescriptions = new HashSet<string>();
                if (finalKeyboard != null && addedDescriptions.Add(finalKeyboard.Description))
                {
                    data.Add(new HardwareInfo
                    {
                        Category = "键盘",
                        Brand = GetBrand(finalKeyboard),
                        Model = finalKeyboard.Description,
                        SerialNumber = "N/A" // <--- vvvv 核心修正 vvvv
                    });
                }
                if (finalMouse != null && addedDescriptions.Add(finalMouse.Description))
                {
                    data.Add(new HardwareInfo
                    {
                        Category = "鼠标",
                        Brand = GetBrand(finalMouse, true),
                        Model = finalMouse.Description,
                        SerialNumber = "N/A" // <--- vvvv 核心修正 vvvv
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"扫描外设失败: {ex.Message}");
                data.Add(new HardwareInfo { Category = "外设", Model = $"扫描失败: {ex.Message}", SerialNumber = "N/A" });
            }
            return data;
        });
    }

    private bool IsGeneric(PeripheralInfo device, IEnumerable<string> blacklist)
    {
        var description = device.Description.ToLower();
        var name = device.Name.ToLower();
        return blacklist.Any(keyword => description.Contains(keyword) || name.Contains(keyword));
    }

    private string GetBrand(PeripheralInfo? device, bool isMouse = false)
    {
        if (device == null) return "N/A";
        if (isMouse) return device.Manufacturer;
        return !string.IsNullOrWhiteSpace(device.Name) ? device.Name.Split(' ')[0] : "N/A";
    }
}