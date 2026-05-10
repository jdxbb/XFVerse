using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop;
using System.Windows.Threading;
using MediaLibrary.App.Helpers;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.App.ViewModels.Player;

namespace MediaLibrary.App.Views.Player;

public partial class PlayerWindow : Window
{
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const int WmLButtonDown = 0x0201;
    private const int WmLButtonDblClk = 0x0203;
    private const int WmMouseWheel = 0x020A;
    private const int WmMouseHWheel = 0x020E;
    private const int WhMouseLl = 14;
    private const int SmCxDoubleClk = 36;
    private const int SmCyDoubleClk = 37;
    private const int VkEscape = 0x1B;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const long DoubleClickSuppressMilliseconds = 250;
    private readonly DispatcherTimer _controlBarTimer;
    private readonly DispatcherTimer _cursorPollTimer;
    private readonly DispatcherTimer _interactionFeedbackTimer;
    private POINT? _lastCursorPosition;
    private PlayerWindowViewModel? _viewModel;
    private WindowState _previousState;
    private WindowStyle _previousStyle;
    private ResizeMode _previousResizeMode;
    private bool _isFullScreen;
    private bool _isSourceMenuOpen;
    private bool _isSubtitleMenuOpen;
    private bool _isAudioTrackMenuOpen;
    private bool _closeRequested;
    private bool _closeConfirmed;
    private POINT? _lastVideoClickPoint;
    private long _lastVideoClickTick;
    private long _lastVideoDoubleClickToggleTick;
    private bool _isPlayerCursorHidden;
    private HwndSource? _windowSource;
    private IntPtr _mouseHook;
    private LowLevelMouseProc? _mouseHookProc;
    private bool _threadFilterMessageInstalled;
    private bool _closeLifecycleStarted;

    public event EventHandler? CloseLifecycleStarted;

