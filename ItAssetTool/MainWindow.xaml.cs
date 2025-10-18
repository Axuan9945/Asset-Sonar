using ItAssetTool.ViewModels;
using ItAssetTool.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.IO; // <-- 确保引用了 System.IO
using Windows.Graphics;
using WinRT.Interop;

namespace ItAssetTool;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        this.InitializeComponent();

        try
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            appWindow?.Resize(new SizeInt32 { Width = 1300, Height = 840 });

            // vvvv 核心修改：设置窗口图标 vvvv
            // 组合图标文件的完整路径
            var iconPath = Path.Combine(AppContext.BaseDirectory, @"Assets\appicon.ico");
            // 检查文件是否存在
            if (File.Exists(iconPath))
            {
                // 设置图标
                appWindow.SetIcon(iconPath);
            }
            // ^^^^ 修改结束 ^^^^
        }
        catch
        {
            // 在不支持 AppWindow 的环境（例如某些虚拟机）中忽略错误
        }

        var customTitleBar = AppWindow.TitleBar;
        if (customTitleBar != null)
        {
            customTitleBar.ExtendsContentIntoTitleBar = true;
            customTitleBar.ButtonBackgroundColor = Colors.Transparent;
            customTitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        }

        SetTitleBar(AppTitleBar);

        if (MicaController.IsSupported())
        {
            this.SystemBackdrop = new MicaBackdrop();
        }

        NavView.IsPaneOpen = false;
        this.Title = "Asset Sonar (资产声呐)";
        this.Closed += MainWindow_Closed;

        ContentFrame.Navigate(typeof(HomePage));
        NavView.SelectedItem = NavView.MenuItems[0];
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        var settingsViewModel = App.Services.GetRequiredService<SettingsViewModel>();
        settingsViewModel.Save();
    }

    private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer != null)
        {
            var tag = args.InvokedItemContainer.Tag.ToString();
            NavigateToPage(tag);
        }
    }

    private void NavigateToPage(string? pageTag)
    {
        Type? pageType = null;
        if (pageTag == "HomePage") pageType = typeof(HomePage);
        else if (pageTag == "NetworkScanPage") pageType = typeof(NetworkScanPage);
        else if (pageTag == "DiagnosticPage") pageType = typeof(DiagnosticPage);
        else if (pageTag == "SettingsPage") pageType = typeof(SettingsPage);
        else if (pageTag == "AboutPage") pageType = typeof(AboutPage);

        if (pageType != null && ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType);
        }
    }
}