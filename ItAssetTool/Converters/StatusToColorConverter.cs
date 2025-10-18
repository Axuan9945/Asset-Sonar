// In Project: ItAssetTool
// Folder: Converters
// File: StatusToColorConverter.cs
using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.UI;

namespace ItAssetTool.Converters;

public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string status)
        {
            if (status == "在线")
            {
                // "在线"状态保持半透明绿色
                return new SolidColorBrush(Color.FromArgb(220, 46, 125, 50));
            }
        }

        // vvvv 核心修改 vvvv
        // 对于“未使用”或任何其他状态，使用用户建议的蓝色，以确保清晰可见
        return new SolidColorBrush(Color.FromArgb(255, 0, 120, 215)); // 这是一个清晰的蓝色
        // ^^^^ 修改结束 ^^^^
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}