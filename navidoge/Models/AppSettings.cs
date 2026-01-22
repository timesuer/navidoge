namespace navidoge.Models;

/// <summary>
/// 应用程序配置
/// </summary>
public class AppSettings
{
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

    /// <summary>上次选中的表名列表</summary>
    public List<string> SelectedTables { get; set; } = new();
}
