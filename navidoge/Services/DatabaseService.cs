using Dapper;
using MySqlConnector;
using navidoge.Models;

namespace navidoge.Services;

/// <summary>
/// 数据库服务 - 处理数据库连接和查询
/// </summary>
public class DatabaseService
{
    private DatabaseConnection? _currentConnection;

    /// <summary>
    /// 设置当前连接
    /// </summary>
    public void SetConnection(DatabaseConnection connection)
    {
        _currentConnection = connection;
    }

    /// <summary>
    /// 获取连接字符串
    /// </summary>
    public string? GetConnectionString() => _currentConnection?.ConnectionString;

    /// <summary>
    /// 测试数据库连接
    /// </summary>
    public async Task<bool> TestConnectionAsync()
    {
        if (_currentConnection == null) return false;

        try
        {
            await using var conn = new MySqlConnection(_currentConnection.ConnectionString);
            await conn.OpenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取数据库中所有表名
    /// </summary>
    public async Task<List<string>> GetTablesAsync()
    {
        if (_currentConnection == null) return new List<string>();

        await using var conn = new MySqlConnection(_currentConnection.ConnectionString);
        await conn.OpenAsync();

        var tables = await conn.QueryAsync<string>(
            "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = @Database AND TABLE_TYPE = 'BASE TABLE'",
            new { _currentConnection.Database });

        return tables.ToList();
    }

    /// <summary>
    /// 获取表的所有列名
    /// </summary>
    public async Task<List<string>> GetTableColumnsAsync(string tableName)
    {
        if (_currentConnection == null) return new List<string>();

        await using var conn = new MySqlConnection(_currentConnection.ConnectionString);
        await conn.OpenAsync();

        var columns = await conn.QueryAsync<string>(
            "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = @Database AND TABLE_NAME = @TableName",
            new { _currentConnection.Database, TableName = tableName });

        return columns.ToList();
    }

    /// <summary>
    /// 获取表的主键列
    /// </summary>
    public async Task<List<string>> GetPrimaryKeysAsync(string tableName)
    {
        if (_currentConnection == null) return new List<string>();

        await using var conn = new MySqlConnection(_currentConnection.ConnectionString);
        await conn.OpenAsync();

        var keys = await conn.QueryAsync<string>(
            @"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
              WHERE TABLE_SCHEMA = @Database AND TABLE_NAME = @TableName AND CONSTRAINT_NAME = 'PRIMARY'
              ORDER BY ORDINAL_POSITION",
            new { _currentConnection.Database, TableName = tableName });

        return keys.ToList();
    }

    /// <summary>
    /// 更新表中的一行数据
    /// </summary>
    public async Task<int> UpdateRowAsync(string tableName, Dictionary<string, object?> newValues, Dictionary<string, object?> originalValues)
    {
        if (_currentConnection == null) return 0;

        await using var conn = new MySqlConnection(_currentConnection.ConnectionString);
        await conn.OpenAsync();

        // 获取主键
        var primaryKeys = await GetPrimaryKeysAsync(tableName);

        // 构建 SET 子句
        var setClause = string.Join(", ", newValues.Keys.Select(k => $"`{k}` = @new_{k}"));

        // 构建 WHERE 子句（使用主键或全部原始值）
        string whereClause;
        if (primaryKeys.Count > 0)
        {
            whereClause = string.Join(" AND ", primaryKeys.Select(k => $"`{k}` = @old_{k}"));
        }
        else
        {
            // 没有主键，使用所有原始值作为条件
            whereClause = string.Join(" AND ", originalValues.Keys.Select(k =>
                originalValues[k] == null ? $"`{k}` IS NULL" : $"`{k}` = @old_{k}"));
        }

        var sql = $"UPDATE `{tableName}` SET {setClause} WHERE {whereClause} LIMIT 1";

        // 构建参数
        var parameters = new DynamicParameters();
        foreach (var kvp in newValues)
        {
            parameters.Add($"new_{kvp.Key}", kvp.Value);
        }
        foreach (var kvp in originalValues)
        {
            if (kvp.Value != null)
            {
                parameters.Add($"old_{kvp.Key}", kvp.Value);
            }
        }

        return await conn.ExecuteAsync(sql, parameters);
    }

    /// <summary>
    /// 删除表中的多行数据
    /// </summary>
    public async Task<int> DeleteRowsAsync(string tableName, List<Dictionary<string, object?>> rows)
    {
        if (_currentConnection == null || rows.Count == 0) return 0;

        await using var conn = new MySqlConnection(_currentConnection.ConnectionString);
        await conn.OpenAsync();

        var primaryKeys = await GetPrimaryKeysAsync(tableName);
        int totalDeleted = 0;

        foreach (var row in rows)
        {
            string whereClause;
            var parameters = new DynamicParameters();

            if (primaryKeys.Count > 0)
            {
                whereClause = string.Join(" AND ", primaryKeys.Select(k => $"`{k}` = @{k}"));
                foreach (var key in primaryKeys)
                {
                    if (row.ContainsKey(key))
                    {
                        parameters.Add(key, row[key]);
                    }
                }
            }
            else
            {
                var conditions = new List<string>();
                foreach (var kvp in row)
                {
                    if (kvp.Value == null)
                    {
                        conditions.Add($"`{kvp.Key}` IS NULL");
                    }
                    else
                    {
                        conditions.Add($"`{kvp.Key}` = @{kvp.Key}");
                        parameters.Add(kvp.Key, kvp.Value);
                    }
                }
                whereClause = string.Join(" AND ", conditions);
            }

            var sql = $"DELETE FROM `{tableName}` WHERE {whereClause} LIMIT 1";
            totalDeleted += await conn.ExecuteAsync(sql, parameters);
        }

        return totalDeleted;
    }

    /// <summary>
    /// 在指定表中搜索
    /// </summary>
    public async Task<SearchResult> SearchInTableAsync(string tableName, string searchText)
    {
        var result = new SearchResult { TableName = tableName };

        if (_currentConnection == null || string.IsNullOrWhiteSpace(searchText))
            return result;

        try
        {
            await using var conn = new MySqlConnection(_currentConnection.ConnectionString);
            await conn.OpenAsync();

            // 获取表的所有列
            var columns = await GetTableColumnsAsync(tableName);
            if (columns.Count == 0) return result;

            // 构建 WHERE 子句，搜索所有列
            var searchPattern = $"%{searchText}%";
            var whereConditions = columns.Select(c => $"`{c}` LIKE @SearchPattern");
            var whereClause = string.Join(" OR ", whereConditions);

            var sql = $"SELECT * FROM `{tableName}` WHERE {whereClause}";
            var rows = await conn.QueryAsync(sql, new { SearchPattern = searchPattern });

            foreach (var row in rows)
            {
                var dict = new Dictionary<string, object?>();
                foreach (var kvp in (IDictionary<string, object>)row)
                {
                    dict[kvp.Key] = kvp.Value;
                }
                result.MatchedRows.Add(dict);
            }

            result.MatchCount = result.MatchedRows.Count;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"搜索表 {tableName} 时出错: {ex.Message}");
        }

        return result;
    }
}
