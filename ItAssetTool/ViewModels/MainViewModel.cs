// File: ItAssetTool/ViewModels/MainViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ItAssetTool.Core;
using ItAssetTool.Logic;
using Microsoft.Extensions.Logging; // <-- 确保 using 存在
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Management; // Needed for ManagementException
using System.Net.Http; // Needed for HttpRequestException
using System.Text;
using System.Text.Json; // Needed for JsonException
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop; // Needed for InitializeWithWindow, WindowNative

namespace ItAssetTool.ViewModels;

public class SnipeItConfig
{
    public List<string>? IGNORE_KEYWORDS { get; set; }
}

public partial class MainViewModel : ObservableObject // Class must be partial
{
    // Dependencies
    private readonly PluginManager _pluginManager;
    private readonly IUxThreadDispatcher _dispatcher;
    private readonly SettingsViewModel _settingsViewModel;
    private readonly ILogger<MainViewModel> _logger;

    // State
    private List<string> _ignoreKeywords = new();
    private StringBuilder _logBuilder = new StringBuilder(); // For UI Log

    // --- Observable Properties ---
    private bool _isScanning;
    public bool IsScanning
    {
        get => _isScanning;
        set { if (SetProperty(ref _isScanning, value)) { ScanCommand.NotifyCanExecuteChanged(); ExportCommand.NotifyCanExecuteChanged(); SyncCommand.NotifyCanExecuteChanged(); CleanupCommand.NotifyCanExecuteChanged(); ScanButtonText = value ? "正在扫描中..." : "重新扫描硬件信息"; } }
    }

    [ObservableProperty]
    private string scanButtonText = "开始扫描硬件信息";

    [ObservableProperty]
    private string logText = "";

    [ObservableProperty]
    private HardwareSummaryViewModel? hardwareSummary;

    // Collections
    public ObservableCollection<SelectablePluginViewModel> ScanPlugins { get; } = new();
    public ObservableCollection<HardwareInfo> ScannedData { get; } = new();

    // --- Constructor ---
    public MainViewModel(PluginManager pluginManager, IUxThreadDispatcher dispatcher, SettingsViewModel settingsViewModel, ILogger<MainViewModel> logger)
    {
        _pluginManager = pluginManager ?? throw new ArgumentNullException(nameof(pluginManager));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _settingsViewModel = settingsViewModel ?? throw new ArgumentNullException(nameof(settingsViewModel));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        HardwareSummary = new HardwareSummaryViewModel();
        LoadConfig();
        Log("正在初始化插件管理器..."); // Calls Log(string)
        _pluginManager.DiscoverPlugins();
        foreach (var plugin in _pluginManager.ScanPlugins.OrderBy(p => p.Name)) { ScanPlugins.Add(new SelectablePluginViewModel(plugin)); }
        _logger.LogInformation("扫描插件加载: {Count} 个。", ScanPlugins.Count);
        // ... (other plugin load logging) ...
        if (ScanPlugins.Any()) Log($"扫描插件加载完成: {ScanPlugins.Count} 个。"); // Calls Log(string)
        if (_pluginManager.ExportPlugins.Any()) Log($"导出插件加载完成: {_pluginManager.ExportPlugins.Count} 个。"); // Calls Log(string)
        if (_pluginManager.SyncPlugins.Any()) Log($"同步插件加载完成: {_pluginManager.SyncPlugins.Count} 个。"); // Calls Log(string)
        _logger.LogInformation("MainViewModel 初始化完成。");
    }

    // --- Logging Methods (Explicit Overloads) ---

    // Overload 1: message only (Information level)
    private void Log(string message)
    {
        LogCore(message, LogLevel.Information, null);
    }

    // Overload 2: message and level
    private void Log(string message, LogLevel level)
    {
        LogCore(message, level, null);
    }

    // Overload 3: message, level, and exception (Core Implementation)
    // Make sure this signature exactly matches how it's called with 3 arguments
    private void Log(string message, LogLevel level, Exception? ex) // <-- This is the 3-argument overload
    {
        LogCore(message, level, ex);
    }

