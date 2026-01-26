using System.Data;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
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
}
