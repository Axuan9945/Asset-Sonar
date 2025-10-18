// In Project: ItAssetTool.Core
// File: INetworkScanPlugin.cs
namespace ItAssetTool.Core;

// 这是一个 “接口”，它定义了一个网络扫描插件必须遵守的规范。
public interface INetworkScanPlugin
{
    // 插件的显示名称
    string Name { get; }

    // 一个异步的扫描方法。
    // IProgress<T> 用于在扫描过程中，每发现一个设备就实时向界面报告一次。
    // CancellationToken 用于让用户可以中途取消扫描。
    Task ScanAsync(IProgress<NetworkDevice> progress, CancellationToken cancellationToken);
}