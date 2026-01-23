namespace navidoge.Models;

/// <summary>
/// 数据库连接配置
/// </summary>
public class DatabaseProfile
{
    /// <summary>配置唯一标识</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>配置别名/备注</summary>
    public string Alias { get; set; } = "";

    /// <summary>主机地址</summary>
    public string Host { get; set; } = "localhost";

    /// <summary>端口</summary>
    public string Port { get; set; } = "3306";

    /// <summary>数据库名</summary>
    public string Database { get; set; } = "";

    /// <summary>用户名</summary>
    public string Username { get; set; } = "root";

    /// <summary>密码</summary>
    public string Password { get; set; } = "";

    /// <summary>显示文本（IP + 备注）</summary>
    public string DisplayText => string.IsNullOrEmpty(Alias) 
        ? $"{Host}:{Port}" 
        : $"{Alias} ({Host})";

    /// <summary>克隆配置</summary>
    public DatabaseProfile Clone() => new()
    {
        Id = Id,
        Alias = Alias,
        Host = Host,
        Port = Port,
        Database = Database,
        Username = Username,
        Password = Password
    };
}
