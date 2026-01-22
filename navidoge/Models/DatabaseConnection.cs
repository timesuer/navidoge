namespace navidoge.Models;

/// <summary>
/// 数据库连接配置模型
/// </summary>
public class DatabaseConnection
{
    /// <summary>连接名称</summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>主机地址</summary>
    public string Host { get; set; } = "localhost";
    
    /// <summary>端口</summary>
    public int Port { get; set; } = 3306;
    
    /// <summary>数据库名</summary>
    public string Database { get; set; } = string.Empty;
    
    /// <summary>用户名</summary>
    public string Username { get; set; } = "root";
    
    /// <summary>密码</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// 生成连接字符串
    /// </summary>
    public string ConnectionString => 
        $"Server={Host};Port={Port};Database={Database};User={Username};Password={Password};";
}
