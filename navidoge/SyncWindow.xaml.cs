using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using navidoge.Models;
using navidoge.ViewModels;

namespace navidoge;

/// <summary>
/// SyncWindow.xaml 的交互逻辑
/// </summary>
public partial class SyncWindow : Window
{
    public SyncWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // 确保同步源配置已初始化
        if (viewModel.SyncSourceProfile == null)
        {
            viewModel.SyncSourceProfile = viewModel.SelectedConnectionProfile ?? viewModel.ConnectionProfiles.FirstOrDefault();
        }

        // 初始化同步表列表（异步加载）
        Loaded += async (s, e) => await viewModel.InitializeSyncTablesAsync();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// 交换同步方向
    /// </summary>
    private void SwapDirection_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.SwapSyncDirection();
        }
    }

    /// <summary>
    /// 双击切换表的勾选状态
    /// </summary>
    private void TableListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SyncTableListBox.SelectedItem is TableInfo tableInfo)
        {
            tableInfo.IsSyncSelected = !tableInfo.IsSyncSelected;
            if (DataContext is MainViewModel vm)
            {
                vm.RefreshSelectedSyncTables();
            }
        }
    }

    /// <summary>
    /// 右键菜单：添加到已选择
    /// </summary>
    private void MenuItem_AddToSelected_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            var selectedTables = SyncTableListBox.SelectedItems.Cast<TableInfo>().ToList();
            vm.AddToSelectedSyncTables(selectedTables);
        }
    }

    /// <summary>
    /// 右键菜单：取消选择
    /// </summary>
    private void MenuItem_RemoveFromSelected_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            var selectedTables = SelectedSyncTableListBox.SelectedItems.Cast<TableInfo>().ToList();
            vm.RemoveFromSelectedSyncTables(selectedTables);
        }
    }

    /// <summary>
    /// 开始同步（带确认）
    /// </summary>
    private async void StartSync_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var sourceProfile = vm.SyncSourceProfile;
        var targetProfile = vm.SyncTargetProfile;

        if (sourceProfile == null)
        {
            MessageBox.Show("请选择源配置", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (targetProfile == null)
        {
            MessageBox.Show("请选择目标配置", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var selectedCount = vm.SelectedSyncTables.Count;
        if (selectedCount == 0)
        {
            MessageBox.Show("请选择要同步的表（双击表名或右键添加）", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var timeRange = "";
        if (vm.SyncStartTime.HasValue || vm.SyncEndTime.HasValue)
        {
            var start = vm.SyncStartTime?.ToString("yyyy-MM-dd") ?? "不限";
            var end = vm.SyncEndTime?.ToString("yyyy-MM-dd") ?? "不限";
            timeRange = $"\n时间范围: {start} ~ {end}";
        }

        var message = $"确认同步数据？\n\n" +
                      $"源: {sourceProfile.DisplayText}\n" +
                      $"    ↓\n" +
                      $"目标: {targetProfile.DisplayText}\n\n" +
                      $"同步表数量: {selectedCount} 个{timeRange}";

        var result = MessageBox.Show(
            message,
            "确认同步",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            await vm.ExecuteSyncAsync();
        }
    }
}
