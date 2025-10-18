using CommunityToolkit.Mvvm.ComponentModel;
using System.Reflection;

namespace ItAssetTool.ViewModels;

public partial class AboutViewModel : ObservableObject
{
    [ObservableProperty]
    private string appVersion = GetAppVersion();

    private static string GetAppVersion()
    {
        try
        {
            // 从程序集中获取版本号
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";
        }
        catch
        {
            // 如果获取失败则返回一个默认值
            return "1.0.0";
        }
    }
}