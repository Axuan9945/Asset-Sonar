// File: ItAssetTool/ViewModels/SettingsViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ItAssetTool.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Encodings.Web; // <-- 添加这个 using
using System.Text.Unicode;       // <-- 添加这个 using
using Windows.Storage;

namespace ItAssetTool.ViewModels;

// 用于序列化整个配置文件的顶级对象
public class ProfilesData
{
    public string? ActiveProfile { get; set; }
    public Dictionary<string, SyncConfig> Profiles { get; set; } = new();
}

// 用于 (string, int) 类型的字典绑定
public partial class IdMapEntry : ObservableObject
{
    [ObservableProperty] private string name = "";
    [ObservableProperty] private int id;
}

// 用于 (string, string) 类型的字典绑定
public partial class CodeMapEntry : ObservableObject
{
    [ObservableProperty] private string name = "";
    [ObservableProperty] private string code = "";
}

public partial class SettingsViewModel : ObservableObject
{
    private const string ProfilesFileName = "profiles.json";

    [ObservableProperty]
    private ProfilesData _profilesData = new();

    public ObservableCollection<string> ProfileNames { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedProfile))]
    private string? _selectedProfileName;

    public SyncConfig? SelectedProfile =>
        SelectedProfileName != null && ProfilesData.Profiles.ContainsKey(SelectedProfileName)
            ? ProfilesData.Profiles[SelectedProfileName]
            : null;

    // 为UI绑定添加 ObservableCollections
    public ObservableCollection<IdMapEntry> CategoryIdMaps { get; } = new();
    public ObservableCollection<IdMapEntry> ComponentIdMaps { get; } = new();
    public ObservableCollection<IdMapEntry> AccessoryIdMaps { get; } = new();
    public ObservableCollection<CodeMapEntry> CategoryCodeMaps { get; } = new();


    public SettingsViewModel()
    {
        Load();
    }

    // 添加OnChanged处理，用于在切换Profile时更新UI
    partial void OnSelectedProfileNameChanged(string? value)
    {
        PopulateIdMaps(SelectedProfile);
        // 手动触发SelectedProfile的变更通知
        OnPropertyChanged(nameof(SelectedProfile));
    }

    // 添加辅助方法，在字典和ObservableCollection之间转换
    private void PopulateIdMaps(SyncConfig? config)
    {
        CategoryIdMaps.Clear();
        ComponentIdMaps.Clear();
        AccessoryIdMaps.Clear();
        CategoryCodeMaps.Clear();

        if (config == null) return;

        // 从字典加载到UI集合
        foreach (var kvp in config.CATEGORY_ID_MAP.OrderBy(k => k.Key))
            CategoryIdMaps.Add(new IdMapEntry { Name = kvp.Key, Id = kvp.Value });
        foreach (var kvp in config.COMPONENT_CATEGORY_ID_MAP.OrderBy(k => k.Key))
            ComponentIdMaps.Add(new IdMapEntry { Name = kvp.Key, Id = kvp.Value });
        foreach (var kvp in config.ACCESSORY_CATEGORY_ID_MAP.OrderBy(k => k.Key))
            AccessoryIdMaps.Add(new IdMapEntry { Name = kvp.Key, Id = kvp.Value });
        foreach (var kvp in config.CATEGORY_CODE_MAP.OrderBy(k => k.Key))
            CategoryCodeMaps.Add(new CodeMapEntry { Name = kvp.Key, Code = kvp.Value });
    }

    // 添加用于UI中 "添加/删除" 按钮的命令
    [RelayCommand]
    private void AddMapEntry(string mapType)
    {
        switch (mapType)
        {
            case "Category": CategoryIdMaps.Add(new IdMapEntry { Name = "新类别", Id = 0 }); break;
            case "Component": ComponentIdMaps.Add(new IdMapEntry { Name = "新组件", Id = 0 }); break;
            case "Accessory": AccessoryIdMaps.Add(new IdMapEntry { Name = "新配件", Id = 0 }); break;
            case "Code": CategoryCodeMaps.Add(new CodeMapEntry { Name = "新类别", Code = "CODE" }); break;
        }
    }

    [RelayCommand]
    private void RemoveMapEntry(object? entry)
    {
        if (entry is IdMapEntry idEntry)
        {
            if (CategoryIdMaps.Contains(idEntry)) CategoryIdMaps.Remove(idEntry);
            if (ComponentIdMaps.Contains(idEntry)) ComponentIdMaps.Remove(idEntry);
            if (AccessoryIdMaps.Contains(idEntry)) AccessoryIdMaps.Remove(idEntry);
        }
        else if (entry is CodeMapEntry codeEntry)
        {
            if (CategoryCodeMaps.Contains(codeEntry)) CategoryCodeMaps.Remove(codeEntry);
        }
    }

    public void Load()
    {
        try
        {
            var folderPath = AppContext.BaseDirectory;
            var filePath = Path.Combine(folderPath, ProfilesFileName);

            if (File.Exists(filePath))
            {
                var jsonString = File.ReadAllText(filePath);
                // 添加 JsonSerializerOptions 以便正确处理大小写和 null 值
                ProfilesData = JsonSerializer.Deserialize<ProfilesData>(jsonString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new ProfilesData();
            }

            if (!ProfilesData.Profiles.Any())
            {
                var defaultConfig = new SyncConfig();
                // 添加一些更全面的默认值
                defaultConfig.CATEGORY_ID_MAP.Add("台式机", 3);
                defaultConfig.CATEGORY_ID_MAP.Add("笔记本电脑", 4);
                defaultConfig.CATEGORY_ID_MAP.Add("显示器", 5);
                defaultConfig.COMPONENT_CATEGORY_ID_MAP.Add("内存", 9);
                defaultConfig.COMPONENT_CATEGORY_ID_MAP.Add("硬盘", 10);
                defaultConfig.COMPONENT_CATEGORY_ID_MAP.Add("处理器", 7);
                defaultConfig.ACCESSORY_CATEGORY_ID_MAP.Add("键盘", 15);
                defaultConfig.ACCESSORY_CATEGORY_ID_MAP.Add("鼠标", 16);
                defaultConfig.CATEGORY_CODE_MAP.Add("台式机", "TSJ");
                defaultConfig.CATEGORY_CODE_MAP.Add("笔记本电脑", "BJB");
                defaultConfig.EmailSuffix = "example.com"; // 提供一个默认后缀

                ProfilesData.Profiles["默认"] = defaultConfig;
                ProfilesData.ActiveProfile = "默认";
            }

            // 确保所有 Profile 都包含字典，以防旧配置文件缺少这些字段
            foreach (var profile in ProfilesData.Profiles.Values)
            {
                profile.CATEGORY_ID_MAP ??= new();
                profile.COMPONENT_CATEGORY_ID_MAP ??= new();
                profile.ACCESSORY_CATEGORY_ID_MAP ??= new();
                profile.CATEGORY_CODE_MAP ??= new();
                profile.ASSET_TAG_PREFIX ??= "DOZ";
            }

            UpdateProfileNames();
            // 确保 ActiveProfile 存在，如果不存在则设为第一个
            if (ProfilesData.ActiveProfile == null || !ProfilesData.Profiles.ContainsKey(ProfilesData.ActiveProfile))
            {
                ProfilesData.ActiveProfile = ProfileNames.FirstOrDefault();
            }
            SelectedProfileName = ProfilesData.ActiveProfile;
            // 初始加载时也需要填充 UI 集合
            PopulateIdMaps(SelectedProfile);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载配置失败: {ex.Message}");
            // 可以在此处添加用户可见的错误提示逻辑
        }
    }

    [RelayCommand] // 改为 RelayCommand 以便在 XAML 中绑定
    public void Save()
    {
        try
        {
            // 在保存前，将UI集合的数据写回字典
            if (SelectedProfile != null)
            {
                // 清理空名称的条目，并使用 ToDictionary 处理重复键
                SelectedProfile.CATEGORY_ID_MAP = CategoryIdMaps
                    .Where(e => !string.IsNullOrWhiteSpace(e.Name))
                    .GroupBy(e => e.Name)
                    .ToDictionary(g => g.Key, g => g.First().Id);
                SelectedProfile.COMPONENT_CATEGORY_ID_MAP = ComponentIdMaps
                    .Where(e => !string.IsNullOrWhiteSpace(e.Name))
                    .GroupBy(e => e.Name)
                    .ToDictionary(g => g.Key, g => g.First().Id);
                SelectedProfile.ACCESSORY_CATEGORY_ID_MAP = AccessoryIdMaps
                    .Where(e => !string.IsNullOrWhiteSpace(e.Name))
                    .GroupBy(e => e.Name)
                    .ToDictionary(g => g.Key, g => g.First().Id);
                SelectedProfile.CATEGORY_CODE_MAP = CategoryCodeMaps
                    .Where(e => !string.IsNullOrWhiteSpace(e.Name) && !string.IsNullOrWhiteSpace(e.Code))
                    .GroupBy(e => e.Name)
                    .ToDictionary(g => g.Key, g => g.First().Code);
            }

            ProfilesData.ActiveProfile = SelectedProfileName;

            // --- 核心修改：配置 JsonSerializerOptions 以允许中文字符 ---
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                // 使用 Encoder 指定允许所有 Unicode 字符，不进行转义
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
            };
            var jsonString = JsonSerializer.Serialize(ProfilesData, options);
            // --- 修改结束 ---

            var folderPath = AppContext.BaseDirectory;
            var filePath = Path.Combine(folderPath, ProfilesFileName);
            File.WriteAllText(filePath, jsonString); // 默认使用 UTF-8 编码写入
            System.Diagnostics.Debug.WriteLine($"配置已保存到: {filePath}");
            // 可以在此处添加一个保存成功的用户提示逻辑
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"保存配置失败: {ex.Message}");
            // 可以在此处添加用户可见的错误提示逻辑
        }
    }

    private void UpdateProfileNames()
    {
        ProfileNames.Clear();
        foreach (var name in ProfilesData.Profiles.Keys.OrderBy(k => k))
        {
            ProfileNames.Add(name);
        }
    }

    [RelayCommand]
    private void AddNewProfile(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName) || ProfilesData.Profiles.ContainsKey(newName))
        {
            return;
        }
        // 创建新 Profile 时也赋予默认的字典结构
        ProfilesData.Profiles[newName] = new SyncConfig
        {
            CATEGORY_ID_MAP = new(),
            COMPONENT_CATEGORY_ID_MAP = new(),
            ACCESSORY_CATEGORY_ID_MAP = new(),
            CATEGORY_CODE_MAP = new(),
            ASSET_TAG_PREFIX = "DOZ" // 或者继承当前选中Profile的前缀？
        };
        UpdateProfileNames();
        SelectedProfileName = newName;
    }

    [RelayCommand] // 改为 RelayCommand 以便在 XAML 中绑定
    private void DeleteProfile()
    {
        if (SelectedProfileName == null || ProfilesData.Profiles.Count <= 1)
        {
            return;
        }
        ProfilesData.Profiles.Remove(SelectedProfileName);
        UpdateProfileNames();
        SelectedProfileName = ProfileNames.FirstOrDefault();
    }

    public SyncConfig GetCurrentConfig()
    {
        // 确保返回的配置对象包含有效的字典
        var config = SelectedProfile ?? new SyncConfig();
        config.CATEGORY_ID_MAP ??= new();
        config.COMPONENT_CATEGORY_ID_MAP ??= new();
        config.ACCESSORY_CATEGORY_ID_MAP ??= new();
        config.CATEGORY_CODE_MAP ??= new();
        config.ASSET_TAG_PREFIX ??= "DOZ";
        return config;
    }
}