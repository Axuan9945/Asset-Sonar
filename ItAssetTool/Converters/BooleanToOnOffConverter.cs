using System;
using Microsoft.UI.Xaml.Data;

namespace ItAssetTool.Converters
{
    public class BooleanToOnOffConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool b)
                return b ? "开" : "关";
            return "关";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is string s)
                return s == "开";
            return false;
        }
    }
}