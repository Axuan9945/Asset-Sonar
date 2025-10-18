using ItAssetTool.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using ItAssetTool.Logic;
using Microsoft.Extensions.DependencyInjection; // [新增引用]

namespace ItAssetTool.Views;

public sealed partial class HomePage : Page
{
    // 这个属性现在可以正确地从 this.DataContext 获取 ViewModel 实例了
    public MainViewModel ViewModel => (MainViewModel)this.DataContext;

    private int _signatureClicks = 0;
    private DispatcherTimer _clickTimer;

    public HomePage()
    {
        this.InitializeComponent();

        // vvvv 核心修改：从 DI 容器中获取 ViewModel 实例 vvvv
        this.DataContext = App.Services.GetRequiredService<MainViewModel>();
        // ^^^^ 核心修改结束 ^^^^

        _clickTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _clickTimer.Tick += (s, e) => {
            _signatureClicks = 0;
            _clickTimer.Stop();
        };
    }

    private void SignatureLabel_Tapped(object sender, TappedRoutedEventArgs e)
    {
        _signatureClicks++;
        _clickTimer.Stop();
        _clickTimer.Start();

        if (_signatureClicks >= 5)
        {
            _signatureClicks = 0;
            _clickTimer.Stop();
            TriggerCleanupAction();
        }
    }

    private async void TriggerCleanupAction()
    {
        var warningDialog = new ContentDialog
        {
            Title = "危险操作警告",
            Content = "您已触发隐藏的开发者功能：清空 Snipe-IT 服务器数据。\n此操作不可逆，仅用于测试环境。\n\n您确定要继续吗？",
            PrimaryButtonText = "确定继续",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        var result = await warningDialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        var inputDialog = new TextBox { AcceptsReturn = false, Height = 32 };
        var confirmDialog = new ContentDialog
        {
            Title = "最终确认",
            Content = inputDialog,
            PrimaryButtonText = "执行清理",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        var confirmResult = await confirmDialog.ShowAsync();
        if (confirmResult == ContentDialogResult.Primary && inputDialog.Text == "确认")
        {
            // 这一行现在可以安全执行了
            ViewModel.CleanupCommand.Execute(null);
        }
    }
}