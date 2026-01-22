using System.Globalization;
using System.Windows.Data;

namespace navidoge.Converters;

/// <summary>
/// 检查文本是否包含搜索文本的转换器
/// </summary>
public class ContainsSearchTextConverter : IValueConverter
{
    public static string? CurrentSearchText { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || string.IsNullOrEmpty(CurrentSearchText))
            return false;

        var text = value.ToString() ?? string.Empty;
        return text.Contains(CurrentSearchText, StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
