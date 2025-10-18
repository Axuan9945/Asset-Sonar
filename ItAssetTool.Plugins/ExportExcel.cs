using ItAssetTool.Core;
using System.Collections.Generic;
using System.Threading.Tasks;
using ClosedXML.Excel;
using System.Linq;

namespace ItAssetTool.Plugins;

public class ExportExcel : IExportPlugin
{
    public string Name => "导出为 Excel";
    public string FileExtension => ".xlsx";
    public string FileFilter => "Excel 工作簿 (*.xlsx)";

    public Task ExportAsync(List<HardwareInfo> data, string filePath)
    {
        return Task.Run(() =>
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("电脑配置信息");

                var headers = new string[] { "类别", "品牌", "型号", "大小", "序列号", "生产日期", "保修查询链接" };
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(1, i + 1).Value = headers[i];
                }
                worksheet.Row(1).Style.Font.Bold = true;

                var dataRows = data.Select(item => new
                {
                    Category = Sanitize(item.Category),
                    Brand = Sanitize(item.Brand),
                    Model = Sanitize(item.Model),
                    Size = Sanitize(item.Size),
                    SerialNumber = Sanitize(item.SerialNumber),
                    ManufactureDate = Sanitize(item.ManufactureDate),
                    WarrantyLink = Sanitize(item.WarrantyLink)
                });

                worksheet.Cell(2, 1).InsertData(dataRows);

                for (int i = 0; i < data.Count; i++)
                {
                    var link = data[i].WarrantyLink;
                    if (!string.IsNullOrEmpty(link) && Uri.IsWellFormedUriString(link, UriKind.Absolute))
                    {
                        // vvvv 这是核心修正：将 "=" 赋值操作改为调用 "SetHyperlink" 方法 vvvv
                        worksheet.Cell(i + 2, 7).SetHyperlink(new XLHyperlink(link));
                    }
                }

                worksheet.Columns().AdjustToContents();

                workbook.SaveAs(filePath);
            }
        });
    }

    private string Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }
        return new string(value.Where(c => !char.IsControl(c)).ToArray());
    }
}