// File: ItAssetTool/App.xaml.cs
using ItAssetTool.Core;
using Microsoft.UI.Xaml;
using System;
using ItAssetTool.Logic;
using ItAssetTool.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using System.IO;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ItAssetTool;

public class WinUIThreadDispatcher : IUxThreadDispatcher
{
    public void Enqueue(Action action)
    {
        if (App.MainWindow?.DispatcherQueue != null)
        {
            App.MainWindow.DispatcherQueue.TryEnqueue(() => action());
        }
        else
        {
            Log.Logger?.Warning("DispatcherQueue 不可用，无法在 UI 线程上执行操作。");
        }
    }
}

public partial class App : Application
{
    public static WinUIThreadDispatcher Dispatcher { get; } = new();
    public static MainWindow? MainWindow { get; private set; }
    public static IServiceProvider Services { get; private set; } = null!;

    public App()
    {
        // --- 配置 Serilog ---
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Debug()
            .WriteTo.File(
                Path.Combine(AppContext.BaseDirectory, "Logs", "AssetSonar-.txt"),
                rollingInterval: RollingInterval.Day,
                restrictedToMinimumLevel: LogEventLevel.Information,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        // 捕获未处理的后台线程异常
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            Log.Fatal((Exception)args.ExceptionObject, "未处理的应用程序域异常");
            Log.CloseAndFlush(); // 重要：尝试在崩溃前写入日志
        };

        this.InitializeComponent();

        // 捕获 UI 线程的未处理异常
        this.UnhandledException += (sender, args) =>
        {
            Log.Fatal(args.Exception, "未处理的 UI 线程异常");
            args.Handled = true; // 阻止应用立即崩溃
            Log.CloseAndFlush(); // 重要：尝试在关闭前写入日志

            // 可以在此处显示一个错误消息对话框
        };

        Services = ConfigureServices();
        Log.Information("应用程序初始化完成");
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        Log.Information("应用程序启动 OnLaunched");
        MainWindow = new MainWindow();
        MainWindow.Activate();

        // vvvv 核心修改：在 MainWindow 关闭时刷新日志 vvvv
        MainWindow.Closed += (sender, e) =>
        {
            Log.Information("主窗口关闭，正在刷新日志...");
            Log.CloseAndFlush();
        };
        // ^^^^ 修改结束 ^^^^
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // --- 将 Serilog 集成到 DI ---
        services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.ClearProviders();
            loggingBuilder.AddSerilog(dispose: true); // dispose: true 可以在 DI 容器销毁时自动 Flush
        });

        // --- 确保 IServiceProvider 可以被注入 ---
        services.TryAddSingleton<IServiceProvider>(sp => sp);

        // 注册核心服务和逻辑层
        services.AddSingleton<PluginManager>();
        services.AddSingleton<IUxThreadDispatcher>(sp => Dispatcher);

        // 注册所有 ViewModels
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddTransient<DiagnosticViewModel>();
        services.AddTransient<NetworkScanViewModel>();
        services.AddTransient<AboutViewModel>();

        return services.BuildServiceProvider();
    }

    // vvvv 核心修改：移除 OnSuspending 方法 vvvv
    // protected override void OnSuspending(object sender, Windows.ApplicationModel.SuspendingEventArgs e)
    // {
    //     ...
    // }
    // ^^^^ 修改结束 ^^^^
}