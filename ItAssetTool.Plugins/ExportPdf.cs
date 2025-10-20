using ItAssetTool.Core;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using QuestPDF.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace ItAssetTool.Plugins;

public class ExportPdf : IExportPlugin
{
    private readonly ILogger<ExportPdf> _logger;

    public string Name => "导出为 PDF";
    public string FileExtension => ".pdf";
    public string FileFilter => "PDF 文件 (*.pdf)";

    public ExportPdf(ILogger<ExportPdf> logger)
    {
        _logger = logger;
    }

    public Task ExportAsync(List<HardwareInfo> data, string filePath)
    {
        _logger.LogInformation("开始导出数据到 PDF 文件: {FilePath}", filePath);
        if (data == null || !data.Any())
        {
            _logger.LogWarning("没有数据可导出到 PDF。");
            return Task.CompletedTask;
        }

        return Task.Run(() =>
        {
            try
            {
                QuestPDF.Settings.License = LicenseType.Community;
                _logger.LogDebug("QuestPDF 许可证类型设置为 Community。");

                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Margin(30);

                        // --- vvvv 核心修改：使用字符串指定字体名称 vvvv ---
                        // 尝试 "Microsoft YaHei UI"，如果生成时报错找不到字体，可以换回 "Microsoft YaHei"
                        page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Microsoft YaHei UI"));
                        // page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Microsoft YaHei")); // 备选
                        // --- ^^^^ 修改结束 ^^^^
                        _logger.LogDebug("PDF 默认字体设置为 Microsoft YaHei UI。");


                        page.Header()
                            .PaddingBottom(10)
                            .Text("IT 资产电脑硬件信息报告")
                            .SemiBold().FontSize(16).FontColor(Colors.Blue.Medium);

                        page.Content()
                            .Column(column =>
                            {
                                int itemCount = 0;
                                foreach (var item in data)
                                {
                                    column.Item().PaddingTop(15).Table(table =>
                                    {
                                        table.ColumnsDefinition(columns =>
                                        {
                                            columns.ConstantColumn(100);
                                            columns.RelativeColumn();
                                        });

                                        table.Cell().Row(1).ColumnSpan(2).Background(Colors.Grey.Lighten3)
                                            .Padding(5).Text(item.Category ?? "未知类别").Bold();

                                        AddTableRow(table, 2, "品牌", item.Brand);
                                        AddTableRow(table, 3, "型号", item.Model);
                                        AddTableRow(table, 4, "大小", item.Size);
                                        AddTableRow(table, 5, "序列号", item.SerialNumber);
                                        AddTableRow(table, 6, "生产日期", item.ManufactureDate);
                                        AddTableRow(table, 7, "保修链接", item.WarrantyLink, true);
                                    });
                                    itemCount++;
                                }
                                _logger.LogInformation("已向 PDF 添加 {ItemCount} 个硬件项目。", itemCount);
                            });

                        page.Footer()
                            .AlignCenter()
                            .Text(x =>
                            {
                                x.Span("Page ");
                                x.CurrentPageNumber();
                                x.Span(" of ");
                                x.TotalPages();
                            });
                    });
                })
                .GeneratePdf(filePath);

                _logger.LogInformation("PDF 文件已成功保存: {FilePath}", filePath);
            }
            catch (IOException ioEx)
            {
                _logger.LogError(ioEx, "写入 PDF 文件时发生 IO 错误: {FilePath}", filePath);
                throw new IOException($"写入 PDF 文件 '{filePath}' 时出错: {ioEx.Message}", ioEx);
            }
            catch (Exception ex) // 捕获 QuestPDF 可能抛出的其他错误，例如字体找不到
            {
                _logger.LogError(ex, "导出 PDF 文件时发生意外错误: {FilePath}", filePath);
                // 检查字体相关的错误
                if (ex.Message.Contains("font") || ex.InnerException?.Message.Contains("font") == true)
                {
                    _logger.LogError("错误可能与字体 'Microsoft YaHei UI' 或 'Microsoft YaHei' 未找到有关。请确保系统安装了该字体。");
                }
                throw new Exception($"导出 PDF 文件 '{filePath}' 时发生意外错误: {ex.Message}", ex);
            }
        });
    }

    // 表行添加辅助方法
    private void AddTableRow(TableDescriptor table, uint row, string key, string? value, bool isLink = false)
    {
        table.Cell().Row(row).Column(1).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(key).SemiBold();
        var valueCell = table.Cell().Row(row).Column(2).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(5);
        if (isLink && !string.IsNullOrEmpty(value) && value.StartsWith("http") && Uri.IsWellFormedUriString(value, UriKind.Absolute))
        {
            valueCell.Hyperlink(value).Text(value).FontColor(Colors.Blue.Medium).Underline();
        }
        else
        {
            valueCell.Text(value ?? "N/A");
        }
    }
}