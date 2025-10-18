using ItAssetTool.Core;
using Microsoft.UI.Xaml;
using System;
using ItAssetTool.Logic;
using ItAssetTool.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ItAssetTool;

public class WinUIThreadDispatcher : IUxThreadDispatcher
{
    public void Enqueue(Action action)
    {
        App.MainWindow?.DispatcherQueue.TryEnqueue(() => action());
    }
}

public partial class App : Application
{
    public static WinUIThreadDispatcher Dispatcher { get; } = new();
    public static MainWindow? MainWindow { get; private set; }
    public static IServiceProvider Services { get; private set; } = null!;

    public App()
    {
        this.InitializeComponent();
        Services = ConfigureServices();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        MainWindow = new MainWindow();
        MainWindow.Activate();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // 注册核心服务和逻辑层
        services.AddSingleton<PluginManager>();
        services.AddSingleton<IUxThreadDispatcher>(sp => Dispatcher);

        // 注册所有 ViewModels (MainViewModel 和 SettingsViewModel 作为 Singleton 以保持状态)
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainViewModel>();

        // 其他 ViewModels 作为 Transient (每次请求都创建新实例)
        services.AddTransient<DiagnosticViewModel>();
        services.AddTransient<NetworkScanViewModel>();
        services.AddTransient<AboutViewModel>(); // <-- 已添加此行

        return services.BuildServiceProvider();
    }
}