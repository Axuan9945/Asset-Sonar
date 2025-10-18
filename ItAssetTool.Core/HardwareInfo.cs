namespace ItAssetTool.Core;

public class HardwareInfo
{
    public string? Category { get; set; }
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? Size { get; set; }
    public string? SerialNumber { get; set; }
    public string? ManufactureDate { get; set; }
    public string? WarrantyLink { get; set; }
    public uint MemoryType { get; set; }

    // vvvv 在这里添加新属性 vvvv
    public uint Speed { get; set; } // 用于存储内存频率
    // ^^^^ 添加结束 ^^^^
}