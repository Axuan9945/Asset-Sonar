// File: ItAssetTool.Plugins/ScanCpu.cs
using ItAssetTool.Core;
using Microsoft.Extensions.Logging; // <-- 添加 using
using System;                    // <-- 添加 using for Exception
using System.Collections.Generic; // <-- 添加 using for List
using System.Diagnostics;         // <-- 添加 using for Debug (如果需要)
using System.Linq;                // <-- 添加 using for Linq methods
using System.Management;          // <-- 添加 using for Management classes
using System.Runtime.Versioning;  // <-- 添加 using for SupportedOSPlatform
using System.Threading.Tasks;     // <-- 添加 using for Task

namespace ItAssetTool.Plugins;

[SupportedOSPlatform("windows")]
public class ScanCpu : IScanPlugin
{
    private readonly ILogger<ScanCpu> _logger; // <-- 注入 ILogger

    public string Name => "处理器信息";

    // 构造函数注入 ILogger
    public ScanCpu(ILogger<ScanCpu> logger)
    {
        _logger = logger;
    }

    public Task<List<HardwareInfo>> ScanAsync()
    {
        // 保持 Task.Run 以避免阻塞 UI 线程
        return Task.Run(() =>
        {
            var data = new List<HardwareInfo>();
            _logger.LogInformation("开始扫描 CPU 信息..."); // 使用 logger
            try
            {
                // 只查询需要的字段以提高性能
                ManagementObjectSearcher searcher = new("SELECT Manufacturer, Name, ProcessorId FROM Win32_Processor");
                var processors = searcher.Get().OfType<ManagementObject>().ToList();

                if (!processors.Any())
                {
                    _logger.LogWarning("WMI 查询未返回任何处理器信息。");
                    // 可以添加一条记录表明未检测到
                    data.Add(new HardwareInfo { Category = "处理器", Model = "未检测到处理器" });
                    return data;
                }

                foreach (var obj in processors)
                {
                    using (obj) // 确保 ManagementObject 被释放
                    {
                        var brand = obj["Manufacturer"]?.ToString() ?? "N/A";
                        var model = obj["Name"]?.ToString()?.Trim() ?? "N/A";
                        var serial = obj["ProcessorId"]?.ToString() ?? "无法获取";

                        _logger.LogDebug("  发现 CPU: {Brand} {Model}, ID: {ProcessorId}", brand, model, serial); // 使用 Debug 级别

                        data.Add(new HardwareInfo
                        {
                            Category = "处理器",
                            Brand = brand,
                            Model = model,
                            SerialNumber = serial,
                            Size = "N/A",
                            ManufactureDate = "N/A",
                            WarrantyLink = "N/A"
                        });
                    }
                }
                _logger.LogInformation("CPU 信息扫描完成，共找到 {Count} 个处理器。", data.Count);
            }
            catch (ManagementException wmiEx) // 捕获 WMI 特有的异常
            {
                _logger.LogError(wmiEx, "扫描 CPU 失败 (WMI 查询出错)");
                // 添加错误信息到结果列表，以便 UI 显示
                data.Add(new HardwareInfo { Category = "处理器", Model = $"扫描失败: WMI 错误 ({wmiEx.Message})" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "扫描 CPU 时发生意外错误");
                data.Add(new HardwareInfo { Category = "处理器", Model = $"扫描失败: {ex.Message}" });
            }
            return data;
        });
    }
}