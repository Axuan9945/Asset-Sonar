// File: axuan9945/itassettool/ItAssetTool-187fc96af793309702a81bc2a64f54675adec7e6/ItAssetTool/ViewModels/SelectablePluginViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using ItAssetTool.Core;

namespace ItAssetTool.ViewModels;

// 这个类是连接扫描插件和UI开关的桥梁
public partial class SelectablePluginViewModel : ObservableObject
{
    public IScanPlugin Plugin { get; }

    [ObservableProperty]
    private bool isEnabled = true; // 默认所有插件都是开启的

    public string Name => Plugin.Name;

    public SelectablePluginViewModel(IScanPlugin plugin)
    {
        Plugin = plugin;
    }
}