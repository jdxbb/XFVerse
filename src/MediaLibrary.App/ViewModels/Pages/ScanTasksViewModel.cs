using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using MediaLibrary.App.Services.Implementations;
using MediaLibrary.App.Services.Interfaces;
using MediaLibrary.App.ViewModels.Base;
using MediaLibrary.Core.Helpers;
using MediaLibrary.Core.Models.Entities;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Models.Settings;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.App.ViewModels.Pages;

public sealed class ScanTasksViewModel : PageViewModelBase
{
    private readonly IMediaScanService _mediaScanService;
    private readonly ILocalMediaScanService _localMediaScanService;
    private readonly ISettingsService _settingsService;
    private readonly IWebDavService _webDavService;
    private readonly IScanPathPickerService _scanPathPickerService;
    private readonly IDataRefreshService _dataRefreshService;
    private readonly List<ScanTaskLogViewModel> _webDavRecentLogs = [];
    private readonly List<ScanTaskLogViewModel> _localRecentLogs = [];
    private CancellationTokenSource? _scanCts;
    private int? _connectionId;
    private bool _hasConnection;
    private bool _isRunning;
    private bool _isConnectionEnabled = true;
    private int? _editingScanPathId;
    private int? _editingLocalScanPathId;
    private string _connectionName = "WebDAV";
    private string _baseUrl = string.Empty;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private string _lastScanAtText = "尚未扫描";
    private string _statusMessage = "点击“开始扫描网盘”后，系统会扫描当前已启用的 WebDAV 路径。";
    private string _connectionStatusMessage = "请先保存 WebDAV 连接配置。";
    private string _scanPathStatusMessage = "当前还没有扫描路径。";
    private string _localScanPathStatusMessage = "当前还没有本地目录配置。";
    private string _editingScanPathValue = string.Empty;
    private string _editingScanPathDisplayName = string.Empty;
    private string _editingLocalScanPathValue = string.Empty;
    private string _editingLocalScanPathDisplayName = string.Empty;
    private bool _editingScanPathEnabled = true;
    private bool _editingScanPathRecursive = true;
    private bool _editingLocalScanPathEnabled = true;
    private bool _editingLocalScanPathRecursive = true;
    private int _scannedCount;
    private int _newFileCount;
    private int _updatedFileCount;
    private int _ignoredFileCount;
    private int _errorCount;
    private string _currentStageText = "当前阶段：等待";
    private string _currentFileText = "尚未开始扫描。";
    private string _elapsedText = "--";

