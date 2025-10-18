namespace ItAssetTool.Core;

public interface IScanPlugin
{
    string Name { get; }

    // 定义一个异步的 Scan 方法，它返回一个硬件信息列表
    Task<List<HardwareInfo>> ScanAsync();
}