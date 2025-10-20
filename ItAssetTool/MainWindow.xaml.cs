// File: ItAssetTool/MainWindow.xaml.cs
using ItAssetTool.ViewModels; // Assuming ViewModels namespace exists
using ItAssetTool.Views;     // Assuming Views namespace exists
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.IO;
using Windows.Graphics;
using WinRT.Interop;
using Serilog; // Use Serilog for logging

namespace ItAssetTool;

public sealed partial class MainWindow : Window
{
    // Use static logger for simplicity in this class, or inject ILogger if preferred
    private static readonly ILogger _logger = Log.ForContext<MainWindow>();

    public MainWindow()
    {
        _logger.Information("MainWindow initializing...");
        this.InitializeComponent();

        AppWindow? appWindow = null;
        try
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            appWindow = AppWindow.GetFromWindowId(windowId);
        }
        catch (Exception ex) { _logger.Error(ex, "��ȡ AppWindow ʱ����"); }

        if (appWindow != null)
        {
            appWindow.Resize(new SizeInt32 { Width = 1300, Height = 840 });
            var iconPath = Path.Combine(AppContext.BaseDirectory, @"Assets\appicon.ico");
            if (File.Exists(iconPath)) { try { appWindow.SetIcon(iconPath); } catch (Exception ex) { _logger.Error(ex, "����ͼ��ʧ��: {IconPath}", iconPath); } }
            else { _logger.Warning("�Ҳ���ͼ���ļ�: {IconPath}", iconPath); }

            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                var customTitleBar = appWindow.TitleBar;
                if (customTitleBar != null)
                {
                    customTitleBar.ExtendsContentIntoTitleBar = true;
                    customTitleBar.ButtonBackgroundColor = Colors.Transparent;
                    customTitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                    if (AppTitleBar != null) { SetTitleBar(AppTitleBar); }
                    else { _logger.Warning("XAML Ԫ�� AppTitleBar δ�ҵ���"); }
                }
                else { _logger.Warning("�޷���ȡ AppWindow.TitleBar��"); }
            }
            else { _logger.Information("��ǰϵͳ��֧���Զ����������"); }
        }
        else { _logger.Warning("�޷���ȡ AppWindow ʵ��..."); }

        if (MicaController.IsSupported()) { try { this.SystemBackdrop = new MicaBackdrop(); } catch (Exception ex) { _logger.Error(ex, "���� Mica ����ʱ����"); } }

        NavView.IsPaneOpen = false;
        this.Title = "Asset Sonar (�ʲ�����)";
        this.Closed += MainWindow_Closed;

        // --- ��ʼ������ ---
        if (ContentFrame != null)
        {
            // ���ó�ʼҳ��
            _logger.Debug("��������ʼҳ�� HomePage...");
            ContentFrame.Navigate(typeof(HomePage));
        }
        else
        {
            _logger.Error("XAML Ԫ�� ContentFrame Ϊ null���޷����г�ʼ������");
        }
        if (NavView?.MenuItems?.Count > 0)
        {
            NavView.SelectedItem = NavView.MenuItems[0]; // Ĭ��ѡ�е�һ��
            _logger.Debug("Ĭ��ѡ�� NavigationView ��һ�");
        }
        else
        {
            _logger.Warning("NavView ���� MenuItems Ϊ�գ��޷�����Ĭ��ѡ���");
        }
        _logger.Information("MainWindow ��ʼ����ɡ�");
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        _logger.Information("MainWindow_Closed �¼�����");
        try
        {
            var settingsViewModel = App.Services?.GetRequiredService<SettingsViewModel>();
            settingsViewModel?.Save();
            _logger.Information("�����ѱ��档");
        }
        catch (Exception ex) { _logger.Error(ex, "�رմ���ʱ��������ʧ�ܡ�"); }
        // Log.CloseAndFlush(); // �� App.xaml.cs ����
    }

    // --- NavigationView �¼����� ---
    private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        _logger.Debug("NavView_ItemInvoked triggered."); // ȷ���¼�����

        // ����Ƿ����������ð�ť����� IsSettingsVisible="True"��
        if (args.IsSettingsInvoked)
        {
            _logger.Debug("Settings item invoked.");
            // NavigateToPage("SettingsPage"); // ����������������ҳ����߼�
        }
        // ��������Ƿ��ǲ˵���
        else if (args.InvokedItemContainer != null)
        {
            var tag = args.InvokedItemContainer.Tag?.ToString(); // ��ȡ Tag
            _logger.Debug("InvokedItemContainer Tag: {Tag}", tag ?? "null"); // ��¼ Tag

            if (!string.IsNullOrEmpty(tag))
            {
                NavigateToPage(tag); // ���õ�������
            }
            else
            {
                _logger.Warning("InvokedItemContainer Tag is null or empty.");
            }
        }
        // ���� args.InvokedItem����� NavigationViewItem û������ Tag�����Գ��Դ� InvokedItem ��ȡ����
        else if (args.InvokedItem is string invokedItemString)
        {
            _logger.Warning("InvokedItemContainer is null, trying InvokedItem string: {InvokedItem}", invokedItemString);
            // ���Ը��� invokedItemString (���� "����ɨ��") ���жϵ���Ŀ�꣬���ⲻ�� Tag �ɿ�
            // if (invokedItemString == "����ɨ��") NavigateToPage("NetworkScanPage");
        }
        else
        {
            _logger.Warning("InvokedItemContainer is null and InvokedItem is not a string.");
        }
    }

    // --- ҳ�浼���߼� ---
    private void NavigateToPage(string pageTag)
    {
        _logger.Debug("NavigateToPage called with Tag: {PageTag}", pageTag);

        Type? pageType = pageTag switch // ʹ�� switch ���ʽƥ�� Tag
        {
            "HomePage" => typeof(HomePage),
            "NetworkScanPage" => typeof(NetworkScanPage),
            "DiagnosticPage" => typeof(DiagnosticPage),
            "SettingsPage" => typeof(SettingsPage),
            "AboutPage" => typeof(AboutPage),
            _ => null // ��� Tag ��ƥ���κ���֪ҳ�棬���� null
        };

        if (pageType != null)
        {
            _logger.Debug("Resolved PageType: {PageTypeName}", pageType.Name);

            // ��� ContentFrame �Ƿ���Ч
            if (ContentFrame == null)
            {
                _logger.Error("ContentFrame is null, cannot navigate.");
                return;
            }

            // ����Ƿ���Ҫ���������⵼������ǰ���ڵ�ҳ�棩
            if (ContentFrame.CurrentSourcePageType != pageType)
            {
                _logger.Information("Navigating ContentFrame to {PageTypeName}...", pageType.Name);
                try
                {
                    ContentFrame.Navigate(pageType); // ִ�е���
                }
                catch (Exception navEx) // ���񵼺������п��ܷ������쳣
                {
                    // ���磬���ҳ��Ĺ��캯���׳��쳣���������ﲶ��
                    _logger.Error(navEx, "ContentFrame.Navigate failed for {PageTypeName}", pageType.Name);
                    // ������ UI ����ʾ������ʾ
                }
            }
            else
            {
                _logger.Debug("Navigation skipped: ContentFrame is already on {PageTypeName}.", pageType.Name);
            }
        }
        else
        {
            _logger.Warning("No matching PageType found for Tag: {PageTag}", pageTag);
        }
    }
}