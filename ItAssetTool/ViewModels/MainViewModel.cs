// File: axuan9945/itassettool/ItAssetTool-187fc96af793309702a81bc2a64f54675adec7e6/ItAssetTool/ViewModels/MainViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ItAssetTool.Core;
using ItAssetTool.Logic;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WinRT.Interop;
using System.Collections.ObjectModel;
// using Microsoft.Extensions.DependencyInjection; // 此 using 已经不需要，因为 MainViewModel 中没有直接使用 ServiceProvider

namespace ItAssetTool.ViewModels;

public class SnipeItConfig
{
    public List<string>? IGNORE_KEYWORDS { get; set; }
}

public partial class MainViewModel : ObservableObject
{
    private readonly PluginManager _pluginManager;
    private readonly IUxThreadDispatcher _dispatcher; // 新增注入的调度器依赖
    private readonly SettingsViewModel _settingsViewModel;
    private List<string> _ignoreKeywords = new();

    [ObservableProperty] private bool isScanning;
    [ObservableProperty] private string scanButtonText = "开始扫描硬件信息";
    public ObservableCollection<SelectablePluginViewModel> ScanPlugins { get; } = new();
    public ObservableCollection<HardwareInfo> ScannedData { get; } = new();
    [ObservableProperty] private string logText = "";

    [ObservableProperty]
    private HardwareSummaryViewModel? _hardwareSummary;

    // vvvv 核心修正：使用构造函数注入 SettingsViewModel vvvv
    public MainViewModel(PluginManager pluginManager, IUxThreadDispatcher dispatcher, SettingsViewModel settingsViewModel)
    {
        _pluginManager = pluginManager;
        _dispatcher = dispatcher;
        _settingsViewModel = settingsViewModel; // 赋值注入的 SettingsViewModel

        HardwareSummary = new HardwareSummaryViewModel();

        LoadConfig();
        Log("正在初始化插件管理器...");
        _pluginManager.DiscoverPlugins();
        foreach (var plugin in _pluginManager.ScanPlugins.OrderBy(p => p.Name))
        {
            ScanPlugins.Add(new SelectablePluginViewModel(plugin));
        }
        if (ScanPlugins.Any()) Log($"扫描插件加载完成: {ScanPlugins.Count} 个。");
        if (_pluginManager.ExportPlugins.Any()) Log($"导出插件加载完成: {_pluginManager.ExportPlugins.Count} 个。");
        if (_pluginManager.SyncPlugins.Any()) Log($"同步插件加载完成: {_pluginManager.SyncPlugins.Count} 个。");
        if (_pluginManager.DebugPlugins.Any()) Log($"调试插件加载完成: {_pluginManager.DebugPlugins.Count} 个。");
    }
    // ^^^^ 核心修正结束 ^^^^

    private void Log(string message) { LogText += message + Environment.NewLine; }