    // Core method containing the actual logging and UI update logic
    private void LogCore(string message, LogLevel level, Exception? ex)
    {
        _logger.Log(level, ex, message); // Log using ILogger first

        _dispatcher.Enqueue(() => // Then update UI on the correct thread
        {
            const int maxLogLength = 15000;
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var prefix = level switch
            {
                LogLevel.Trace => "🔎 TRACE:",
                LogLevel.Debug => "🐛 DEBUG:",
                LogLevel.Information => "✅ INFO:",
                LogLevel.Warning => "⚠️ WARN:",
                LogLevel.Error => "❌ ERROR:",
                LogLevel.Critical => "💥 CRITICAL:",
                _ => "❓ UNKNOWN:"
            };
            var logEntry = $"{timestamp} {prefix} {message}";
            if (ex != null) { logEntry += $" -> {ex.GetType().Name}: {ex.Message}"; }

            _logBuilder.AppendLine(logEntry);
            while (_logBuilder.Length > maxLogLength)
            {
                int firstNewLine = _logBuilder.ToString().IndexOf(Environment.NewLine);
                if (firstNewLine == -1) { _logBuilder.Remove(0, _logBuilder.Length - maxLogLength); break; }
                _logBuilder.Remove(0, firstNewLine + Environment.NewLine.Length);
            }
            LogText = _logBuilder.ToString();
        });
    }

    // --- Configuration Loading ---
    private void LoadConfig()
    {
        _logger.LogInformation("开始加载忽略配置 (snipeit_config.json)...");
        try
        {
            var configFile = Path.Combine(AppContext.BaseDirectory, "snipeit_config.json");
            if (File.Exists(configFile))
            {
                var jsonString = File.ReadAllText(configFile); var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var config = JsonSerializer.Deserialize<SnipeItConfig>(jsonString, options);
                if (config?.IGNORE_KEYWORDS != null && config.IGNORE_KEYWORDS.Any()) { _ignoreKeywords = config.IGNORE_KEYWORDS; Log($"成功加载忽略配置，共 {_ignoreKeywords.Count} 个关键字。"); } // Calls Log(string)
                else { Log("snipeit_config.json 文件为空或不包含 IGNORE_KEYWORDS。", LogLevel.Warning); } // Calls Log(string, LogLevel)
            }
            else { Log("在程序目录中未找到 snipeit_config.json 文件！无法过滤虚拟设备。", LogLevel.Warning); } // Calls Log(string, LogLevel)
        }
        catch (JsonException jsonEx) { Log($"加载配置文件时发生 JSON 解析错误: {jsonEx.Message}", LogLevel.Error, jsonEx); } // Calls Log(string, LogLevel, Exception)
        catch (IOException ioEx) { Log($"读取配置文件时发生 IO 错误: {ioEx.Message}", LogLevel.Error, ioEx); } // Calls Log(string, LogLevel, Exception)
        catch (Exception ex) { Log($"加载配置文件时发生严重错误: {ex.Message}", LogLevel.Critical, ex); } // Calls Log(string, LogLevel, Exception)
    }

    // --- Commands ---
    private bool CanExecuteWhenNotScanning() => !IsScanning;

