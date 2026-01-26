using CommunityToolkit.Mvvm.ComponentModel;

namespace navidoge.Models;

/// <summary>
/// 表信息模型（支持勾选）
/// </summary>
public partial class TableInfo : ObservableObject
{
    /// <summary>表名</summary>
    [ObservableProperty]
    private string _tableName = string.Empty;

    /// <summary>是否选中搜索</summary>
    [ObservableProperty]
    private bool _isSelected = true;

    /// <summary>是否选中同步</summary>
    [ObservableProperty]
    private bool _isSyncSelected = false;

    public TableInfo() { }

    public TableInfo(string tableName)
    {
        TableName = tableName;
    }
}
