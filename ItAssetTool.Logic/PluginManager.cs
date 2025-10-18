// In Project: ItAssetTool.Logic
// File: PluginManager.cs
using System.Reflection;
using ItAssetTool.Core;

namespace ItAssetTool.Logic;

public class PluginManager
{
    public List<IScanPlugin> ScanPlugins { get; } = new();
    public List<IExportPlugin> ExportPlugins { get; } = new();
    public List<IDiagnosticPlugin> DiagnosticPlugins { get; } = new();
    public List<ISyncPlugin> SyncPlugins { get; } = new();
    public List<IDebugPlugin> DebugPlugins { get; } = new();

    // vvvv 【修改1】: 添加一个新的列表来存储网络扫描插件 vvvv
    public List<INetworkScanPlugin> NetworkScanPlugins { get; } = new();
    // ^^^^ 修改结束 ^^^^

    public void DiscoverPlugins()
    {
        try
        {
            var baseDirectory = AppContext.BaseDirectory;
            var pluginAssemblyPath = Path.Combine(baseDirectory, "ItAssetTool.Plugins.dll");

            if (!File.Exists(pluginAssemblyPath)) { return; }

            var pluginAssembly = Assembly.LoadFrom(pluginAssemblyPath);

            foreach (var type in pluginAssembly.GetTypes())
            {
                if (typeof(IScanPlugin).IsAssignableFrom(type) && !type.IsInterface)
                {
                    var plugin = Activator.CreateInstance(type) as IScanPlugin;
                    if (plugin != null) ScanPlugins.Add(plugin);
                }
                else if (typeof(IExportPlugin).IsAssignableFrom(type) && !type.IsInterface)
                {
                    var plugin = Activator.CreateInstance(type) as IExportPlugin;
                    if (plugin != null) ExportPlugins.Add(plugin);
                }
                else if (typeof(IDiagnosticPlugin).IsAssignableFrom(type) && !type.IsInterface)
                {
                    var plugin = Activator.CreateInstance(type) as IDiagnosticPlugin;
                    if (plugin != null) DiagnosticPlugins.Add(plugin);
                }
                else if (typeof(ISyncPlugin).IsAssignableFrom(type) && !type.IsInterface)
                {
                    var plugin = Activator.CreateInstance(type) as ISyncPlugin;
                    if (plugin != null) SyncPlugins.Add(plugin);
                }
                else if (typeof(IDebugPlugin).IsAssignableFrom(type) && !type.IsInterface)
                {
                    var plugin = Activator.CreateInstance(type) as IDebugPlugin;
                    if (plugin != null)
                    {
                        DebugPlugins.Add(plugin);
                        Console.WriteLine($"成功加载调试插件: {plugin.Name}");
                    }
                }
                // vvvv 【修改2】: 添加一个新的代码块来发现网络扫描插件 vvvv
                else if (typeof(INetworkScanPlugin).IsAssignableFrom(type) && !type.IsInterface)
                {
                    var plugin = Activator.CreateInstance(type) as INetworkScanPlugin;
                    if (plugin != null)
                    {
                        NetworkScanPlugins.Add(plugin);
                    }
                }
                // ^^^^ 修改结束 ^^^^
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"加载插件时发生错误: {ex.Message}");
        }
    }
}