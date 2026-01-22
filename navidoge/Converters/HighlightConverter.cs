using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace navidoge.Converters;

/// <summary>
/// 文本高亮转换器 - 将匹配的搜索文本显示为黄色背景
/// </summary>
public class HighlightConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2)
            return values[0]?.ToString() ?? string.Empty;

        var text = values[0]?.ToString() ?? string.Empty;
        var searchText = values[1]?.ToString() ?? string.Empty;

        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(searchText))
            return text;

        var textBlock = new System.Windows.Controls.TextBlock();
        var index = text.IndexOf(searchText, StringComparison.OrdinalIgnoreCase);

        if (index < 0)
        {
            textBlock.Inlines.Add(new Run(text));
        }
        else
        {
            var lastIndex = 0;
            while (index >= 0)
            {
                // 添加匹配前的文本
                if (index > lastIndex)
                {
                    textBlock.Inlines.Add(new Run(text.Substring(lastIndex, index - lastIndex)));
                }

                // 添加高亮的匹配文本
                var highlightRun = new Run(text.Substring(index, searchText.Length))
                {
                    Background = Brushes.Yellow,
                    FontWeight = FontWeights.Bold
                };
                textBlock.Inlines.Add(highlightRun);

                lastIndex = index + searchText.Length;
                index = text.IndexOf(searchText, lastIndex, StringComparison.OrdinalIgnoreCase);
            }

            // 添加剩余文本
            if (lastIndex < text.Length)
            {
                textBlock.Inlines.Add(new Run(text.Substring(lastIndex)));
            }
        }

        return textBlock;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
