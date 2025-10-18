// In Project: ItAssetTool
// Folder: Views
// File: NetworkScanPage.xaml.cs
using Microsoft.UI.Xaml.Controls;
using ItAssetTool.ViewModels;
using ItAssetTool.Logic;
using Microsoft.Extensions.DependencyInjection; // [新增引用]

namespace ItAssetTool.Views;

public sealed partial class NetworkScanPage : Page
{
    public NetworkScanPage()
    {
        // vvvv 核心修改：从 DI 容器中获取 ViewModel 实例 vvvv
        this.DataContext = App.Services.GetRequiredService<NetworkScanViewModel>();
        // ^^^^ 核心修改结束 ^^^^

        this.InitializeComponent();
    }
}