    [RelayCommand(CanExecute = nameof(CanExecuteWhenNotScanning))]
    private async Task ScanAsync()
    {
        if (IsScanning) return; IsScanning = true; _dispatcher.Enqueue(() => ScannedData.Clear());
        Log("--- 扫描任务开始 ---"); // Calls Log(string)
        var allResults = new List<HardwareInfo>(); var enabledPlugins = ScanPlugins.Where(p => p.IsEnabled).Select(p => p.Plugin).ToList();
        if (!enabledPlugins.Any()) { Log("未选择任何扫描模块，任务已取消。", LogLevel.Warning); IsScanning = false; return; } // Calls Log(string, LogLevel)

        var scanTasks = new List<Task>(); var resultsDictionary = new System.Collections.Concurrent.ConcurrentDictionary<string, List<HardwareInfo>>();
        foreach (var plugin in enabledPlugins)
        {
            var pluginLogPrefix = $"--- 开始扫描: {plugin.Name} ---"; Log(pluginLogPrefix); // Calls Log(string)
            scanTasks.Add(Task.Run(async () => {
                try { var r = await plugin.ScanAsync(); resultsDictionary.TryAdd(plugin.Name, r ?? new()); Log($"--- 完成扫描: {plugin.Name} (获取到 {r?.Count ?? 0} 条) ---"); } // Calls Log(string)
                catch (ManagementException wmiEx) { var msg = $"模块 '{plugin.Name}' 扫描失败 (WMI): {wmiEx.Message}"; Log(msg, LogLevel.Error, wmiEx); resultsDictionary.TryAdd(plugin.Name, new List<HardwareInfo> { new HardwareInfo { Category = plugin.Name, Model = msg } }); } // Calls Log(string, LogLevel, Exception)
                catch (Exception ex) { var msg = $"模块 '{plugin.Name}' 扫描失败: {ex.Message}"; Log(msg, LogLevel.Error, ex); resultsDictionary.TryAdd(plugin.Name, new List<HardwareInfo> { new HardwareInfo { Category = plugin.Name, Model = msg } }); } // Calls Log(string, LogLevel, Exception)
            }));
        }
        await Task.WhenAll(scanTasks);
        foreach (var name in enabledPlugins.Select(p => p.Name)) { if (resultsDictionary.TryGetValue(name, out var res)) allResults.AddRange(res); }

        Log("扫描完成，正在过滤虚拟设备..."); // Calls Log(string)
        var filteredResults = allResults.Where(i => i != null && !_ignoreKeywords.Any(k => !string.IsNullOrEmpty(k) && $"{i.Brand} {i.Model}".Contains(k, StringComparison.OrdinalIgnoreCase))).ToList();
        var filteredCount = allResults.Count(i => i != null) - filteredResults.Count;
        if (filteredCount > 0) { Log($"已过滤掉 {filteredCount} 个虚拟或忽略的设备。"); } else { Log("未发现需要过滤的虚拟设备。"); } // Calls Log(string)

        _dispatcher.Enqueue(() => { ScannedData.Clear(); foreach (var item in filteredResults) ScannedData.Add(item); HardwareSummary?.Update(ScannedData); });
        Log("--- 所有扫描任务完成 ---"); // Calls Log(string)
        IsScanning = false;
    }

    [RelayCommand(CanExecute = nameof(CanExecuteWhenNotScanning))]
    private async Task ExportAsync(string? format)
    {
        if (string.IsNullOrEmpty(format)) return; if (!ScannedData.Any()) { Log("没有可导出的扫描数据。", LogLevel.Warning); return; } // Calls Log(string, LogLevel)
        var plugin = _pluginManager.ExportPlugins.FirstOrDefault(p => p.Name.Contains(format, StringComparison.OrdinalIgnoreCase));
        if (plugin == null) { Log($"未找到 {format} 导出插件。", LogLevel.Error); return; } // Calls Log(string, LogLevel)

        FileSavePicker? savePicker = null;
        try { savePicker = new FileSavePicker(); /* ... init ... */ }
        catch (Exception ex) { Log($"初始化文件保存对话框时出错: {ex.Message}", LogLevel.Error, ex); return; } // Calls Log(string, LogLevel, Exception)

        StorageFile? file = null;
        try { if (savePicker != null) file = await savePicker.PickSaveFileAsync(); else { /* log error */ return; } }
        catch (Exception ex) { Log($"选择保存文件时出错: {ex.Message}", LogLevel.Error, ex); return; } // Calls Log(string, LogLevel, Exception)

        if (file != null)
        {
            Log($"--- 开始导出到: {file.Name} ---"); _logger.LogInformation("..."); // Calls Log(string)
            try { await plugin.ExportAsync(ScannedData.ToList(), file.Path); Log($"文件已成功保存到: {file.Path}"); _logger.LogInformation("..."); } // Calls Log(string)
            catch (IOException ioEx) { Log($"导出文件时发生 IO 错误: {ioEx.Message}", LogLevel.Error, ioEx); _logger.LogError(ioEx, "..."); } // Calls Log(string, LogLevel, Exception)
            catch (Exception ex) { Log($"导出文件时发生错误: {ex.Message}", LogLevel.Error, ex); _logger.LogError(ex, "..."); } // Calls Log(string, LogLevel, Exception)
        }
        else { Log("用户取消了导出操作。"); } // Calls Log(string)
    }