    private void LoadConfig()
    {
        try
        {
            var configFile = Path.Combine(AppContext.BaseDirectory, "snipeit_config.json");
            if (File.Exists(configFile))
            {
                var jsonString = File.ReadAllText(configFile);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var config = JsonSerializer.Deserialize<SnipeItConfig>(jsonString, options);

                if (config?.IGNORE_KEYWORDS != null && config.IGNORE_KEYWORDS.Any())
                {
                    _ignoreKeywords = config.IGNORE_KEYWORDS;
                    Log($"✅ 成功加载忽略配置，共 {_ignoreKeywords.Count} 个关键字。");
                }
                else
                {
                    Log("⚠️ 警告：snipeit_config.json 文件为空或不包含 IGNORE_KEYWORDS。");
                }
            }
            else
            {
                Log("❌ 错误：在程序目录中未找到 snipeit_config.json 文件！无法过滤虚拟设备。");
            }
        }
        catch (Exception ex)
        {
            Log($"❌ 加载配置文件时发生严重错误: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        IsScanning = true;
        ScanButtonText = "正在扫描中...";
        _dispatcher.Enqueue(() => ScannedData.Clear()); // 使用注入的调度器
        Log("--- 扫描任务开始 ---");
        var allResults = new List<HardwareInfo>();
        var enabledPlugins = ScanPlugins.Where(p => p.IsEnabled).Select(p => p.Plugin);
        if (!enabledPlugins.Any())
        {
            Log("⚠️ 未选择任何扫描模块，任务已取消。");
            IsScanning = false;
            ScanButtonText = "重新扫描硬件信息";
            return;
        }
        foreach (var plugin in enabledPlugins)
        {
            Log($"--- 正在扫描: {plugin.Name} ---");
            try
            {
                var results = await plugin.ScanAsync();
                allResults.AddRange(results);
            }
            catch (Exception ex) { Log($"❌ 模块 '{plugin.Name}' 扫描失败: {ex.Message}"); }
        }
        Log("✅ 扫描完成，正在过滤虚拟设备...");
        var filteredResults = allResults.Where(item =>
            !_ignoreKeywords.Any(keyword =>
                $"{item.Brand} {item.Model}".Contains(keyword, StringComparison.OrdinalIgnoreCase)
            )).ToList();
        var filteredCount = allResults.Count - filteredResults.Count;
        if (filteredCount > 0)
        {
            Log($"  -> 已根据 'IGNORE_KEYWORDS' 过滤掉 {filteredCount} 个虚拟或忽略的设备。");
        }
        else
        {
            Log("  -> 未发现需要过滤的虚拟设备。");
        }

        _dispatcher.Enqueue(() => // 使用注入的调度器
        {
            ScannedData.Clear();
            foreach (var item in filteredResults) ScannedData.Add(item);
            HardwareSummary?.Update(ScannedData);
        });

        Log("--- 所有任务完成 ---");
        IsScanning = false;
        ScanButtonText = "重新扫描硬件信息";
    }

    #region Unchanged Methods
    [RelayCommand]
    private async Task ExportAsync(string? format)
    {
        if (string.IsNullOrEmpty(format)) return;
        if (!ScannedData.Any()) { Log("警告：没有可导出的扫描数据。"); return; }
        var plugin = _pluginManager.ExportPlugins.FirstOrDefault(p => p.Name.Contains(format, StringComparison.OrdinalIgnoreCase));
        if (plugin == null) { Log($"错误：未找到 {format} 导出插件。"); return; }
        var savePicker = new FileSavePicker();
        var window = App.MainWindow;
        InitializeWithWindow.Initialize(savePicker, WindowNative.GetWindowHandle(window!));
        savePicker.SuggestedStartLocation = PickerLocationId.Desktop;
        savePicker.FileTypeChoices.Add(plugin.FileFilter, new List<string> { plugin.FileExtension });
        savePicker.SuggestedFileName = $"IT资产电脑硬件信息-{DateTime.Now:yyyyMMddHHmmss}";
        var file = await savePicker.PickSaveFileAsync();
        if (file != null)
        {
            Log($"--- 开始导出到: {file.Name} ---");
            try
            {
                await plugin.ExportAsync(ScannedData.ToList(), file.Path);
                Log($"✅ 文件已成功保存到: {file.Path}");
            }
            catch (Exception ex) { Log($"❌ 导出文件时发生错误: {ex.Message}"); }
        }
        else { Log("用户取消了导出操作。"); }
    }

    [RelayCommand]
    private async Task SyncAsync()
    {
        if (!ScannedData.Any()) { Log("警告：没有可同步的扫描数据。"); return; }
        var syncPlugin = _pluginManager.SyncPlugins.FirstOrDefault();
        if (syncPlugin == null) { Log("错误：未找到同步插件。"); return; }

        // ⚠️ 核心修正: 使用注入的 SettingsViewModel 获取配置
        var currentConfig = _settingsViewModel.GetCurrentConfig();

        Log("--- 开始同步到 Snipe-IT ---");
        try
        {
            await syncPlugin.SyncAsync(ScannedData.ToList(), currentConfig, (logMessage) =>
            {
                _dispatcher.Enqueue(() => Log(logMessage)); // 使用注入的调度器
            });
        }
        catch (Exception ex) { Log($"❌ 同步过程中发生严重错误: {ex.Message}"); }
    }

    [RelayCommand]
    private async Task CleanupAsync()
    {
        var debugPlugin = _pluginManager.DebugPlugins.FirstOrDefault();
        if (debugPlugin == null) { Log("错误：未找到数据清理插件。"); return; }

        // ⚠️ 核心修正: 使用注入的 SettingsViewModel 获取配置
        var currentConfig = _settingsViewModel.GetCurrentConfig();

        LogText = "";
        Log("--- 用户确认，开始执行 Snipe-IT 数据清理任务 ---");
        try
        {
            await debugPlugin.RunCleanupAsync(currentConfig, (logMessage) =>
            {
                _dispatcher.Enqueue(() => Log(logMessage)); // 使用注入的调度器
            });
        }
        catch (Exception ex) { Log($"❌ 清理过程中发生严重错误: {ex.Message}"); }
    }
    #endregion
}