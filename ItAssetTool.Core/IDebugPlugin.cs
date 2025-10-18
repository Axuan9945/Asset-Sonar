using System;
using System.Threading.Tasks;

namespace ItAssetTool.Core;

public interface IUxThreadDispatcher
{
    void Enqueue(Action action);
}

public interface IDebugPlugin
{
    string Name { get; }

    // 定义一个异步的清理方法
    Task RunCleanupAsync(SyncConfig config, Action<string> logCallback);
}