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
        catch (Exception ex) { _logger.Error(ex, "获取 AppWindow 时出错。"); }

        if (appWindow != null)
        {
            appWindow.Resize(new SizeInt32 { Width = 1300, Height = 840 });
            var iconPath = Path.Combine(AppContext.BaseDirectory, @"Assets\appicon.ico");
            if (File.Exists(iconPath)) { try { appWindow.SetIcon(iconPath); } catch (Exception ex) { _logger.Error(ex, "设置图标失败: {IconPath}", iconPath); } }
            else { _logger.Warning("找不到图标文件: {IconPath}", iconPath); }

            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                var customTitleBar = appWindow.TitleBar;
                if (customTitleBar != null)
                {
                    customTitleBar.ExtendsContentIntoTitleBar = true;
                    customTitleBar.ButtonBackgroundColor = Colors.Transparent;
                    customTitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                    if (AppTitleBar != null) { SetTitleBar(AppTitleBar); }
                    else { _logger.Warning("XAML 元素 AppTitleBar 未找到。"); }
                }
                else { _logger.Warning("无法获取 AppWindow.TitleBar。"); }
            }
            else { _logger.Information("当前系统不支持自定义标题栏。"); }
        }
        else { _logger.Warning("无法获取 AppWindow 实例..."); }

        if (MicaController.IsSupported()) { try { this.SystemBackdrop = new MicaBackdrop(); } catch (Exception ex) { _logger.Error(ex, "设置 Mica 背景时出错"); } }

        NavView.IsPaneOpen = false;
        this.Title = "Asset Sonar (资产声呐)";
        this.Closed += MainWindow_Closed;

        // --- 初始化导航 ---
        if (ContentFrame != null)
        {
            // 设置初始页面
            _logger.Debug("导航到初始页面 HomePage...");
            ContentFrame.Navigate(typeof(HomePage));
        }
        else
        {
            _logger.Error("XAML 元素 ContentFrame 为 null，无法进行初始导航。");
        }
        if (NavView?.MenuItems?.Count > 0)
        {
            NavView.SelectedItem = NavView.MenuItems[0]; // 默认选中第一项
            _logger.Debug("默认选中 NavigationView 第一项。");
        }
        else
        {
            _logger.Warning("NavView 或其 MenuItems 为空，无法设置默认选中项。");
        }
        _logger.Information("MainWindow 初始化完成。");
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        _logger.Information("MainWindow_Closed 事件触发");
        try
        {
            var settingsViewModel = App.Services?.GetRequiredService<SettingsViewModel>();
            settingsViewModel?.Save();
            _logger.Information("设置已保存。");
        }
        catch (Exception ex) { _logger.Error(ex, "关闭窗口时保存设置失败。"); }
        // Log.CloseAndFlush(); // 由 App.xaml.cs 处理
    }

    // --- NavigationView 事件处理 ---
    private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        _logger.Debug("NavView_ItemInvoked triggered."); // 确认事件触发

        // 检查是否点击的是设置按钮（如果 IsSettingsVisible="True"）
        if (args.IsSettingsInvoked)
        {
            _logger.Debug("Settings item invoked.");
            // NavigateToPage("SettingsPage"); // 或者其他处理设置页面的逻辑
        }
        // 检查点击的是否是菜单项
        else if (args.InvokedItemContainer != null)
        {
            var tag = args.InvokedItemContainer.Tag?.ToString(); // 获取 Tag
            _logger.Debug("InvokedItemContainer Tag: {Tag}", tag ?? "null"); // 记录 Tag

            if (!string.IsNullOrEmpty(tag))
            {
                NavigateToPage(tag); // 调用导航方法
            }
            else
            {
                _logger.Warning("InvokedItemContainer Tag is null or empty.");
            }
        }
        // 处理 args.InvokedItem，如果 NavigationViewItem 没有设置 Tag，可以尝试从 InvokedItem 获取内容
        else if (args.InvokedItem is string invokedItemString)
        {
            _logger.Warning("InvokedItemContainer is null, trying InvokedItem string: {InvokedItem}", invokedItemString);
            // 可以根据 invokedItemString (例如 "网络扫描") 来判断导航目标，但这不如 Tag 可靠
            // if (invokedItemString == "网络扫描") NavigateToPage("NetworkScanPage");
        }
        else
        {
            _logger.Warning("InvokedItemContainer is null and InvokedItem is not a string.");
        }
    }

    // --- 页面导航逻辑 ---
    private void NavigateToPage(string pageTag)
    {
        _logger.Debug("NavigateToPage called with Tag: {PageTag}", pageTag);

        Type? pageType = pageTag switch // 使用 switch 表达式匹配 Tag
        {
            "HomePage" => typeof(HomePage),
            "NetworkScanPage" => typeof(NetworkScanPage),
            "DiagnosticPage" => typeof(DiagnosticPage),
            "SettingsPage" => typeof(SettingsPage),
            "AboutPage" => typeof(AboutPage),
            _ => null // 如果 Tag 不匹配任何已知页面，返回 null
        };

        if (pageType != null)
        {
            _logger.Debug("Resolved PageType: {PageTypeName}", pageType.Name);

            // 检查 ContentFrame 是否有效
            if (ContentFrame == null)
            {
                _logger.Error("ContentFrame is null, cannot navigate.");
                return;
            }

            // 检查是否需要导航（避免导航到当前已在的页面）
            if (ContentFrame.CurrentSourcePageType != pageType)
            {
                _logger.Information("Navigating ContentFrame to {PageTypeName}...", pageType.Name);
                try
                {
                    ContentFrame.Navigate(pageType); // 执行导航
                }
                catch (Exception navEx) // 捕获导航过程中可能发生的异常
                {
                    // 例如，如果页面的构造函数抛出异常，会在这里捕获
                    _logger.Error(navEx, "ContentFrame.Navigate failed for {PageTypeName}", pageType.Name);
                    // 可以在 UI 上显示错误提示
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