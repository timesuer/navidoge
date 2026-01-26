using System.Diagnostics.CodeAnalysis;

namespace navidoge.Models;

/// <summary>
/// 单表同步结果
/// </summary>
public class TableSyncResult
{
    /// <summary>表名</summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>成功同步的行数</summary>
    public int SyncedCount { get; set; }

    /// <summary>是否成功</summary>
    public bool Success { get; set; }

    /// <summary>提示信息</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>时间列是否缺失（供提示）</summary>
    public bool MissingTimeColumn { get; set; }

    /// <summary>安全打印</summary>
    public override string ToString() => $"{TableName}: {(Success ? "OK" : "FAIL")}, {SyncedCount}, {Message}";
}
