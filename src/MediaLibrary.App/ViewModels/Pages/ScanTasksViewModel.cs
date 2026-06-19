using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
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
    private const string ConnectionConfigStatusUntested = "untested";
    private const string ConnectionConfigStatusSuccess = "success";
    private const string ConnectionConfigStatusFailure = "failure";
    private const int LocalStatusSegmentMaxLength = 24;
    private const int LocalStatusDisplayMaxLength = 86;

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
    private bool _isTestingConnection;
    private bool _isApplyingConnection;
    private bool _isConnectionEnabled = true;
    private bool _savedIsConnectionEnabled = true;
    private int? _editingScanPathId;
    private int? _editingLocalScanPathId;
    private string _connectionName = "WebDAV";
    private string _connectionConfigStatusKind = ConnectionConfigStatusFailure;
    private string _savedConnectionName = "WebDAV";
    private string _baseUrl = string.Empty;
    private string _savedBaseUrl = string.Empty;
    private string _username = string.Empty;
    private string _savedUsername = string.Empty;
    private string _password = string.Empty;
    private string _savedPassword = string.Empty;
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
    private string _currentFileText = "当前扫描文件：尚未开始扫描。";
    private string _elapsedText = "--";

    public ScanTasksViewModel(
        IMediaScanService mediaScanService,
        ILocalMediaScanService localMediaScanService,
        ISettingsService settingsService,
        IWebDavService webDavService,
        IScanPathPickerService scanPathPickerService,
        IDataRefreshService dataRefreshService)
        : base("扫描任务", "基于WebDAV协议进行扫描")
    {
        _mediaScanService = mediaScanService;
        _localMediaScanService = localMediaScanService;
        _settingsService = settingsService;
        _webDavService = webDavService;
        _scanPathPickerService = scanPathPickerService;
        _dataRefreshService = dataRefreshService;

        RefreshCommand = new AsyncRelayCommand(() => LoadAsync(), () => !IsRunning);
        SaveConnectionCommand = new AsyncRelayCommand(SaveConnectionAsync, () => !IsRunning && !IsTestingConnection && HasConnectionConfigChanges);
        TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync, () => !IsRunning && !IsTestingConnection);
        AddWebDavScanPathFromPickerCommand = new AsyncRelayCommand(AddWebDavScanPathFromPickerAsync, () => !IsRunning);
        BeginAddScanPathCommand = new RelayCommand(BeginAddScanPath, () => !IsRunning);
        PickWebDavScanPathCommand = new AsyncRelayCommand(PickWebDavScanPathAsync, () => !IsRunning);
        SaveScanPathCommand = new AsyncRelayCommand(SaveScanPathAsync, () => !IsRunning);
        EditScanPathCommand = new RelayCommand(EditScanPath, _ => !IsRunning);
        DeleteScanPathCommand = new AsyncRelayCommand(DeleteScanPathAsync, _ => !IsRunning);
        ToggleScanPathCommand = new AsyncRelayCommand(ToggleScanPathAsync, _ => !IsRunning);
        CancelEditScanPathCommand = new RelayCommand(CancelEditScanPath, () => !IsRunning);
        AddLocalScanPathFromPickerCommand = new AsyncRelayCommand(AddLocalScanPathFromPickerAsync, () => !IsRunning);
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

    public AsyncRelayCommand AddWebDavScanPathFromPickerCommand { get; }

    public RelayCommand BeginAddScanPathCommand { get; }

    public AsyncRelayCommand PickWebDavScanPathCommand { get; }

    public AsyncRelayCommand SaveScanPathCommand { get; }

    public RelayCommand EditScanPathCommand { get; }

    public AsyncRelayCommand DeleteScanPathCommand { get; }

    public AsyncRelayCommand ToggleScanPathCommand { get; }

    public RelayCommand CancelEditScanPathCommand { get; }

    public AsyncRelayCommand AddLocalScanPathFromPickerCommand { get; }

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
                OnPropertyChanged(nameof(ConnectionConfigStatusText));
                OnPropertyChanged(nameof(ConnectionConfigStatusKind));
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

    public string ConnectionConfigStatusText
    {
        get
        {
            if (!HasSavedConnection)
            {
                return "未配置";
            }

            if (!IsConnectionEnabled)
            {
                return "已停用";
            }

            return _connectionConfigStatusKind switch
            {
                ConnectionConfigStatusSuccess => "测试通过",
                ConnectionConfigStatusFailure => "测试失败",
                _ => "已保存未测试"
            };
        }
    }

    public string ConnectionConfigStatusKind => !HasSavedConnection
        ? ConnectionConfigStatusFailure
        : IsConnectionEnabled
            ? _connectionConfigStatusKind
            : ConnectionConfigStatusUntested;

    public bool HasConnectionConfigChanges =>
        !string.Equals(NormalizeConnectionInput(ConnectionName), _savedConnectionName, StringComparison.Ordinal)
        || !string.Equals(NormalizeConnectionInput(BaseUrl), _savedBaseUrl, StringComparison.Ordinal)
        || !string.Equals(NormalizeConnectionInput(Username), _savedUsername, StringComparison.Ordinal)
        || !string.Equals(Password ?? string.Empty, _savedPassword, StringComparison.Ordinal)
        || IsConnectionEnabled != _savedIsConnectionEnabled;

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

    public bool IsTestingConnection
    {
        get => _isTestingConnection;
        private set
        {
            if (SetProperty(ref _isTestingConnection, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public string ConnectionName
    {
        get => _connectionName;
        set
        {
            if (SetProperty(ref _connectionName, value))
            {
                OnConnectionConfigInputChanged();
            }
        }
    }

    public string BaseUrl
    {
        get => _baseUrl;
        set
        {
            if (SetProperty(ref _baseUrl, value))
            {
                OnConnectionConfigInputChanged();
            }
        }
    }

    public string Username
    {
        get => _username;
        set
        {
            if (SetProperty(ref _username, value))
            {
                OnConnectionConfigInputChanged();
            }
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            if (SetProperty(ref _password, value))
            {
                OnConnectionConfigInputChanged();
            }
        }
    }

    public bool IsConnectionEnabled
    {
        get => _isConnectionEnabled;
        set
        {
            if (SetProperty(ref _isConnectionEnabled, value))
            {
                OnPropertyChanged(nameof(CanRunScan));
                OnPropertyChanged(nameof(ConnectionConfigStatusText));
                OnPropertyChanged(nameof(ConnectionConfigStatusKind));
                OnConnectionConfigInputChanged();
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
        private set
        {
            if (SetProperty(ref _scanPathStatusMessage, value))
            {
                OnPropertyChanged(nameof(ScanPathStatusDisplayText));
            }
        }
    }

    public string LocalScanPathStatusMessage
    {
        get => _localScanPathStatusMessage;
        private set
        {
            if (SetProperty(ref _localScanPathStatusMessage, value))
            {
                OnPropertyChanged(nameof(LocalScanPathStatusDisplayText));
            }
        }
    }

    public string ScanPathStatusDisplayText => FormatLocalScanPathStatusDisplay(ScanPathStatusMessage);

    public string LocalScanPathStatusDisplayText => FormatLocalScanPathStatusDisplay(LocalScanPathStatusMessage);

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

    public string LocalRunScanButtonText => IsRunning ? "扫描中..." : "开始扫描本地";

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
        _isApplyingConnection = true;
        try
        {
            ConnectionId = connection.Id;
            ConnectionName = string.IsNullOrWhiteSpace(connection.Name) ? "WebDAV" : connection.Name;
            BaseUrl = connection.BaseUrl;
            Username = connection.Username;
            Password = connection.Password;
            IsConnectionEnabled = connection.IsEnabled;
            HasConnection = connection.Id.HasValue;
        }
        finally
        {
            _isApplyingConnection = false;
        }

        SetConnectionConfigStatusKind(ConnectionId.HasValue
            ? ConnectionConfigStatusUntested
            : ConnectionConfigStatusFailure);
        CaptureConnectionConfigBaseline();
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
        foreach (var path in paths
            .OrderByDescending(x => x.UpdatedAt)
            .ThenByDescending(x => x.CreatedAt)
            .ThenBy(x => x.DisplayName, StringComparer.CurrentCulture)
            .ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase))
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
        IsTestingConnection = true;
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
            ConnectionStatusMessage = "WebDAV 连接配置已保存，正在测试连接。";

            await TestConnectionCoreAsync(false);

            if (ConnectionId.HasValue)
            {
                try
                {
                    await LoadScanPathsAsync(ConnectionId.Value, CancellationToken.None);
                    await LoadOverviewAsync(CancellationToken.None);
                }
                catch
                {
                    ScanPathStatusMessage = "连接已保存并完成测试，但刷新扫描路径状态失败。";
                }
            }
        }
        catch (Exception exception)
        {
            SetConnectionConfigStatusKind(ConnectionConfigStatusFailure);
            ConnectionStatusMessage = exception.Message;
        }
        finally
        {
            IsTestingConnection = false;
        }
    }

    private async Task TestConnectionAsync()
    {
        await TestConnectionCoreAsync();
    }

    private async Task TestConnectionCoreAsync(bool manageTestingState = true)
    {
        if (manageTestingState)
        {
            IsTestingConnection = true;
        }

        try
        {
            SetConnectionConfigStatusKind(ConnectionConfigStatusUntested);
            ConnectionStatusMessage = "正在测试 WebDAV 连接。";
            var result = await _webDavService.TestConnectionAsync(BuildCurrentWebDavConnectionModel());

            SetConnectionConfigStatusKind(result.IsSuccess
                ? ConnectionConfigStatusSuccess
                : ConnectionConfigStatusFailure);
            ConnectionStatusMessage = result.Message;
        }
        catch (Exception exception)
        {
            SetConnectionConfigStatusKind(ConnectionConfigStatusFailure);
            ConnectionStatusMessage = $"测试连接失败：{exception.Message}";
        }
        finally
        {
            if (manageTestingState)
            {
                IsTestingConnection = false;
            }
        }
    }

    private async Task AddWebDavScanPathFromPickerAsync()
    {
        if (!ConnectionId.HasValue)
        {
            ScanPathStatusMessage = "请先保存 WebDAV 连接配置。";
            return;
        }

        if (string.IsNullOrWhiteSpace(BaseUrl))
        {
            ScanPathStatusMessage = "请先填写 WebDAV BaseUrl，再选择远端目录。";
            return;
        }

        var skipped = new List<string>();
        var removedNestedCount = 0;
        var selectedPaths = FilterNestedVirtualSelections((await _scanPathPickerService.PickWebDavDirectoriesAsync(
                BuildCurrentWebDavConnectionModel(),
                "/"))
            .Select(WebDavPathHelper.NormalizeVirtualPath)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray(), skipped);
        if (selectedPaths.Length == 0)
        {
            ScanPathStatusMessage = FormatWebDavPathBatchAddStatus([], skipped, removedNestedCount: 0);
            return;
        }

        var added = new List<string>();
        foreach (var selectedPath in selectedPaths)
        {
            try
            {
                removedNestedCount += await DeleteNestedWebDavScanPathsAsync(selectedPath, excludedScanPathId: null);
                var saved = await _settingsService.SaveScanPathAsync(
                    new ScanPath
                    {
                        SourceConnectionId = ConnectionId.Value,
                        Path = selectedPath,
                        DisplayName = BuildDisplayNameFromVirtualPath(selectedPath),
                        IsEnabled = true,
                        IsRecursive = true
                    });
                added.Add(saved.DisplayName);
            }
            catch (Exception exception)
            {
                skipped.Add($"{BuildDisplayNameFromVirtualPath(selectedPath)}：{exception.Message}");
            }
        }

        await LoadOverviewAsync(CancellationToken.None);
        ScanPathStatusMessage = FormatWebDavPathBatchAddStatus(added, skipped, removedNestedCount);
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
            var removedNestedCount = await DeleteNestedWebDavScanPathsAsync(
                EditingScanPathValue,
                EditingScanPathId);
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
            ScanPathStatusMessage = AppendNestedPathRemovalStatus(
                $"扫描路径已保存：{saved.DisplayName}",
                removedNestedCount);
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

    private async Task AddLocalScanPathFromPickerAsync()
    {
        var skipped = new List<string>();
        var removedNestedCount = 0;
        var selectedPaths = FilterNestedLocalSelections((await _scanPathPickerService.PickLocalDirectoriesAsync(string.Empty))
            .Select(NormalizeLocalInputPath)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray(), skipped);
        if (selectedPaths.Length == 0)
        {
            LocalScanPathStatusMessage = FormatLocalPathBatchAddStatus([], skipped, removedNestedCount: 0);
            return;
        }

        var added = new List<string>();
        foreach (var normalizedPath in selectedPaths)
        {
            var displayName = BuildDisplayNameFromLocalPath(normalizedPath);
            var pathExists = Directory.Exists(normalizedPath);
            try
            {
                removedNestedCount += await DeleteNestedLocalScanPathsAsync(
                    normalizedPath,
                    excludedScanPathId: null);
                var saved = await _settingsService.SaveLocalScanPathAsync(
                    new ScanPath
                    {
                        Path = normalizedPath,
                        DisplayName = displayName,
                        IsEnabled = true,
                        IsRecursive = true
                    });

                added.Add(pathExists ? saved.DisplayName : $"{saved.DisplayName}（路径当前不可访问）");
            }
            catch (Exception exception)
            {
                skipped.Add($"{displayName}：{FormatLocalPathSaveFailure(exception.Message)}");
            }
        }

        await LoadLocalOverviewAsync(CancellationToken.None);
        LocalScanPathStatusMessage = FormatLocalPathBatchAddStatus(added, skipped, removedNestedCount);
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
            var removedNestedCount = await DeleteNestedLocalScanPathsAsync(
                EditingLocalScanPathValue ?? string.Empty,
                EditingLocalScanPathId);
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
            var statusMessage = pathExists
                ? $"本地目录配置已保存：{saved.DisplayName}"
                : $"本地目录配置已保存：{saved.DisplayName}（路径当前不可访问）";
            LocalScanPathStatusMessage = AppendNestedPathRemovalStatus(statusMessage, removedNestedCount);
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
        CurrentFileText = "当前扫描文件：--";
        var progress = new Progress<ScanProgressUpdate>(ApplyScanProgress);

        try
        {
            var result = await scanAction(progress);
            ApplyScanResult(result, stopwatch.Elapsed);
            StatusMessage = result.StatusMessage;
            CurrentStageText = "当前阶段：完成";
            CurrentFileText = "当前扫描文件：--";
            _dataRefreshService.NotifyScanChanged();
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "本地扫描已取消。";
            CurrentStageText = "当前阶段：已取消";
            CurrentFileText = "当前扫描文件：--";
        }
        catch (Exception exception)
        {
            StatusMessage = $"本地扫描失败：{exception.GetType().Name}";
            CurrentStageText = "当前阶段：失败";
            CurrentFileText = "当前扫描文件：--";
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
        CurrentFileText = "当前扫描文件：--";
        var progress = new Progress<ScanProgressUpdate>(ApplyScanProgress);

        try
        {
            var result = await _mediaScanService.RunScanAsync(_scanCts.Token, progress);
            ApplyScanResult(result, stopwatch.Elapsed);
            StatusMessage = result.StatusMessage;
            CurrentStageText = "当前阶段：完成";
            CurrentFileText = "当前扫描文件：--";
            _dataRefreshService.NotifyScanChanged();
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "扫描已取消。";
            CurrentStageText = "当前阶段：已取消";
            CurrentFileText = "当前扫描文件：--";
        }
        catch (Exception exception)
        {
            StatusMessage = $"扫描失败：{exception.GetType().Name}";
            CurrentStageText = "当前阶段：失败";
            CurrentFileText = "当前扫描文件：--";
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
        CurrentFileText = "当前扫描文件：--";
        ElapsedText = "--";
    }

    private void ApplyScanProgress(ScanProgressUpdate update)
    {
        CurrentStageText = string.IsNullOrWhiteSpace(update.StageText)
            ? "当前阶段：处理中"
            : $"当前阶段：{update.StageText}";
        CurrentFileText = string.IsNullOrWhiteSpace(update.CurrentItemName)
            ? "当前扫描文件：--"
            : $"当前扫描文件：{update.CurrentItemName}";
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
        AddWebDavScanPathFromPickerCommand.RaiseCanExecuteChanged();
        BeginAddScanPathCommand.RaiseCanExecuteChanged();
        PickWebDavScanPathCommand.RaiseCanExecuteChanged();
        SaveScanPathCommand.RaiseCanExecuteChanged();
        EditScanPathCommand.RaiseCanExecuteChanged();
        DeleteScanPathCommand.RaiseCanExecuteChanged();
        ToggleScanPathCommand.RaiseCanExecuteChanged();
        CancelEditScanPathCommand.RaiseCanExecuteChanged();
        AddLocalScanPathFromPickerCommand.RaiseCanExecuteChanged();
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

    private void MarkConnectionConfigUntested()
    {
        if (_isApplyingConnection || !HasSavedConnection)
        {
            return;
        }

        SetConnectionConfigStatusKind(ConnectionConfigStatusUntested);
    }

    private void OnConnectionConfigInputChanged()
    {
        if (_isApplyingConnection)
        {
            return;
        }

        OnPropertyChanged(nameof(HasConnectionConfigChanges));
        SaveConnectionCommand.RaiseCanExecuteChanged();
        MarkConnectionConfigUntested();
    }

    private void CaptureConnectionConfigBaseline()
    {
        _savedConnectionName = NormalizeConnectionInput(ConnectionName);
        _savedBaseUrl = NormalizeConnectionInput(BaseUrl);
        _savedUsername = NormalizeConnectionInput(Username);
        _savedPassword = Password ?? string.Empty;
        _savedIsConnectionEnabled = IsConnectionEnabled;
        OnPropertyChanged(nameof(HasConnectionConfigChanges));
        SaveConnectionCommand.RaiseCanExecuteChanged();
    }

    private void SetConnectionConfigStatusKind(string statusKind)
    {
        var normalizedStatusKind = statusKind switch
        {
            ConnectionConfigStatusSuccess => ConnectionConfigStatusSuccess,
            ConnectionConfigStatusFailure => ConnectionConfigStatusFailure,
            _ => ConnectionConfigStatusUntested
        };

        if (_connectionConfigStatusKind == normalizedStatusKind)
        {
            return;
        }

        _connectionConfigStatusKind = normalizedStatusKind;
        OnPropertyChanged(nameof(ConnectionConfigStatusText));
        OnPropertyChanged(nameof(ConnectionConfigStatusKind));
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

    private static string NormalizeConnectionInput(string? value)
    {
        return (value ?? string.Empty).Trim();
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

    private static string NormalizeLocalInputPath(string path)
    {
        return (path ?? string.Empty).Trim().Trim('"');
    }

    private async Task<int> DeleteNestedWebDavScanPathsAsync(string parentPath, int? excludedScanPathId)
    {
        if (!ConnectionId.HasValue || string.IsNullOrWhiteSpace(parentPath))
        {
            return 0;
        }

        var existingPaths = await _settingsService.GetScanPathsAsync(ConnectionId.Value);
        var nestedPaths = existingPaths
            .Where(path => path.Id != excludedScanPathId
                           && IsVirtualChildPath(parentPath, path.Path))
            .ToArray();
        foreach (var nestedPath in nestedPaths)
        {
            await _settingsService.DeleteScanPathAsync(nestedPath.Id);
        }

        return nestedPaths.Length;
    }

    private async Task<int> DeleteNestedLocalScanPathsAsync(string parentPath, int? excludedScanPathId)
    {
        if (string.IsNullOrWhiteSpace(parentPath))
        {
            return 0;
        }

        var existingPaths = await _settingsService.GetLocalScanPathsAsync();
        var nestedPaths = existingPaths
            .Where(path => path.Id != excludedScanPathId
                           && IsLocalChildPath(parentPath, path.Path))
            .ToArray();
        foreach (var nestedPath in nestedPaths)
        {
            await _settingsService.DeleteLocalScanPathAsync(nestedPath.Id);
        }

        return nestedPaths.Length;
    }

    private static string[] FilterNestedVirtualSelections(IReadOnlyList<string> selectedPaths, IList<string> skipped)
    {
        var result = new List<string>();
        foreach (var selectedPath in selectedPaths)
        {
            var coveredBySelectedParent = selectedPaths.Any(parentPath =>
                !string.Equals(parentPath, selectedPath, StringComparison.OrdinalIgnoreCase)
                && IsVirtualChildPath(parentPath, selectedPath));
            if (coveredBySelectedParent)
            {
                skipped.Add($"{BuildDisplayNameFromVirtualPath(selectedPath)}：已被同批选择的父路径覆盖");
                continue;
            }

            result.Add(selectedPath);
        }

        return result.ToArray();
    }

    private static string[] FilterNestedLocalSelections(IReadOnlyList<string> selectedPaths, IList<string> skipped)
    {
        var result = new List<string>();
        foreach (var selectedPath in selectedPaths)
        {
            var coveredBySelectedParent = selectedPaths.Any(parentPath =>
                !string.Equals(parentPath, selectedPath, StringComparison.OrdinalIgnoreCase)
                && IsLocalChildPath(parentPath, selectedPath));
            if (coveredBySelectedParent)
            {
                skipped.Add($"{BuildDisplayNameFromLocalPath(selectedPath)}：已被同批选择的父目录覆盖");
                continue;
            }

            result.Add(selectedPath);
        }

        return result.ToArray();
    }

    private static bool IsVirtualChildPath(string parentPath, string candidatePath)
    {
        var normalizedParent = WebDavPathHelper.NormalizeVirtualPath(parentPath);
        var normalizedCandidate = WebDavPathHelper.NormalizeVirtualPath(candidatePath);
        if (string.Equals(normalizedParent, normalizedCandidate, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (normalizedParent == "/")
        {
            return normalizedCandidate != "/";
        }

        return normalizedCandidate.StartsWith(
            $"{normalizedParent.TrimEnd('/')}/",
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLocalChildPath(string parentPath, string candidatePath)
    {
        var normalizedParent = NormalizeLocalComparablePath(parentPath);
        var normalizedCandidate = NormalizeLocalComparablePath(candidatePath);
        if (string.IsNullOrWhiteSpace(normalizedParent)
            || string.IsNullOrWhiteSpace(normalizedCandidate)
            || string.Equals(normalizedParent, normalizedCandidate, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return normalizedCandidate.StartsWith(
            $"{normalizedParent}{Path.DirectorySeparatorChar}",
            StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeLocalComparablePath(string path)
    {
        var normalizedPath = NormalizeLocalInputPath(path);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return string.Empty;
        }

        try
        {
            normalizedPath = Path.GetFullPath(normalizedPath);
        }
        catch
        {
            // Keep the user's normalized input if the path is currently not parseable.
        }

        return normalizedPath
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .TrimEnd(Path.DirectorySeparatorChar);
    }

    private static string FormatWebDavPathBatchAddStatus(
        IReadOnlyList<string> added,
        IReadOnlyList<string> skipped,
        int removedNestedCount)
    {
        if (added.Count == 0 && skipped.Count == 0)
        {
            return AppendNestedPathRemovalStatus("没有选择 WebDAV 目录。", removedNestedCount);
        }

        var parts = new List<string>();
        if (added.Count > 0)
        {
            parts.Add($"已添加 {added.Count} 个 WebDAV 路径：{FormatNamePreview(added)}");
        }

        if (skipped.Count > 0)
        {
            parts.Add($"跳过 {skipped.Count} 个：{FormatNamePreview(skipped)}");
        }

        return AppendNestedPathRemovalStatus(string.Join("；", parts), removedNestedCount);
    }

    private static string FormatLocalPathBatchAddStatus(
        IReadOnlyList<string> added,
        IReadOnlyList<string> skipped,
        int removedNestedCount)
    {
        if (added.Count == 0 && skipped.Count == 0)
        {
            return AppendNestedPathRemovalStatus("没有选择本地目录。", removedNestedCount);
        }

        var parts = new List<string>();
        if (added.Count > 0)
        {
            parts.Add($"已添加 {added.Count} 个本地目录：{FormatNamePreview(added)}");
        }

        if (skipped.Count > 0)
        {
            parts.Add($"跳过 {skipped.Count} 个：{FormatNamePreview(skipped)}");
        }

        return AppendNestedPathRemovalStatus(string.Join("；", parts), removedNestedCount);
    }

    private static string AppendNestedPathRemovalStatus(string message, int removedNestedCount)
    {
        if (removedNestedCount <= 0)
        {
            return message;
        }

        return $"{message}；已自动移除 {removedNestedCount} 个已有子路径，原因：新添加的父路径已覆盖它们。";
    }

    private static string FormatLocalScanPathStatusDisplay(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        var compactMessage = TruncateDelimitedStatusSegments(message.Trim(), LocalStatusSegmentMaxLength);
        return TruncateMiddle(compactMessage, LocalStatusDisplayMaxLength);
    }

    private static string TruncateDelimitedStatusSegments(string message, int maxSegmentLength)
    {
        if (string.IsNullOrEmpty(message))
        {
            return message;
        }

        var builder = new StringBuilder(message.Length);
        var segmentStart = 0;
        for (var i = 0; i < message.Length; i++)
        {
            if (!IsLocalStatusDelimiter(message[i]))
            {
                continue;
            }

            AppendTruncatedStatusSegment(builder, message.AsSpan(segmentStart, i - segmentStart), maxSegmentLength);
            builder.Append(message[i]);
            segmentStart = i + 1;
        }

        AppendTruncatedStatusSegment(builder, message.AsSpan(segmentStart), maxSegmentLength);
        return builder.ToString();
    }

    private static bool IsLocalStatusDelimiter(char value)
    {
        return value is ':' or '：' or '、' or ',' or '，' or ';' or '；';
    }

    private static void AppendTruncatedStatusSegment(StringBuilder builder, ReadOnlySpan<char> segment, int maxSegmentLength)
    {
        if (segment.Length == 0)
        {
            return;
        }

        var leadingWhitespaceLength = 0;
        while (leadingWhitespaceLength < segment.Length && char.IsWhiteSpace(segment[leadingWhitespaceLength]))
        {
            leadingWhitespaceLength++;
        }

        var trailingWhitespaceLength = 0;
        while (trailingWhitespaceLength < segment.Length - leadingWhitespaceLength
               && char.IsWhiteSpace(segment[segment.Length - trailingWhitespaceLength - 1]))
        {
            trailingWhitespaceLength++;
        }

        if (leadingWhitespaceLength > 0)
        {
            builder.Append(segment[..leadingWhitespaceLength]);
        }

        var content = segment.Slice(
            leadingWhitespaceLength,
            segment.Length - leadingWhitespaceLength - trailingWhitespaceLength);
        builder.Append(TruncateMiddle(content.ToString(), maxSegmentLength));

        if (trailingWhitespaceLength > 0)
        {
            builder.Append(segment[^trailingWhitespaceLength..]);
        }
    }

    private static string TruncateMiddle(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        var headLength = Math.Max(8, (maxLength - 3) / 2);
        var tailLength = Math.Max(8, maxLength - 3 - headLength);
        return $"{value[..headLength]}...{value[^tailLength..]}";
    }

    private static string FormatNamePreview(IReadOnlyList<string> values)
    {
        const int previewLimit = 3;
        var preview = values
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Take(previewLimit)
            .ToArray();
        if (preview.Length == 0)
        {
            return "--";
        }

        var suffix = values.Count > previewLimit ? $" 等 {values.Count} 项" : string.Empty;
        return $"{string.Join("、", preview)}{suffix}";
    }

    private static string FormatLocalPathSaveFailure(string message)
    {
        if (message.Contains("重复", StringComparison.OrdinalIgnoreCase))
        {
            return "已存在相同目录";
        }

        if (message.Contains("包含关系", StringComparison.OrdinalIgnoreCase))
        {
            return "与已有目录存在父子包含关系";
        }

        if (message.Contains("绝对路径", StringComparison.OrdinalIgnoreCase)
            || message.Contains("格式无效", StringComparison.OrdinalIgnoreCase)
            || message.Contains("不能为空", StringComparison.OrdinalIgnoreCase))
        {
            return "路径格式无效";
        }

        return string.IsNullOrWhiteSpace(message) ? "未添加" : message;
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
            TitleText = FormatTitlePath(ScanPath, ScanPathDisplayName, isLocal);
            var historicalBaseUrl = string.IsNullOrWhiteSpace(item.BaseUrl) ? baseUrl : item.BaseUrl;
            var historicalUsername = string.IsNullOrWhiteSpace(item.Username) ? username : item.Username;
            BaseUrlText = isLocal ? "本地文件系统" : FormatBaseUrl(historicalBaseUrl);
            TargetText = isLocal ? $"本地目录：{TitleText}" : $"WebDAV 路径：{TitleText}";
            UsernameText = isLocal ? "--" : FormatUsername(historicalUsername);
            StatusText = FormatStatus(item.Status);
            StatusKind = FormatStatusKind(item.Status);
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
            ReasonSummaryText = FormatReasonSummary(item);
            TopReasonSummaryText = FormatTopReasonSummary(item);
            ScanResultText = BuildScanResultText(ReasonSummaryText, TopReasonSummaryText, ErrorMessage);
        }

        public int Id { get; }

        public bool IsLocal { get; }

        public string SourceText { get; }

        public int? ScanPathId { get; }

        public string ScanPathDisplayName { get; }

        public string ScanPath { get; }

        public string TitleText { get; }

        public string BaseUrlText { get; }

        public string TargetText { get; }

        public string UsernameText { get; }

        public string StatusText { get; }

        public string StatusKind { get; }

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

        public string ScanResultText { get; }

        public bool HasScanResultText => !string.IsNullOrWhiteSpace(ScanResultText);

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

        private static string FormatStatusKind(ScanTaskStatus status)
        {
            return status switch
            {
                ScanTaskStatus.Success => "success",
                ScanTaskStatus.Failed => "failure",
                ScanTaskStatus.PartialSuccess or ScanTaskStatus.Cancelled => "warning",
                ScanTaskStatus.Running => "running",
                _ => "untested"
            };
        }

        private static string FormatTitlePath(string path, string displayName, bool isLocal)
        {
            var title = string.IsNullOrWhiteSpace(path) ? displayName : path;
            if (string.IsNullOrWhiteSpace(title))
            {
                title = isLocal ? "本地目录" : "WebDAV 路径";
            }

            return HideUrls(title);
        }

        private static string FormatBaseUrl(string baseUrl)
        {
            return string.IsNullOrWhiteSpace(baseUrl) ? "未配置" : baseUrl.Trim();
        }

        private static string FormatUsername(string username)
        {
            return string.IsNullOrWhiteSpace(username) ? "未填写" : username.Trim();
        }

        private static string FormatReasonSummary(ScanTaskLogItem item)
        {
            var summary = ScanReasonSummaryFormatter.Parse(item.ReasonSummaryJson);
            if (summary is null || summary.Entries.Count == 0)
            {
                return string.IsNullOrWhiteSpace(item.ReasonSummaryText)
                    ? string.Empty
                    : TrimReasonLabel(item.ReasonSummaryText);
            }

            var parts = new List<string>();
            AddReasonCategoryTotal(parts, summary, "success", "成功处理");
            AddReasonCategoryTotal(parts, summary, "skipped", "跳过");
            AddReasonCategoryTotal(parts, summary, "cancelled", "已取消");
            AddReasonCategoryTotal(parts, summary, "warning", "需要注意");
            AddReasonCategoryTotal(parts, summary, "error", "失败");
            return parts.Count == 0 ? string.Empty : string.Join("，", parts);
        }

        private static string FormatTopReasonSummary(ScanTaskLogItem item)
        {
            var summary = ScanReasonSummaryFormatter.Parse(item.ReasonSummaryJson);
            if (summary is null || summary.Entries.Count == 0)
            {
                return string.IsNullOrWhiteSpace(item.TopReasonSummaryText)
                    ? string.Empty
                    : TrimReasonLabel(item.TopReasonSummaryText);
            }

            var reasons = summary.Entries
                .Where(x => x.Count > 0 && !string.Equals(x.Category, "success", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.Label, StringComparer.CurrentCulture)
                .Take(3)
                .Select(x => $"{x.Label} {x.Count} 项")
                .ToArray();
            return reasons.Length == 0 ? string.Empty : string.Join("，", reasons);
        }

        private static void AddReasonCategoryTotal(
            ICollection<string> parts,
            ScanReasonSummary summary,
            string category,
            string label)
        {
            var count = summary.Entries
                .Where(x => string.Equals(x.Category, category, StringComparison.OrdinalIgnoreCase))
                .Sum(x => x.Count);
            if (count > 0)
            {
                parts.Add($"{label} {count} 项");
            }
        }

        private static string BuildScanResultText(string reasonSummary, string topReasonSummary, string errorMessage)
        {
            var parts = new[]
                {
                    TrimReasonLabel(reasonSummary),
                    TrimReasonLabel(topReasonSummary)
                }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToArray();
            if (parts.Length > 0)
            {
                return $"扫描结果：{string.Join(Environment.NewLine, parts)}";
            }

            return string.IsNullOrWhiteSpace(errorMessage)
                ? "扫描结果：未记录额外原因。"
                : $"扫描结果：{errorMessage}";
        }

        private static string TrimReasonLabel(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var text = value.Trim();
            var prefixes = new[]
            {
                "原因摘要：",
                "原因摘要:",
                "主要原因：",
                "主要原因:"
            };
            foreach (var prefix in prefixes)
            {
                if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return text[prefix.Length..].Trim();
                }
            }

            return text;
        }

        private static string SanitizeLogText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var sanitized = HideUrls(value.Trim());
            sanitized = SensitivePairRegex.Replace(
                sanitized,
                match => $"{match.Groups[1].Value}=[已隐藏]");

            const int maxLength = 300;
            return sanitized.Length <= maxLength
                ? sanitized
                : sanitized[..maxLength] + "...";
        }

        private static string HideUrls(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : UrlRegex.Replace(value.Trim(), "[WebDAV URL 已隐藏]");
        }
    }
}
