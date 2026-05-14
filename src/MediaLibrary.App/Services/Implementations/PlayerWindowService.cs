using MediaLibrary.App.Services.Interfaces;
using MediaLibrary.App.ViewModels.Player;
using MediaLibrary.App.Views.Player;
using System.Windows.Threading;

namespace MediaLibrary.App.Services.Implementations;

public sealed class PlayerWindowService : IPlayerWindowService
{
    private readonly IDataRefreshService _dataRefreshService;
    private PlayerWindow? _activeWindow;
    private bool _isOpening;
    private bool _closeNotified;

    public PlayerWindowService(IDataRefreshService dataRefreshService)
    {
        _dataRefreshService = dataRefreshService;
    }

    public event EventHandler? PlayerWindowClosed;

    public bool IsPlayerOpen => _activeWindow is not null || _isOpening;

    public int? ActiveMovieId { get; private set; }

    public int? ActiveEpisodeId { get; private set; }

    public int? ActiveMediaFileId { get; private set; }

    public async Task OpenAsync(int movieId, int? mediaFileId = null, CancellationToken cancellationToken = default)
    {
        if (_activeWindow is not null)
        {
            _activeWindow.Activate();
            return;
        }

        if (_isOpening)
        {
            return;
        }

        _isOpening = true;
        ActiveMovieId = movieId;
        ActiveEpisodeId = null;
        ActiveMediaFileId = mediaFileId;
        PlayerWindow? window = null;

        var viewModel = AppServiceProvider.GetRequiredService<PlayerWindowViewModel>();
        try
        {
            window = new PlayerWindow
            {
                DataContext = viewModel
            };
            _activeWindow = window;
            _closeNotified = false;
            window.CloseLifecycleStarted += OnPlayerWindowCloseLifecycleStarted;
            window.Closed += OnPlayerWindowClosed;

            window.Show();

            // The video output must start after WPF creates the playback host.
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            if (!ReferenceEquals(_activeWindow, window) || !window.IsVisible)
            {
                return;
            }

            await viewModel.InitializeAsync(movieId, mediaFileId, cancellationToken);
        }
        catch
        {
            if (window is not null)
            {
                window.CloseLifecycleStarted -= OnPlayerWindowCloseLifecycleStarted;
                window.Closed -= OnPlayerWindowClosed;
                if (window.IsVisible)
                {
                    try
                    {
                        window.Close();
                    }
                    catch
                    {
                        // A failed open can race with user-initiated close; the original open error is enough.
                    }
                }

                if (ReferenceEquals(_activeWindow, window))
                {
                    _activeWindow = null;
                }
            }

            ActiveMovieId = null;
            ActiveEpisodeId = null;
            ActiveMediaFileId = null;

            throw;
        }
        finally
        {
            _isOpening = false;
        }
    }

    public async Task OpenEpisodeAsync(int episodeId, int? mediaFileId = null, CancellationToken cancellationToken = default)
    {
        if (_activeWindow is not null)
        {
            _activeWindow.Activate();
            return;
        }

        if (_isOpening)
        {
            return;
        }

        _isOpening = true;
        ActiveMovieId = null;
        ActiveEpisodeId = episodeId;
        ActiveMediaFileId = mediaFileId;
        PlayerWindow? window = null;

        var viewModel = AppServiceProvider.GetRequiredService<PlayerWindowViewModel>();
        try
        {
            window = new PlayerWindow
            {
                DataContext = viewModel
            };
            _activeWindow = window;
            _closeNotified = false;
            window.CloseLifecycleStarted += OnPlayerWindowCloseLifecycleStarted;
            window.Closed += OnPlayerWindowClosed;

            window.Show();

            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            if (!ReferenceEquals(_activeWindow, window) || !window.IsVisible)
            {
                return;
            }

            await viewModel.InitializeEpisodeAsync(episodeId, mediaFileId, cancellationToken);
        }
        catch
        {
            if (window is not null)
            {
                window.CloseLifecycleStarted -= OnPlayerWindowCloseLifecycleStarted;
                window.Closed -= OnPlayerWindowClosed;
                if (window.IsVisible)
                {
                    try
                    {
                        window.Close();
                    }
                    catch
                    {
                    }
                }

                if (ReferenceEquals(_activeWindow, window))
                {
                    _activeWindow = null;
                }
            }

            ActiveMovieId = null;
            ActiveEpisodeId = null;
            ActiveMediaFileId = null;

            throw;
        }
        finally
        {
            _isOpening = false;
        }
    }

    private void OnPlayerWindowCloseLifecycleStarted(object? sender, EventArgs e)
    {
        if (sender is PlayerWindow window)
        {
            CompleteWindowClose(window);
        }
    }

    private void OnPlayerWindowClosed(object? sender, EventArgs e)
    {
        if (sender is PlayerWindow window)
        {
            CompleteWindowClose(window);
        }
    }

    private void CompleteWindowClose(PlayerWindow window)
    {
        window.CloseLifecycleStarted -= OnPlayerWindowCloseLifecycleStarted;
        window.Closed -= OnPlayerWindowClosed;

        if (ReferenceEquals(_activeWindow, window))
        {
            _activeWindow = null;
        }

        ActiveMovieId = null;
        ActiveEpisodeId = null;
        ActiveMediaFileId = null;

        if (_closeNotified)
        {
            return;
        }

        _closeNotified = true;

        try
        {
            _dataRefreshService.NotifyPlaybackChanged();
        }
        catch
        {
            // Playback refresh is best-effort; closing the player must not crash the app.
        }

        try
        {
            PlayerWindowClosed?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            // Subscribers update UI state only; a subscriber failure must not escape window closing.
        }
    }
}
