using System.Data;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;
using navidoge.Converters;
using navidoge.ViewModels;

namespace navidoge;

/// <summary>
/// MainWindow.xaml 的交互逻辑
/// </summary>
public partial class MainWindow : Window
{
    private Dictionary<string, object?>? _editingRowOriginalValues;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    /// <summary>
    /// 窗口加载时，恢复密码
    /// </summary>
    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            PasswordBox.Password = vm.Password;
        }
    }

    /// <summary>
    /// 窗口关闭时，保存配置
    /// </summary>
    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.SaveSettings();
        }
    }

    /// <summary>
    /// 连接按钮点击时，将密码传递给ViewModel
    /// </summary>
    private void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.Password = PasswordBox.Password;
        }
    }

    /// <summary>
    /// 打开数据库配置管理窗口
    /// </summary>
    private void ManageProfiles_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            var configWindow = new DatabaseConfigWindow(vm.ConnectionProfiles.ToList())
            {
                Owner = this
            };

            if (configWindow.ShowDialog() == true)
            {
                vm.UpdateConnectionProfiles(configWindow.GetProfiles());
            }
        }
    }

    /// <summary>
    /// 表列表双击时，切换选中状态
    /// </summary>
    private void TableListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (TableListBox.SelectedItem is Models.TableInfo tableInfo)
        {
            tableInfo.IsSelected = !tableInfo.IsSelected;
        }
    }

    /// <summary>
    /// 打开数据同步窗口
    /// </summary>
    private void OpenSyncWindow_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            var syncWindow = new SyncWindow(vm)
            {
                Owner = this
            };
            syncWindow.ShowDialog();
        }
    }

    /// <summary>
    /// 数据详情表格自动生成列时，设置高亮样式和列头右键菜单
    /// </summary>
    private void DetailDataGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        if (e.Column is DataGridTextColumn textColumn)
        {
            // 创建带高亮的 CellStyle
            var cellStyle = new Style(typeof(DataGridCell));

            // 使用 DataTrigger 判断单元格是否包含搜索文本
            var trigger = new DataTrigger
            {
                Binding = new Binding(e.PropertyName)
                {
                    Converter = new ContainsSearchTextConverter()
                },
                Value = true
            };
            trigger.Setters.Add(new Setter(DataGridCell.BackgroundProperty, new SolidColorBrush(Color.FromRgb(255, 255, 0))));

            cellStyle.Triggers.Add(trigger);
            textColumn.CellStyle = cellStyle;
        }

        // 为列头添加右键菜单
        var headerStyle = new Style(typeof(DataGridColumnHeader));
        var contextMenu = new ContextMenu();
        var filterMenuItem = new MenuItem { Header = "筛选此列" };
        filterMenuItem.Click += MenuItem_FilterColumn_Click;
        contextMenu.Items.Add(filterMenuItem);
        headerStyle.Setters.Add(new Setter(DataGridColumnHeader.ContextMenuProperty, contextMenu));
        e.Column.HeaderStyle = headerStyle;
    }

    /// <summary>
    /// 开始编辑单元格时，保存原始行数据
    /// </summary>
    private void DetailDataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
    {
        if (e.Row.Item is DataRowView rowView)
        {
            _editingRowOriginalValues = new Dictionary<string, object?>();
            foreach (DataColumn col in rowView.Row.Table.Columns)
            {
                _editingRowOriginalValues[col.ColumnName] = rowView.Row[col];
            }
        }
    }

    /// <summary>
    /// 单元格编辑结束时，询问是否更新数据库
    /// </summary>
    private async void DetailDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction == DataGridEditAction.Cancel)
        {
            _editingRowOriginalValues = null;
            return;
        }

        if (_editingRowOriginalValues == null) return;
        if (DataContext is not MainViewModel vm) return;
        if (string.IsNullOrEmpty(vm.CurrentTableName)) return;

        // 获取编辑后的值
        var editedElement = e.EditingElement as TextBox;
        var newValue = editedElement?.Text;
        var columnName = e.Column.Header?.ToString() ?? "";

        // 检查值是否真的改变了
        var originalValue = _editingRowOriginalValues.ContainsKey(columnName)
            ? _editingRowOriginalValues[columnName]?.ToString()
            : null;

        if (newValue == originalValue)
        {
            _editingRowOriginalValues = null;
            return;
        }

        // 弹出确认对话框
        var result = MessageBox.Show(
            $"是否更新此行数据？\n\n列: {columnName}\n原值: {originalValue}\n新值: {newValue}",
            "确认更新",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                // 构建新值字典
                var newValues = new Dictionary<string, object?>();
                if (e.Row.Item is DataRowView rowView)
                {
                    foreach (DataColumn col in rowView.Row.Table.Columns)
                    {
                        if (col.ColumnName == columnName)
                        {
                            newValues[col.ColumnName] = newValue;
                        }
                        else
                        {
                            newValues[col.ColumnName] = rowView.Row[col];
                        }
                    }
                }

                var affected = await vm.DatabaseService.UpdateRowAsync(
                    vm.CurrentTableName,
                    newValues,
                    _editingRowOriginalValues);

                if (affected > 0)
                {
                    vm.StatusMessage = "数据更新成功";
                }
                else
                {
                    vm.StatusMessage = "更新失败：未找到匹配的行";
                    // 回滚编辑
                    e.Cancel = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"更新失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Cancel = true;
            }
        }
        else
        {
            // 用户取消，回滚编辑
            e.Cancel = true;
        }

        _editingRowOriginalValues = null;
    }

    /// <summary>
    /// 删除选中行
    /// </summary>
    private async void MenuItem_DeleteRows_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (string.IsNullOrEmpty(vm.CurrentTableName)) return;

        var selectedItems = DetailDataGrid.SelectedItems;
        if (selectedItems.Count == 0)
        {
            MessageBox.Show("请先选择要删除的行", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(
            $"确定要删除选中的 {selectedItems.Count} 行数据吗？\n\n此操作不可撤销！",
            "确认删除",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            var rowsToDelete = new List<Dictionary<string, object?>>();

            foreach (var item in selectedItems)
            {
                if (item is DataRowView rowView)
                {
                    var rowData = new Dictionary<string, object?>();
                    foreach (DataColumn col in rowView.Row.Table.Columns)
                    {
                        rowData[col.ColumnName] = rowView.Row[col] == DBNull.Value ? null : rowView.Row[col];
                    }
                    rowsToDelete.Add(rowData);
                }
            }

            var deleted = await vm.DatabaseService.DeleteRowsAsync(vm.CurrentTableName, rowsToDelete);
            vm.StatusMessage = $"已删除 {deleted} 行数据";

            // 从 DataTable 中移除已删除的行
            if (vm.CurrentDataTable != null)
            {
                var rowsToRemove = new List<DataRow>();
                foreach (var item in selectedItems)
                {
                    if (item is DataRowView rowView)
                    {
                        rowsToRemove.Add(rowView.Row);
                    }
                }
                foreach (var row in rowsToRemove)
                {
                    vm.CurrentDataTable.Rows.Remove(row);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"删除失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 复制选中单元格数据到剪贴板
    /// </summary>
    private void MenuItem_CopyCellData_Click(object sender, RoutedEventArgs e)
    {
        var currentCell = DetailDataGrid.CurrentCell;
        if (currentCell.Column == null || currentCell.Item == null)
        {
            MessageBox.Show("请先选择要复制的单元格", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (currentCell.Item is DataRowView rowView)
        {
            var columnName = currentCell.Column.Header?.ToString() ?? "";
            var cellValue = rowView.Row[columnName]?.ToString() ?? "";
            Clipboard.SetText(cellValue);

            if (DataContext is MainViewModel vm)
            {
                vm.StatusMessage = $"已复制单元格数据: {cellValue}";
            }
        }
    }

    /// <summary>
    /// 复制当前行数据到剪贴板
    /// </summary>
    private void MenuItem_CopyRowData_Click(object sender, RoutedEventArgs e)
    {
        var currentCell = DetailDataGrid.CurrentCell;
        if (currentCell.Item == null)
        {
            MessageBox.Show("请先选择要复制的行", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (currentCell.Item is DataRowView rowView)
        {
            var sb = new StringBuilder();

            // 添加表头
            var headers = rowView.Row.Table.Columns.Cast<DataColumn>().Select(c => c.ColumnName);
            sb.AppendLine(string.Join("\t", headers));

            // 添加数据行
            var values = rowView.Row.ItemArray.Select(v => v?.ToString() ?? "");
            sb.AppendLine(string.Join("\t", values));

            Clipboard.SetText(sb.ToString());

            if (DataContext is MainViewModel vm)
            {
                vm.StatusMessage = "已复制本行数据到剪贴板";
            }
        }
    }

    /// <summary>
    /// 复制选中行为 INSERT 语句
    /// </summary>
    private void MenuItem_CopyAsInsert_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (string.IsNullOrEmpty(vm.CurrentTableName)) return;

        var selectedItems = DetailDataGrid.SelectedItems;
        if (selectedItems.Count == 0)
        {
            MessageBox.Show("请先选择要复制的行", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var sb = new StringBuilder();

        foreach (var item in selectedItems)
        {
            if (item is DataRowView rowView)
            {
                var columns = rowView.Row.Table.Columns.Cast<DataColumn>().Select(c => $"`{c.ColumnName}`");
                var values = rowView.Row.ItemArray.Select(v =>
                {
                    if (v == null || v == DBNull.Value) return "NULL";
                    if (v is DateTime dt) return $"'{dt:yyyy-MM-dd HH:mm:ss}'";
                    if (v is bool b) return b ? "1" : "0";
                    if (v is int or long or float or double or decimal) return v.ToString();
                    // 转义单引号
                    return $"'{v.ToString()?.Replace("'", "''")}'";
                });

                sb.AppendLine($"INSERT INTO `{vm.CurrentTableName}` ({string.Join(", ", columns)}) VALUES ({string.Join(", ", values)});");
            }
        }

        Clipboard.SetText(sb.ToString());
        vm.StatusMessage = $"已复制 {selectedItems.Count} 条 INSERT 语句到剪贴板";
    }

    /// <summary>
    /// 复制选中行为 CSV 格式
    /// </summary>
    private void MenuItem_CopyAsCsv_Click(object sender, RoutedEventArgs e)
    {
        var selectedItems = DetailDataGrid.SelectedItems;
        if (selectedItems.Count == 0)
        {
            MessageBox.Show("请先选择要复制的行", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var sb = new StringBuilder();

        // 添加表头
        if (selectedItems[0] is DataRowView firstRow)
        {
            var headers = firstRow.Row.Table.Columns.Cast<DataColumn>().Select(c => EscapeCsvField(c.ColumnName));
            sb.AppendLine(string.Join(",", headers));
        }

        // 添加数据行
        foreach (var item in selectedItems)
        {
            if (item is DataRowView rowView)
            {
                var values = rowView.Row.ItemArray.Select(v => EscapeCsvField(v?.ToString() ?? ""));
                sb.AppendLine(string.Join(",", values));
            }
        }

        Clipboard.SetText(sb.ToString());
        if (DataContext is MainViewModel vm)
        {
            vm.StatusMessage = $"已复制 {selectedItems.Count} 行 CSV 数据到剪贴板";
        }
    }

    /// <summary>
    /// 转义 CSV 字段
    /// </summary>
    private static string EscapeCsvField(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
    }

    /// <summary>
    /// 列头右键菜单 - 筛选此列
    /// </summary>
    private void MenuItem_FilterColumn_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        // 获取点击的列头
        if (sender is MenuItem menuItem &&
            menuItem.Parent is ContextMenu contextMenu &&
            contextMenu.PlacementTarget is DataGridColumnHeader header)
        {
            var columnName = header.Content?.ToString();
            if (string.IsNullOrEmpty(columnName)) return;

            // 显示筛选对话框
            var dialog = new FilterDialog(columnName, vm.GetNextFilterIndex())
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                vm.AddFilter(dialog.FilterName, dialog.ColumnName, dialog.FilterValue);
                vm.StatusMessage = $"已添加筛选: {dialog.FilterName}";
            }
        }
    }

    /// <summary>
    /// 移除筛选标签
    /// </summary>
    private void FilterTag_Remove_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        if (sender is Button button && button.Tag is Models.FilterCondition filter)
        {
            vm.RemoveFilter(filter);
            vm.StatusMessage = $"已移除筛选: {filter.Name}";
        }
    }

    private void MenuItem_ShowColumnSelector_Click(object sender, RoutedEventArgs e)
    {
        if (DetailDataGrid.Columns.Count == 0)
        {
            MessageBox.Show("当前没有可选择的列", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new Window
        {
            Title = "显示筛选（选择显示列）",
            Width = 320,
            Height = 420,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize
        };

        var root = new DockPanel { Margin = new Thickness(12) };
        var topPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        var searchBox = new TextBox
        {
            Height = 28,
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(0, 0, 0, 6)
        };
        var actionPanel = new StackPanel { Orientation = Orientation.Horizontal };
        var selectAllBtn = new Button { Content = "全选", Width = 70, Margin = new Thickness(0, 0, 8, 0) };
        var deselectAllBtn = new Button { Content = "取消全选", Width = 70 };
        actionPanel.Children.Add(selectAllBtn);
        actionPanel.Children.Add(deselectAllBtn);
        topPanel.Children.Add(searchBox);
        topPanel.Children.Add(actionPanel);
        DockPanel.SetDock(topPanel, Dock.Top);

        var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var okBtn = new Button { Content = "确定", Width = 70, Margin = new Thickness(0, 0, 8, 0) };
        var cancelBtn = new Button { Content = "取消", Width = 70 };
        buttonPanel.Children.Add(okBtn);
        buttonPanel.Children.Add(cancelBtn);
        DockPanel.SetDock(buttonPanel, Dock.Bottom);

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var panel = new StackPanel();
        var checkMap = new Dictionary<DataGridColumn, CheckBox>();
        foreach (var column in DetailDataGrid.Columns)
        {
            var cb = new CheckBox
            {
                Content = column.Header?.ToString() ?? "(无名列)",
                IsChecked = column.Visibility == Visibility.Visible,
                Margin = new Thickness(0, 4, 0, 4)
            };
            checkMap[column] = cb;
            panel.Children.Add(cb);
        }

        void ApplyFilter()
        {
            var keyword = searchBox.Text?.Trim() ?? string.Empty;
            foreach (var cb in checkMap.Values)
            {
                var text = cb.Content?.ToString() ?? string.Empty;
                cb.Visibility = string.IsNullOrWhiteSpace(keyword) ||
                                text.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        searchBox.TextChanged += (_, _) => ApplyFilter();
        selectAllBtn.Click += (_, _) =>
        {
            foreach (var cb in checkMap.Values.Where(x => x.Visibility == Visibility.Visible))
            {
                cb.IsChecked = true;
            }
        };
        deselectAllBtn.Click += (_, _) =>
        {
            foreach (var cb in checkMap.Values.Where(x => x.Visibility == Visibility.Visible))
            {
                cb.IsChecked = false;
            }
        };

        scroll.Content = panel;
        root.Children.Add(topPanel);
        root.Children.Add(buttonPanel);
        root.Children.Add(scroll);
        dialog.Content = root;

        okBtn.Click += (_, _) => dialog.DialogResult = true;
        cancelBtn.Click += (_, _) => dialog.DialogResult = false;

        if (dialog.ShowDialog() == true)
        {
            foreach (var kv in checkMap)
            {
                kv.Key.Visibility = kv.Value.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }

    private void MenuItem_QuickFilter_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var columnName = DetailDataGrid.CurrentCell.Column?.Header?.ToString();
        if (string.IsNullOrWhiteSpace(columnName))
        {
            MessageBox.Show("请先选中一个列单元格，再执行过滤", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new FilterDialog(columnName, vm.GetNextFilterIndex())
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            vm.AddFilter(dialog.FilterName, dialog.ColumnName, dialog.FilterValue);
            vm.StatusMessage = $"已添加筛选: {dialog.FilterName}";
        }
    }

    private async void MenuItem_SqlQuery_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (!vm.IsConnected)
        {
            MessageBox.Show("请先连接数据库", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var suggestedTable = !string.IsNullOrWhiteSpace(vm.CurrentTableName)
            ? vm.CurrentTableName
            : vm.SelectedResult?.TableName;
        var escapedTable = string.IsNullOrWhiteSpace(suggestedTable)
            ? "your_table"
            : suggestedTable.Replace("`", "``");

        var inputWindow = new Window
        {
            Title = "SQL 查询（仅 SELECT）",
            Width = 700,
            Height = 420,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this
        };

        var root = new DockPanel { Margin = new Thickness(12) };
        var sqlBox = new TextBox
        {
            AcceptsReturn = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            TextWrapping = TextWrapping.Wrap,
            Text = $"SELECT * FROM `{escapedTable}` LIMIT 100;"
        };

        var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        var runBtn = new Button { Content = "执行", Width = 80, Margin = new Thickness(0, 0, 8, 0) };
        var cancelBtn = new Button { Content = "取消", Width = 80 };
        buttonPanel.Children.Add(runBtn);
        buttonPanel.Children.Add(cancelBtn);
        DockPanel.SetDock(buttonPanel, Dock.Bottom);

        root.Children.Add(buttonPanel);
        root.Children.Add(sqlBox);
        inputWindow.Content = root;

        runBtn.Click += (_, _) => inputWindow.DialogResult = true;
        cancelBtn.Click += (_, _) => inputWindow.DialogResult = false;

        if (inputWindow.ShowDialog() != true) return;

        try
        {
            var rows = await vm.DatabaseService.ExecuteSelectQueryAsync(sqlBox.Text);
            var dt = BuildDataTableFromRows(rows);
            vm.CurrentDataTable = dt;
            vm.StatusMessage = $"SQL 查询完成，共 {dt.Rows.Count} 条记录";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"SQL 查询失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void MenuItem_ExportQueryResult_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (vm.CurrentDataTable == null)
        {
            MessageBox.Show("当前没有可导出的查询结果", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var exportChoice = MessageBox.Show(
            "导出当前页请选择【是】\n导出全部请选择【否】",
            "导出查询结果",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        if (exportChoice == MessageBoxResult.Cancel)
        {
            return;
        }

        DataTable exportTable;
        if (exportChoice == MessageBoxResult.Yes)
        {
            exportTable = vm.CurrentDataTable.Copy();
        }
        else
        {
            if (string.IsNullOrWhiteSpace(vm.SearchText) && !string.IsNullOrEmpty(vm.CurrentTableName) && vm.IsConnected)
            {
                var rows = await vm.DatabaseService.GetAllTableRowsAsync(vm.CurrentTableName);
                exportTable = BuildDataTableFromRows(rows);
            }
            else if (vm.SelectedResult != null && vm.SelectedResult.MatchedRows.Count > 0)
            {
                exportTable = BuildDataTableFromRows(vm.SelectedResult.MatchedRows);
            }
            else
            {
                exportTable = vm.CurrentDataTable.Copy();
            }
        }

        var saveDialog = new SaveFileDialog
        {
            Filter = "CSV 文件 (*.csv)|*.csv",
            FileName = $"query_result_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (saveDialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            using var writer = new StreamWriter(saveDialog.FileName, false, Encoding.UTF8);
            var headers = exportTable.Columns.Cast<DataColumn>().Select(c => EscapeCsvField(c.ColumnName));
            writer.WriteLine(string.Join(",", headers));

            foreach (DataRow row in exportTable.Rows)
            {
                var values = row.ItemArray.Select(v => EscapeCsvField(v?.ToString() ?? ""));
                writer.WriteLine(string.Join(",", values));
            }

            vm.StatusMessage = $"导出成功：{exportTable.Rows.Count} 行";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static DataTable BuildDataTableFromRows(IReadOnlyList<Dictionary<string, object?>> rows)
    {
        var dt = new DataTable();
        if (rows.Count == 0)
        {
            return dt;
        }

        foreach (var key in rows[0].Keys)
        {
            dt.Columns.Add(key, typeof(string));
        }

        foreach (var row in rows)
        {
            var dr = dt.NewRow();
            foreach (var kv in row)
            {
                dr[kv.Key] = kv.Value?.ToString() ?? "";
            }
            dt.Rows.Add(dr);
        }

        return dt;
    }
}
