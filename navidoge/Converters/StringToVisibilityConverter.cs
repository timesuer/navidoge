using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace navidoge.Converters;

/// <summary>
/// 字符串到可见性转换器：非空字符串显示，空字符串隐藏
/// 支持参数 "invert" 反转逻辑
/// </summary>
public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool hasValue = value is string str && !string.IsNullOrEmpty(str);
        bool invert = parameter is string p && p == "invert";

        if (invert)
            hasValue = !hasValue;

        return hasValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
