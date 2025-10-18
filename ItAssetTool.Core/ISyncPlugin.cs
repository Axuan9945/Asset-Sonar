using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ItAssetTool.Core;

public interface ISyncPlugin
{
    string Name { get; }
    Task SyncAsync(List<HardwareInfo> data, SyncConfig config, Action<string> logCallback);
}