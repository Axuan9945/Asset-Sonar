using ItAssetTool.Core;
using System.Linq;
using System.Management; // <--- 就是添加这一行
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace ItAssetTool.Plugins;

[SupportedOSPlatform("windows")]
public class ScanActivation : IScanPlugin
{
    public string Name => "系统激活状态";

    public Task<List<HardwareInfo>> ScanAsync()
    {
        return Task.Run(() =>
        {
            var data = new List<HardwareInfo>();
            string status = "未激活或无法确定";
            try
            {
                // WMI 查询，查找所有软件授权产品
                var query = new SelectQuery("SELECT * FROM SoftwareLicensingProduct");
                using var searcher = new ManagementObjectSearcher(query);

                foreach (var obj in searcher.Get().OfType<ManagementObject>())
                {
                    var description = obj["Description"]?.ToString()?.ToLower() ?? "";
                    // 寻找包含 "windows" 描述且有部分产品密钥的授权信息
                    if (description.Contains("windows") && obj["PartialProductKey"] != null)
                    {
                        // LicenseStatus = 1 表示已授权 (已激活)
                        if (obj["LicenseStatus"] != null && (uint)obj["LicenseStatus"] == 1)
                        {
                            status = "已激活";
                            break; // 找到一个已激活的 Windows 就可以停止了
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"扫描激活状态失败: {ex.Message}");
                status = $"查询失败: {ex.Message}";
            }

            data.Add(new HardwareInfo
            {
                Category = "系统激活状态",
                Model = status,
                Brand = "N/A",
                SerialNumber = "N/A",
                Size = "N/A",
                ManufactureDate = "N/A",
                WarrantyLink = "N/A"
            });

            return data;
        });
    }
}