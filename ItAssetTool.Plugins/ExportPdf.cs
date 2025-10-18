using ItAssetTool.Core;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using QuestPDF.Helpers;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ItAssetTool.Plugins;

public class ExportPdf : IExportPlugin
{
    public string Name => "导出为 PDF";
    public string FileExtension => ".pdf";
    public string FileFilter => "PDF 文件 (*.pdf)";

    public Task ExportAsync(List<HardwareInfo> data, string filePath)
    {
        return Task.Run(() =>
        {
            QuestPDF.Settings.License = LicenseType.Community;

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);

                    // vvvv 这是核心修正 vvvv
                    // 将 Fonts.MicrosoftYaHei 替换为字体的字符串名称 "Microsoft YaHei"
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Microsoft YaHei"));
                    // ^^^^ 核心修正结束 ^^^^

                    page.Header()
                        .Text("IT 资产电脑硬件信息报告")
                        .SemiBold().FontSize(16).FontColor(Colors.Blue.Medium);

                    page.Content()
                        .Column(column =>
                        {
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
                            }
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Page ");
                            x.CurrentPageNumber();
                        });
                });
            })
            .GeneratePdf(filePath);
        });
    }

    private void AddTableRow(TableDescriptor table, uint row, string key, string? value, bool isLink = false)
    {
        table.Cell().Row(row).Column(1).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(key).SemiBold();
        var valueCell = table.Cell().Row(row).Column(2).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(5);

        if (isLink && !string.IsNullOrEmpty(value) && value.StartsWith("http"))
        {
            valueCell.Hyperlink(value).Text(value).FontColor(Colors.Blue.Medium).Underline();
        }
        else
        {
            valueCell.Text(value ?? "N/A");
        }
    }
}