using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using navidoge.Models;

namespace navidoge;

/// <summary>
/// 数据库配置管理窗口
/// </summary>
public partial class DatabaseConfigWindow : Window, INotifyPropertyChanged
{
    private DatabaseProfile? _selectedProfile;
    private DatabaseProfile? _editingProfile;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>配置列表</summary>
    public ObservableCollection<DatabaseProfile> Profiles { get; }

    /// <summary>选中的配置</summary>
    public DatabaseProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            _selectedProfile = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelection));
            
            // 复制到编辑区域
            if (value != null)
            {
                EditingProfile = value.Clone();
                PasswordBox.Password = value.Password;
            }
            else
            {
                EditingProfile = null;
                PasswordBox.Password = "";
            }
        }
    }

    /// <summary>正在编辑的配置</summary>
    public DatabaseProfile? EditingProfile
    {
        get => _editingProfile;
        set
        {
            _editingProfile = value;
            OnPropertyChanged();
        }
    }

    /// <summary>是否有选中项</summary>
    public bool HasSelection => SelectedProfile != null;

    public DatabaseConfigWindow(List<DatabaseProfile> profiles)
    {
        InitializeComponent();
        DataContext = this;

        Profiles = new ObservableCollection<DatabaseProfile>(profiles.Select(p => p.Clone()));
        
        if (Profiles.Count > 0)
        {
            SelectedProfile = Profiles[0];
        }
    }

    /// <summary>获取编辑后的配置列表</summary>
    public List<DatabaseProfile> GetProfiles() => Profiles.ToList();

    private void AddConfig_Click(object sender, RoutedEventArgs e)
    {
        var newProfile = new DatabaseProfile
        {
            Alias = $"新配置 {Profiles.Count + 1}",
            Host = "localhost",
            Port = "3306",
            Database = "",
            Username = "root",
            Password = ""
        };
        Profiles.Add(newProfile);
        SelectedProfile = newProfile;
    }

    private void DeleteConfig_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedProfile == null) return;

        var result = MessageBox.Show(
            $"确定要删除配置 \"{SelectedProfile.Alias}\" 吗？",
            "确认删除",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            var index = Profiles.IndexOf(SelectedProfile);
            Profiles.Remove(SelectedProfile);
            
            if (Profiles.Count > 0)
            {
                SelectedProfile = Profiles[Math.Min(index, Profiles.Count - 1)];
            }
            else
            {
                SelectedProfile = null;
            }
        }
    }

    private void SaveConfig_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedProfile == null || EditingProfile == null) return;

        if (string.IsNullOrWhiteSpace(EditingProfile.Alias))
        {
            MessageBox.Show("请输入配置别名", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // 更新选中项
        SelectedProfile.Alias = EditingProfile.Alias;
        SelectedProfile.Host = EditingProfile.Host;
        SelectedProfile.Port = EditingProfile.Port;
        SelectedProfile.Database = EditingProfile.Database;
        SelectedProfile.Username = EditingProfile.Username;
        SelectedProfile.Password = EditingProfile.Password;

        // 刷新列表显示
        var index = Profiles.IndexOf(SelectedProfile);
        if (index >= 0)
        {
            var profile = SelectedProfile;
            Profiles.RemoveAt(index);
            Profiles.Insert(index, profile);
            SelectedProfile = profile;
        }

        MessageBox.Show("配置已保存", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (EditingProfile != null)
        {
            EditingProfile.Password = PasswordBox.Password;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
