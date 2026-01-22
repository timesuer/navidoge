using navidoge.Models;

namespace navidoge.Services;

/// <summary>
/// 搜索服务 - 处理多表并行搜索
/// </summary>
public class SearchService
{
    private readonly DatabaseService _databaseService;

    public SearchService(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    /// <summary>
    /// 在多个表中并行搜索
    /// </summary>
    public async Task<List<SearchResult>> SearchAsync(IEnumerable<string> tableNames, string searchText)
    {
        var results = new List<SearchResult>();

        if (string.IsNullOrWhiteSpace(searchText))
            return results;

        var tasks = tableNames.Select(tableName => 
            _databaseService.SearchInTableAsync(tableName, searchText));

        var searchResults = await Task.WhenAll(tasks);

        // 只返回有匹配结果的表
        results.AddRange(searchResults.Where(r => r.MatchCount > 0));

        return results.OrderByDescending(r => r.MatchCount).ToList();
    }
}
