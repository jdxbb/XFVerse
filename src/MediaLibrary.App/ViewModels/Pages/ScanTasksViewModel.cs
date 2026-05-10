using System.Collections.ObjectModel;
using MediaLibrary.App.Services.Interfaces;
using MediaLibrary.App.ViewModels.Base;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.App.ViewModels.Pages;

public sealed class ScanTasksViewModel : PageViewModelBase
{
    private readonly IMediaScanService _mediaScanService;
    private readonly IDataRefreshService _dataRefreshService;
    private bool _hasConnection;
    private bool _isRunning;
    private string _connectionName = "未配置";
    private string _baseUrl = string.Empty;
    private string _lastScanAtText = "尚未扫描";
    private string _statusMessage = "点击“开始扫描”后，系统会扫描当前已启用的 WebDAV 路径。";

    public ScanTasksViewModel(IMediaScanService mediaScanService, IDataRefreshService dataRefreshService)
        : base("扫描任务", "手动触发扫描流程，查看当前连接、启用路径与最近扫描日志。")
    {
        _mediaScanService = mediaScanService;
        _dataRefreshService = dataRefreshService;
        RunScanCommand = new AsyncRelayCommand(RunScanAsync, () => CanRunScan);
    }

    public ObservableCollection<ScanPathSummaryItem> EnabledScanPaths { get; } = [];

    public ObservableCollection<ScanTaskLogItem> RecentLogs { get; } = [];

    public AsyncRelayCommand RunScanCommand { get; }

    public bool HasConnection { get => _hasConnection; private set => SetProperty(ref _hasConnection, value); }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value))
            {
                RunScanCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(CanRunScan));
            }
        }
    }

    public string ConnectionName { get => _connectionName; private set => SetProperty(ref _connectionName, value); }

    public string BaseUrl { get => _baseUrl; private set => SetProperty(ref _baseUrl, value); }

    public string LastScanAtText { get => _lastScanAtText; private set => SetProperty(ref _lastScanAtText, value); }

    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public bool CanRunScan => HasConnection && EnabledScanPaths.Count > 0 && !IsRunning;

    public override Task ActivateAsync(CancellationToken cancellationToken = default)
    {
        return LoadOverviewAsync(cancellationToken);
    }

    private async Task LoadOverviewAsync(CancellationToken cancellationToken)
    {
        try
        {
            var overview = await _mediaScanService.GetOverviewAsync(cancellationToken);
            HasConnection = overview.HasConnection;
            ConnectionName = overview.ConnectionName;
            BaseUrl = overview.BaseUrl;
            LastScanAtText = overview.LastScanAt.HasValue
                ? overview.LastScanAt.Value.ToString("yyyy-MM-dd HH:mm:ss")
                : "尚未扫描";

            EnabledScanPaths.Clear();
            foreach (var item in overview.EnabledScanPaths)
            {
                EnabledScanPaths.Add(item);
            }

            RecentLogs.Clear();
            foreach (var item in overview.RecentLogs)
            {
                RecentLogs.Add(item);
            }

            StatusMessage = !HasConnection
                ? "当前还没有可用的 WebDAV 连接。请先到设置页保存连接配置。"
                : EnabledScanPaths.Count == 0
                    ? "当前没有启用的扫描路径。请先到设置页启用至少一个扫描路径。"
                    : "扫描页已加载，可手动发起扫描。";

            OnPropertyChanged(nameof(CanRunScan));
        }
        catch (Exception exception)
        {
            StatusMessage = $"加载扫描概览失败：{exception.Message}";
        }
    }

    private async Task RunScanAsync()
    {
        try
        {
            IsRunning = true;
            StatusMessage = "正在扫描已启用的 WebDAV 路径，请稍候。";

            var result = await _mediaScanService.RunScanAsync();
            StatusMessage = result.StatusMessage;
            await LoadOverviewAsync(CancellationToken.None);
            _dataRefreshService.NotifyScanChanged();
        }
        catch (Exception exception)
        {
            StatusMessage = $"扫描失败：{exception.Message}";
        }
        finally
        {
            IsRunning = false;
        }
    }
}
