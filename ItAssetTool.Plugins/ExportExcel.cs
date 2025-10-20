using ItAssetTool.Core;
using Microsoft.Extensions.Logging; // <-- 添加 using
using System;                    // <-- 添加 using for Exception, Uri, ArgumentException
using System.Collections.Generic; // <-- 添加 using for List
using System.IO;                  // <-- 添加 using for IOException
using System.Linq;                // <-- 添加 using for Linq methods
using System.Threading.Tasks;     // <-- 添加 using for Task
using ClosedXML.Excel;          // <-- 确保 using 存在


namespace ItAssetTool.Plugins;

public class ExportExcel : IExportPlugin
{
    private readonly ILogger<ExportExcel> _logger; // <-- 添加 logger

    public string Name => "导出为 Excel";
    public string FileExtension => ".xlsx";
    public string FileFilter => "Excel 工作簿 (*.xlsx)";

    // 构造函数注入
    public ExportExcel(ILogger<ExportExcel> logger)
    {
        _logger = logger;
    }


    public Task ExportAsync(List<HardwareInfo> data, string filePath)
    {
        _logger.LogInformation("开始导出数据到 Excel 文件: {FilePath}", filePath);
        if (data == null || !data.Any())
        {
            _logger.LogWarning("没有数据可导出到 Excel。");
            return Task.CompletedTask; // 对于 Task 返回的方法，返回已完成的任务
        }

        // 使用 Task.Run 保持异步，避免阻塞 UI，同时简化非 async 方法的包装
        return Task.Run(() =>
        {
            try
            {
                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("电脑配置信息");
                    _logger.LogDebug("已创建 Excel 工作簿和工作表 '电脑配置信息'");

                    // 设置表头
                    var headers = new string[] { "类别", "品牌", "型号", "大小", "序列号", "生产日期", "保修查询链接" };
                    for (int i = 0; i < headers.Length; i++)
                    {
                        worksheet.Cell(1, i + 1).Value = headers[i];
                    }
                    worksheet.Row(1).Style.Font.Bold = true;
                    _logger.LogDebug("已写入 Excel 表头。");

                    // 准备数据行 (进行清理)
                    // 使用匿名对象以便 ClosedXML 自动映射
                    var dataRows = data.Select((item, index) => new // 添加 index 用于定位单元格
                    {
                        RowIndex = index + 2, // Excel 行号从 1 开始，数据从第 2 行开始
                        Category = Sanitize(item.Category),
                        Brand = Sanitize(item.Brand),
                        Model = Sanitize(item.Model),
                        Size = Sanitize(item.Size),
                        SerialNumber = Sanitize(item.SerialNumber),
                        ManufactureDate = Sanitize(item.ManufactureDate),
                        WarrantyLink = Sanitize(item.WarrantyLink) // 保留原始链接用于设置超链接
                    }).ToList(); // 转换为 List

                    _logger.LogInformation("已准备 {RowCount} 条数据行。", dataRows.Count);

                    // 插入数据（不包括链接本身，链接后续单独设置）
                    if (dataRows.Any()) // 检查是否有数据行
                    {
                        // InsertData 需要一个 IEnumerable<object> 或 DataTable
                        // 创建一个只包含数据的匿名对象列表
                        var rowsToInsert = dataRows.Select(r => new {
                            r.Category,
                            r.Brand,
                            r.Model,
                            r.Size,
                            r.SerialNumber,
                            r.ManufactureDate,
                            Value = r.WarrantyLink // 显示链接文本
                        });
                        worksheet.Cell(2, 1).InsertData(rowsToInsert);
                        _logger.LogDebug("已将数据插入工作表。");
                    }


                    // 设置超链接
                    _logger.LogDebug("正在设置保修链接...");
                    int linkCount = 0;
                    foreach (var rowData in dataRows)
                    {
                        var link = rowData.WarrantyLink;
                        if (!string.IsNullOrEmpty(link) && Uri.IsWellFormedUriString(link, UriKind.Absolute))
                        {
                            try
                            {
                                // ClosedXML 中，需要获取单元格并设置其 Hyperlink 属性
                                var cell = worksheet.Cell(rowData.RowIndex, 7); // 第 7 列是保修链接列
                                cell.SetHyperlink(new XLHyperlink(link));
                                linkCount++;
                            }
                            catch (UriFormatException uriEx) // 处理无效 URI 格式
                            {
                                _logger.LogWarning(uriEx, "设置超链接时遇到无效的 URI 格式: {Link} (行: {RowIndex})", link, rowData.RowIndex);
                            }
                            catch (Exception linkEx)
                            {
                                _logger.LogError(linkEx, "设置超链接时发生错误: {Link} (行: {RowIndex})", link, rowData.RowIndex);
                            }
                        }
                    }
                    _logger.LogInformation("已设置 {LinkCount} 个保修超链接。", linkCount);


                    // 调整列宽
                    worksheet.Columns().AdjustToContents();
                    _logger.LogDebug("已调整列宽。");

                    // 保存文件
                    workbook.SaveAs(filePath);
                    _logger.LogInformation("Excel 文件已成功保存: {FilePath}", filePath);
                }
            }
            catch (IOException ioEx)
            {
                _logger.LogError(ioEx, "写入 Excel 文件时发生 IO 错误: {FilePath}", filePath);
                throw new IOException($"写入 Excel 文件 '{filePath}' 时出错: {ioEx.Message}", ioEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "导出 Excel 文件时发生意外错误: {FilePath}", filePath);
                throw new Exception($"导出 Excel 文件 '{filePath}' 时发生意外错误: {ex.Message}", ex);
            }
        });
    }

    // 清理无效 XML 字符的辅助方法
    private string Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }
        // 移除 XML 1.0 不允许的字符 (除了合法的 #x9, #xA, #xD)
        // 参考: https://www.w3.org/TR/xml/#charsets
        return new string(value.Where(c =>
            c == 0x9 || c == 0xA || c == 0xD ||
            (c >= 0x20 && c <= 0xD7FF) ||
            (c >= 0xE000 && c <= 0xFFFD) ||
            (c >= 0x10000 && c <= 0x10FFFF)
        ).ToArray());

        // 更简单的（但可能移除过多字符）的方法：移除所有控制字符
        // return new string(value.Where(c => !char.IsControl(c)).ToArray());
    }
}