    [RelayCommand(CanExecute = nameof(CanExecuteWhenNotScanning))]
    private async Task SyncAsync()
    {
        if (!ScannedData.Any()) { Log("没有可同步的数据。", LogLevel.Warning); return; } // Calls Log(string, LogLevel)
        var syncPlugin = _pluginManager.SyncPlugins.FirstOrDefault(); if (syncPlugin == null) { Log("未找到同步插件。", LogLevel.Error); return; } // Calls Log(string, LogLevel)
        var currentConfig = _settingsViewModel.GetCurrentConfig(); if (currentConfig == null) { Log("无法获取当前配置。", LogLevel.Error); return; } // Calls Log(string, LogLevel)
        Log("--- 开始同步到 Snipe-IT ---"); _logger.LogInformation("..."); // Calls Log(string)
        try
        {
            Action<string> logCallback = Log; // Pass Log method directly
            await syncPlugin.SyncAsync(ScannedData.ToList(), currentConfig, logCallback);
            Log("--- ✅ 同步任务执行完毕 ---"); // Calls Log(string)
        }
        catch (HttpRequestException httpEx) { Log($"同步过程中发生网络错误: {httpEx.Message}", LogLevel.Error, httpEx); _logger.LogError(httpEx, "..."); Log("--- ❌ 同步任务失败 ---", LogLevel.Error); } // Calls Log(string, LogLevel, Exception) & Log(string, LogLevel)
        catch (JsonException jsonEx) { Log($"同步过程中发生 JSON 解析错误: {jsonEx.Message}", LogLevel.Error, jsonEx); _logger.LogError(jsonEx, "..."); Log("--- ❌ 同步任务失败 ---", LogLevel.Error); } // Calls Log(string, LogLevel, Exception) & Log(string, LogLevel)
        catch (Exception ex) { Log($"同步过程中发生严重错误: {ex.Message}", LogLevel.Critical, ex); _logger.LogCritical(ex, "..."); Log("--- ❌ 同步任务失败 ---", LogLevel.Critical); } // Calls Log(string, LogLevel, Exception) & Log(string, LogLevel)
    }

    [RelayCommand(CanExecute = nameof(CanExecuteWhenNotScanning))]
    private async Task CleanupAsync()
    {
        var debugPlugin = _pluginManager.DebugPlugins.FirstOrDefault(); if (debugPlugin == null) { Log("未找到数据清理插件。", LogLevel.Error); return; } // Calls Log(string, LogLevel)
        var currentConfig = _settingsViewModel.GetCurrentConfig(); if (currentConfig == null) { Log("无法获取当前配置。", LogLevel.Error); return; } // Calls Log(string, LogLevel)
        LogText = ""; _logBuilder.Clear();
        Log("--- 用户确认，开始执行 Snipe-IT 数据清理任务 ---"); _logger.LogWarning("..."); // Calls Log(string)
        try
        {
            Action<string> logCallback = Log; // Pass Log method directly
            await debugPlugin.RunCleanupAsync(currentConfig, logCallback);
            Log("--- ✅ 清理任务执行完毕 ---"); _logger.LogWarning("..."); // Calls Log(string)
        }
        catch (Exception ex) { Log($"清理过程中发生严重错误: {ex.Message}", LogLevel.Critical, ex); _logger.LogCritical(ex, "..."); Log("--- ❌ 清理任务失败 ---", LogLevel.Critical); } // Calls Log(string, LogLevel, Exception) & Log(string, LogLevel)
    }
}