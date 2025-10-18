// ItAssetTool/Converters/KnobMarginConverter.cs
using Microsoft.UI.Xaml.Data;
using System;

namespace ItAssetTool.Converters
{
    public class KnobMarginConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            // »¬¿é×ó²à/ÓÒ²àÎ»ÖÃ
            bool isOn = value is bool b && b;
            return isOn ? new Microsoft.UI.Xaml.Thickness(23, 0, 0, 0) : new Microsoft.UI.Xaml.Thickness(7, 0, 0, 0);
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }
}