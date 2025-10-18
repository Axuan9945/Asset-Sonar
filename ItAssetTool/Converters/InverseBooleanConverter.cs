using Microsoft.UI.Xaml.Data;
using System;

namespace ItAssetTool.Converters;

public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return !(value is bool b && b);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}