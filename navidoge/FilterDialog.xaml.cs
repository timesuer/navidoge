using System.Windows;

namespace navidoge;

/// <summary>
/// 筛选对话框
/// </summary>
public partial class FilterDialog : Window
{
    public string ColumnName { get; private set; }
    public string FilterName { get; private set; } = "";
    public string FilterValue { get; private set; } = "";

    private readonly int _filterIndex;

    public FilterDialog(string columnName, int filterIndex)
    {
        InitializeComponent();
        ColumnName = columnName;
        _filterIndex = filterIndex;
        ColumnNameText.Text = columnName;
        FilterNameBox.Text = $"筛选{filterIndex}";
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        FilterName = string.IsNullOrWhiteSpace(FilterNameBox.Text)
            ? $"筛选{_filterIndex}"
            : FilterNameBox.Text.Trim();
        FilterValue = FilterValueBox.Text;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
