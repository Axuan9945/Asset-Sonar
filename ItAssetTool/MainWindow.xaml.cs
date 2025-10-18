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
using System.IO; // <-- ȷ�������� System.IO
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

            // vvvv �����޸ģ����ô���ͼ�� vvvv
            // ���ͼ���ļ�������·��
            var iconPath = Path.Combine(AppContext.BaseDirectory, @"Assets\appicon.ico");
            // ����ļ��Ƿ����
            if (File.Exists(iconPath))
            {
                // ����ͼ��
                appWindow.SetIcon(iconPath);
            }
            // ^^^^ �޸Ľ��� ^^^^
        }
        catch
        {
            // �ڲ�֧�� AppWindow �Ļ���������ĳЩ��������к��Դ���
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
        this.Title = "Asset Sonar (�ʲ�����)";
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