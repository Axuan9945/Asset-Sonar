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
public class ScanActivation : IScanPlugin
{
    private readonly ILogger<ScanActivation> _logger; // <-- 添加 logger

    public string Name => "系统激活状态";

    // 构造函数注入
    public ScanActivation(ILogger<ScanActivation> logger)
    {
        _logger = logger;
    }

    public Task<List<HardwareInfo>> ScanAsync()
    {
        return Task.Run(() =>
        {
            _logger.LogInformation("开始扫描系统激活状态...");
            var data = new List<HardwareInfo>();
            string status = "未激活或无法确定"; // 默认状态
            bool foundWindowsLicense = false;

            try
            {
                // WMI 查询 SoftwareLicensingProduct 类
                var query = new SelectQuery("SELECT Description, PartialProductKey, LicenseStatus FROM SoftwareLicensingProduct");
                using var searcher = new ManagementObjectSearcher(query);
                var licenses = searcher.Get().OfType<ManagementObject>().ToList();

                _logger.LogDebug("查询到 {LicenseCount} 条 SoftwareLicensingProduct 记录。", licenses.Count);

                foreach (var obj in licenses)
                {
                    using (obj)
                    {
                        var description = obj["Description"]?.ToString()?.ToLower() ?? "";
                        var partialKey = obj["PartialProductKey"]?.ToString();
                        var licenseStatusObj = obj["LicenseStatus"]; // 获取原始对象

                        // 寻找包含 "windows" 描述且 *通常* 有部分产品密钥的授权信息
                        // 注意：有些许可证（如 KMS）可能没有 PartialProductKey
                        if (description.Contains("windows"))
                        {
                            foundWindowsLicense = true; // 标记找到了 Windows 相关的许可证
                            _logger.LogDebug("  找到 Windows 许可证: {Description}, 部分密钥: {PartialKey}, 状态代码: {StatusCode}",
                                             description, partialKey ?? "无", licenseStatusObj ?? "未知");

                            // LicenseStatus = 1 表示已授权 (已激活)
                            // 需要正确处理可能的 null 值和类型转换
                            if (licenseStatusObj != null && uint.TryParse(licenseStatusObj.ToString(), out uint licenseStatusCode) && licenseStatusCode == 1)
                            {
                                status = "已激活";
                                _logger.LogInformation("检测到已激活的 Windows 许可证: {Description}", description);
                                break; // 找到一个已激活的 Windows 就可以停止了
                            }
                        }
                    }
                }

                if (!foundWindowsLicense)
                {
                    _logger.LogWarning("未查询到任何包含 'windows' 描述的许可证信息。");
                    status = "无法确定 (未找到 Windows 许可证)";
                }
                else if (status != "已激活") // 如果找到了 Windows 许可证但没有一个是激活状态
                {
                    _logger.LogWarning("找到了 Windows 许可证，但没有检测到激活状态 (LicenseStatus != 1)。");
                }

            }
            catch (ManagementException wmiEx)
            {
                _logger.LogError(wmiEx, "扫描激活状态失败 (WMI 查询出错)");
                status = $"查询失败: WMI 错误 ({wmiEx.Message})";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "扫描激活状态时发生意外错误");
                status = $"查询失败: {ex.Message}";
            }

            _logger.LogInformation("系统激活状态扫描完成: {Status}", status);
            data.Add(new HardwareInfo
            {
                Category = "系统激活状态",
                Model = status, // 将状态放在 Model 字段
                Brand = "N/A",
                SerialNumber = "N/A",
                Size = "N/A",
                ManufactureDate = "N/A",
                WarrantyLink = "N/A"
            });

            return data;
        });
    }
}