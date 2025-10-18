namespace ItAssetTool.Core;

// 用于承载单条诊断结果的数据结构
public class DiagnosticResult
{
    public string? Task { get; set; }    // 任务名称, e.g., "CPU 使用率"
    public string? Status { get; set; }  // 状态, e.g., "正常", "警告"
    public string? Message { get; set; } // 详细信息, e.g., "35%"
    public double? Value { get; set; }   // 用于图表的数值, e.g., 35.0
}

public interface IDiagnosticPlugin
{
    // 插件的显示名称
    string Name { get; }

    // 异步执行诊断任务的方法
    Task<List<DiagnosticResult>> RunDiagnosticAsync();
}