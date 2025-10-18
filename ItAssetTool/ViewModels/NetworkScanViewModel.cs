using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ItAssetTool.Core;
using ItAssetTool.Logic;
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
    private readonly IUxThreadDispatcher _dispatcher; // 新增注入的调度器依赖
    private CancellationTokenSource? _cancellationTokenSource;

    [ObservableProperty]
    private bool isScanning;

    public ObservableCollection<NetworkDevice> ScannedDevices { get; } = new();

    // vvvv 【优化：使用构造函数注入依赖】 vvvv
    public NetworkScanViewModel(PluginManager pluginManager, IUxThreadDispatcher dispatcher)
    {
        _dispatcher = dispatcher;

        pluginManager.DiscoverPlugins();
        _scanner = pluginManager.NetworkScanPlugins.FirstOrDefault();
    }
    // ^^^^ 优化结束 ^^^^

    [RelayCommand]
    private async Task ScanNetworkAsync()
    {
        if (_scanner == null) return;

        IsScanning = true;
        ScannedDevices.Clear();
        _cancellationTokenSource = new CancellationTokenSource();

        var progress = new Progress<NetworkDevice>(device =>
        {
            _dispatcher.Enqueue(() => // 使用注入的调度器
            {
                // vvvv 这是核心修改：调用新的方法来按顺序插入 vvvv
                InsertSorted(device);
                // ^^^^ 修改结束 ^^^^
            });
        });

        try
        {
            await _scanner.ScanAsync(progress, _cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            // 用户取消了扫描
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private void CancelScan()
    {
        _cancellationTokenSource?.Cancel();
        IsScanning = false;
    }

    // vvvv 这是一个新的辅助方法，用于将设备按IP顺序插入到列表中 vvvv
    private void InsertSorted(NetworkDevice newDevice)
    {
        if (newDevice.IpAddress == null || !IPAddress.TryParse(newDevice.IpAddress, out var newIp))
        {
            ScannedDevices.Add(newDevice); // 如果IP无效，直接添加到末尾
            return;
        }

        var newIpBytes = newIp.GetAddressBytes();

        for (int i = 0; i < ScannedDevices.Count; i++)
        {
            if (ScannedDevices[i].IpAddress == null || !IPAddress.TryParse(ScannedDevices[i].IpAddress, out var existingIp))
            {
                continue; // 跳过列表中无效的IP
            }

            var existingIpBytes = existingIp.GetAddressBytes();

            // 比较IP地址的每个字节
            for (int j = 0; j < 4; j++)
            {
                if (newIpBytes[j] < existingIpBytes[j])
                {
                    ScannedDevices.Insert(i, newDevice);
                    return;
                }
                if (newIpBytes[j] > existingIpBytes[j])
                {
                    break; // 当前 existingIp 更小，继续比较下一个
                }
            }
        }

        // 如果循环结束都没找到更大的IP，说明新设备是最大的，添加到末尾
        ScannedDevices.Add(newDevice);
    }
    // ^^^^ 方法结束 ^^^^
}