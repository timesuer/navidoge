namespace navidoge.Models;

/// <summary>
/// 应用程序配置
/// </summary>
public class AppSettings
{
    /// <summary>主机地址（保留兼容）</summary>
    public string Host { get; set; } = "localhost";

    /// <summary>端口（保留兼容）</summary>
    public string Port { get; set; } = "3306";

    /// <summary>数据库名（保留兼容）</summary>
    public string Database { get; set; } = "";

    /// <summary>用户名（保留兼容）</summary>
    public string Username { get; set; } = "root";

    /// <summary>密码（保留兼容）</summary>
    public string Password { get; set; } = "";

    /// <summary>上次选中的表名列表</summary>
    public List<string> SelectedTables { get; set; } = new();

    /// <summary>数据库连接配置列表</summary>
    public List<DatabaseProfile> ConnectionProfiles { get; set; } = new();

    /// <summary>上次使用的配置ID</summary>
    public string? LastUsedProfileId { get; set; }

    /// <summary>上次同步时间</summary>
    public DateTime? LastSyncAt { get; set; }

    /// <summary>同步开始时间</summary>
    public DateTime? SyncStartTime { get; set; }

    /// <summary>同步结束时间</summary>
    public DateTime? SyncEndTime { get; set; }

    /// <summary>同步选中的表名列表</summary>
    public List<string> SyncSelectedTables { get; set; } = new();

    /// <summary>同步目标配置ID</summary>
    public string? SyncTargetProfileId { get; set; }
}

