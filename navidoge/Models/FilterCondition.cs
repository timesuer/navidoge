using CommunityToolkit.Mvvm.ComponentModel;

namespace navidoge.Models;

/// <summary>
/// 筛选条件模型
/// </summary>
public partial class FilterCondition : ObservableObject
{
    /// <summary>筛选名称（显示用）</summary>
    [ObservableProperty]
    private string _name = "";

    /// <summary>列名</summary>
    [ObservableProperty]
    private string _columnName = "";

    /// <summary>筛选值</summary>
    [ObservableProperty]
    private string _value = "";

    public FilterCondition(string name, string columnName, string value)
    {
        Name = name;
        ColumnName = columnName;
        Value = value;
    }

    public override string ToString() => $"{Name}: {ColumnName}={Value}";
}