    public PlayerWindow()
    {
        InitializeComponent();
        _controlBarTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2.5)
        };
        _controlBarTimer.Tick += OnControlBarTimerTick;

        _cursorPollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(80)
        };
        _cursorPollTimer.Tick += OnCursorPollTimerTick;

        _interactionFeedbackTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1.2)
        };
        _interactionFeedbackTimer.Tick += OnInteractionFeedbackTimerTick;

        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) =>
        {
            _viewModel?.SetPlaybackHostHandle(PlaybackHost.HostHandle);
            Keyboard.Focus(this);
            ShowControlBar();
            RestartControlBarTimer();
            _cursorPollTimer.Start();
        };
        Activated += OnWindowActivated;
        Deactivated += OnWindowDeactivated;
        LocationChanged += (_, _) => UpdatePopupPlacements();
        SizeChanged += (_, _) => UpdatePopupPlacements();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        _viewModel = null;
        if (e.NewValue is PlayerWindowViewModel viewModel)
        {
            _viewModel = viewModel;
            if (IsLoaded)
            {
                viewModel.SetPlaybackHostHandle(PlaybackHost.HostHandle);
            }
        }
    }

    protected override async void OnClosing(CancelEventArgs e)
    {
        var closeStopwatch = Stopwatch.StartNew();
        if (_closeConfirmed)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        if (_closeRequested)
        {
            return;
        }

        _closeRequested = true;
        MpvPlaybackDiagnostics.Write("player-close-start source=window");
        IsEnabled = false;
        _controlBarTimer.Stop();
        _cursorPollTimer.Stop();
        _interactionFeedbackTimer.Stop();
        ShowPlayerCursor();
        CleanupInputHooks();
        ControlBarPopup.IsOpen = false;
        InteractionFeedbackPopup.IsOpen = false;
        BufferingOverlayPopup.IsOpen = false;
        OperationNoticePopup.IsOpen = false;
        Hide();
        MpvPlaybackDiagnostics.Write("player-close-window-hidden");
        NotifyCloseLifecycleStarted();
        MpvPlaybackDiagnostics.Write($"player-r4-ui-release reason=close elapsedMs={closeStopwatch.ElapsedMilliseconds}");

        var viewModel = DataContext as PlayerWindowViewModel;
        try
        {
            if (_viewModel is not null)
            {
                _viewModel = null;
            }

            if (viewModel is not null)
            {
                await viewModel.SaveAndCloseAsync();
            }
        }
        catch
        {
            // Closing must never take down the shell; invalid ultra-short sessions are ignored by the VM.
        }
        finally
        {
            try
            {
                viewModel?.Dispose();
            }
            catch
            {
                // Dispose can race playback engine shutdown after an immediate close.
            }

            DataContext = null;
            _closeConfirmed = true;
            MpvPlaybackDiagnostics.Write($"player-close-complete source=window elapsedMs={closeStopwatch.ElapsedMilliseconds}");
            if (closeStopwatch.ElapsedMilliseconds >= 3000)
            {
                MpvPlaybackDiagnostics.Write($"player-close-slow stage=window-closing elapsedMs={closeStopwatch.ElapsedMilliseconds}");
            }

            _ = Dispatcher.BeginInvoke(Close, DispatcherPriority.Background);
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _windowSource = PresentationSource.FromVisual(this) as HwndSource;
        _windowSource?.AddHook(PlayerWindowWndProc);
        if (!_threadFilterMessageInstalled)
        {
            ComponentDispatcher.ThreadFilterMessage += OnThreadFilterMessage;
            _threadFilterMessageInstalled = true;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        NotifyCloseLifecycleStarted();
        CleanupInputHooks();
        base.OnClosed(e);
    }

    private void NotifyCloseLifecycleStarted()
    {
        if (_closeLifecycleStarted)
        {
            return;
        }

        _closeLifecycleStarted = true;
        try
        {
            CloseLifecycleStarted?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            // Close notifications update shell UI only; they must not block player shutdown.
        }
    }

    private void ToggleFullScreen_Click(object sender, RoutedEventArgs e)
    {
        ToggleFullScreen();
    }

    private void ControlBar_PreviewMouseRightButton(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    private void PlayerSurface_PreviewMouseRightButton(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    private void SourceMenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not Button button)
        {
            return;
        }

        var menu = BuildSourceMenu(_viewModel);
        menu.PlacementTarget = button;
        menu.Placement = PlacementMode.Top;
        menu.Closed += (_, _) =>
        {
            _isSourceMenuOpen = false;
            RestartControlBarTimer();
            Keyboard.Focus(this);
        };
        _isSourceMenuOpen = true;
        button.ContextMenu = menu;
        menu.IsOpen = true;
    }

    private void SubtitleMenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not Button button)
        {
            return;
        }

        var menu = BuildSubtitleMenu(_viewModel);
        menu.PlacementTarget = button;
        menu.Placement = PlacementMode.Top;
        menu.Closed += (_, _) =>
        {
            _isSubtitleMenuOpen = false;
            RestartControlBarTimer();
            Keyboard.Focus(this);
        };
        _isSubtitleMenuOpen = true;
        button.ContextMenu = menu;
        menu.IsOpen = true;
    }

    private void AudioTrackMenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not Button button)
        {
            return;
        }

        var menu = BuildAudioTrackMenu(_viewModel);
        menu.PlacementTarget = button;
        menu.Placement = PlacementMode.Top;
        menu.Closed += (_, _) =>
        {
            _isAudioTrackMenuOpen = false;
            RestartControlBarTimer();
            Keyboard.Focus(this);
        };
        _isAudioTrackMenuOpen = true;
        button.ContextMenu = menu;
        menu.IsOpen = true;
    }

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        if (!IsActive)
        {
            return;
        }

        if (TryGetCursorPosition(out var cursorPosition))
        {
            _lastCursorPosition = cursorPosition;
        }

        HandleFullScreenPointerMovement();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (!IsActive)
        {
            return;
        }

        if (_isFullScreen && e.Key == Key.Escape)
        {
            ExitFullScreen();
            e.Handled = true;
            return;
        }

        if (IsAnyPlayerMenuOpen() || _viewModel is null)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Space:
                if (!HasNoModifiers())
                {
                    break;
                }

                if (_viewModel.TogglePlayPauseCommand.CanExecute(null))
                {
                    _viewModel.TogglePlayPauseCommand.Execute(null);
                }

                e.Handled = true;
                break;
            case Key.F:
                if (!HasNoModifiers())
                {
                    break;
                }

                ToggleFullScreen();
                e.Handled = true;
                break;
            case Key.M:
                if (!HasNoModifiers())
                {
                    break;
                }

                _viewModel.ToggleMute();
                ShowVolumeFeedback();
                e.Handled = true;
                break;
            case Key.Left:
                if (!HasNoModifiers() && !HasOnlyControlModifier())
                {
                    break;
                }

                _viewModel.SeekBySeconds(HasOnlyControlModifier() ? -30 : -5);
                e.Handled = true;
                break;
            case Key.Right:
                if (!HasNoModifiers() && !HasOnlyControlModifier())
                {
                    break;
                }

                _viewModel.SeekBySeconds(HasOnlyControlModifier() ? 30 : 5);
                e.Handled = true;
                break;
            case Key.Up:
                if (!HasNoModifiers())
                {
                    break;
                }

                _viewModel.AdjustVolume(5);
                ShowVolumeFeedback();
                e.Handled = true;
                break;
            case Key.Down:
                if (!HasNoModifiers())
                {
                    break;
                }

                _viewModel.AdjustVolume(-5);
                ShowVolumeFeedback();
                e.Handled = true;
                break;
        }

        if (e.Handled)
        {
            ShowControlBar();
            RestartControlBarTimer();
        }
    }

    private void VideoSurface_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || e.ClickCount != 2)
        {
            return;
        }

        if (e.OriginalSource is DependencyObject originalSource
            && IsDescendantOf(originalSource, ControlBar))
        {
            return;
        }

        if (TryToggleFullScreenFromVideoPoint(e.GetPosition(RootLayout)))
        {
            e.Handled = true;
        }
    }

    private static bool HasNoModifiers()
    {
        return Keyboard.Modifiers == ModifierKeys.None;
    }

    private static bool HasOnlyControlModifier()
    {
        return Keyboard.Modifiers == ModifierKeys.Control;
    }

    private bool IsAnyPlayerMenuOpen()
    {
        return _isSourceMenuOpen || _isSubtitleMenuOpen || _isAudioTrackMenuOpen;
    }

    private ContextMenu BuildSourceMenu(PlayerWindowViewModel viewModel)
    {
        var menu = new ContextMenu
        {
            DataContext = viewModel
        };

        if (viewModel.Sources.Count == 0)
        {
            menu.Items.Add(new MenuItem
            {
                Header = "\u65e0\u53ef\u7528\u64ad\u653e\u6e90",
                IsEnabled = false
            });
            return menu;
        }

        if (viewModel.SelectedSource is not null)
        {
            var selectedSource = viewModel.SelectedSource;
            menu.Items.Add(CreateVideoCacheStatusItem(selectedSource));

            menu.Items.Add(new Separator());
        }

        foreach (var source in viewModel.Sources)
        {
            menu.Items.Add(CreateSourceLeaf(source, viewModel));
        }

        return menu;
    }

    private static MenuItem CreateVideoCacheStatusItem(PlaybackSourceItem source)
    {
        var header = string.IsNullOrWhiteSpace(source.VideoCacheError)
            ? $"本地缓存：{source.VideoCacheStatusText}"
            : $"本地缓存：{source.VideoCacheStatusText}（{source.VideoCacheError}）";
        var item = new MenuItem
        {
            Header = header,
            IsEnabled = false
        };
        SuppressRightClick(item);
        return item;
    }

    private static MenuItem CreateSourceLeaf(
        PlaybackSourceItem source,
        PlayerWindowViewModel viewModel)
    {
        var isSelected = ReferenceEquals(viewModel.SelectedSource, source)
                         || viewModel.SelectedSource?.MediaFileId == source.MediaFileId;
        var item = new MenuItem
        {
            Header = CreateSourceMenuHeader(source, isSelected),
            IsCheckable = true,
            IsChecked = isSelected,
            ToolTip = BuildSourceToolTip(source)
        };
        SuppressRightClick(item);
        item.Click += (_, _) => viewModel.SelectedSource = source;
        return item;
    }

    private static StackPanel CreateSourceMenuHeader(PlaybackSourceItem source, bool isSelected)
    {
        var panel = new StackPanel
        {
            MaxWidth = 420d
        };

        panel.Children.Add(
            new TextBlock
            {
                Text = isSelected ? $"\u2713 {source.FileName}" : source.FileName,
                TextTrimming = TextTrimming.CharacterEllipsis
            });

        var summary = source.SourceSummaryText;
        if (!string.IsNullOrWhiteSpace(summary))
        {
            panel.Children.Add(
                new TextBlock
                {
                    Margin = new Thickness(0d, 3d, 0d, 0d),
                    FontSize = 12d,
                    Opacity = 0.72d,
                    Text = summary,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
        }

        return panel;
    }

    private static string BuildSourceToolTip(PlaybackSourceItem source)
    {
        var lines = new[]
            {
                source.SourceSummaryText,
                source.PlaybackHistoryText,
                string.IsNullOrWhiteSpace(source.FilePath) ? string.Empty : $"路径：{source.FilePath}"
            }
            .Where(line => !string.IsNullOrWhiteSpace(line));

        return string.Join(Environment.NewLine, lines);
    }

    private ContextMenu BuildSubtitleMenu(PlayerWindowViewModel viewModel)
    {
        viewModel.NotifySubtitleMenuOpened();
        var menu = new ContextMenu
        {
            DataContext = viewModel
        };

        menu.Items.Add(CreateSubtitleLeaf(viewModel.NoneSubtitle, viewModel));
        menu.Items.Add(new Separator());

        var embeddedGroup = CreateRightOpeningSubtitleGroup("\u5185\u5d4c");
        if (!viewModel.IsSubtitleTrackDiscoveryReady)
        {
            embeddedGroup.Items.Add(new MenuItem
            {
                Header = "\u6b63\u5728\u8bfb\u53d6\u5b57\u5e55\u8f68\u9053...",
                IsEnabled = false
            });
        }
        else if (viewModel.EmbeddedSubtitles.Count == 0)
        {
            embeddedGroup.Items.Add(new MenuItem
            {
                Header = "\u65e0\u5185\u5d4c\u5b57\u5e55",
                IsEnabled = false
            });
        }
        else
        {
            foreach (var subtitle in viewModel.EmbeddedSubtitles)
            {
                embeddedGroup.Items.Add(CreateSubtitleLeaf(subtitle, viewModel));
            }
        }

        menu.Items.Add(embeddedGroup);

        var externalGroup = CreateRightOpeningSubtitleGroup("\u5916\u6302");
        if (viewModel.ExternalSubtitles.Count == 0)
        {
            externalGroup.Items.Add(new MenuItem
            {
                Header = "\u65e0\u5916\u6302\u5b57\u5e55",
                IsEnabled = false
            });
        }
        else
        {
            foreach (var subtitle in viewModel.ExternalSubtitles)
            {
                externalGroup.Items.Add(CreateSubtitleLeaf(subtitle, viewModel));
            }
        }

        menu.Items.Add(externalGroup);
        return menu;
    }

    private static MenuItem CreateRightOpeningSubtitleGroup(string header)
    {
        var item = new MenuItem { Header = header };
        SuppressRightClick(item);
        item.Loaded += (_, _) => ConfigureSubmenuPopupToOpenRight(item);
        item.SubmenuOpened += (_, _) =>
        {
            ConfigureSubmenuPopupToOpenRight(item);
            _ = item.Dispatcher.BeginInvoke(
                () => ConfigureSubmenuPopupToOpenRight(item),
                DispatcherPriority.Loaded);
        };
        return item;
    }

    private static void ConfigureSubmenuPopupToOpenRight(MenuItem menuItem)
    {
        menuItem.ApplyTemplate();
        var popup = menuItem.Template.FindName("PART_Popup", menuItem) as Popup
                    ?? FindVisualChild<Popup>(menuItem);
        if (popup is null)
        {
            return;
        }

        popup.PlacementTarget = menuItem;
        popup.Placement = PlacementMode.Custom;
        popup.CustomPopupPlacementCallback = PlaceSubmenuOnRight;
        popup.HorizontalOffset = 0d;
        popup.VerticalOffset = 0d;

        if (popup.IsOpen)
        {
            MovePopupToRightOfMenuItem(menuItem, popup);
        }
    }

    private static CustomPopupPlacement[] PlaceSubmenuOnRight(Size popupSize, Size targetSize, Point offset)
    {
        return
        [
            new CustomPopupPlacement(
                new Point(targetSize.Width, 0d),
                PopupPrimaryAxis.Horizontal)
        ];
    }

    private static void MovePopupToRightOfMenuItem(MenuItem menuItem, Popup popup)
    {
        if (popup.Child is not UIElement child)
        {
            return;
        }

        var popupSource = PresentationSource.FromVisual(child) as HwndSource;
        if (popupSource?.Handle is not { } popupHandle || popupHandle == IntPtr.Zero)
        {
            return;
        }

        if (!GetWindowRect(popupHandle, out var rect))
        {
            return;
        }

        var popupTopLeft = menuItem.PointToScreen(new Point(menuItem.ActualWidth, 0d));
        _ = SetWindowPos(
            popupHandle,
            IntPtr.Zero,
            (int)Math.Round(popupTopLeft.X),
            (int)Math.Round(popupTopLeft.Y),
            Math.Max(1, rect.Right - rect.Left),
            Math.Max(1, rect.Bottom - rect.Top),
            SwpNoZOrder | SwpNoActivate);
    }

    private static MenuItem CreateSubtitleLeaf(
        PlaybackSubtitleItem subtitle,
        PlayerWindowViewModel viewModel)
    {
        var text = viewModel.IsSubtitleSelected(subtitle)
            ? $"\u2713 {subtitle.DisplayName}"
            : subtitle.DisplayName;
        var item = new MenuItem
        {
            Header = CreateMenuHeader(text),
            IsCheckable = true,
            IsChecked = viewModel.IsSubtitleSelected(subtitle),
            ToolTip = subtitle.TooltipText
        };
        SuppressRightClick(item);
        item.Click += (_, _) => viewModel.SelectSubtitleFromMenu(subtitle);
        return item;
    }

    private ContextMenu BuildAudioTrackMenu(PlayerWindowViewModel viewModel)
    {
        var menu = new ContextMenu
        {
            DataContext = viewModel
        };

        if (viewModel.AudioTracks.Count == 0)
        {
            menu.Items.Add(new MenuItem
            {
                Header = "\u97f3\u8f68\u4e0d\u53ef\u7528",
                IsEnabled = false
            });
            return menu;
        }

        foreach (var audioTrack in viewModel.AudioTracks)
        {
            menu.Items.Add(CreateAudioTrackLeaf(audioTrack, viewModel));
        }

        return menu;
    }

    private static MenuItem CreateAudioTrackLeaf(
        PlaybackAudioTrackItem audioTrack,
        PlayerWindowViewModel viewModel)
    {
        var text = viewModel.IsAudioTrackSelected(audioTrack)
            ? $"\u2713 {audioTrack.DisplayName}"
            : audioTrack.DisplayName;
        var item = new MenuItem
        {
            Header = CreateMenuHeader(text),
            IsCheckable = true,
            IsChecked = viewModel.IsAudioTrackSelected(audioTrack),
            ToolTip = audioTrack.TooltipText
        };
        SuppressRightClick(item);
        item.Click += (_, _) => viewModel.SelectAudioTrackFromMenu(audioTrack);
        return item;
    }

    private static void SuppressRightClick(MenuItem item)
    {
        item.PreviewMouseRightButtonDown += SuppressMouseRightButton;
        item.PreviewMouseRightButtonUp += SuppressMouseRightButton;
    }

    private static void SuppressMouseRightButton(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    private static TextBlock CreateMenuHeader(string text)
    {
        return new TextBlock
        {
            Text = text,
            MaxWidth = 360d,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
    }

    private void ProgressSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        if (sender is not Slider slider)
        {
            return;
        }

        if (e.OriginalSource is DependencyObject originalSource && FindThumb(originalSource) is not null)
        {
            return;
        }

        var point = e.GetPosition(slider);
        var ratio = slider.ActualWidth <= 0 ? 0 : point.X / slider.ActualWidth;
        ratio = Math.Clamp(ratio, 0d, 1d);
        slider.Value = slider.Minimum + ((slider.Maximum - slider.Minimum) * ratio);
        e.Handled = true;
    }

    private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.OriginalSource is DependencyObject originalSource && IsDescendantOf(originalSource, ControlBar))
        {
            return;
        }

        if (TryHandlePlayerMouseWheel(e.Delta, e.GetPosition(RootLayout)))
        {
            e.Handled = true;
        }
    }

    private bool TryHandlePlayerMouseWheel(int wheelDelta, Point point)
    {
        if (!IsActive || _viewModel is null || IsAnyPlayerMenuOpen())
        {
            return false;
        }

        if (point.X < 0
            || point.Y < 0
            || point.X > RootLayout.ActualWidth
            || point.Y > RootLayout.ActualHeight)
        {
            return false;
        }

        var step = wheelDelta > 0 ? 5 : -5;
        if (point.X < RootLayout.ActualWidth / 2d)
        {
            _viewModel.AdjustBrightness(step);
            ShowBrightnessFeedback();
        }
        else
        {
            _viewModel.AdjustVolume(step);
            ShowVolumeFeedback();
        }

        ShowControlBar();
        RestartControlBarTimer();
        return true;
    }

    private bool TryHandleNativeMouseWheel(int wheelDelta, POINT cursorPosition)
    {
        if (!IsCursorInsidePlayer(cursorPosition)
            || IsCursorInsideControlBar(cursorPosition))
        {
            return false;
        }

        var point = RootLayout.PointFromScreen(new Point(cursorPosition.X, cursorPosition.Y));
        return TryHandlePlayerMouseWheel(wheelDelta, point);
    }

    private bool TryHandleNativeVideoDoubleClick()
    {
        return TryGetCursorPosition(out var cursorPosition)
               && IsCursorInsideVideoInteractionArea(cursorPosition)
               && TryToggleFullScreenFromVideoPoint(RootLayout.PointFromScreen(new Point(cursorPosition.X, cursorPosition.Y)));
    }

    private bool TryHandleNativeVideoLeftButtonDown(POINT cursorPosition)
    {
        if (!IsCursorInsideVideoInteractionArea(cursorPosition))
        {
            _lastVideoClickPoint = null;
            _lastVideoClickTick = 0;
            return false;
        }

        var now = Environment.TickCount64;
        var previousPoint = _lastVideoClickPoint;
        var previousTick = _lastVideoClickTick;
        _lastVideoClickPoint = cursorPosition;
        _lastVideoClickTick = now;

        if (!previousPoint.HasValue
            || now - previousTick > GetDoubleClickTime()
            || Math.Abs(cursorPosition.X - previousPoint.Value.X) > GetSystemMetrics(SmCxDoubleClk)
            || Math.Abs(cursorPosition.Y - previousPoint.Value.Y) > GetSystemMetrics(SmCyDoubleClk))
        {
            return false;
        }

        _lastVideoClickPoint = null;
        _lastVideoClickTick = 0;
        return TryToggleFullScreenFromVideoPoint(RootLayout.PointFromScreen(new Point(cursorPosition.X, cursorPosition.Y)));
    }

    private bool TryToggleFullScreenFromVideoPoint(Point point)
    {
        if (!IsActive
            || _closeRequested
            || IsAnyPlayerMenuOpen()
            || RootLayout.ActualWidth <= 0
            || RootLayout.ActualHeight <= 0
            || point.X < 0
            || point.Y < 0
            || point.X > RootLayout.ActualWidth
            || point.Y > RootLayout.ActualHeight)
        {
            return false;
        }

        if (TryGetCursorPosition(out var cursorPosition)
            && IsCursorInsideControlBar(cursorPosition))
        {
            return false;
        }

        var now = Environment.TickCount64;
        if (now - _lastVideoDoubleClickToggleTick < DoubleClickSuppressMilliseconds)
        {
            return true;
        }

        _lastVideoDoubleClickToggleTick = now;
        ToggleFullScreen();
        return true;
    }

    private void ControlBar_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateControlBarPopupPlacement();
    }

    private void PromptOverlayPopup_Opened(object sender, EventArgs e)
    {
        UpdatePromptOverlayPopupPlacement();
    }

    private void OnControlBarTimerTick(object? sender, EventArgs e)
    {
        _controlBarTimer.Stop();
        if (!IsAnyPlayerMenuOpen() || !IsActive)
        {
            HideControlBar();
        }
    }

    private void OnInteractionFeedbackTimerTick(object? sender, EventArgs e)
    {
        _interactionFeedbackTimer.Stop();
        VolumeFeedback.Visibility = Visibility.Collapsed;
        BrightnessFeedback.Visibility = Visibility.Collapsed;
        InteractionFeedbackPopup.IsOpen = false;
    }

    private void ShowVolumeFeedback()
    {
        if (_viewModel is null)
        {
            return;
        }

        VolumeFeedbackText.Text = _viewModel.VolumeFeedbackText;
        VolumeFeedbackBar.Value = _viewModel.Volume;
        BrightnessFeedback.Visibility = Visibility.Collapsed;
        ShowInteractionFeedbackPopup();
        VolumeFeedback.Visibility = Visibility.Visible;
        RestartInteractionFeedbackTimer();
    }

    private void ShowBrightnessFeedback()
    {
        if (_viewModel is null)
        {
            return;
        }

        BrightnessFeedbackText.Text = _viewModel.BrightnessText;
        BrightnessFeedbackBar.Value = _viewModel.Brightness;
        VolumeFeedback.Visibility = Visibility.Collapsed;
        ShowInteractionFeedbackPopup();
        BrightnessFeedback.Visibility = Visibility.Visible;
        RestartInteractionFeedbackTimer();
    }

    private void ShowInteractionFeedbackPopup()
    {
        UpdateInteractionFeedbackPopupPlacement();
        InteractionFeedbackPopup.IsOpen = true;
        UpdateInteractionFeedbackPopupPlacement();
    }

    private void RestartInteractionFeedbackTimer()
    {
        _interactionFeedbackTimer.Stop();
        _interactionFeedbackTimer.Start();
    }

    private void ToggleFullScreen()
    {
        if (_isFullScreen)
        {
            ExitFullScreen();
            return;
        }

        _previousState = WindowState;
        _previousStyle = WindowStyle;
        _previousResizeMode = ResizeMode;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        WindowState = WindowState.Maximized;
        _isFullScreen = true;
        _lastCursorPosition = null;
        ShowControlBar();
        RestartControlBarTimer();
    }

    private void ExitFullScreen()
    {
        WindowStyle = _previousStyle;
        ResizeMode = _previousResizeMode;
        WindowState = _previousState;
        _isFullScreen = false;
        _controlBarTimer.Stop();
        _lastCursorPosition = null;
        ShowControlBar();
        RestartControlBarTimer();
    }

    private void RestartControlBarTimer()
    {
        _controlBarTimer.Stop();
        if (!IsAnyPlayerMenuOpen() && IsActive)
        {
            _controlBarTimer.Start();
        }
    }

    private void ShowControlBar()
    {
        ShowPlayerCursor();
        if (!IsActive)
        {
            HideControlBar();
            return;
        }

        UpdateControlBarPopupPlacement();
        ControlBarPopup.IsOpen = true;
        UpdateControlBarPopupPlacement();
        ControlBar.Opacity = 1d;
        ControlBar.IsHitTestVisible = true;
    }

    private void HideControlBar()
    {
        _controlBarTimer.Stop();
        ControlBarPopup.IsOpen = false;
        ControlBar.IsHitTestVisible = false;
        UpdatePlayerCursorForControlBarState();
    }

    private void UpdateControlBarPopupPlacement()
    {
        if (!IsLoaded)
        {
            return;
        }

        ControlBarPopup.HorizontalOffset = 0d;
        ControlBarPopup.VerticalOffset = Math.Max(0d, RootLayout.ActualHeight - ControlBar.ActualHeight);
        MoveControlBarPopupWindow();
    }

    private void UpdatePopupPlacements()
    {
        UpdateControlBarPopupPlacement();
        UpdateInteractionFeedbackPopupPlacement();
        UpdatePromptOverlayPopupPlacement();
    }

    private void UpdateInteractionFeedbackPopupPlacement()
    {
        if (!IsLoaded)
        {
            return;
        }

        InteractionFeedbackLayer.Width = Math.Max(1d, RootLayout.ActualWidth);
        InteractionFeedbackLayer.Height = Math.Max(1d, RootLayout.ActualHeight);
        InteractionFeedbackPopup.HorizontalOffset = 0d;
        InteractionFeedbackPopup.VerticalOffset = 0d;
        MoveInteractionFeedbackPopupWindow();
    }

    private void UpdatePromptOverlayPopupPlacement()
    {
        if (!IsLoaded)
        {
            return;
        }

        BufferingOverlayLayer.Width = Math.Max(1d, RootLayout.ActualWidth);
        BufferingOverlayLayer.Height = Math.Max(1d, RootLayout.ActualHeight);
        BufferingOverlayPopup.HorizontalOffset = 0d;
        BufferingOverlayPopup.VerticalOffset = 0d;
        MoveFullWindowPopup(BufferingOverlayPopup, BufferingOverlayLayer, UpdatePromptOverlayPopupPlacement);

        OperationNoticeLayer.Width = Math.Max(1d, RootLayout.ActualWidth);
        OperationNoticeLayer.Height = Math.Max(1d, RootLayout.ActualHeight);
        OperationNoticePopup.HorizontalOffset = 0d;
        OperationNoticePopup.VerticalOffset = 0d;
        MoveFullWindowPopup(OperationNoticePopup, OperationNoticeLayer, UpdatePromptOverlayPopupPlacement);
    }

    private void MoveControlBarPopupWindow()
    {
        if (!ControlBarPopup.IsOpen)
        {
            return;
        }

        var popupSource = PresentationSource.FromVisual(ControlBar) as HwndSource;
        if (popupSource?.Handle is not { } popupHandle || popupHandle == IntPtr.Zero)
        {
            _ = Dispatcher.BeginInvoke(UpdateControlBarPopupPlacement, DispatcherPriority.Loaded);
            return;
        }

        var popupTopLeft = RootLayout.PointToScreen(
            new Point(0d, Math.Max(0d, RootLayout.ActualHeight - ControlBar.ActualHeight)));
        var transformToDevice = PresentationSource.FromVisual(RootLayout)?.CompositionTarget?.TransformToDevice
                                ?? Matrix.Identity;
        var popupSize = transformToDevice.Transform(new Point(RootLayout.ActualWidth, ControlBar.ActualHeight));

        _ = SetWindowPos(
            popupHandle,
            IntPtr.Zero,
            (int)Math.Round(popupTopLeft.X),
            (int)Math.Round(popupTopLeft.Y),
            Math.Max(1, (int)Math.Round(popupSize.X)),
            Math.Max(1, (int)Math.Round(popupSize.Y)),
            SwpNoZOrder | SwpNoActivate);
    }

    private void MoveInteractionFeedbackPopupWindow()
    {
        if (!InteractionFeedbackPopup.IsOpen)
        {
            return;
        }

        var popupSource = PresentationSource.FromVisual(InteractionFeedbackLayer) as HwndSource;
        if (popupSource?.Handle is not { } popupHandle || popupHandle == IntPtr.Zero)
        {
            _ = Dispatcher.BeginInvoke(UpdateInteractionFeedbackPopupPlacement, DispatcherPriority.Loaded);
            return;
        }

        var popupTopLeft = RootLayout.PointToScreen(new Point(0d, 0d));
        var transformToDevice = PresentationSource.FromVisual(RootLayout)?.CompositionTarget?.TransformToDevice
                                ?? Matrix.Identity;
        var popupSize = transformToDevice.Transform(new Point(RootLayout.ActualWidth, RootLayout.ActualHeight));

        _ = SetWindowPos(
            popupHandle,
            IntPtr.Zero,
            (int)Math.Round(popupTopLeft.X),
            (int)Math.Round(popupTopLeft.Y),
            Math.Max(1, (int)Math.Round(popupSize.X)),
            Math.Max(1, (int)Math.Round(popupSize.Y)),
            SwpNoZOrder | SwpNoActivate);
    }

    private void MoveFullWindowPopup(Popup popup, FrameworkElement layer, Action retry)
    {
        if (!popup.IsOpen)
        {
            return;
        }

        var popupSource = PresentationSource.FromVisual(layer) as HwndSource;
        if (popupSource?.Handle is not { } popupHandle || popupHandle == IntPtr.Zero)
        {
            _ = Dispatcher.BeginInvoke(retry, DispatcherPriority.Loaded);
            return;
        }

        var popupTopLeft = RootLayout.PointToScreen(new Point(0d, 0d));
        var transformToDevice = PresentationSource.FromVisual(RootLayout)?.CompositionTarget?.TransformToDevice
                                ?? Matrix.Identity;
        var popupSize = transformToDevice.Transform(new Point(RootLayout.ActualWidth, RootLayout.ActualHeight));

        _ = SetWindowPos(
            popupHandle,
            IntPtr.Zero,
            (int)Math.Round(popupTopLeft.X),
            (int)Math.Round(popupTopLeft.Y),
            Math.Max(1, (int)Math.Round(popupSize.X)),
            Math.Max(1, (int)Math.Round(popupSize.Y)),
            SwpNoZOrder | SwpNoActivate);
    }

    private void OnCursorPollTimerTick(object? sender, EventArgs e)
    {
        if (!IsActive)
        {
            HideControlBar();
            return;
        }

        if (!TryGetCursorPosition(out var cursorPosition))
        {
            ShowPlayerCursor();
            return;
        }

        if (!IsCursorInsidePlayer(cursorPosition))
        {
            ShowPlayerCursor();
            return;
        }

        if (_lastCursorPosition.HasValue
            && _lastCursorPosition.Value.X == cursorPosition.X
            && _lastCursorPosition.Value.Y == cursorPosition.Y)
        {
            return;
        }

        _lastCursorPosition = cursorPosition;
        HandleFullScreenPointerMovement();
    }

    private bool IsCursorInsidePlayer(POINT cursorPosition)
    {
        if (!IsLoaded || RootLayout.ActualWidth <= 0 || RootLayout.ActualHeight <= 0)
        {
            return false;
        }

        var point = RootLayout.PointFromScreen(new Point(cursorPosition.X, cursorPosition.Y));
        return point.X >= 0
               && point.Y >= 0
               && point.X <= RootLayout.ActualWidth
               && point.Y <= RootLayout.ActualHeight;
    }

    private bool IsCursorInsideControlBar(POINT cursorPosition)
    {
        if (!ControlBarPopup.IsOpen || ControlBar.ActualWidth <= 0 || ControlBar.ActualHeight <= 0)
        {
            return false;
        }

        var point = ControlBar.PointFromScreen(new Point(cursorPosition.X, cursorPosition.Y));
        return point.X >= 0
               && point.Y >= 0
               && point.X <= ControlBar.ActualWidth
               && point.Y <= ControlBar.ActualHeight;
    }

    private bool IsCursorInsideVideoInteractionArea(POINT cursorPosition)
    {
        return IsActive
               && !_closeRequested
               && !IsAnyPlayerMenuOpen()
               && IsCursorInsidePlayer(cursorPosition)
               && !IsCursorInsideControlBar(cursorPosition);
    }

    private void HandleFullScreenPointerMovement()
    {
        ShowControlBar();
        RestartControlBarTimer();
    }

    private void UpdatePlayerCursorForControlBarState()
    {
        if (ControlBarPopup.IsOpen
            || !IsActive
            || _closeRequested
            || !TryGetCursorPosition(out var cursorPosition)
            || !IsCursorInsidePlayer(cursorPosition))
        {
            ShowPlayerCursor();
            return;
        }

        HidePlayerCursor();
    }

    private void HidePlayerCursor()
    {
        if (_isPlayerCursorHidden)
        {
            return;
        }

        _isPlayerCursorHidden = true;
        Cursor = Cursors.None;
        RootLayout.Cursor = Cursors.None;
        PlaybackHost.SetCursorHidden(true);
    }

    private void ShowPlayerCursor()
    {
        if (!_isPlayerCursorHidden)
        {
            return;
        }

        _isPlayerCursorHidden = false;
        Cursor = null;
        RootLayout.Cursor = null;
        PlaybackHost.SetCursorHidden(false);
        Mouse.UpdateCursor();
    }

    private void OnWindowActivated(object? sender, EventArgs e)
    {
        Keyboard.Focus(this);
        _lastCursorPosition = null;
        _cursorPollTimer.Start();
        InstallMouseWheelHook();

        if (TryGetCursorPosition(out var cursorPosition) && IsCursorInsidePlayer(cursorPosition))
        {
            ShowControlBar();
            RestartControlBarTimer();
        }
    }

    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        _lastCursorPosition = null;
        _lastVideoClickPoint = null;
        _lastVideoClickTick = 0;
        _isSourceMenuOpen = false;
        _isSubtitleMenuOpen = false;
        _isAudioTrackMenuOpen = false;
        _cursorPollTimer.Stop();
        UninstallMouseWheelHook();
        HideControlBar();
    }

    private void OnThreadFilterMessage(ref MSG msg, ref bool handled)
    {
        if (handled)
        {
            return;
        }

        if (_isFullScreen
            && (msg.message == WmKeyDown || msg.message == WmSysKeyDown)
            && msg.wParam.ToInt32() == VkEscape)
        {
            ExitFullScreen();
            handled = true;
            return;
        }

        if ((msg.message == WmMouseWheel || msg.message == WmMouseHWheel)
            && TryHandleNativeMouseWheel(GetWheelDelta(msg.wParam), GetScreenPoint(msg.lParam)))
        {
            handled = true;
            return;
        }

        if (msg.message == WmLButtonDblClk && TryHandleNativeVideoDoubleClick())
        {
            handled = true;
        }
    }

    private IntPtr PlayerWindowWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if ((msg == WmMouseWheel || msg == WmMouseHWheel)
            && TryHandleNativeMouseWheel(GetWheelDelta(wParam), GetScreenPoint(lParam)))
        {
            handled = true;
        }
        else if (msg == WmLButtonDblClk && TryHandleNativeVideoDoubleClick())
        {
            handled = true;
        }

        return IntPtr.Zero;
    }

    private void InstallMouseWheelHook()
    {
        if (_mouseHook != IntPtr.Zero || _closeRequested)
        {
            return;
        }

        _mouseHookProc = LowLevelMouseHookProc;
        var moduleHandle = GetModuleHandle(null);
        _mouseHook = SetWindowsHookEx(WhMouseLl, _mouseHookProc, moduleHandle, 0);
        if (_mouseHook == IntPtr.Zero && moduleHandle != IntPtr.Zero)
        {
            _mouseHook = SetWindowsHookEx(WhMouseLl, _mouseHookProc, IntPtr.Zero, 0);
        }

        if (_mouseHook == IntPtr.Zero)
        {
            _mouseHookProc = null;
        }
    }

    private void UninstallMouseWheelHook()
    {
        if (_mouseHook == IntPtr.Zero)
        {
            return;
        }

        _ = UnhookWindowsHookEx(_mouseHook);
        _mouseHook = IntPtr.Zero;
        _mouseHookProc = null;
    }

    private IntPtr LowLevelMouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && IsActive && !_closeRequested)
        {
            var hookData = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            var message = wParam.ToInt32();
            if (message == WmLButtonDown && TryHandleNativeVideoLeftButtonDown(hookData.Point))
            {
                return new IntPtr(1);
            }

            if (message is WmMouseWheel or WmMouseHWheel
                && TryHandleNativeMouseWheel(unchecked((short)((hookData.MouseData >> 16) & 0xffff)), hookData.Point))
            {
                return new IntPtr(1);
            }
        }

        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private void CleanupInputHooks()
    {
        if (_threadFilterMessageInstalled)
        {
            ComponentDispatcher.ThreadFilterMessage -= OnThreadFilterMessage;
            _threadFilterMessageInstalled = false;
        }

        _windowSource?.RemoveHook(PlayerWindowWndProc);
        _windowSource = null;
        UninstallMouseWheelHook();
    }

    private static int GetWheelDelta(IntPtr wParam)
    {
        return unchecked((short)((wParam.ToInt64() >> 16) & 0xffff));
    }

    private static POINT GetScreenPoint(IntPtr lParam)
    {
        var value = lParam.ToInt64();
        return new POINT
        {
            X = unchecked((short)(value & 0xffff)),
            Y = unchecked((short)((value >> 16) & 0xffff))
        };
    }

    private static bool TryGetCursorPosition(out POINT point)
    {
        return GetCursorPos(out point);
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(
        int idHook,
        LowLevelMouseProc lpfn,
        IntPtr hMod,
        uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;

        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;

        public int Top;

        public int Right;

        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT Point;

        public uint MouseData;

        public uint Flags;

        public uint Time;

        public IntPtr ExtraInfo;
    }

    private static T? FindVisualChild<T>(DependencyObject current)
        where T : DependencyObject
    {
        var childCount = VisualTreeHelper.GetChildrenCount(current);
        for (var index = 0; index < childCount; index++)
        {
            var child = VisualTreeHelper.GetChild(current, index);
            if (child is T typedChild)
            {
                return typedChild;
            }

            var nestedChild = FindVisualChild<T>(child);
            if (nestedChild is not null)
            {
                return nestedChild;
            }
        }

        return null;
    }

    private static Thumb? FindThumb(DependencyObject? current)
    {
        while (current is not null)
        {
            if (current is Thumb thumb)
            {
                return thumb;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static bool IsDescendantOf(DependencyObject current, DependencyObject ancestor)
    {
        var node = current;
        while (node is not null)
        {
            if (ReferenceEquals(node, ancestor))
            {
                return true;
            }

            node = VisualTreeHelper.GetParent(node);
        }

        return false;
    }
}
