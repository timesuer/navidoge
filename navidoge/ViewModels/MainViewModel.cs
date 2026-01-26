using System.Collections.ObjectModel;
using System.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dapper;
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
    private HashSet<string> _savedSyncSelectedTables = new();
    private DataTable? _unfilteredDataTable;

    /// <summary>数据库服务（供外部访问）</summary>
    public DatabaseService DatabaseService => _databaseService;

    /// <summary>当前显示的表名</summary>
    public string? CurrentTableName => SelectedResult?.TableName;

    /// <summary>筛选条件列表</summary>
    public ObservableCollection<FilterCondition> FilterConditions { get; } = new();

    /// <summary>数据库连接配置列表</summary>
    public ObservableCollection<DatabaseProfile> ConnectionProfiles { get; } = new();

    #region 属性

    /// <summary>选中的连接配置</summary>
    [ObservableProperty]
    private DatabaseProfile? _selectedConnectionProfile;

    /// <summary>当前连接的配置别名（连接后显示）</summary>
    [ObservableProperty]
    private string _currentProfileAlias = "";

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

    /// <summary>表过滤文本（主窗口用）</summary>
    [ObservableProperty]
    private string _tableFilter = "";

    /// <summary>同步表过滤文本（同步窗口用）</summary>
    [ObservableProperty]
    private string _syncTableFilter = "";

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

    /// <summary>表列表（主窗口用）</summary>
    public ObservableCollection<TableInfo> Tables { get; } = new();

    /// <summary>同步表列表（同步窗口用，独立过滤）</summary>
    public ObservableCollection<TableInfo> SyncTables { get; } = new();

    /// <summary>已选择的同步表列表</summary>
    public ObservableCollection<TableInfo> SelectedSyncTables { get; } = new();

    /// <summary>已选择的同步表数量</summary>
    public int SelectedSyncTableCount => SelectedSyncTables.Count;

    /// <summary>搜索结果列表</summary>
    public ObservableCollection<SearchResult> SearchResults { get; } = new();

    /// <summary>当前查看的数据行 (使用DataTable以正确显示列)</summary>
    [ObservableProperty]
    private DataTable? _currentDataTable;

    /// <summary>同步开始时间</summary>
    [ObservableProperty]
    private DateTime? _syncStartTime;

    /// <summary>同步结束时间</summary>
    [ObservableProperty]
    private DateTime? _syncEndTime;

    /// <summary>同步目标配置</summary>
    [ObservableProperty]
    private DatabaseProfile? _syncTargetProfile;

    /// <summary>同步源配置（用于同步窗口）</summary>
    [ObservableProperty]
    private DatabaseProfile? _syncSourceProfile;

    /// <summary>同步目标配置ID（持久化用）</summary>
    [ObservableProperty]
    private string? _syncTargetProfileId;

    /// <summary>上次同步时间</summary>
    [ObservableProperty]
    private DateTime? _lastSyncAt;

    /// <summary>同步选中的表</summary>
    [ObservableProperty]
    private List<string> _syncSelectedTables = new();

    /// <summary>同步结果摘要</summary>
    [ObservableProperty]
    private string _syncSummary = "";

    /// <summary>是否正在同步</summary>
    [ObservableProperty]
    private bool _isSyncing;

    /// <summary>同步结果列表</summary>
    public ObservableCollection<TableSyncResult> SyncResults { get; } = new();

    /// <summary>同步窗口标题（显示同步方向）</summary>
    public string SyncWindowTitle
    {
        get
        {
            var source = SyncSourceProfile?.Alias ?? SyncSourceProfile?.Host ?? "未选择";
            var target = SyncTargetProfile?.Alias ?? SyncTargetProfile?.Host ?? "未选择";
            return $"数据同步: {source} → {target}";
        }
    }

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
    /// 当选中的配置改变时，更新连接参数
    /// </summary>
    partial void OnSelectedConnectionProfileChanged(DatabaseProfile? value)
    {
        if (value != null)
        {
            Host = value.Host;
            Port = value.Port;
            Database = value.Database;
            Username = value.Username;
            Password = value.Password;
        }
    }

    /// <summary>
    /// 加载配置
    /// </summary>
    private void LoadSettings()
    {
        var settings = _settingsService.Load();

        SyncStartTime = settings.SyncStartTime;
        SyncEndTime = settings.SyncEndTime;
        LastSyncAt = settings.LastSyncAt;
        SyncTargetProfileId = settings.SyncTargetProfileId;
        SyncSelectedTables = settings.SyncSelectedTables ?? new List<string>();
        
        // 加载连接配置列表
        ConnectionProfiles.Clear();
        foreach (var profile in settings.ConnectionProfiles)
        {
            ConnectionProfiles.Add(profile);
        }

        // 兼容旧版配置：如果没有新配置但有旧的连接信息，创建一个默认配置
        if (ConnectionProfiles.Count == 0 && !string.IsNullOrEmpty(settings.Host))
        {
            var defaultProfile = new DatabaseProfile
            {
                Alias = "默认配置",
                Host = settings.Host,
                Port = settings.Port,
                Database = settings.Database,
                Username = settings.Username,
                Password = settings.Password
            };
            ConnectionProfiles.Add(defaultProfile);
        }

        // 选中上次使用的配置
        if (!string.IsNullOrEmpty(settings.LastUsedProfileId))
        {
            SelectedConnectionProfile = ConnectionProfiles.FirstOrDefault(p => p.Id == settings.LastUsedProfileId);
        }
        SelectedConnectionProfile ??= ConnectionProfiles.FirstOrDefault();

        _savedSelectedTables = new HashSet<string>(settings.SelectedTables);
        _savedSyncSelectedTables = new HashSet<string>(SyncSelectedTables ?? new List<string>());

        // 设置同步目标配置
        if (!string.IsNullOrEmpty(SyncTargetProfileId))
        {
            SyncTargetProfile = ConnectionProfiles.FirstOrDefault(p => p.Id == SyncTargetProfileId);
        }

        // 设置同步源配置（默认使用当前选中的配置）
        SyncSourceProfile = SelectedConnectionProfile;
    }

    /// <summary>
    /// 保存配置
    /// </summary>
    public void SaveSettings()
    {
        var selectedTables = _allTables.Where(t => t.IsSelected).Select(t => t.TableName).ToList();
        var syncSelectedTables = _allTables.Where(t => t.IsSyncSelected).Select(t => t.TableName).ToList();
        SyncSelectedTables = syncSelectedTables;

        var settings = new AppSettings
        {
            Host = Host,
            Port = Port,
            Database = Database,
            Username = Username,
            Password = Password,
            SelectedTables = selectedTables,
            SyncSelectedTables = syncSelectedTables,
            SyncStartTime = SyncStartTime,
            SyncEndTime = SyncEndTime,
            SyncTargetProfileId = SyncTargetProfile?.Id ?? SyncTargetProfileId,
            LastSyncAt = LastSyncAt,
            ConnectionProfiles = ConnectionProfiles.ToList(),
            LastUsedProfileId = SelectedConnectionProfile?.Id
        };
        _settingsService.Save(settings);
    }

    /// <summary>
    /// 更新连接配置列表（从配置管理窗口调用）
    /// </summary>
    public void UpdateConnectionProfiles(List<DatabaseProfile> profiles)
    {
        var currentId = SelectedConnectionProfile?.Id;
        ConnectionProfiles.Clear();
        foreach (var profile in profiles)
        {
            ConnectionProfiles.Add(profile);
        }

        // 如果当前选中的配置被删除，选择第一个
        if (currentId == null || !ConnectionProfiles.Any(p => p.Id == currentId))
        {
            SelectedConnectionProfile = ConnectionProfiles.FirstOrDefault();
        }
        else
        {
            SelectedConnectionProfile = ConnectionProfiles.FirstOrDefault(p => p.Id == currentId);
        }

        if (!string.IsNullOrEmpty(SyncTargetProfileId))
        {
            SyncTargetProfile = ConnectionProfiles.FirstOrDefault(p => p.Id == SyncTargetProfileId);
        }
        SyncTargetProfile ??= ConnectionProfiles.FirstOrDefault();

        SaveSettings();
    }

    /// <summary>
    /// 表过滤文本改变时过滤表列表
    /// </summary>
    partial void OnTableFilterChanged(string value)
    {
        FilterTables();
    }

    /// <summary>
    /// 同步表过滤文本改变时过滤同步表列表
    /// </summary>
    partial void OnSyncTableFilterChanged(string value)
    {
        FilterSyncTables();
    }

    /// <summary>
    /// 过滤表列表（主窗口用）
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
    /// 过滤同步表列表（同步窗口用）
    /// </summary>
    private void FilterSyncTables()
    {
        SyncTables.Clear();
        var filter = _syncTableFilter?.Trim().ToLower() ?? "";

        foreach (var table in _allTables)
        {
            if (string.IsNullOrEmpty(filter) || table.TableName.ToLower().Contains(filter))
            {
                SyncTables.Add(table);
            }
        }
    }

    /// <summary>
    /// 刷新已选择的同步表列表
    /// </summary>
    public void RefreshSelectedSyncTables()
    {
        SelectedSyncTables.Clear();
        foreach (var table in _allTables.Where(t => t.IsSyncSelected))
        {
            SelectedSyncTables.Add(table);
        }
        OnPropertyChanged(nameof(SelectedSyncTableCount));
    }

    /// <summary>
    /// 添加表到已选择列表
    /// </summary>
    public void AddToSelectedSyncTables(IEnumerable<TableInfo> tables)
    {
        foreach (var table in tables)
        {
            table.IsSyncSelected = true;
        }
        RefreshSelectedSyncTables();
    }

    /// <summary>
    /// 从已选择列表移除表
    /// </summary>
    public void RemoveFromSelectedSyncTables(IEnumerable<TableInfo> tables)
    {
        foreach (var table in tables)
        {
            table.IsSyncSelected = false;
        }
        RefreshSelectedSyncTables();
    }

    /// <summary>
    /// 初始化同步表列表（打开同步窗口时调用）
    /// </summary>
    public async Task InitializeSyncTablesAsync()
    {
        _syncTableFilter = "";
        OnPropertyChanged(nameof(SyncTableFilter));
        
        // 如果主窗口已连接，直接使用已有的表列表
        if (_allTables.Count > 0)
        {
            FilterSyncTables();
            RefreshSelectedSyncTables();
            return;
        }
        
        // 如果没有连接但有源配置，从源配置加载表列表
        if (SyncSourceProfile != null)
        {
            await LoadSyncTablesFromSourceAsync();
        }
    }

    /// <summary>
    /// 从源配置加载同步表列表
    /// </summary>
    public async Task LoadSyncTablesFromSourceAsync()
    {
        if (SyncSourceProfile == null) return;
        
        try
        {
            var connection = new DatabaseConnection
            {
                Host = SyncSourceProfile.Host,
                Port = int.TryParse(SyncSourceProfile.Port, out var p) ? p : 3306,
                Database = SyncSourceProfile.Database,
                Username = SyncSourceProfile.Username,
                Password = SyncSourceProfile.Password
            };

            await using var conn = new MySqlConnector.MySqlConnection(connection.ConnectionString);
            await conn.OpenAsync();

            var tables = await conn.QueryAsync<string>(
                "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = @Database AND TABLE_TYPE = 'BASE TABLE'",
                new { connection.Database });

            _allTables.Clear();
            SyncTables.Clear();
            Tables.Clear();

            foreach (var name in tables)
            {
                var tableInfo = new TableInfo(name);
                tableInfo.IsSelected = _savedSelectedTables.Contains(name);
                tableInfo.IsSyncSelected = _savedSyncSelectedTables.Contains(name);
                _allTables.Add(tableInfo);
                SyncTables.Add(tableInfo);
                Tables.Add(tableInfo);
            }
            
            RefreshSelectedSyncTables();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载同步表列表失败: {ex.Message}");
        }
    }

    /// <summary>按钮文本（连接/断开）</summary>
    [ObservableProperty]
    private string _connectionButtonText = "🔌 连接数据库";

    /// <summary>
    /// 切换连接/断开
    /// </summary>
    [RelayCommand]
    private async Task ToggleConnectionAsync()
    {
        if (IsConnected)
        {
            Disconnect();
        }
        else
        {
            await ConnectAsync();
        }
    }

    /// <summary>
    /// 连接数据库
    /// </summary>
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
                ConnectionButtonText = "🔌 断开数据库";
                CurrentProfileAlias = SelectedConnectionProfile?.Alias ?? "";
                StatusMessage = $"已连接到 {Database}";

                // 获取表列表
                var tableNames = await _databaseService.GetTablesAsync();
                _allTables.Clear();
                Tables.Clear();
                SyncTables.Clear();
                foreach (var name in tableNames)
                {
                    var tableInfo = new TableInfo(name);
                    // 恢复上次的选中状态
                    tableInfo.IsSelected = _savedSelectedTables.Contains(name);
                    tableInfo.IsSyncSelected = _savedSyncSelectedTables.Contains(name);
                    _allTables.Add(tableInfo);
                    Tables.Add(tableInfo);
                    SyncTables.Add(tableInfo);
                }

                // 保存连接配置
                SaveSettings();
            }
            else
            {
                IsConnected = false;
                ConnectionButtonText = "🔌 连接数据库";
                StatusMessage = "连接失败";
            }
        }
        catch (Exception ex)
        {
            IsConnected = false;
            ConnectionButtonText = "🔌 连接数据库";
            StatusMessage = $"连接错误: {ex.Message}";
        }
    }

    /// <summary>
    /// 断开数据库连接
    /// </summary>
    private void Disconnect()
    {
        _databaseService.ClearConnection();
        IsConnected = false;
        ConnectionButtonText = "🔌 连接数据库";
        CurrentProfileAlias = "";
        Tables.Clear();
        SyncTables.Clear();
        _allTables.Clear();
        SearchResults.Clear();
        CurrentDataTable = null;
        SearchResultSummary = "";
        DetailTitle = "";
        StatusMessage = "已断开连接";
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
    /// 同步数据到目标库（内部执行）
    /// </summary>
    public async Task ExecuteSyncAsync()
    {
        if (IsSyncing) return;

        var sourceProfile = SyncSourceProfile ?? SelectedConnectionProfile;
        if (sourceProfile == null)
        {
            StatusMessage = "请先选择源配置";
            return;
        }

        var targetProfile = SyncTargetProfile ?? ConnectionProfiles.FirstOrDefault(p => p.Id == SyncTargetProfileId);
        if (targetProfile == null)
        {
            StatusMessage = "请选择目标配置";
            return;
        }

        var selectedTables = _allTables.Where(t => t.IsSyncSelected).Select(t => t.TableName).ToList();
        if (selectedTables.Count == 0)
        {
            StatusMessage = "请选择要同步的表";
            return;
        }

        IsSyncing = true;
        SyncResults.Clear();
        SyncSummary = "正在同步...";
        StatusMessage = SyncSummary;

        var sourceConnection = new DatabaseConnection
        {
            Host = sourceProfile.Host,
            Port = int.TryParse(sourceProfile.Port, out var sourcePort) ? sourcePort : 3306,
            Database = sourceProfile.Database,
            Username = sourceProfile.Username,
            Password = sourceProfile.Password
        };

        var targetConnection = new DatabaseConnection
        {
            Host = targetProfile.Host,
            Port = int.TryParse(targetProfile.Port, out var targetPort) ? targetPort : 3306,
            Database = targetProfile.Database,
            Username = targetProfile.Username,
            Password = targetProfile.Password
        };

        try
        {
            var results = await _databaseService.SyncTablesAsync(
                sourceConnection,
                targetConnection,
                selectedTables,
                SyncStartTime,
                SyncEndTime);

            foreach (var result in results)
            {
                SyncResults.Add(result);
            }

            var successCount = results.Count(r => r.Success);
            var totalSynced = results.Sum(r => r.SyncedCount);

            SyncSummary = $"同步完成：{successCount}/{results.Count} 表成功，共 {totalSynced} 条记录";
            LastSyncAt = DateTime.Now;
            SyncTargetProfile = targetProfile;
            SyncTargetProfileId = targetProfile.Id;
            SyncSelectedTables = selectedTables;
            SaveSettings();
            StatusMessage = SyncSummary;
        }
        catch (Exception ex)
        {
            SyncSummary = $"同步失败：{ex.Message}";
            StatusMessage = SyncSummary;
        }
        finally
        {
            IsSyncing = false;
        }
    }

    /// <summary>
    /// 交换同步方向（源和目标配置互换）
    /// </summary>
    public void SwapSyncDirection()
    {
        (SyncSourceProfile, SyncTargetProfile) = (SyncTargetProfile, SyncSourceProfile);
        OnPropertyChanged(nameof(SyncWindowTitle));
    }

    /// <summary>
    /// 当同步源配置改变时，更新窗口标题
    /// </summary>
    partial void OnSyncSourceProfileChanged(DatabaseProfile? value)
    {
        OnPropertyChanged(nameof(SyncWindowTitle));
    }

    /// <summary>
    /// 当同步目标配置改变时，更新窗口标题
    /// </summary>
    partial void OnSyncTargetProfileChanged(DatabaseProfile? value)
    {
        OnPropertyChanged(nameof(SyncWindowTitle));
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
    /// 全选同步表
    /// </summary>
    [RelayCommand]
    private void SyncSelectAll()
    {
        foreach (var table in _allTables)
        {
            table.IsSyncSelected = true;
        }
    }

    /// <summary>
    /// 取消全选同步表
    /// </summary>
    [RelayCommand]
    private void SyncDeselectAll()
    {
        foreach (var table in _allTables)
        {
            table.IsSyncSelected = false;
        }
    }

    /// <summary>
    /// 当选中的搜索结果改变时，更新数据行显示
    /// </summary>
    partial void OnSelectedResultChanged(SearchResult? value)
    {
        // 清除筛选条件
        FilterConditions.Clear();

        if (value == null || value.MatchedRows.Count == 0)
        {
            CurrentDataTable = null;
            _unfilteredDataTable = null;
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

        _unfilteredDataTable = dt;
        CurrentDataTable = dt;
    }

    /// <summary>
    /// 添加筛选条件
    /// </summary>
    public void AddFilter(string name, string columnName, string value)
    {
        var filter = new FilterCondition(name, columnName, value);
        FilterConditions.Add(filter);
        ApplyFilters();
    }

    /// <summary>
    /// 移除筛选条件
    /// </summary>
    public void RemoveFilter(FilterCondition filter)
    {
        FilterConditions.Remove(filter);
        ApplyFilters();
    }

    /// <summary>
    /// 应用所有筛选条件
    /// </summary>
    private void ApplyFilters()
    {
        if (_unfilteredDataTable == null)
        {
            return;
        }

        if (FilterConditions.Count == 0)
        {
            CurrentDataTable = _unfilteredDataTable;
            UpdateDetailTitle(_unfilteredDataTable.Rows.Count);
            return;
        }

        // 创建筛选后的 DataTable
        var filteredDt = _unfilteredDataTable.Clone();

        foreach (DataRow row in _unfilteredDataTable.Rows)
        {
            bool matchAll = true;
            foreach (var filter in FilterConditions)
            {
                if (_unfilteredDataTable.Columns.Contains(filter.ColumnName))
                {
                    var cellValue = row[filter.ColumnName]?.ToString() ?? "";
                    if (!cellValue.Contains(filter.Value, StringComparison.OrdinalIgnoreCase))
                    {
                        matchAll = false;
                        break;
                    }
                }
            }

            if (matchAll)
            {
                filteredDt.ImportRow(row);
            }
        }

        CurrentDataTable = filteredDt;
        UpdateDetailTitle(filteredDt.Rows.Count);
    }

    /// <summary>
    /// 更新详情标题
    /// </summary>
    private void UpdateDetailTitle(int rowCount)
    {
        if (SelectedResult != null)
        {
            var filterInfo = FilterConditions.Count > 0 ? $"（已筛选，原 {SelectedResult.MatchCount} 条）" : "";
            DetailTitle = $"表 [{SelectedResult.TableName}] 的匹配数据，共 {rowCount} 条记录{filterInfo}";
        }
    }

    /// <summary>
    /// 获取下一个筛选序号
    /// </summary>
    public int GetNextFilterIndex()
    {
        return FilterConditions.Count + 1;
    }
}
