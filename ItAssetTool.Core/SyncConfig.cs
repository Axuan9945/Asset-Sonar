namespace ItAssetTool.Core;

public class SyncConfig
{
    public string? InternalUrl { get; set; }
    public string? ExternalUrl { get; set; }
    public string? ApiKey { get; set; }
    public string? Department { get; set; }
    public string? AssignName { get; set; }
    public string? AssignUser { get; set; }
    public string? AssignPassword { get; set; }
    public string? EmailSuffix { get; set; }

    // vvvv 核心修改：添加新的配置属性 vvvv
    public string ASSET_TAG_PREFIX { get; set; } = "DOZ";
    public Dictionary<string, int> CATEGORY_ID_MAP { get; set; } = new();
    public Dictionary<string, int> COMPONENT_CATEGORY_ID_MAP { get; set; } = new();
    public Dictionary<string, int> ACCESSORY_CATEGORY_ID_MAP { get; set; } = new();
    public Dictionary<string, string> CATEGORY_CODE_MAP { get; set; } = new();
    // ^^^^ 修改结束 ^^^^
}