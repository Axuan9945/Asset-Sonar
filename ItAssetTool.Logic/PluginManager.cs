// File: ItAssetTool.Logic/PluginManager.cs
using System.Reflection;
using ItAssetTool.Core;
using Microsoft.Extensions.DependencyInjection; // 确保 using 存在
using Microsoft.Extensions.Logging;          // 确保 using 存在
using System;
using System.IO;
using System.Linq;

namespace ItAssetTool.Logic;

public class PluginManager
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PluginManager> _logger;

    public List<IScanPlugin> ScanPlugins { get; } = new();
    public List<IExportPlugin> ExportPlugins { get; } = new();
    public List<IDiagnosticPlugin> DiagnosticPlugins { get; } = new();
    public List<ISyncPlugin> SyncPlugins { get; } = new();
    public List<IDebugPlugin> DebugPlugins { get; } = new();
    public List<INetworkScanPlugin> NetworkScanPlugins { get; } = new();

    public PluginManager(IServiceProvider serviceProvider, ILogger<PluginManager> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public void DiscoverPlugins()
    {
        _logger.LogInformation("开始发现插件...");
        try
        {
            var baseDirectory = AppContext.BaseDirectory;
            var pluginAssemblyName = "ItAssetTool.Plugins.dll";
            var pluginAssemblyPath = Path.Combine(baseDirectory, pluginAssemblyName);

            if (!File.Exists(pluginAssemblyPath))
            {
                _logger.LogWarning("插件文件 {PluginAssembly} 未找到。", pluginAssemblyName);
                return;
            }

            _logger.LogDebug("正在加载插件程序集: {AssemblyPath}", pluginAssemblyPath);
            var pluginAssembly = Assembly.LoadFrom(pluginAssemblyPath);

            var pluginTypes = pluginAssembly.GetTypes()
                                            .Where(t => !t.IsInterface && !t.IsAbstract)
                                            .ToList();

            _logger.LogInformation("正在从已加载的程序集中实例化插件...");
            foreach (var type in pluginTypes)
            {
                try
                {
                    object? pluginInstance = null;

                    try
                    {
                        // 使用根 ServiceProvider 创建实例
                        pluginInstance = ActivatorUtilities.CreateInstance(_serviceProvider, type); // CS0103 会在此修复后解决
                        _logger.LogDebug("通过 DI 成功创建插件实例: {PluginType}", type.FullName);
                    }
                    catch (Exception creationEx)
                    {
                        _logger.LogWarning(creationEx, "通过 DI 创建插件 {PluginType} 失败，尝试使用无参数构造函数。", type.FullName);
                        try
                        {
                            pluginInstance = Activator.CreateInstance(type);
                            _logger.LogDebug("通过无参数构造函数成功创建插件实例: {PluginType}", type.FullName);
                        }
                        catch (Exception activatorEx)
                        {
                            _logger.LogError(activatorEx, "无法通过无参数构造函数创建插件实例: {PluginType}", type.FullName);
                            continue;
                        }
                    }

                    // 分配到列表
                    if (pluginInstance is IScanPlugin scanPlugin) ScanPlugins.Add(scanPlugin);
                    else if (pluginInstance is IExportPlugin exportPlugin) ExportPlugins.Add(exportPlugin);
                    else if (pluginInstance is IDiagnosticPlugin diagnosticPlugin) DiagnosticPlugins.Add(diagnosticPlugin);
                    else if (pluginInstance is ISyncPlugin syncPlugin) SyncPlugins.Add(syncPlugin);
                    else if (pluginInstance is IDebugPlugin debugPlugin) DebugPlugins.Add(debugPlugin);
                    else if (pluginInstance is INetworkScanPlugin networkScanPlugin) NetworkScanPlugins.Add(networkScanPlugin);

                    // 可以在添加后记录日志，如果需要插件名称
                    // if (pluginInstance is IScanPlugin p) _logger.LogDebug("已添加扫描插件: {PluginName}", p.Name);
                    // ... etc ...
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "处理插件类型 {PluginType} 时发生错误", type.FullName);
                }
            }
            _logger.LogInformation("插件发现和实例化完成。");
        }
        catch (FileNotFoundException fnfEx)
        {
            _logger.LogError(fnfEx, "加载插件程序集时发生文件未找到错误");
        }
        catch (BadImageFormatException bifEx)
        {
            _logger.LogError(bifEx, "加载插件程序集时发生格式错误，请确保插件是兼容的 .NET 程序集");
        }
        catch (ReflectionTypeLoadException rtlEx)
        {
            _logger.LogError(rtlEx, "加载插件程序集中的类型时出错。检查依赖项是否完整。Loader Exceptions: {LoaderExceptions}",
                string.Join("; ", rtlEx.LoaderExceptions.Select(e => e?.Message ?? "N/A")));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载插件时发生意外错误");
        }
    }
}