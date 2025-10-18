namespace ItAssetTool.Core;

public interface IExportPlugin
{
    // 插件的显示名称，例如 "导出为 Excel"
    string Name { get; }

    // 建议的文件扩展名，例如 ".xlsx"
    string FileExtension { get; }

    // 用于文件保存对话框的文件类型过滤器
    string FileFilter { get; }

    // 异步执行导出操作的方法
    // 它接收硬件数据列表和用户选择的保存路径
    Task ExportAsync(List<HardwareInfo> data, string filePath);
}