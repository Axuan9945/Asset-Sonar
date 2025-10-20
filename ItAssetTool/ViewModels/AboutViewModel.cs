using CommunityToolkit.Mvvm.ComponentModel;
using System.Reflection;

namespace ItAssetTool.ViewModels;

public partial class AboutViewModel : ObservableObject
{
    [ObservableProperty]
    private string appVersion = GetAppVersion(); // 保持不变，调用下面的方法

    private static string GetAppVersion()
    {
        // --- vvvv 核心修改：直接返回硬编码的版本号 vvvv ---
        return "1.0.1";
        // --- ^^^^ 修改结束 ^^^^

        /*
        // 原来的尝试获取程序集版本的代码 (现在被注释掉了)
        try
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version != null) return $"{version.Major}.{version.Minor}.{version.Build}";
            return "1.0.1"; // 原来的备选值
        }
        catch
        {
            return "1.0.1"; // 原来的备选值
        }
        */
    }
}
