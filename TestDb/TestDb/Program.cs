using MySqlConnector;

// 数据库连接测试
var connectionString = "Server=192.168.31.1;Port=3307;Database=ac_boot;User=root;Password=dsfwer@#$23fdsff3245;";

Console.WriteLine("正在测试数据库连接...");
Console.WriteLine($"连接地址: 192.168.31.1:3307/ac_boot");

try
{
    await using var conn = new MySqlConnection(connectionString);
    await conn.OpenAsync();
    
    Console.WriteLine("✅ 连接成功！");
    
    // 获取表列表
    var cmd = new MySqlCommand(
        "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'ac_boot' AND TABLE_TYPE = 'BASE TABLE'", 
        conn);
    
    await using var reader = await cmd.ExecuteReaderAsync();
    
    Console.WriteLine("\n数据库表列表:");
    int count = 0;
    while (await reader.ReadAsync())
    {
        count++;
        Console.WriteLine($"  {count}. {reader.GetString(0)}");
    }
    Console.WriteLine($"\n共 {count} 个表");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ 连接失败: {ex.Message}");
}
