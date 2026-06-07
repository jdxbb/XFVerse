using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using MediaLibrary.Core.Helpers;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Models.Settings;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.App.Views.Dialogs;

public partial class WebDavPathPickerWindow : Window, INotifyPropertyChanged
{
    private readonly IWebDavService _webDavService;
    private readonly WebDavConnectionModel _connection;
    private string _currentPath = "/";
    private string _statusText = "正在读取目录。";
    private bool _isBusy;
    private WebDavDirectoryItem? _selectedItem;

    public WebDavPathPickerWindow(
        IWebDavService webDavService,
        WebDavConnectionModel connection,
        string? initialPath)
    {
        _webDavService = webDavService;
        _connection = connection;
        _currentPath = WebDavPathHelper.NormalizeVirtualPath(initialPath ?? "/");

        InitializeComponent();
        DataContext = this;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<WebDavDirectoryItem> DirectoryItems { get; } = [];

    public string CurrentPath
    {
        get => _currentPath;
        private set
        {
            if (_currentPath != value)
            {
                _currentPath = value;
                OnPropertyChanged(nameof(CurrentPath));
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (_statusText != value)
            {
                _statusText = value;
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public WebDavDirectoryItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (!ReferenceEquals(_selectedItem, value))
            {
                _selectedItem = value;
                OnPropertyChanged(nameof(SelectedItem));
            }
        }
    }

    public string SelectedPath { get; private set; } = "/";

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadDirectoryAsync(CurrentPath);
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch
        {
            // DragMove can throw if the mouse capture is interrupted by the shell.
        }
    }

    private async void ParentButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        await LoadDirectoryAsync(GetParentPath(CurrentPath));
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        await LoadDirectoryAsync(CurrentPath);
    }

    private async void EnterSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        await NavigateToSelectedAsync();
    }

    private async void DirectoryList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        await NavigateToSelectedAsync();
    }

    private void SelectCurrentButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedPath = CurrentPath;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private async Task NavigateToSelectedAsync()
    {
        if (_isBusy || SelectedItem is null)
        {
            return;
        }

        await LoadDirectoryAsync(SelectedItem.Path);
    }

    private async Task LoadDirectoryAsync(string path)
    {
        var normalizedPath = WebDavPathHelper.NormalizeVirtualPath(path);
        IsBusy = true;
        StatusText = "正在读取目录。";

        try
        {
            var entries = await _webDavService.ListDirectoryAsync(_connection, normalizedPath);
            var directories = entries
                .Where(entry => entry.IsDirectory)
                .Select(entry => new WebDavDirectoryItem(entry))
                .ToList();

            CurrentPath = normalizedPath;
            DirectoryItems.Clear();
            foreach (var directory in directories)
            {
                DirectoryItems.Add(directory);
            }

            SelectedItem = null;
            StatusText = directories.Count == 0
                ? "当前目录没有可进入的子目录，可以直接选择当前目录。"
                : $"已加载 {directories.Count} 个子目录。";
        }
        catch (Exception exception)
        {
            StatusText = DescribeSafeError(exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (_isBusy != value)
            {
                _isBusy = value;
                OnPropertyChanged(nameof(IsBusy));
            }
        }
    }

    private static string GetParentPath(string path)
    {
        var normalizedPath = WebDavPathHelper.NormalizeVirtualPath(path);
        if (normalizedPath == "/")
        {
            return "/";
        }

        var segments = normalizedPath
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return segments.Length <= 1
            ? "/"
            : WebDavPathHelper.NormalizeVirtualPath(string.Join("/", segments.Take(segments.Length - 1)));
    }

    private static string DescribeSafeError(Exception exception)
    {
        return exception switch
        {
            TaskCanceledException => "目录读取超时，请检查网络或服务端响应。",
            InvalidOperationException invalidOperationException => invalidOperationException.Message,
            _ => $"目录读取失败：{exception.GetType().Name}"
        };
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public sealed class WebDavDirectoryItem
    {
        public WebDavDirectoryItem(RemoteEntry entry)
        {
            Name = string.IsNullOrWhiteSpace(entry.Name)
                ? WebDavPathHelper.GetFileName(entry.Path)
                : entry.Name;
            Path = WebDavPathHelper.NormalizeVirtualPath(entry.Path);
        }

        public string Name { get; }

        public string Path { get; }
    }
}
