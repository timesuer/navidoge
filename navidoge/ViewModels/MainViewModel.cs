using System.Collections.ObjectModel;
using System.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using navidoge.Models;
using navidoge.Services;

namespace navidoge.ViewModels;

/// <summary>
/// 主窗口视图模型
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly DatabaseService _databaseService;
    private readonly SearchService _searchService;
    private readonly SettingsService _settingsService;
    private List<TableInfo> _allTables = new();
    private HashSet<string> _savedSelectedTables = new();

    /// <summary>数据库服务（供外部访问）</summary>
    public DatabaseService DatabaseService => _databaseService;

    /// <summary>当前显示的表名</summary>
    public string? CurrentTableName => SelectedResult?.TableName;

    #region 属性

    /// <summary>主机地址</summary>
    [ObservableProperty]
    private string _host = "localhost";

    /// <summary>端口</summary>
    [ObservableProperty]
    private string _port = "3306";

    /// <summary>数据库名</summary>
    [ObservableProperty]
    private string _database = "";

    /// <summary>用户名</summary>
    [ObservableProperty]
    private string _username = "root";

    /// <summary>密码</summary>
    [ObservableProperty]
    private string _password = "";

    /// <summary>搜索文本</summary>
    [ObservableProperty]
    private string _searchText = "";

    /// <summary>表过滤文本</summary>
    [ObservableProperty]
    private string _tableFilter = "";

    /// <summary>是否正在搜索</summary>
    [ObservableProperty]
    private bool _isSearching;

    /// <summary>是否已连接</summary>
    [ObservableProperty]
    private bool _isConnected;

    /// <summary>状态消息</summary>
    [ObservableProperty]
    private string _statusMessage = "未连接";

    /// <summary>选中的搜索结果</summary>
    [ObservableProperty]
    private SearchResult? _selectedResult;

    /// <summary>搜索结果汇总信息</summary>
    [ObservableProperty]
    private string _searchResultSummary = "";

    /// <summary>数据详情标题</summary>
    [ObservableProperty]
    private string _detailTitle = "";

    /// <summary>表列表</summary>
    public ObservableCollection<TableInfo> Tables { get; } = new();

    /// <summary>搜索结果列表</summary>
    public ObservableCollection<SearchResult> SearchResults { get; } = new();

    /// <summary>当前查看的数据行 (使用DataTable以正确显示列)</summary>
    [ObservableProperty]
    private DataTable? _currentDataTable;

    #endregion

    public MainViewModel()
    {
        _databaseService = new DatabaseService();
        _searchService = new SearchService(_databaseService);
        _settingsService = new SettingsService();

        // 加载上次的配置
        LoadSettings();
    }

    /// <summary>
    /// 加载配置
    /// </summary>
    private void LoadSettings()
    {
        var settings = _settingsService.Load();
        Host = settings.Host;
        Port = settings.Port;
        Database = settings.Database;
        Username = settings.Username;
        Password = settings.Password;
        _savedSelectedTables = new HashSet<string>(settings.SelectedTables);
    }

    /// <summary>
    /// 保存配置
    /// </summary>
    public void SaveSettings()
    {
        var settings = new AppSettings
        {
            Host = Host,
            Port = Port,
            Database = Database,
            Username = Username,
            Password = Password,
            SelectedTables = _allTables.Where(t => t.IsSelected).Select(t => t.TableName).ToList()
        };
        _settingsService.Save(settings);
    }

    /// <summary>
    /// 表过滤文本改变时过滤表列表
    /// </summary>
    partial void OnTableFilterChanged(string value)
    {
        FilterTables();
    }

    /// <summary>
    /// 过滤表列表
    /// </summary>
    private void FilterTables()
    {
        Tables.Clear();
        var filter = TableFilter?.Trim().ToLower() ?? "";

        foreach (var table in _allTables)
        {
            if (string.IsNullOrEmpty(filter) || table.TableName.ToLower().Contains(filter))
            {
                Tables.Add(table);
            }
        }
    }

    /// <summary>
    /// 连接数据库
    /// </summary>
    [RelayCommand]
    private async Task ConnectAsync()
    {
        try
        {
            StatusMessage = "正在连接...";

            var connection = new DatabaseConnection
            {
                Host = Host,
                Port = int.TryParse(Port, out var p) ? p : 3306,
                Database = Database,
                Username = Username,
                Password = Password
            };

            _databaseService.SetConnection(connection);

            if (await _databaseService.TestConnectionAsync())
            {
                IsConnected = true;
                StatusMessage = $"已连接到 {Database}";

                // 获取表列表
                var tableNames = await _databaseService.GetTablesAsync();
                _allTables.Clear();
                Tables.Clear();
                foreach (var name in tableNames)
                {
                    var tableInfo = new TableInfo(name);
                    // 恢复上次的选中状态
                    tableInfo.IsSelected = _savedSelectedTables.Contains(name);
                    _allTables.Add(tableInfo);
                    Tables.Add(tableInfo);
                }

                // 保存连接配置
                SaveSettings();
            }
            else
            {
                IsConnected = false;
                StatusMessage = "连接失败";
            }
        }
        catch (Exception ex)
        {
            IsConnected = false;
            StatusMessage = $"连接错误: {ex.Message}";
        }
    }

    /// <summary>
    /// 搜索
    /// </summary>
    [RelayCommand]
    private async Task SearchAsync()
    {
        if (!IsConnected || string.IsNullOrWhiteSpace(SearchText))
            return;

        try
        {
            IsSearching = true;
            StatusMessage = "正在搜索...";
            SearchResults.Clear();
            CurrentDataTable = null;
            SearchResultSummary = "";
            DetailTitle = "";

            // 设置搜索文本供高亮转换器使用
            Converters.ContainsSearchTextConverter.CurrentSearchText = SearchText;

            // 从所有表中找出被选中的表（包括被过滤隐藏的）
            var selectedTables = _allTables.Where(t => t.IsSelected).Select(t => t.TableName);
            var results = await _searchService.SearchAsync(selectedTables, SearchText);

            int totalRecords = 0;
            foreach (var result in results)
            {
                SearchResults.Add(result);
                totalRecords += result.MatchCount;
            }

            SearchResultSummary = $"匹配 {SearchResults.Count} 个表，共 {totalRecords} 条记录";
            StatusMessage = $"搜索完成，{SearchResultSummary}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"搜索错误: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
        }
    }

    /// <summary>
    /// 全选表
    /// </summary>
    [RelayCommand]
    private void SelectAll()
    {
        foreach (var table in _allTables)
        {
            table.IsSelected = true;
        }
    }

    /// <summary>
    /// 取消全选
    /// </summary>
    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var table in _allTables)
        {
            table.IsSelected = false;
        }
    }

    /// <summary>
    /// 当选中的搜索结果改变时，更新数据行显示
    /// </summary>
    partial void OnSelectedResultChanged(SearchResult? value)
    {
        if (value == null || value.MatchedRows.Count == 0)
        {
            CurrentDataTable = null;
            DetailTitle = "";
            return;
        }

        DetailTitle = $"表 [{value.TableName}] 的匹配数据，共 {value.MatchCount} 条记录";

        // 将 Dictionary 转换为 DataTable 以正确显示列
        var dt = new DataTable();

        // 从第一行获取列名
        var firstRow = value.MatchedRows[0];
        foreach (var key in firstRow.Keys)
        {
            dt.Columns.Add(key, typeof(string));
        }

        // 添加数据行
        foreach (var row in value.MatchedRows)
        {
            var dataRow = dt.NewRow();
            foreach (var kvp in row)
            {
                dataRow[kvp.Key] = kvp.Value?.ToString() ?? "";
            }
            dt.Rows.Add(dataRow);
        }

        CurrentDataTable = dt;
    }
}
