using ItAssetTool.Core;
using Microsoft.Extensions.Logging; // <-- 添加 using
using System;                    // <-- 添加 using for Exception
using System.Collections.Generic; // <-- 添加 using for List
using System.IO;                  // <-- 添加 using for File, IOException
using System.Text;                // <-- 添加 using for StringBuilder, Encoding
using System.Threading.Tasks;     // <-- 添加 using for Task

namespace ItAssetTool.Plugins;

public class ExportCsv : IExportPlugin
{
    private readonly ILogger<ExportCsv> _logger; // <-- 添加 logger

    public string Name => "导出为 CSV";
    public string FileExtension => ".csv";
    public string FileFilter => "CSV 文件 (*.csv)";

    // 构造函数注入
    public ExportCsv(ILogger<ExportCsv> logger)
    {
        _logger = logger;
    }

    public async Task ExportAsync(List<HardwareInfo> data, string filePath)
    {
        _logger.LogInformation("开始导出数据到 CSV 文件: {FilePath}", filePath);
        if (data == null || !data.Any())
        {
            _logger.LogWarning("没有数据可导出到 CSV。");
            // 可以选择抛出异常或直接返回
            // throw new ArgumentException("数据列表为空，无法导出。");
            return;
        }

        var sb = new StringBuilder();

        try
        {
            // 添加UTF-8 BOM头
            sb.Append('\uFEFF');
            _logger.LogDebug("已添加 UTF-8 BOM 头。");

            // 创建表头
            var headers = new string[] { "类别", "品牌", "型号", "大小", "序列号", "生产日期", "保修查询链接" };
            sb.AppendLine(string.Join(",", headers));
            _logger.LogDebug("已写入 CSV 表头。");

            // 遍历数据并创建每一行
            int rowCount = 0;
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
                rowCount++;
            }
            _logger.LogInformation("已处理 {RowCount} 条数据。", rowCount);

            // 异步地将所有内容一次性写入文件
            await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
            _logger.LogInformation("CSV 文件已成功保存: {FilePath}", filePath);
        }
        catch (IOException ioEx) // 捕获文件读写相关的异常
        {
            _logger.LogError(ioEx, "写入 CSV 文件时发生 IO 错误: {FilePath}", filePath);
            // 将异常重新抛出，让调用者 (ViewModel) 知道导出失败
            throw new IOException($"写入 CSV 文件 '{filePath}' 时出错: {ioEx.Message}", ioEx);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导出 CSV 文件时发生意外错误: {FilePath}", filePath);
            // 将异常重新抛出
            throw new Exception($"导出 CSV 文件 '{filePath}' 时发生意外错误: {ex.Message}", ex);
        }
    }

    // 辅助函数，处理字段中可能包含逗号或引号的情况
    private string QuoteCsvField(string? field)
    {
        if (string.IsNullOrEmpty(field))
        {
            return "";
        }

        // 如果字段包含逗号、双引号或换行符，则需要用双引号括起来，并将内部的双引号替换为两个双引号
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        return field;
    }
}