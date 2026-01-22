namespace navidoge.Models;

/// <summary>
/// 搜索结果模型
/// </summary>
public class SearchResult
{
    /// <summary>表名</summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>匹配记录数</summary>
    public int MatchCount { get; set; }

    /// <summary>匹配的行数据</summary>
    public List<Dictionary<string, object?>> MatchedRows { get; set; } = new();
}
