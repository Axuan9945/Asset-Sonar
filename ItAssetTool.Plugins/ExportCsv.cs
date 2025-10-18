using ItAssetTool.Core;
using System.Text;

namespace ItAssetTool.Plugins;

public class ExportCsv : IExportPlugin
{
    public string Name => "导出为 CSV";

    public string FileExtension => ".csv";

    public string FileFilter => "CSV 文件 (*.csv)";

    public async Task ExportAsync(List<HardwareInfo> data, string filePath)
    {
        var sb = new StringBuilder();

        // 添加UTF-8 BOM头，确保Excel能正确识别编码打开CSV
        sb.Append('\uFEFF');

        // 创建表头
        var headers = new string[] { "类别", "品牌", "型号", "大小", "序列号", "生产日期", "保修查询链接" };
        sb.AppendLine(string.Join(",", headers));

        // 遍历数据并创建每一行
        foreach (var item in data)
        {
            var line = new string[]
            {
                QuoteCsvField(item.Category),
                QuoteCsvField(item.Brand),
                QuoteCsvField(item.Model),
                QuoteCsvField(item.Size),
                QuoteCsvField(item.SerialNumber),
                QuoteCsvField(item.ManufactureDate),
                QuoteCsvField(item.WarrantyLink)
            };
            sb.AppendLine(string.Join(",", line));
        }

        // 异步地将所有内容一次性写入文件
        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
    }

    // 一个辅助函数，用于处理字段中可能包含逗号或引号的情况
    private string QuoteCsvField(string? field)
    {
        if (string.IsNullOrEmpty(field))
        {
            return "";
        }

        if (field.Contains(',') || field.Contains('"'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        return field;
    }
}