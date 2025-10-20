using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ItAssetTool.Core;
using ItAssetTool.Logic;
using Microsoft.Extensions.Logging; // <-- 添加 using
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace ItAssetTool.ViewModels;

public partial class NetworkScanViewModel : ObservableObject
{
    private readonly INetworkScanPlugin? _scanner;
    private readonly IUxThreadDispatcher _dispatcher;
    private readonly ILogger<NetworkScanViewModel> _logger; // <-- 添加 logger
    private CancellationTokenSource? _cancellationTokenSource;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanNetworkCommand))] // 当 IsScanning 改变时，通知命令 CanExecute 可能已改变
    private bool isScanning;

    public ObservableCollection<NetworkDevice> ScannedDevices { get; } = new();

    // 构造函数注入 ILogger
    public NetworkScanViewModel(PluginManager pluginManager, IUxThreadDispatcher dispatcher, ILogger<NetworkScanViewModel> logger)
    {
        _dispatcher = dispatcher;
        _logger = logger;
        _logger.LogInformation("NetworkScanViewModel 初始化...");

        try
        {
            // PluginManager 应该在 App 启动时已经 DiscoverPlugins
            _scanner = pluginManager?.NetworkScanPlugins?.FirstOrDefault(); // 安全访问

            if (_scanner == null)
            {
                _logger.LogError("未能从 PluginManager 获取 INetworkScanPlugin 实例！网络扫描功能将不可用。");
                // 可以在 UI 上显示错误消息，或者禁用扫描按钮
            }
            else
            {
                _logger.LogInformation("已成功获取网络扫描插件实例: {PluginName}", _scanner.Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "NetworkScanViewModel 初始化过程中发生严重错误。");
            // 考虑抛出异常或进行其他错误处理
        }
    }

    // CanExecute 用于控制按钮是否可用
    private bool CanScan() => !IsScanning && _scanner != null;

    [RelayCommand(CanExecute = nameof(CanScan))] // 绑定 CanExecute 条件
    private async Task ScanNetworkAsync()
    {
        _logger.LogInformation("ScanNetworkAsync 命令执行开始..."); // 添加日志确认命令被触发
        if (_scanner == null)
        {
            _logger.LogError("无法开始扫描，因为扫描插件实例 (_scanner) 为 null。");
            // 可以在 UI 上显示错误
            return;
        }

        IsScanning = true; // Setter 会自动触发 CanExecuteChanged
        ScannedDevices.Clear();
        _cancellationTokenSource = new CancellationTokenSource();
        _logger.LogDebug("已清除旧设备列表并创建了新的 CancellationTokenSource。");


        // 使用 IProgress<T> 在 UI 线程上更新列表
        var progress = new Progress<NetworkDevice>(device =>
        {
            // 在 UI 线程上执行插入操作
            _dispatcher.Enqueue(() =>
            {
                // 检查是否取消，避免在取消后还添加设备
                if (_cancellationTokenSource?.IsCancellationRequested == false)
                {
                    InsertSorted(device);
                }
            });
        });

        try
        {
            _logger.LogInformation("调用扫描插件 ScanAsync 方法...");
            await _scanner.ScanAsync(progress, _cancellationTokenSource.Token);
            _logger.LogInformation("扫描插件 ScanAsync 方法执行完成。");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("网络扫描被用户取消。");
            // UI Log 可以由 CancelScan 命令处理
        }
        catch (Exception ex) // 捕获插件执行过程中的其他异常
        {
            _logger.LogError(ex, "执行网络扫描时发生错误。");
            // 可以在 UI 上显示错误信息
            _dispatcher.Enqueue(() => ScannedDevices.Add(new NetworkDevice { Status = "错误", HostName = $"扫描失败: {ex.Message}" }));
        }
        finally
        {
            _logger.LogDebug("ScanNetworkAsync finally 块执行...");
            IsScanning = false; // Setter 会自动触发 CanExecuteChanged
            _cancellationTokenSource?.Dispose(); // 释放 CancellationTokenSource
            _cancellationTokenSource = null;
            _logger.LogInformation("扫描任务结束。");
        }
    }

    [RelayCommand]
    private void CancelScan()
    {
        if (IsScanning && _cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
        {
            _logger.LogInformation("用户请求取消网络扫描...");
            _cancellationTokenSource.Cancel();
            // IsScanning 将在 ScanNetworkAsync 的 finally 块中设置为 false
            // 可以在 UI Log 中添加一条消息
            // Log("扫描已取消。", LogLevel.Warning); // 假设ViewModel有Log方法
        }
        else
        {
            _logger.LogDebug("CancelScan 命令被调用，但扫描未在进行或已被取消。");
        }
    }

    // InsertSorted 方法 (保持不变)
    private void InsertSorted(NetworkDevice newDevice)
    {
        if (newDevice?.IpAddress == null || !IPAddress.TryParse(newDevice.IpAddress, out var newIp))
        {
            // 处理无效或 null 设备
            if (newDevice != null) ScannedDevices.Add(newDevice);
            return;
        }

        byte[]? newIpBytes = null;
        try { newIpBytes = newIp.GetAddressBytes(); } catch { /* IP 地址无效 */ return; }
        if (newIpBytes == null || newIpBytes.Length != 4) return; // 只处理 IPv4


        for (int i = 0; i < ScannedDevices.Count; i++)
        {
            var existingDevice = ScannedDevices[i];
            if (existingDevice?.IpAddress == null || !IPAddress.TryParse(existingDevice.IpAddress, out var existingIp)) continue;

            byte[]? existingIpBytes = null;
            try { existingIpBytes = existingIp.GetAddressBytes(); } catch { continue; }
            if (existingIpBytes == null || existingIpBytes.Length != 4) continue;


            // 比较IP地址的每个字节
            bool shouldInsert = false;
            for (int j = 0; j < 4; j++)
            {
                if (newIpBytes[j] < existingIpBytes[j])
                {
                    shouldInsert = true;
                    break;
                }
                if (newIpBytes[j] > existingIpBytes[j])
                {
                    break; // 当前 existingIp 更小，继续比较下一个
                }
                // 如果字节相等，继续比较下一个字节
            }

            if (shouldInsert)
            {
                ScannedDevices.Insert(i, newDevice);
                return;
            }
        }
        ScannedDevices.Add(newDevice); // 如果是最大的，添加到末尾
    }
}