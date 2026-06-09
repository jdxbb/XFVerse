using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MediaLibrary.Core.Helpers;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Models.Settings;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.App.Views.Dialogs;

public partial class WebDavPathPickerWindow : Window, INotifyPropertyChanged
{
    private const double DirectoryWheelScrollStep = 18d;

    private readonly IWebDavService _webDavService;
    private readonly WebDavConnectionModel _connection;
    private readonly bool _allowMultiple;
    private string _currentPath = "/";
    private string _statusText = "正在读取目录。";
    private bool _isBusy;
    private readonly Stack<string> _forwardPaths = [];
    private WebDavDirectoryItem? _selectedItem;

    public WebDavPathPickerWindow(
        IWebDavService webDavService,
        WebDavConnectionModel connection,
        string? initialPath,
        bool allowMultiple = false)
    {
        _webDavService = webDavService;
        _connection = connection;
        _allowMultiple = allowMultiple;
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
                OnPropertyChanged(nameof(CanGoParent));
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
                OnPropertyChanged(nameof(CanEnterSelected));
            }
        }
    }

    public bool IsNotBusy => !IsBusy;

    public bool CanGoParent => !IsBusy && CurrentPath != "/";

    public bool CanGoForward => !IsBusy && _forwardPaths.Count > 0;

    public bool CanEnterSelected => !IsBusy && SelectedItem is not null;

    public SelectionMode DirectorySelectionMode => _allowMultiple ? SelectionMode.Multiple : SelectionMode.Single;

    public string SelectedPath { get; private set; } = "/";

    public IReadOnlyList<string> SelectedPaths { get; private set; } = ["/"];

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
        if (_isBusy || CurrentPath == "/")
        {
            return;
        }

        var currentPath = CurrentPath;
        if (await LoadDirectoryAsync(GetParentPath(CurrentPath)))
        {
            _forwardPaths.Push(currentPath);
            OnPropertyChanged(nameof(CanGoForward));
        }
    }

    private async void ForwardButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy || _forwardPaths.Count == 0)
        {
            return;
        }

        var nextPath = _forwardPaths.Peek();
        if (await LoadDirectoryAsync(nextPath))
        {
            _forwardPaths.Pop();
            OnPropertyChanged(nameof(CanGoForward));
        }
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

    private void DirectoryItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount > 1)
        {
            return;
        }

        if (e.OriginalSource is DependencyObject source
            && FindAncestor<Button>(source) is not null)
        {
            return;
        }

        e.Handled = true;
    }

    private void SelectionDotButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DependencyObject source
            || FindAncestor<ListBoxItem>(source) is not { } item)
        {
            return;
        }

        item.IsSelected = !item.IsSelected;
        SelectedItem = DirectoryList.SelectedItem as WebDavDirectoryItem;
        e.Handled = true;
    }

    private void DirectoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelectedItem = DirectoryList.SelectedItem as WebDavDirectoryItem;
    }

    private void DirectoryList_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ListBox listBox)
        {
            return;
        }

        var scrollViewer = FindDescendant<ScrollViewer>(listBox);
        if (scrollViewer is null
            || scrollViewer.ScrollableHeight <= 0
            || (e.Delta < 0 && scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight)
            || (e.Delta > 0 && scrollViewer.VerticalOffset <= 0))
        {
            return;
        }

        var direction = e.Delta > 0 ? -1d : 1d;
        var targetOffset = Math.Clamp(
            scrollViewer.VerticalOffset + (DirectoryWheelScrollStep * direction),
            0d,
            scrollViewer.ScrollableHeight);
        scrollViewer.ScrollToVerticalOffset(targetOffset);
        e.Handled = true;
    }

    private void SelectCurrentButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        var selectedPaths = DirectoryList.SelectedItems
            .OfType<WebDavDirectoryItem>()
            .Select(item => WebDavPathHelper.NormalizeVirtualPath(item.Path))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        SelectedPaths = selectedPaths.Length > 0
            ? selectedPaths
            : [WebDavPathHelper.NormalizeVirtualPath(CurrentPath)];
        SelectedPath = SelectedPaths[0];
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

        if (await LoadDirectoryAsync(SelectedItem.Path))
        {
            ClearForwardHistory();
        }
    }

    private async Task<bool> LoadDirectoryAsync(string path)
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
            return true;
        }
        catch (Exception exception)
        {
            StatusText = DescribeSafeError(exception);
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ClearForwardHistory()
    {
        if (_forwardPaths.Count == 0)
        {
            return;
        }

        _forwardPaths.Clear();
        OnPropertyChanged(nameof(CanGoForward));
    }

    private static T? FindDescendant<T>(DependencyObject source)
        where T : DependencyObject
    {
        var childrenCount = VisualTreeHelper.GetChildrenCount(source);
        for (var i = 0; i < childrenCount; i++)
        {
            var child = VisualTreeHelper.GetChild(source, i);
            if (child is T match)
            {
                return match;
            }

            var descendant = FindDescendant<T>(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private static T? FindAncestor<T>(DependencyObject source)
        where T : DependencyObject
    {
        var parent = VisualTreeHelper.GetParent(source);
        while (parent is not null)
        {
            if (parent is T match)
            {
                return match;
            }

            parent = VisualTreeHelper.GetParent(parent);
        }

        return null;
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy != value)
            {
                _isBusy = value;
                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(IsNotBusy));
                OnPropertyChanged(nameof(CanGoParent));
                OnPropertyChanged(nameof(CanGoForward));
                OnPropertyChanged(nameof(CanEnterSelected));
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
