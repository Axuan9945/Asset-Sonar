// In Project: ItAssetTool
// Folder: Views
// File: NetworkScanPage.xaml.cs
using Microsoft.UI.Xaml.Controls;
using ItAssetTool.ViewModels;
using ItAssetTool.Logic;
using Microsoft.Extensions.DependencyInjection; // [��������]

namespace ItAssetTool.Views;

public sealed partial class NetworkScanPage : Page
{
    public NetworkScanPage()
    {
        // vvvv �����޸ģ��� DI �����л�ȡ ViewModel ʵ�� vvvv
        this.DataContext = App.Services.GetRequiredService<NetworkScanViewModel>();
        // ^^^^ �����޸Ľ��� ^^^^

        this.InitializeComponent();
    }
}