    public ScanTasksViewModel(
        IMediaScanService mediaScanService,
        ILocalMediaScanService localMediaScanService,
        ISettingsService settingsService,
        IWebDavService webDavService,
        IScanPathPickerService scanPathPickerService,
        IDataRefreshService dataRefreshService)
        : base("扫描任务", "管理 WebDAV 连接、扫描路径、扫描进度与最近扫描记录。")
    {
        _mediaScanService = mediaScanService;
        _localMediaScanService = localMediaScanService;
        _settingsService = settingsService;
        _webDavService = webDavService;
        _scanPathPickerService = scanPathPickerService;
        _dataRefreshService = dataRefreshService;

        RefreshCommand = new AsyncRelayCommand(() => LoadAsync(), () => !IsRunning);
        SaveConnectionCommand = new AsyncRelayCommand(SaveConnectionAsync, () => !IsRunning);
        TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync, () => !IsRunning);
        BeginAddScanPathCommand = new RelayCommand(BeginAddScanPath, () => !IsRunning);
        PickWebDavScanPathCommand = new AsyncRelayCommand(PickWebDavScanPathAsync, () => !IsRunning);
        SaveScanPathCommand = new AsyncRelayCommand(SaveScanPathAsync, () => !IsRunning);
        EditScanPathCommand = new RelayCommand(EditScanPath, _ => !IsRunning);
        DeleteScanPathCommand = new AsyncRelayCommand(DeleteScanPathAsync, _ => !IsRunning);
        ToggleScanPathCommand = new AsyncRelayCommand(ToggleScanPathAsync, _ => !IsRunning);
        CancelEditScanPathCommand = new RelayCommand(CancelEditScanPath, () => !IsRunning);
        BeginAddLocalScanPathCommand = new RelayCommand(BeginAddLocalScanPath, () => !IsRunning);
        PickLocalScanPathCommand = new AsyncRelayCommand(PickLocalScanPathAsync, () => !IsRunning);
        SaveLocalScanPathCommand = new AsyncRelayCommand(SaveLocalScanPathAsync, () => !IsRunning);
        EditLocalScanPathCommand = new RelayCommand(EditLocalScanPath, _ => !IsRunning);
        DeleteLocalScanPathCommand = new AsyncRelayCommand(DeleteLocalScanPathAsync, _ => !IsRunning);
        ToggleLocalScanPathCommand = new AsyncRelayCommand(ToggleLocalScanPathAsync, _ => !IsRunning);
        CancelEditLocalScanPathCommand = new RelayCommand(CancelEditLocalScanPath, () => !IsRunning);
        RunLocalScanCommand = new AsyncRelayCommand(RunLocalScanAsync, () => CanRunLocalScan);
        RunLocalScanPathCommand = new AsyncRelayCommand(RunLocalScanPathAsync, _ => !IsRunning);
        RunScanCommand = new AsyncRelayCommand(RunScanAsync, () => CanRunScan);
        CancelScanCommand = new RelayCommand(CancelScan, () => IsRunning);
    }

    public ObservableCollection<ScanPathViewModel> ScanPaths { get; } = [];

    public ObservableCollection<ScanPathViewModel> LocalScanPaths { get; } = [];

    public ObservableCollection<ScanTaskLogViewModel> RecentLogs { get; } = [];

    public AsyncRelayCommand RefreshCommand { get; }

    public AsyncRelayCommand SaveConnectionCommand { get; }

    public AsyncRelayCommand TestConnectionCommand { get; }

    public RelayCommand BeginAddScanPathCommand { get; }

    public AsyncRelayCommand PickWebDavScanPathCommand { get; }

    public AsyncRelayCommand SaveScanPathCommand { get; }

    public RelayCommand EditScanPathCommand { get; }

    public AsyncRelayCommand DeleteScanPathCommand { get; }

    public AsyncRelayCommand ToggleScanPathCommand { get; }

    public RelayCommand CancelEditScanPathCommand { get; }

    public RelayCommand BeginAddLocalScanPathCommand { get; }

    public AsyncRelayCommand PickLocalScanPathCommand { get; }

    public AsyncRelayCommand SaveLocalScanPathCommand { get; }

    public RelayCommand EditLocalScanPathCommand { get; }

    public AsyncRelayCommand DeleteLocalScanPathCommand { get; }

    public AsyncRelayCommand ToggleLocalScanPathCommand { get; }

    public RelayCommand CancelEditLocalScanPathCommand { get; }

    public AsyncRelayCommand RunLocalScanCommand { get; }

    public AsyncRelayCommand RunLocalScanPathCommand { get; }

    public AsyncRelayCommand RunScanCommand { get; }

    public RelayCommand CancelScanCommand { get; }

    public override bool IsRefreshing => IsRunning;

    public int? ConnectionId
    {
        get => _connectionId;
        private set
        {
            if (SetProperty(ref _connectionId, value))
            {
                OnPropertyChanged(nameof(HasSavedConnection));
                RaiseCommandStates();
            }
        }
    }

    public bool HasConnection
    {
        get => _hasConnection;
        private set
        {
            if (SetProperty(ref _hasConnection, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public bool HasSavedConnection => ConnectionId.HasValue;

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value))
            {
                OnPropertyChanged(nameof(IsRefreshing));
                OnPropertyChanged(nameof(CanRunScan));
                OnPropertyChanged(nameof(CanRunLocalScan));
                OnPropertyChanged(nameof(IsProgressIndeterminate));
                OnPropertyChanged(nameof(RunScanButtonText));
                OnPropertyChanged(nameof(LocalRunScanButtonText));
                RaiseCommandStates();
            }
        }
    }

    public string ConnectionName
    {
        get => _connectionName;
        set => SetProperty(ref _connectionName, value);
    }

    public string BaseUrl
    {
        get => _baseUrl;
        set => SetProperty(ref _baseUrl, value);
    }

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public bool IsConnectionEnabled
    {
        get => _isConnectionEnabled;
        set
        {
            if (SetProperty(ref _isConnectionEnabled, value))
            {
                OnPropertyChanged(nameof(CanRunScan));
                RaiseCommandStates();
            }
        }
    }

    public string LastScanAtText
    {
        get => _lastScanAtText;
        private set => SetProperty(ref _lastScanAtText, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string ConnectionStatusMessage
    {
        get => _connectionStatusMessage;
        private set => SetProperty(ref _connectionStatusMessage, value);
    }

    public string ScanPathStatusMessage
    {
        get => _scanPathStatusMessage;
        private set => SetProperty(ref _scanPathStatusMessage, value);
    }

    public string LocalScanPathStatusMessage
    {
        get => _localScanPathStatusMessage;
        private set => SetProperty(ref _localScanPathStatusMessage, value);
    }

    public int? EditingScanPathId
    {
        get => _editingScanPathId;
        private set
        {
            if (SetProperty(ref _editingScanPathId, value))
            {
                OnPropertyChanged(nameof(IsEditingExistingScanPath));
                OnPropertyChanged(nameof(ScanPathEditorTitle));
                OnPropertyChanged(nameof(ScanPathSubmitButtonText));
            }
        }
    }

    public string EditingScanPathValue
    {
        get => _editingScanPathValue;
        set => SetProperty(ref _editingScanPathValue, value);
    }

    public string EditingScanPathDisplayName
    {
        get => _editingScanPathDisplayName;
        set => SetProperty(ref _editingScanPathDisplayName, value);
    }

    public bool EditingScanPathEnabled
    {
        get => _editingScanPathEnabled;
        set => SetProperty(ref _editingScanPathEnabled, value);
    }

    public bool EditingScanPathRecursive
    {
        get => _editingScanPathRecursive;
        set => SetProperty(ref _editingScanPathRecursive, value);
    }

    public int? EditingLocalScanPathId
    {
        get => _editingLocalScanPathId;
        private set
        {
            if (SetProperty(ref _editingLocalScanPathId, value))
            {
                OnPropertyChanged(nameof(IsEditingExistingLocalScanPath));
                OnPropertyChanged(nameof(LocalScanPathEditorTitle));
                OnPropertyChanged(nameof(LocalScanPathSubmitButtonText));
            }
        }
    }

    public string EditingLocalScanPathValue
    {
        get => _editingLocalScanPathValue;
        set => SetProperty(ref _editingLocalScanPathValue, value);
    }

    public string EditingLocalScanPathDisplayName
    {
        get => _editingLocalScanPathDisplayName;
        set => SetProperty(ref _editingLocalScanPathDisplayName, value);
    }

    public bool EditingLocalScanPathEnabled
    {
        get => _editingLocalScanPathEnabled;
        set => SetProperty(ref _editingLocalScanPathEnabled, value);
    }

    public bool EditingLocalScanPathRecursive
    {
        get => _editingLocalScanPathRecursive;
        set => SetProperty(ref _editingLocalScanPathRecursive, value);
    }

    public bool IsEditingExistingScanPath => EditingScanPathId.HasValue;

    public string ScanPathEditorTitle => IsEditingExistingScanPath ? "编辑扫描路径" : "新增扫描路径";

    public string ScanPathSubmitButtonText => IsEditingExistingScanPath ? "保存修改" : "新增路径";

    public bool IsEditingExistingLocalScanPath => EditingLocalScanPathId.HasValue;

    public string LocalScanPathEditorTitle => IsEditingExistingLocalScanPath ? "编辑本地目录" : "新增本地目录";

    public string LocalScanPathSubmitButtonText => IsEditingExistingLocalScanPath ? "保存修改" : "添加目录";

    public bool HasScanPaths => ScanPaths.Count > 0;

    public bool HasLocalScanPaths => LocalScanPaths.Count > 0;

    public bool HasRecentLogs => RecentLogs.Count > 0;

    public int EnabledScanPathCount => ScanPaths.Count(x => x.IsEnabled);

    public int EnabledLocalScanPathCount => LocalScanPaths.Count(x => x.IsEnabled);

    public bool CanRunScan => HasConnection && IsConnectionEnabled && EnabledScanPathCount > 0 && !IsRunning;

    public bool CanRunLocalScan => EnabledLocalScanPathCount > 0 && !IsRunning;

    public bool IsProgressIndeterminate => IsRunning;

    public string RunScanButtonText => IsRunning ? "扫描中..." : "开始扫描网盘";

    public string LocalRunScanButtonText => IsRunning ? "扫描中..." : "开始扫描本地文件";

    public string CurrentFileText
    {
        get => _currentFileText;
        private set => SetProperty(ref _currentFileText, value);
    }

    public string CurrentStageText
    {
        get => _currentStageText;
        private set => SetProperty(ref _currentStageText, value);
    }

    public int ScannedCount
    {
        get => _scannedCount;
        private set => SetProperty(ref _scannedCount, value);
    }

    public int NewFileCount
    {
        get => _newFileCount;
        private set => SetProperty(ref _newFileCount, value);
    }

    public int UpdatedFileCount
    {
        get => _updatedFileCount;
        private set => SetProperty(ref _updatedFileCount, value);
    }

    public int IgnoredFileCount
    {
        get => _ignoredFileCount;
        private set => SetProperty(ref _ignoredFileCount, value);
    }

    public int ErrorCount
    {
        get => _errorCount;
        private set => SetProperty(ref _errorCount, value);
    }

    public string ElapsedText
    {
        get => _elapsedText;
        private set => SetProperty(ref _elapsedText, value);
    }

    public override Task ActivateAsync(CancellationToken cancellationToken = default)
    {
        return LoadAsync(cancellationToken);
    }

    private async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = await _settingsService.GetPrimaryConnectionAsync(cancellationToken);
            ApplyConnection(connection);

            if (ConnectionId.HasValue)
            {
                await LoadScanPathsAsync(ConnectionId.Value, cancellationToken);
            }
            else
            {
                ScanPaths.Clear();
                ScanPathStatusMessage = "先保存 WebDAV 连接，再添加扫描路径。";
                RaiseScanPathStateChanged();
            }

            await LoadLocalOverviewAsync(cancellationToken);
            await LoadOverviewAsync(cancellationToken);
            StatusMessage = BuildReadyStatus();
        }
        catch (Exception exception)
        {
            StatusMessage = $"加载扫描任务失败：{exception.Message}";
        }
    }

    private void ApplyConnection(WebDavConnectionModel connection)
    {
        ConnectionId = connection.Id;
        ConnectionName = string.IsNullOrWhiteSpace(connection.Name) ? "WebDAV" : connection.Name;
        BaseUrl = connection.BaseUrl;
        Username = connection.Username;
        Password = connection.Password;
        IsConnectionEnabled = connection.IsEnabled;
        HasConnection = connection.Id.HasValue;
        LastScanAtText = connection.LastScanAt.HasValue
            ? ToLocalDisplayTime(connection.LastScanAt.Value).ToString("yyyy-MM-dd HH:mm:ss")
            : "尚未扫描";
        ConnectionStatusMessage = ConnectionId.HasValue
            ? "已加载当前 WebDAV 连接配置。"
            : "当前还没有保存 WebDAV 连接。";
    }

    private async Task LoadScanPathsAsync(int sourceConnectionId, CancellationToken cancellationToken)
    {
        var paths = await _settingsService.GetScanPathsAsync(sourceConnectionId, cancellationToken);
        var lastScanByPathId = RecentLogs
            .Where(x => x.ScanPathId.HasValue)
            .GroupBy(x => x.ScanPathId!.Value)
            .ToDictionary(
                x => x.Key,
                x => x.Max(item => item.StartedAt),
                EqualityComparer<int>.Default);

        ScanPaths.Clear();
        foreach (var path in paths)
        {
            var lastScanAt = lastScanByPathId.TryGetValue(path.Id, out var value) ? value : (DateTime?)null;
            ScanPaths.Add(new ScanPathViewModel(path, lastScanAt));
        }

        ScanPathStatusMessage = ScanPaths.Count == 0
            ? "当前还没有扫描路径。"
            : $"已加载 {ScanPaths.Count} 条扫描路径，启用 {EnabledScanPathCount} 条。";
        RaiseScanPathStateChanged();
    }

    private async Task LoadLocalScanPathsAsync(CancellationToken cancellationToken)
    {
        var paths = await _settingsService.GetLocalScanPathsAsync(cancellationToken);

        LocalScanPaths.Clear();
        foreach (var path in paths)
        {
            LocalScanPaths.Add(new ScanPathViewModel(path, null));
        }

        LocalScanPathStatusMessage = LocalScanPaths.Count == 0
            ? "当前还没有本地目录配置。"
            : $"已加载 {LocalScanPaths.Count} 个本地目录，启用 {EnabledLocalScanPathCount} 个。";
        RaiseLocalScanPathStateChanged();
    }

    private async Task LoadLocalOverviewAsync(CancellationToken cancellationToken)
    {
        await LoadLocalScanPathsAsync(cancellationToken);

        var overview = await _localMediaScanService.GetOverviewAsync(cancellationToken);
        _localRecentLogs.Clear();
        foreach (var log in overview.RecentLogs)
        {
            _localRecentLogs.Add(new ScanTaskLogViewModel(log, string.Empty, string.Empty, "本地", isLocal: true));
        }

        RefreshUnifiedRecentLogs();
    }

    private async Task LoadOverviewAsync(CancellationToken cancellationToken)
    {
        var overview = await _mediaScanService.GetOverviewAsync(cancellationToken);
        _webDavRecentLogs.Clear();
        foreach (var log in overview.RecentLogs)
        {
            _webDavRecentLogs.Add(new ScanTaskLogViewModel(log, BaseUrl, Username, "网盘"));
        }

        LastScanAtText = overview.LastScanAt.HasValue
            ? overview.LastScanAt.Value.ToString("yyyy-MM-dd HH:mm:ss")
            : LastScanAtText;
        RefreshUnifiedRecentLogs();

        if (ConnectionId.HasValue)
        {
            await LoadScanPathsAsync(ConnectionId.Value, cancellationToken);
        }
    }

    private void RefreshUnifiedRecentLogs()
    {
        RecentLogs.Clear();
        foreach (var log in _webDavRecentLogs
            .Concat(_localRecentLogs)
            .OrderByDescending(x => x.StartedAt))
        {
            RecentLogs.Add(log);
        }

        OnPropertyChanged(nameof(HasRecentLogs));
    }

    private string BuildReadyStatus()
    {
        if (!HasConnection)
        {
            return "当前还没有可用的 WebDAV 连接。请先在扫描任务页保存连接配置。";
        }

        if (!IsConnectionEnabled)
        {
            return "当前 WebDAV 连接已停用。";
        }

        return EnabledScanPathCount == 0
            ? "当前没有启用的扫描路径。请先启用至少一个 WebDAV 扫描路径。"
            : "扫描任务页已加载，可手动发起扫描。";
    }

    private async Task SaveConnectionAsync()
    {
        try
        {
            var connection = await _settingsService.SaveConnectionAsync(
                new WebDavConnectionModel
                {
                    Id = ConnectionId,
                    Name = string.IsNullOrWhiteSpace(ConnectionName) ? "WebDAV" : ConnectionName,
                    BaseUrl = BaseUrl,
                    Username = Username,
                    Password = Password,
                    IsEnabled = IsConnectionEnabled
                });

            ApplyConnection(connection);
            ConnectionStatusMessage = "WebDAV 连接配置已保存。";

            if (ConnectionId.HasValue)
            {
                await LoadScanPathsAsync(ConnectionId.Value, CancellationToken.None);
                await LoadOverviewAsync(CancellationToken.None);
            }
        }
        catch (Exception exception)
        {
            ConnectionStatusMessage = exception.Message;
        }
    }

    private async Task TestConnectionAsync()
    {
        try
        {
            ConnectionStatusMessage = "正在测试 WebDAV 连接。";
            var result = await _webDavService.TestConnectionAsync(
                new WebDavConnectionModel
                {
                    Id = ConnectionId,
                    Name = string.IsNullOrWhiteSpace(ConnectionName) ? "WebDAV" : ConnectionName,
                    BaseUrl = BaseUrl,
                    Username = Username,
                    Password = Password,
                    IsEnabled = IsConnectionEnabled
                });

            ConnectionStatusMessage = result.Message;
        }
        catch (Exception exception)
        {
            ConnectionStatusMessage = $"测试连接失败：{exception.Message}";
        }
    }

    private void BeginAddScanPath()
    {
        if (!HasSavedConnection)
        {
            ScanPathStatusMessage = "请先保存 WebDAV 连接配置。";
            return;
        }

        EditingScanPathId = null;
        EditingScanPathValue = "/";
        EditingScanPathDisplayName = string.Empty;
        EditingScanPathEnabled = true;
        EditingScanPathRecursive = true;
        ScanPathStatusMessage = "正在新增 WebDAV 扫描路径。";
    }

    private void EditScanPath(object? parameter)
    {
        if (parameter is not ScanPathViewModel scanPath)
        {
            return;
        }

        EditingScanPathId = scanPath.Id;
        EditingScanPathValue = scanPath.Path;
        EditingScanPathDisplayName = scanPath.DisplayName;
        EditingScanPathEnabled = scanPath.IsEnabled;
        EditingScanPathRecursive = scanPath.IsRecursive;
        ScanPathStatusMessage = $"正在编辑：{scanPath.DisplayName}";
    }

    private async Task PickWebDavScanPathAsync()
    {
        if (string.IsNullOrWhiteSpace(BaseUrl))
        {
            ScanPathStatusMessage = "请先填写 WebDAV BaseUrl，再选择远端目录。";
            return;
        }

        var selectedPath = await _scanPathPickerService.PickWebDavDirectoryAsync(
            BuildCurrentWebDavConnectionModel(),
            string.IsNullOrWhiteSpace(EditingScanPathValue) ? "/" : EditingScanPathValue);
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        EditingScanPathValue = selectedPath;
        if (string.IsNullOrWhiteSpace(EditingScanPathDisplayName))
        {
            EditingScanPathDisplayName = BuildDisplayNameFromVirtualPath(selectedPath);
        }

        ScanPathStatusMessage = "已选择 WebDAV 目录，请确认显示名称后保存。";
    }

    private async Task SaveScanPathAsync()
    {
        if (!ConnectionId.HasValue)
        {
            ScanPathStatusMessage = "请先保存 WebDAV 连接配置。";
            return;
        }

        try
        {
            var saved = await _settingsService.SaveScanPathAsync(
                new ScanPath
                {
                    Id = EditingScanPathId ?? 0,
                    SourceConnectionId = ConnectionId.Value,
                    Path = EditingScanPathValue,
                    DisplayName = EditingScanPathDisplayName,
                    IsEnabled = EditingScanPathEnabled,
                    IsRecursive = EditingScanPathRecursive
                });

            await LoadOverviewAsync(CancellationToken.None);
            ScanPathStatusMessage = $"扫描路径已保存：{saved.DisplayName}";
            CancelEditScanPath();
        }
        catch (Exception exception)
        {
            ScanPathStatusMessage = exception.Message;
        }
    }

    private async Task DeleteScanPathAsync(object? parameter)
    {
        if (parameter is not ScanPathViewModel scanPath || !ConnectionId.HasValue)
        {
            return;
        }

        await _settingsService.DeleteScanPathAsync(scanPath.Id);
        await LoadOverviewAsync(CancellationToken.None);
        ScanPathStatusMessage = $"已删除扫描路径：{scanPath.DisplayName}";

        if (EditingScanPathId == scanPath.Id)
        {
            CancelEditScanPath();
        }
    }

    private async Task ToggleScanPathAsync(object? parameter)
    {
        if (parameter is not ScanPathViewModel scanPath || !ConnectionId.HasValue)
        {
            return;
        }

        await _settingsService.SetScanPathEnabledAsync(scanPath.Id, !scanPath.IsEnabled);
        await LoadOverviewAsync(CancellationToken.None);
        ScanPathStatusMessage = scanPath.IsEnabled
            ? $"已停用扫描路径：{scanPath.DisplayName}"
            : $"已启用扫描路径：{scanPath.DisplayName}";
    }

    private void CancelEditScanPath()
    {
        EditingScanPathId = null;
        EditingScanPathValue = string.Empty;
        EditingScanPathDisplayName = string.Empty;
        EditingScanPathEnabled = true;
        EditingScanPathRecursive = true;
    }

    private void BeginAddLocalScanPath()
    {
        EditingLocalScanPathId = null;
        EditingLocalScanPathValue = string.Empty;
        EditingLocalScanPathDisplayName = string.Empty;
        EditingLocalScanPathEnabled = true;
        EditingLocalScanPathRecursive = true;
        LocalScanPathStatusMessage = "正在新增本地目录配置。";
    }

    private async Task PickLocalScanPathAsync()
    {
        var selectedPath = await _scanPathPickerService.PickLocalDirectoryAsync(EditingLocalScanPathValue);
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        EditingLocalScanPathValue = selectedPath;
        if (string.IsNullOrWhiteSpace(EditingLocalScanPathDisplayName))
        {
            EditingLocalScanPathDisplayName = BuildDisplayNameFromLocalPath(selectedPath);
        }

        LocalScanPathStatusMessage = "已选择本地目录，请确认显示名称后保存。";
    }

    private void EditLocalScanPath(object? parameter)
    {
        if (parameter is not ScanPathViewModel scanPath)
        {
            return;
        }

        EditingLocalScanPathId = scanPath.Id;
        EditingLocalScanPathValue = scanPath.Path;
        EditingLocalScanPathDisplayName = scanPath.DisplayName;
        EditingLocalScanPathEnabled = scanPath.IsEnabled;
        EditingLocalScanPathRecursive = scanPath.IsRecursive;
        LocalScanPathStatusMessage = $"正在编辑本地目录：{scanPath.DisplayName}";
    }

    private async Task SaveLocalScanPathAsync()
    {
        var pathExists = Directory.Exists((EditingLocalScanPathValue ?? string.Empty).Trim().Trim('"'));
        try
        {
            var saved = await _settingsService.SaveLocalScanPathAsync(
                new ScanPath
                {
                    Id = EditingLocalScanPathId ?? 0,
                    Path = EditingLocalScanPathValue ?? string.Empty,
                    DisplayName = EditingLocalScanPathDisplayName ?? string.Empty,
                    IsEnabled = EditingLocalScanPathEnabled,
                    IsRecursive = EditingLocalScanPathRecursive
                });

            await LoadLocalOverviewAsync(CancellationToken.None);
            LocalScanPathStatusMessage = pathExists
                ? $"本地目录配置已保存：{saved.DisplayName}"
                : $"本地目录配置已保存：{saved.DisplayName}（路径当前不可访问）";
            CancelEditLocalScanPath();
        }
        catch (Exception exception)
        {
            LocalScanPathStatusMessage = exception.Message;
        }
    }

    private async Task DeleteLocalScanPathAsync(object? parameter)
    {
        if (parameter is not ScanPathViewModel scanPath)
        {
            return;
        }

        await _settingsService.DeleteLocalScanPathAsync(scanPath.Id);
        await LoadLocalOverviewAsync(CancellationToken.None);
        LocalScanPathStatusMessage = $"已移除本地目录配置：{scanPath.DisplayName}。仅影响软件内配置和相关记录可见性，不删除真实本地文件。";

        if (EditingLocalScanPathId == scanPath.Id)
        {
            CancelEditLocalScanPath();
        }
    }

    private async Task ToggleLocalScanPathAsync(object? parameter)
    {
        if (parameter is not ScanPathViewModel scanPath)
        {
            return;
        }

        await _settingsService.SetLocalScanPathEnabledAsync(scanPath.Id, !scanPath.IsEnabled);
        await LoadLocalOverviewAsync(CancellationToken.None);
        LocalScanPathStatusMessage = scanPath.IsEnabled
            ? $"已停用本地目录：{scanPath.DisplayName}"
            : $"已启用本地目录：{scanPath.DisplayName}";
    }

    private void CancelEditLocalScanPath()
    {
        EditingLocalScanPathId = null;
        EditingLocalScanPathValue = string.Empty;
        EditingLocalScanPathDisplayName = string.Empty;
        EditingLocalScanPathEnabled = true;
        EditingLocalScanPathRecursive = true;
    }

    private async Task RunLocalScanAsync()
    {
        if (!CanRunLocalScan)
        {
            return;
        }

        await RunLocalScanCoreAsync(progress => _localMediaScanService.RunScanAsync(_scanCts!.Token, progress));
    }

    private async Task RunLocalScanPathAsync(object? parameter)
    {
        if (parameter is not ScanPathViewModel scanPath || IsRunning)
        {
            return;
        }

        await RunLocalScanCoreAsync(progress => _localMediaScanService.RunScanPathAsync(scanPath.Id, _scanCts!.Token, progress));
    }

    private async Task RunLocalScanCoreAsync(Func<IProgress<ScanProgressUpdate>, Task<ScanExecutionResult>> scanAction)
    {
        var stopwatch = Stopwatch.StartNew();
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();

        ResetProgress();
        IsRunning = true;
        StatusMessage = "正在扫描本地目录，请稍候。";
        CurrentStageText = "当前阶段：准备扫描";
        CurrentFileText = "当前文件：--";
        var progress = new Progress<ScanProgressUpdate>(ApplyScanProgress);

        try
        {
            var result = await scanAction(progress);
            ApplyScanResult(result, stopwatch.Elapsed);
            StatusMessage = result.StatusMessage;
            CurrentStageText = "当前阶段：完成";
            CurrentFileText = "当前文件：--";
            _dataRefreshService.NotifyScanChanged();
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "本地扫描已取消。";
            CurrentStageText = "当前阶段：已取消";
            CurrentFileText = "当前文件：--";
        }
        catch (Exception exception)
        {
            StatusMessage = $"本地扫描失败：{exception.GetType().Name}";
            CurrentStageText = "当前阶段：失败";
            CurrentFileText = "当前文件：--";
        }
        finally
        {
            stopwatch.Stop();
            ElapsedText = FormatElapsed(stopwatch.Elapsed);
            _scanCts?.Dispose();
            _scanCts = null;
            IsRunning = false;
            await LoadLocalOverviewAsync(CancellationToken.None);
        }
    }

    private async Task RunScanAsync()
    {
        if (!CanRunScan)
        {
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();

        ResetProgress();
        IsRunning = true;
        StatusMessage = "正在扫描已启用的 WebDAV 路径，请稍候。";
        CurrentStageText = "当前阶段：准备扫描";
        CurrentFileText = "当前文件：--";
        var progress = new Progress<ScanProgressUpdate>(ApplyScanProgress);

        try
        {
            var result = await _mediaScanService.RunScanAsync(_scanCts.Token, progress);
            ApplyScanResult(result, stopwatch.Elapsed);
            StatusMessage = result.StatusMessage;
            CurrentStageText = "当前阶段：完成";
            CurrentFileText = "当前文件：--";
            _dataRefreshService.NotifyScanChanged();
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "扫描已取消。";
            CurrentStageText = "当前阶段：已取消";
            CurrentFileText = "当前文件：--";
        }
        catch (Exception exception)
        {
            StatusMessage = $"扫描失败：{exception.GetType().Name}";
            CurrentStageText = "当前阶段：失败";
            CurrentFileText = "当前文件：--";
        }
        finally
        {
            stopwatch.Stop();
            ElapsedText = FormatElapsed(stopwatch.Elapsed);
            _scanCts?.Dispose();
            _scanCts = null;
            IsRunning = false;
            await LoadOverviewAsync(CancellationToken.None);
        }
    }

    private void CancelScan()
    {
        if (!IsRunning)
        {
            return;
        }

        StatusMessage = "正在取消扫描。";
        CurrentStageText = "当前阶段：取消中";
        _scanCts?.Cancel();
    }

    private void ResetProgress()
    {
        ScannedCount = 0;
        NewFileCount = 0;
        UpdatedFileCount = 0;
        IgnoredFileCount = 0;
        ErrorCount = 0;
        CurrentStageText = "当前阶段：等待";
        CurrentFileText = "当前文件：--";
        ElapsedText = "--";
    }

    private void ApplyScanProgress(ScanProgressUpdate update)
    {
        CurrentStageText = string.IsNullOrWhiteSpace(update.StageText)
            ? "当前阶段：处理中"
            : $"当前阶段：{update.StageText}";
        CurrentFileText = string.IsNullOrWhiteSpace(update.CurrentItemName)
            ? "当前文件：--"
            : $"当前文件：{update.CurrentItemName}";
        ScannedCount = update.ScannedCount;
        NewFileCount = update.NewFileCount;
        UpdatedFileCount = update.UpdatedFileCount;
        IgnoredFileCount = update.IgnoredFileCount;
        ErrorCount = update.ErrorCount;
    }

    private void ApplyScanResult(ScanExecutionResult result, TimeSpan elapsed)
    {
        ScannedCount = result.TotalScannedCount;
        NewFileCount = result.NewFileCount;
        UpdatedFileCount = result.UpdatedFileCount;
        IgnoredFileCount = result.IgnoredFileCount;
        ErrorCount = result.ErrorCount;
        ElapsedText = FormatElapsed(elapsed);
    }

    private void RaiseScanPathStateChanged()
    {
        OnPropertyChanged(nameof(HasScanPaths));
        OnPropertyChanged(nameof(EnabledScanPathCount));
        OnPropertyChanged(nameof(CanRunScan));
        RunScanCommand.RaiseCanExecuteChanged();
    }

    private void RaiseLocalScanPathStateChanged()
    {
        OnPropertyChanged(nameof(HasLocalScanPaths));
        OnPropertyChanged(nameof(EnabledLocalScanPathCount));
        OnPropertyChanged(nameof(CanRunLocalScan));
        RunLocalScanCommand.RaiseCanExecuteChanged();
    }

    private void RaiseCommandStates()
    {
        RefreshCommand.RaiseCanExecuteChanged();
        SaveConnectionCommand.RaiseCanExecuteChanged();
        TestConnectionCommand.RaiseCanExecuteChanged();
        BeginAddScanPathCommand.RaiseCanExecuteChanged();
        PickWebDavScanPathCommand.RaiseCanExecuteChanged();
        SaveScanPathCommand.RaiseCanExecuteChanged();
        EditScanPathCommand.RaiseCanExecuteChanged();
        DeleteScanPathCommand.RaiseCanExecuteChanged();
        ToggleScanPathCommand.RaiseCanExecuteChanged();
        CancelEditScanPathCommand.RaiseCanExecuteChanged();
        BeginAddLocalScanPathCommand.RaiseCanExecuteChanged();
        PickLocalScanPathCommand.RaiseCanExecuteChanged();
        SaveLocalScanPathCommand.RaiseCanExecuteChanged();
        EditLocalScanPathCommand.RaiseCanExecuteChanged();
        DeleteLocalScanPathCommand.RaiseCanExecuteChanged();
        ToggleLocalScanPathCommand.RaiseCanExecuteChanged();
        CancelEditLocalScanPathCommand.RaiseCanExecuteChanged();
        RunLocalScanCommand.RaiseCanExecuteChanged();
        RunLocalScanPathCommand.RaiseCanExecuteChanged();
        RunScanCommand.RaiseCanExecuteChanged();
        CancelScanCommand.RaiseCanExecuteChanged();
    }

    private static DateTime ToLocalDisplayTime(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        return utc.ToLocalTime();
    }

    private WebDavConnectionModel BuildCurrentWebDavConnectionModel()
    {
        return new WebDavConnectionModel
        {
            Id = ConnectionId,
            Name = string.IsNullOrWhiteSpace(ConnectionName) ? "WebDAV" : ConnectionName,
            BaseUrl = BaseUrl,
            Username = Username,
            Password = Password,
            IsEnabled = IsConnectionEnabled
        };
    }

    private static string BuildDisplayNameFromVirtualPath(string path)
    {
        var fileName = WebDavPathHelper.GetFileName(path);
        return string.IsNullOrWhiteSpace(fileName) ? "WebDAV 根目录" : fileName;
    }

    private static string BuildDisplayNameFromLocalPath(string path)
    {
        try
        {
            var directoryName = new DirectoryInfo(path).Name;
            return string.IsNullOrWhiteSpace(directoryName) ? "本地目录" : directoryName;
        }
        catch
        {
            return "本地目录";
        }
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        if (elapsed.TotalHours >= 1)
        {
            return elapsed.ToString(@"hh\:mm\:ss");
        }

        return elapsed.ToString(@"mm\:ss");
    }

    public sealed class ScanPathViewModel
    {
        public ScanPathViewModel(ScanPath scanPath, DateTime? lastScanAt)
        {
            Id = scanPath.Id;
            DisplayName = scanPath.DisplayName;
            Path = scanPath.Path;
            IsEnabled = scanPath.IsEnabled;
            IsRecursive = scanPath.IsRecursive;
            UpdatedAt = scanPath.UpdatedAt;
            LastScanAt = lastScanAt;
        }

        public int Id { get; }

        public string DisplayName { get; }

        public string Path { get; }

        public bool IsEnabled { get; }

        public bool IsRecursive { get; }

        public DateTime UpdatedAt { get; }

        public DateTime? LastScanAt { get; }

        public string EnabledText => IsEnabled ? "已启用" : "已停用";

        public string RecursiveText => IsRecursive ? "递归扫描" : "仅当前目录";

        public string ToggleText => IsEnabled ? "停用" : "启用";

        public string DetailText => LastScanAt.HasValue
            ? $"{EnabledText} · {RecursiveText} · 最近：{LastScanAtText}"
            : $"{EnabledText} · {RecursiveText}";

        public string LastScanAtText => LastScanAt.HasValue
            ? LastScanAt.Value.ToString("yyyy-MM-dd HH:mm")
            : "暂无记录";
    }

    public sealed class ScanTaskLogViewModel
    {
        private static readonly Regex UrlRegex =
            new(@"(?i)\b(?:https?|webdavs?)://[^\s;，。]+", RegexOptions.Compiled);

        private static readonly Regex SensitivePairRegex =
            new(@"(?i)\b(password|passwd|token|api[-_ ]?key|access[-_ ]?token|authorization)\b\s*[:=]\s*[^\s;，。]+", RegexOptions.Compiled);

        private static readonly Regex WindowsPathRegex =
            new(@"[A-Za-z]:[\\/][^\s;，。]+", RegexOptions.Compiled);

        private static readonly Regex UncPathRegex =
            new(@"\\\\[^\\/\s]+[\\/][^\s;，。]+", RegexOptions.Compiled);

        public ScanTaskLogViewModel(
            ScanTaskLogItem item,
            string baseUrl,
            string username,
            string sourceText = "网盘",
            bool isLocal = false)
        {
            Id = item.Id;
            IsLocal = isLocal;
            SourceText = sourceText;
            ScanPathId = item.ScanPathId;
            ScanPathDisplayName = string.IsNullOrWhiteSpace(item.ScanPathDisplayName)
                ? isLocal ? "本地目录" : "扫描路径"
                : item.ScanPathDisplayName;
            ScanPath = item.ScanPath;
            TargetText = isLocal ? $"本地目录：{ScanPathDisplayName}" : $"WebDAV 路径：{ScanPathDisplayName}";
            UsernameText = isLocal ? "Local" : "已隐藏";
            StatusText = FormatStatus(item.Status);
            StartedAt = item.StartedAt;
            EndedAt = item.EndedAt;
            StartedAtText = item.StartedAt.ToString("yyyy-MM-dd HH:mm");
            EndedAtText = item.EndedAt.HasValue ? item.EndedAt.Value.ToString("yyyy-MM-dd HH:mm") : "--";
            DurationText = item.EndedAt.HasValue ? FormatElapsed(item.EndedAt.Value - item.StartedAt) : "--";
            ScannedCount = item.ScannedCount;
            NewFileCount = item.NewFileCount;
            UpdatedFileCount = item.UpdatedFileCount;
            IgnoredFileCount = item.IgnoredFileCount;
            ErrorCount = item.ErrorCount;
            ErrorMessage = SanitizeLogText(item.ErrorMessage);
            ReasonSummaryText = item.ReasonSummaryText;
            TopReasonSummaryText = item.TopReasonSummaryText;
        }

        public int Id { get; }

        public bool IsLocal { get; }

        public string SourceText { get; }

        public int? ScanPathId { get; }

        public string ScanPathDisplayName { get; }

        public string ScanPath { get; }

        public string TargetText { get; }

        public string UsernameText { get; }

        public string StatusText { get; }

        public DateTime StartedAt { get; }

        public DateTime? EndedAt { get; }

        public string StartedAtText { get; }

        public string EndedAtText { get; }

        public string DurationText { get; }

        public int ScannedCount { get; }

        public int NewFileCount { get; }

        public int UpdatedFileCount { get; }

        public int IgnoredFileCount { get; }

        public int ErrorCount { get; }

        public string ErrorMessage { get; }

        public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);

        public string ReasonSummaryText { get; }

        public string TopReasonSummaryText { get; }

        public bool HasReasonSummary => !string.IsNullOrWhiteSpace(ReasonSummaryText)
                                        || !string.IsNullOrWhiteSpace(TopReasonSummaryText);

        private static string FormatStatus(ScanTaskStatus status)
        {
            return status switch
            {
                ScanTaskStatus.Pending => "等待中",
                ScanTaskStatus.Running => "扫描中",
                ScanTaskStatus.Success => "已完成",
                ScanTaskStatus.Failed => "失败",
                ScanTaskStatus.PartialSuccess => "部分完成",
                ScanTaskStatus.Cancelled => "已取消",
                _ => status.ToString()
            };
        }

        private static string SanitizeLogText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var sanitized = UrlRegex.Replace(value.Trim(), "[WebDAV 地址已隐藏]");
            sanitized = SensitivePairRegex.Replace(
                sanitized,
                match => $"{match.Groups[1].Value}=[已隐藏]");
            sanitized = WindowsPathRegex.Replace(sanitized, "[本地路径已隐藏]");
            sanitized = UncPathRegex.Replace(sanitized, "[本地路径已隐藏]");

            const int maxLength = 300;
            return sanitized.Length <= maxLength
                ? sanitized
                : sanitized[..maxLength] + "...";
        }
    }
}
