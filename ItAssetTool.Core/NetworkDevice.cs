// In Project: ItAssetTool.Core
// File: NetworkDevice.cs
using CommunityToolkit.Mvvm.ComponentModel;

namespace ItAssetTool.Core;

// 这是一个表示网络设备的 “类”，用于存储扫描到的设备信息。
// 它继承自 ObservableObject，以便在信息更新时能自动通知UI。
public partial class NetworkDevice : ObservableObject
{
    [ObservableProperty]
    private string? ipAddress;

    [ObservableProperty]
    private string? hostName;

    [ObservableProperty]
    private string? macAddress;

    [ObservableProperty]
    private string? status;

    // vvvv 核心修改 vvvv
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LatencyText))] // 当 Latency 改变时，通知UI LatencyText 也发生了变化
    private long? latency;

    // 添加一个只读属性，用于在UI上友好地显示延迟信息
    public string LatencyText => Latency.HasValue ? $"{Latency.Value} ms" : "";
    // ^^^^ 修改结束 ^^^^
}