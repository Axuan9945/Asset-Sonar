using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ItAssetTool.Core;
using ItAssetTool.Logic;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ItAssetTool.ViewModels;

public partial class DiagnosticViewModel : ObservableObject
{
    private readonly PluginManager _pluginManager;

    [ObservableProperty]
    private bool isDiagnosing;

    // 用于绑定到UI上的诊断结果列表
    public ObservableCollection<DiagnosticResult> DiagnosticResults { get; } = new();

    // vvvv 【优化：使用构造函数注入依赖】 vvvv
    public DiagnosticViewModel(PluginManager pluginManager)
    {
        _pluginManager = pluginManager;
        _pluginManager.DiscoverPlugins();
    }
    // ^^^^ 优化结束 ^^^^

    [RelayCommand]
    private async Task RunDiagnosticsAsync()
    {
        IsDiagnosing = true;
        DiagnosticResults.Clear();

        var diagnosticPlugin = _pluginManager.DiagnosticPlugins.FirstOrDefault();
        if (diagnosticPlugin == null)
        {
            DiagnosticResults.Add(new DiagnosticResult { Task = "错误", Status = "失败", Message = "未找到任何诊断插件。" });
            IsDiagnosing = false;
            return;
        }

        try
        {
            var results = await diagnosticPlugin.RunDiagnosticAsync();
            foreach (var result in results)
            {
                DiagnosticResults.Add(result);
            }
        }
        catch (Exception ex)
        {
            DiagnosticResults.Add(new DiagnosticResult { Task = "插件执行失败", Status = "错误", Message = ex.Message });
        }
        finally
        {
            IsDiagnosing = false;
        }
    }
}