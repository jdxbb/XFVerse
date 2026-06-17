using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop;
using System.Windows.Threading;
using MediaLibrary.App.Helpers;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.App.ViewModels.Player;

namespace MediaLibrary.App.Views.Player;

public partial class PlayerWindow : Window
{
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const int WmNcHitTest = 0x0084;
    private const int WmGetMinMaxInfo = 0x0024;
    private const int WmSizing = 0x0214;
    private const int WmLButtonDblClk = 0x0203;
    private const int WmMouseWheel = 0x020A;
    private const int WmMouseHWheel = 0x020E;
    private const int WhMouseLl = 14;
    private const int MonitorDefaultToNearest = 0x00000002;
    private const int WmszLeft = 1;
    private const int WmszRight = 2;
    private const int WmszTop = 3;
    private const int WmszTopLeft = 4;
    private const int WmszTopRight = 5;
    private const int WmszBottom = 6;
    private const int WmszBottomLeft = 7;
    private const int WmszBottomRight = 8;
    private const int VkEscape = 0x1B;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const long DoubleClickSuppressMilliseconds = 250;
    private const long ControlBarAutoHideMilliseconds = 2500;
    private const double ControlBarBottomLift = 14d;
    private const double ControlBarWidthRatio = 0.75d;
    private const double OnlineSubtitleSubmenuItemMinWidth = 224d;
    private const double OnlineSubtitleSubmenuHeaderMaxWidth = 176d;
    private const double OnlineSubtitleMenuFontSize = 11d;
    private const double ResizeHitTestThickness = 6d;
    private readonly DispatcherTimer _controlBarTimer;
    private readonly DispatcherTimer _cursorPollTimer;
    private readonly DispatcherTimer _interactionFeedbackTimer;
    private readonly DispatcherTimer _volumeHoverCloseTimer;
    private POINT? _lastCursorPosition;
    private PlayerWindowViewModel? _viewModel;
    private WindowState _previousState;
    private WindowState _lastWindowState = WindowState.Normal;
    private WindowStyle _previousStyle;
    private ResizeMode _previousResizeMode;
    private bool _isFullScreen;
    private bool _isRestoringMaximizedForDrag;
    private bool _isSourceMenuOpen;
    private bool _isSubtitleMenuOpen;
    private bool _isAudioTrackMenuOpen;
    private bool _closeRequested;
    private bool _closeConfirmed;
    private bool _isFullScreenChromeVisible = true;
    private double _windowAspectRatio = 1180d / 760d;
    private OnlineSubtitleSearchWindow? _onlineSubtitleSearchWindow;
    private long _lastVideoDoubleClickToggleTick;
    private long _lastControlBarActivityTick;
    private bool _isPlayerCursorHidden;
    private HwndSource? _windowSource;
    private IntPtr _mouseHook;
    private LowLevelMouseProc? _mouseHookProc;
    private bool _threadFilterMessageInstalled;
    private bool _closeLifecycleStarted;
    private Button? _openMenuButton;
    private ContextMenu? _openContextMenu;
    private Rect _controlBarScreenBounds = Rect.Empty;

    public event EventHandler? CloseLifecycleStarted;

    public bool StartFullscreenOnOpen { get; set; } = true;

    public PlayerWindow()
    {
        InitializeComponent();
        _controlBarTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(ControlBarAutoHideMilliseconds)
        };
        _controlBarTimer.Tick += OnControlBarTimerTick;

        _cursorPollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(25)
        };
        _cursorPollTimer.Tick += OnCursorPollTimerTick;

        _interactionFeedbackTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1.2)
        };
        _interactionFeedbackTimer.Tick += OnInteractionFeedbackTimerTick;

        _volumeHoverCloseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(220)
        };
        _volumeHoverCloseTimer.Tick += OnVolumeHoverCloseTimerTick;

        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) =>
        {
            _viewModel?.SetPlaybackHostHandle(PlaybackHost.HostHandle);
            CaptureWindowAspectRatio();
            Keyboard.Focus(this);
            ShowControlBar();
            RestartControlBarTimer();
            _cursorPollTimer.Start();
            if (StartFullscreenOnOpen)
            {
                EnterFullScreen();
            }
            else
            {
                CenterWindowOnCurrentMonitor();
            }

            UpdatePlayerChromeVisibility();
            UpdatePlayerMaximizeRestoreButton();
        };
        Activated += OnWindowActivated;
        Deactivated += OnWindowDeactivated;
        StateChanged += OnWindowStateChanged;
        LocationChanged += (_, _) => UpdatePopupPlacements();
        SizeChanged += (_, _) => UpdatePopupPlacements();
        UpdatePlayerChromeVisibility();
        UpdatePlayerMaximizeRestoreButton();
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
        _volumeHoverCloseTimer.Stop();
        ShowPlayerCursor();
        CleanupInputHooks();
        ControlBarPopup.IsOpen = false;
        InteractionFeedbackPopup.IsOpen = false;
        VolumeHoverPopup.IsOpen = false;
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
        _volumeHoverCloseTimer.Stop();
        StateChanged -= OnWindowStateChanged;
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

    private void PlayerMinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void PlayerMaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
    {
        TogglePlayerMaximizeRestore();
    }

    private void PlayerCloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void VolumeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.ToggleMute();
        ShowVolumeFeedback();
        e.Handled = true;
    }

    private void VolumeArea_MouseEnter(object sender, MouseEventArgs e)
    {
        ShowVolumeHoverPopup();
    }

    private void VolumeArea_MouseLeave(object sender, MouseEventArgs e)
    {
        ScheduleVolumeHoverPopupClose();
    }

    private void VolumeHoverPopup_MouseEnter(object sender, MouseEventArgs e)
    {
        _volumeHoverCloseTimer.Stop();
    }

    private void VolumeHoverPopup_MouseLeave(object sender, MouseEventArgs e)
    {
        ScheduleVolumeHoverPopupClose();
    }

    private void DisplayStatusTextBlock_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is TextBlock textBlock)
        {
            UpdateTrimmedTextToolTip(textBlock);
        }
    }

    private void DisplayStatusTextBlock_TargetUpdated(object sender, DataTransferEventArgs e)
    {
        if (sender is TextBlock textBlock)
        {
            UpdateTrimmedTextToolTip(textBlock);
        }
    }

    private static void UpdateTrimmedTextToolTip(TextBlock textBlock)
    {
        if (string.IsNullOrWhiteSpace(textBlock.Text) || textBlock.ActualWidth <= 0)
        {
            textBlock.ToolTip = null;
            return;
        }

        var typeface = new Typeface(
            textBlock.FontFamily,
            textBlock.FontStyle,
            textBlock.FontWeight,
            textBlock.FontStretch);
        var dpi = VisualTreeHelper.GetDpi(textBlock);
        var formattedText = new FormattedText(
            textBlock.Text,
            CultureInfo.CurrentUICulture,
            textBlock.FlowDirection,
            typeface,
            textBlock.FontSize,
            textBlock.Foreground,
            dpi.PixelsPerDip);

        textBlock.ToolTip = formattedText.WidthIncludingTrailingWhitespace > textBlock.ActualWidth + 0.5d
            ? textBlock.Text
            : null;
    }

    private void PlayerTitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left
            || IsInteractiveElement(e.OriginalSource as DependencyObject))
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            ToggleFullScreen();
            e.Handled = true;
            return;
        }

        if (_isFullScreen)
        {
            return;
        }

        BeginWindowDrag(e);
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        var previousState = _lastWindowState;
        _lastWindowState = WindowState;
        UpdatePlayerMaximizeRestoreButton();
        if (!_isFullScreen
            && !_isRestoringMaximizedForDrag
            && previousState == WindowState.Maximized
            && WindowState == WindowState.Normal)
        {
            CenterWindowOnCurrentMonitor();
            _ = Dispatcher.BeginInvoke(CenterWindowOnCurrentMonitor, DispatcherPriority.Loaded);
        }
    }

    private void BeginWindowDrag(MouseButtonEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            RestoreWindowForDrag(e);
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // DragMove requires the left mouse button to remain pressed.
        }
    }

    private void RestoreWindowForDrag(MouseButtonEventArgs e)
    {
        var restoreBounds = RestoreBounds;
        var mousePosition = PointToScreen(e.GetPosition(this));
        var source = PresentationSource.FromVisual(this);

        if (source?.CompositionTarget is not null)
        {
            mousePosition = source.CompositionTarget.TransformFromDevice.Transform(mousePosition);
        }

        _isRestoringMaximizedForDrag = true;
        try
        {
            WindowState = WindowState.Normal;
            Left = mousePosition.X - (restoreBounds.Width * 0.5);
            Top = Math.Max(SystemParameters.VirtualScreenTop, mousePosition.Y - 12);
        }
        finally
        {
            _isRestoringMaximizedForDrag = false;
        }
    }

    private void TogglePlayerMaximizeRestore()
    {
        if (!_isFullScreen && WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            CenterWindowOnCurrentMonitor();
            _ = Dispatcher.BeginInvoke(CenterWindowOnCurrentMonitor, DispatcherPriority.Loaded);
            return;
        }

        ToggleFullScreen();
    }

    private void UpdatePlayerMaximizeRestoreButton()
    {
        UpdateMaximizeRestoreButton(PlayerMaximizeRestoreButton);
        UpdateMaximizeRestoreButton(FullscreenPlayerMaximizeRestoreButton);
    }

    private void UpdatePlayerChromeVisibility()
    {
        if (PlayerTitleBar is null || PlayerTitleBarPopup is null)
        {
            return;
        }

        PlayerTitleBar.Visibility = _isFullScreen ? Visibility.Collapsed : Visibility.Visible;
        PlayerTitleBarPopup.IsOpen = _isFullScreen
                                      && _isFullScreenChromeVisible
                                      && IsActive
                                      && !_closeRequested;
        UpdatePlayerTitleBarPopupPlacement();
    }

    private void UpdateMaximizeRestoreButton(Button? button)
    {
        if (button is null)
        {
            return;
        }

        if (_isFullScreen || WindowState == WindowState.Maximized)
        {
            button.Content = "copy-simple";
            button.ToolTip = null;
            return;
        }

        button.Content = "square";
        button.ToolTip = null;
    }

    private void CaptureWindowAspectRatio()
    {
        if (ActualWidth > 0 && ActualHeight > 0)
        {
            _windowAspectRatio = ActualWidth / ActualHeight;
        }
    }

    private void ShowFullScreenChrome()
    {
        if (!_isFullScreen || _isFullScreenChromeVisible)
        {
            return;
        }

        _isFullScreenChromeVisible = true;
        UpdatePlayerChromeVisibility();
    }

    private void HideFullScreenChrome()
    {
        if (!_isFullScreen || !_isFullScreenChromeVisible)
        {
            return;
        }

        _isFullScreenChromeVisible = false;
        UpdatePlayerChromeVisibility();
    }

    private void ControlBar_PreviewMouseRightButton(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    private void ControlBar_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        MarkControlBarActivity();
        RestartControlBarTimer();
        RefreshControlBarScreenBoundsFromElement();
    }

    private void PlayerSurface_PreviewMouseRightButton(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    private void MenuButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        if (IsOpenMenuButton(button))
        {
            CloseOpenMenu();
            e.Handled = true;
            return;
        }
    }

    private void SourceMenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not Button button)
        {
            return;
        }

        if (IsOpenMenuButton(button))
        {
            CloseOpenMenu();
            e.Handled = true;
            return;
        }

        HideVolumeHoverPopup();
        CloseOpenMenu();
        var menu = BuildSourceMenu(_viewModel);
        PlaceContextMenuCenteredAbove(menu, button);
        menu.Closed += ContextMenu_Closed;
        menu.Closed += (_, _) =>
        {
            _isSourceMenuOpen = false;
            RestartControlBarTimer();
            Keyboard.Focus(this);
        };
        _isSourceMenuOpen = true;
        button.ContextMenu = menu;
        _openMenuButton = button;
        _openContextMenu = menu;
        menu.IsOpen = true;
    }

    private void SubtitleMenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not Button button)
        {
            return;
        }

        if (IsOpenMenuButton(button))
        {
            CloseOpenMenu();
            e.Handled = true;
            return;
        }

        HideVolumeHoverPopup();
        CloseOpenMenu();
        var menu = BuildSubtitleMenu(_viewModel);
        PlaceContextMenuCenteredAbove(menu, button);
        menu.Closed += ContextMenu_Closed;
        menu.Closed += (_, _) =>
        {
            _isSubtitleMenuOpen = false;
            RestartControlBarTimer();
            Keyboard.Focus(this);
        };
        _isSubtitleMenuOpen = true;
        button.ContextMenu = menu;
        _openMenuButton = button;
        _openContextMenu = menu;
        menu.IsOpen = true;
    }

    private void AudioTrackMenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not Button button)
        {
            return;
        }

        if (IsOpenMenuButton(button))
        {
            CloseOpenMenu();
            e.Handled = true;
            return;
        }

        HideVolumeHoverPopup();
        CloseOpenMenu();
        var menu = BuildAudioTrackMenu(_viewModel);
        PlaceContextMenuCenteredAbove(menu, button);
        menu.Closed += ContextMenu_Closed;
        menu.Closed += (_, _) =>
        {
            _isAudioTrackMenuOpen = false;
            RestartControlBarTimer();
            Keyboard.Focus(this);
        };
        _isAudioTrackMenuOpen = true;
        button.ContextMenu = menu;
        _openMenuButton = button;
        _openContextMenu = menu;
        menu.IsOpen = true;
    }

    private void ContextMenu_Closed(object? sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu { PlacementTarget: Button button })
        {
            return;
        }

        if (ReferenceEquals(_openContextMenu, sender))
        {
            _openMenuButton = null;
            _openContextMenu = null;
        }
    }

    private bool IsOpenMenuButton(Button button)
    {
        return ReferenceEquals(_openMenuButton, button)
               && _openContextMenu is not null;
    }

    private void CloseOpenMenu()
    {
        if (_openContextMenu is not null)
        {
            _openContextMenu.IsOpen = false;
        }

        _openMenuButton = null;
        _openContextMenu = null;
    }

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        if (!IsActive)
        {
            return;
        }

        if (TryGetCursorPosition(out var cursorPosition))
        {
            var hasMoved = !_lastCursorPosition.HasValue
                           || _lastCursorPosition.Value.X != cursorPosition.X
                           || _lastCursorPosition.Value.Y != cursorPosition.Y;
            _lastCursorPosition = cursorPosition;
            if (_isFullScreen && !hasMoved)
            {
                return;
            }
        }

        MarkControlBarActivity();
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

        var keepChromeHidden = false;
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
                keepChromeHidden = ShouldKeepHiddenChromeForPassiveInput();
                e.Handled = true;
                break;
            case Key.Left:
                if (!HasNoModifiers() && !HasOnlyControlModifier())
                {
                    break;
                }

                _viewModel.SeekBySeconds(HasOnlyControlModifier() ? -30 : -5);
                keepChromeHidden = ShouldKeepHiddenChromeForPassiveInput();
                e.Handled = true;
                break;
            case Key.Right:
                if (!HasNoModifiers() && !HasOnlyControlModifier())
                {
                    break;
                }

                _viewModel.SeekBySeconds(HasOnlyControlModifier() ? 30 : 5);
                keepChromeHidden = ShouldKeepHiddenChromeForPassiveInput();
                e.Handled = true;
                break;
            case Key.Up:
                if (!HasNoModifiers())
                {
                    break;
                }

                _viewModel.AdjustVolume(5);
                ShowVolumeFeedback();
                keepChromeHidden = ShouldKeepHiddenChromeForPassiveInput();
                e.Handled = true;
                break;
            case Key.Down:
                if (!HasNoModifiers())
                {
                    break;
                }

                _viewModel.AdjustVolume(-5);
                ShowVolumeFeedback();
                keepChromeHidden = ShouldKeepHiddenChromeForPassiveInput();
                e.Handled = true;
                break;
        }

        if (e.Handled)
        {
            RefreshControlBarAfterKeyboardInput(keepChromeHidden);
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

    private ContextMenu CreatePlayerContextMenu(PlayerWindowViewModel viewModel)
    {
        return new ContextMenu
        {
            DataContext = viewModel,
            Style = (Style)FindResource("PlayerMenuStyle")
        };
    }

    private ContextMenu FinalizePlayerMenu(ContextMenu menu)
    {
        ApplyPlayerMenuStyles(menu);
        return menu;
    }

    private static void PlaceContextMenuCenteredAbove(ContextMenu menu, Button button)
    {
        menu.PlacementTarget = button;
        menu.Placement = PlacementMode.Custom;
        menu.CustomPopupPlacementCallback = PlacePopupCenteredAbove;
        menu.HorizontalOffset = 0d;
        menu.VerticalOffset = 0d;
    }

    private static CustomPopupPlacement[] PlacePopupCenteredAbove(Size popupSize, Size targetSize, Point offset)
    {
        var horizontal = ((targetSize.Width - popupSize.Width) / 2d) + offset.X;
        return
        [
            new CustomPopupPlacement(
                new Point(horizontal, -popupSize.Height - 8d + offset.Y),
                PopupPrimaryAxis.Vertical),
            new CustomPopupPlacement(
                new Point(horizontal, targetSize.Height + 8d + offset.Y),
                PopupPrimaryAxis.Vertical)
        ];
    }

    private CustomPopupPlacement[] VolumeHoverPopup_PlacementCallback(Size popupSize, Size targetSize, Point offset)
    {
        return PlacePopupCenteredAbove(popupSize, targetSize, offset);
    }

    private void ApplyPlayerMenuStyles(ItemsControl root)
    {
        var menuItemStyle = (Style)FindResource("PlayerMenuItemStyle");
        var submenuItemStyle = (Style)FindResource("PlayerSubmenuItemStyle");
        var separatorStyle = (Style)FindResource("PlayerMenuSeparatorStyle");

        foreach (var child in root.Items)
        {
            switch (child)
            {
                case MenuItem menuItem:
                    menuItem.Style = menuItem.HasItems ? submenuItemStyle : menuItemStyle;
                    if (menuItem.HasItems)
                    {
                        AttachPlayerSubmenuOpenRight(menuItem);
                        ApplyPlayerMenuStyles(menuItem);
                    }

                    break;
                case Separator separator:
                    separator.Style = separatorStyle;
                    break;
            }
        }
    }

    private static void AttachPlayerSubmenuOpenRight(MenuItem menuItem)
    {
        menuItem.MouseEnter -= PlayerSubmenuItem_MouseEnter;
        menuItem.MouseEnter += PlayerSubmenuItem_MouseEnter;
        menuItem.SubmenuOpened -= PlayerSubmenuItem_SubmenuOpened;
        menuItem.SubmenuOpened += PlayerSubmenuItem_SubmenuOpened;
    }

    private static void PlayerSubmenuItem_SubmenuOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem)
        {
            return;
        }

        ConfigureSubmenuPopupToOpenRight(menuItem);
        _ = menuItem.Dispatcher.BeginInvoke(
            () => ConfigureSubmenuPopupToOpenRight(menuItem),
            DispatcherPriority.Loaded);
    }

    private static void PlayerSubmenuItem_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is not MenuItem { HasItems: true, IsEnabled: true } menuItem)
        {
            return;
        }

        if (!menuItem.IsSubmenuOpen)
        {
            menuItem.IsSubmenuOpen = true;
        }
    }

    private ContextMenu BuildSourceMenu(PlayerWindowViewModel viewModel)
    {
        var menu = CreatePlayerContextMenu(viewModel);
        menu.MaxHeight = 188d;

        if (viewModel.Sources.Count == 0)
        {
            menu.Items.Add(new MenuItem
            {
                Header = "\u65e0\u53ef\u7528\u64ad\u653e\u6e90",
                IsEnabled = false
            });
            return FinalizePlayerMenu(menu);
        }

        menu.Items.Add(CreateSourceTableHeader());
        foreach (var source in viewModel.Sources)
        {
            menu.Items.Add(CreateSourceLeaf(source, viewModel));
        }

        return FinalizePlayerMenu(menu);
    }

    private static MenuItem CreateSourceTableHeader()
    {
        var item = new MenuItem
        {
            Header = CreateSourceTableHeaderContent(),
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

    private static Grid CreateSourceTableHeaderContent()
    {
        var grid = CreateSourceTableGrid();
        AddSourceCell(grid, "播放源", 0, isHeader: true);
        AddSourceCell(grid, "分辨率", 1, isHeader: true);
        AddSourceCell(grid, "视频码率", 2, isHeader: true);
        AddSourceCell(grid, "大小", 3, isHeader: true);
        return grid;
    }

    private static Grid CreateSourceMenuHeader(PlaybackSourceItem source, bool isSelected)
    {
        var grid = CreateSourceTableGrid();
        var sourcePanel = new StackPanel
        {
            MaxWidth = 280d
        };

        sourcePanel.Children.Add(
            new TextBlock
            {
                Text = source.FileName,
                TextTrimming = TextTrimming.CharacterEllipsis
            });

        var meta = BuildSourceMenuMeta(source);
        if (!string.IsNullOrWhiteSpace(meta))
        {
            sourcePanel.Children.Add(CreateMutedSourceText(meta));
        }

        Grid.SetColumn(sourcePanel, 0);
        grid.Children.Add(sourcePanel);
        AddSourceCell(grid, FormatSourceColumn(source.ResolutionShortText), 1);
        AddSourceCell(grid, FormatSourceColumn(source.BitrateText), 2);
        AddSourceCell(grid, FormatSourceColumn(source.FormattedFileSize), 3);
        return grid;
    }

    private static Grid CreateSourceTableGrid()
    {
        var grid = new Grid
        {
            Width = 540d
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(78d) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92d) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(76d) });
        return grid;
    }

    private static void AddSourceCell(Grid grid, string text, int column, bool isHeader = false)
    {
        var textBlock = new TextBlock
        {
            Text = text,
            FontWeight = isHeader ? FontWeights.SemiBold : FontWeights.Normal,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            TextAlignment = isHeader ? TextAlignment.Center : column == 0 ? TextAlignment.Left : TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        textBlock.SetResourceReference(
            TextBlock.ForegroundProperty,
            isHeader ? "BrushPlayerTextMuted" : "BrushPlayerTextPrimary");

        Grid.SetColumn(textBlock, column);
        grid.Children.Add(textBlock);
    }

    private static TextBlock CreateMutedSourceText(string text)
    {
        var textBlock = new TextBlock
        {
            Margin = new Thickness(0d, 3d, 0d, 0d),
            FontSize = 12d,
            Text = text,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        textBlock.SetResourceReference(TextBlock.ForegroundProperty, "BrushPlayerTextMuted");
        return textBlock;
    }

    private static string BuildSourceMenuMeta(PlaybackSourceItem source)
    {
        var parts = new[]
            {
                source.IsDefault ? "默认源" : string.Empty,
                source.SourceTypeText,
                source.ResumeText
            }
            .Where(part => !string.IsNullOrWhiteSpace(part));

        return string.Join(" · ", parts);
    }

    private static string FormatSourceColumn(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
    }

    private static string BuildSourceToolTip(PlaybackSourceItem source)
    {
        var lines = new[]
            {
                source.SourceSummaryText,
                source.PlaybackHistoryText
            }
            .Where(line => !string.IsNullOrWhiteSpace(line));

        return string.Join(Environment.NewLine, lines);
    }

    private ContextMenu BuildSubtitleMenu(PlayerWindowViewModel viewModel)
    {
        viewModel.NotifySubtitleMenuOpened();
        var menu = CreatePlayerContextMenu(viewModel);

        menu.Items.Add(CreateSubtitleLeaf(viewModel.NoneSubtitle, viewModel, "\u65e0\u5b57\u5e55", centerHeader: true));
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
        menu.Items.Add(new Separator());

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
        menu.Items.Add(new Separator());

        var onlineGroup = CreateRightOpeningSubtitleGroup("\u5728\u7ebf\u5b57\u5e55");
        var searchItem = new MenuItem
        {
            Header = CreateOnlineSubtitleSubmenuHeader("\u641c\u7d22\u5728\u7ebf\u5b57\u5e55"),
            IsEnabled = viewModel.HasPlayableOnlineSubtitleSearchContext
        };
        ConfigureOnlineSubtitleSubmenuItem(searchItem);
        SuppressRightClick(searchItem);
        searchItem.Click += (_, _) => OpenOnlineSubtitleSearch(viewModel);
        onlineGroup.Items.Add(searchItem);
        onlineGroup.Items.Add(new Separator());

        if (viewModel.OnlineSubtitleMenuItems.Count == 0)
        {
            var emptyItem = new MenuItem
            {
                Header = CreateOnlineSubtitleSubmenuHeader("\u6682\u65e0\u5df2\u4e0b\u8f7d\u5b57\u5e55"),
                IsEnabled = false
            };
            ConfigureOnlineSubtitleSubmenuItem(emptyItem);
            onlineGroup.Items.Add(emptyItem);
        }
        else
        {
            foreach (var subtitle in viewModel.OnlineSubtitleMenuItems)
            {
                var isSelected = viewModel.IsOnlineSubtitleSelected(subtitle);
                var item = new MenuItem
                {
                    Header = CreateOnlineSubtitleSubmenuHeader(subtitle.DisplayName, enableTrimmedToolTip: false),
                    IsChecked = isSelected,
                    IsEnabled = true
                };
                ConfigureOnlineSubtitleSubmenuItem(item);
                ConfigureOnlineSubtitleHoverText(item, subtitle.DisplayName);
                SuppressRightClick(item);
                AddOnlineSubtitleDeleteMenuItem(item, subtitle, viewModel);
                item.PreviewMouseLeftButtonDown += (_, e) =>
                {
                    if (!subtitle.HasCacheFile || !IsDirectMenuItemMouseEvent(item, e.OriginalSource as DependencyObject))
                    {
                        return;
                    }

                    viewModel.SelectOnlineSubtitleFromMenu(subtitle);
                    CloseMenuItemToolTip(item);
                    CloseOpenMenu();
                    e.Handled = true;
                };
                onlineGroup.Items.Add(item);
            }
        }

        menu.Items.Add(onlineGroup);
        return FinalizePlayerMenu(menu);
    }

    private static void AddOnlineSubtitleDeleteMenuItem(
        MenuItem parent,
        OnlineSubtitleMenuItemViewModel subtitle,
        PlayerWindowViewModel viewModel)
    {
        var deleteItem = new MenuItem
        {
            Header = CreateCompactMenuHeader(subtitle.IsTemporary ? "\u79fb\u9664\u4e34\u65f6\u5b57\u5e55" : "\u5220\u9664\u7ed1\u5b9a"),
            MinWidth = 0d,
            MinHeight = 24d,
            Padding = new Thickness(8d, 4d, 8d, 4d),
            FontSize = OnlineSubtitleMenuFontSize
        };
        SuppressRightClick(deleteItem);
        deleteItem.Click += (_, _) => viewModel.DeleteOnlineSubtitleFromMenu(subtitle);
        parent.Items.Add(deleteItem);
    }

    private void OpenOnlineSubtitleSearch(PlayerWindowViewModel viewModel)
    {
        if (_onlineSubtitleSearchWindow is { IsVisible: true } existingWindow)
        {
            existingWindow.Activate();
            return;
        }

        viewModel.PauseForOnlineSubtitleSearch();
        var dialogViewModel = viewModel.CreateOnlineSubtitleSearchViewModel();
        var dialog = new OnlineSubtitleSearchWindow(dialogViewModel)
        {
            Owner = this
        };
        _onlineSubtitleSearchWindow = dialog;
        dialog.Closed += (_, _) =>
        {
            if (ReferenceEquals(_onlineSubtitleSearchWindow, dialog))
            {
                _onlineSubtitleSearchWindow = null;
            }
        };
        dialog.ShowDialog();
    }

    private static MenuItem CreateRightOpeningSubtitleGroup(string header)
    {
        var item = new MenuItem { Header = CreateMenuHeader(header, center: true) };
        SuppressRightClick(item);
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
                new Point(targetSize.Width + offset.X, targetSize.Height - popupSize.Height + offset.Y),
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

        var popupHeight = Math.Max(1, rect.Bottom - rect.Top);
        var itemBottomRight = menuItem.PointToScreen(new Point(menuItem.ActualWidth, menuItem.ActualHeight));
        _ = SetWindowPos(
            popupHandle,
            IntPtr.Zero,
            (int)Math.Round(itemBottomRight.X),
            (int)Math.Round(itemBottomRight.Y - popupHeight),
            Math.Max(1, rect.Right - rect.Left),
            popupHeight,
            SwpNoZOrder | SwpNoActivate);
    }

    private static MenuItem CreateSubtitleLeaf(
        PlaybackSubtitleItem subtitle,
        PlayerWindowViewModel viewModel,
        string? displayNameOverride = null,
        bool centerHeader = false)
    {
        var displayName = displayNameOverride ?? subtitle.DisplayName;
        var item = new MenuItem
        {
            Header = CreateMenuHeader(displayName, centerHeader),
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
        var menu = CreatePlayerContextMenu(viewModel);
        var statusText = viewModel.AudioTrackMenuStatusText;
        if (!string.IsNullOrWhiteSpace(statusText))
        {
            menu.Items.Add(new MenuItem
            {
                Header = CreateMenuHint(statusText),
                IsEnabled = false
            });

            if (viewModel.AudioTracks.Count > 0)
            {
                menu.Items.Add(new Separator());
            }
        }

        if (viewModel.AudioTracks.Count == 0)
        {
            menu.Items.Add(new MenuItem
            {
                Header = viewModel.SelectedSource is not null && !viewModel.IsAudioTrackDiscoveryReady
                    ? "\u6b63\u5728\u8bfb\u53d6\u97f3\u8f68..."
                    : "\u6682\u65e0\u53ef\u7528\u97f3\u8f68",
                IsEnabled = false
            });
            return FinalizePlayerMenu(menu);
        }

        foreach (var audioTrack in viewModel.AudioTracks)
        {
            menu.Items.Add(CreateAudioTrackLeaf(audioTrack, viewModel));
        }

        return FinalizePlayerMenu(menu);
    }

    private static MenuItem CreateAudioTrackLeaf(
        PlaybackAudioTrackItem audioTrack,
        PlayerWindowViewModel viewModel)
    {
        var item = new MenuItem
        {
            Header = CreateMenuHeader(audioTrack.DisplayName),
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

    private static void ConfigureOnlineSubtitleSubmenuItem(MenuItem item)
    {
        item.MinWidth = OnlineSubtitleSubmenuItemMinWidth;
        item.MinHeight = 24d;
        item.Padding = new Thickness(6d, 4d, 8d, 4d);
        item.FontSize = OnlineSubtitleMenuFontSize;
        item.HorizontalContentAlignment = HorizontalAlignment.Stretch;
    }

    private static void ConfigureOnlineSubtitleHoverText(MenuItem item, string text)
    {
        var toolTip = new ToolTip
        {
            Content = text,
            PlacementTarget = item,
            Placement = PlacementMode.Left,
            HorizontalOffset = -6d,
            VerticalOffset = 0d
        };

        item.ToolTip = toolTip;
        ToolTipService.SetInitialShowDelay(item, 0);
        ToolTipService.SetBetweenShowDelay(item, 0);
        ToolTipService.SetShowDuration(item, 60000);
        item.MouseEnter += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                toolTip.IsOpen = true;
            }
        };
        item.MouseLeave += (_, _) =>
        {
            if (!item.IsSubmenuOpen)
            {
                toolTip.IsOpen = false;
            }
        };
        item.SubmenuClosed += (_, _) => toolTip.IsOpen = false;
    }

    private static void CloseMenuItemToolTip(MenuItem item)
    {
        if (item.ToolTip is ToolTip toolTip)
        {
            toolTip.IsOpen = false;
        }
    }

    private static bool IsDirectMenuItemMouseEvent(MenuItem owner, DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is MenuItem menuItem)
            {
                return ReferenceEquals(menuItem, owner);
            }

            source = VisualTreeHelper.GetParent(source) ?? LogicalTreeHelper.GetParent(source);
        }

        return false;
    }

    private static TextBlock CreateOnlineSubtitleSubmenuHeader(string text, bool enableTrimmedToolTip = true)
    {
        return CreateMenuHeader(
            text,
            center: true,
            maxWidth: OnlineSubtitleSubmenuHeaderMaxWidth,
            useTrimmedToolTip: enableTrimmedToolTip,
            fixedWidth: true,
            fontSize: OnlineSubtitleMenuFontSize);
    }

    private static TextBlock CreateCompactMenuHeader(string text)
    {
        return CreateMenuHeader(
            text,
            maxWidth: 84d,
            fontSize: OnlineSubtitleMenuFontSize);
    }

    private static TextBlock CreateMenuHeader(
        string text,
        bool center = false,
        double maxWidth = 360d,
        bool useTrimmedToolTip = false,
        bool fixedWidth = false,
        double? fontSize = null)
    {
        var textBlock = new TextBlock
        {
            Text = text,
            MaxWidth = maxWidth,
            HorizontalAlignment = center ? HorizontalAlignment.Center : HorizontalAlignment.Left,
            TextAlignment = center ? TextAlignment.Center : TextAlignment.Left,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        if (fixedWidth)
        {
            textBlock.Width = maxWidth;
        }

        if (fontSize.HasValue)
        {
            textBlock.FontSize = fontSize.Value;
        }

        if (useTrimmedToolTip)
        {
            TrimmedTextToolTipBehavior.SetFullText(textBlock, text);
        }

        return textBlock;
    }

    private static TextBlock CreateMenuHint(string text)
    {
        return new TextBlock
        {
            Text = text,
            MaxWidth = 240d,
            TextWrapping = TextWrapping.Wrap
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

        if (!ShouldKeepHiddenChromeForPassiveInput())
        {
            ShowControlBar();
            RestartControlBarTimer();
        }

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
        if (IsAnyPlayerMenuOpen())
        {
            RestartControlBarTimer();
            return;
        }

        if (IsCursorInsidePinnedChromeArea())
        {
            RestartControlBarTimer();
            return;
        }

        var idleMilliseconds = Environment.TickCount64 - _lastControlBarActivityTick;
        if (idleMilliseconds < ControlBarAutoHideMilliseconds)
        {
            StartControlBarTimer(ControlBarAutoHideMilliseconds - idleMilliseconds);
            return;
        }

        if (IsActive || _isFullScreen)
        {
            HideControlBar();
        }
    }

    private bool IsCursorInsidePinnedChromeArea()
    {
        if (!TryGetCursorPosition(out var cursorPosition))
        {
            return false;
        }

        return IsCursorInsideControlBar(cursorPosition)
               || IsCursorInsidePlayerTitleBar(cursorPosition)
               || (VolumeHoverPopup.IsOpen && IsCursorInsideVolumeHoverArea());
    }

    private void OnInteractionFeedbackTimerTick(object? sender, EventArgs e)
    {
        _interactionFeedbackTimer.Stop();
        VolumeFeedback.Visibility = Visibility.Collapsed;
        BrightnessFeedback.Visibility = Visibility.Collapsed;
        InteractionFeedbackPopup.IsOpen = false;
    }

    private void OnVolumeHoverCloseTimerTick(object? sender, EventArgs e)
    {
        _volumeHoverCloseTimer.Stop();
        if (!IsCursorInsideVolumeHoverArea())
        {
            HideVolumeHoverPopup();
        }
    }

    private void ShowVolumeFeedback()
    {
        if (_viewModel is null)
        {
            return;
        }

        VolumeFeedbackText.Text = _viewModel.VolumeFeedbackText;
        VolumeFeedbackBar.Value = _viewModel.Volume;
        VolumeFeedbackBar.Foreground = PlayerMeterBrushConverter.CreateBrush(_viewModel.Volume, 200d);
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
        BrightnessFeedbackBar.Foreground = PlayerMeterBrushConverter.CreateBrush(_viewModel.Brightness, 100d);
        VolumeFeedback.Visibility = Visibility.Collapsed;
        ShowInteractionFeedbackPopup();
        BrightnessFeedback.Visibility = Visibility.Visible;
        RestartInteractionFeedbackTimer();
    }

    private void ShowVolumeHoverPopup()
    {
        if (_viewModel is null
            || !IsActive
            || _closeRequested
            || !ControlBarPopup.IsOpen
            || IsAnyPlayerMenuOpen())
        {
            return;
        }

        _volumeHoverCloseTimer.Stop();
        VolumeHoverPopup.IsOpen = true;
        MarkControlBarActivity();
        RestartControlBarTimer();
    }

    private void ScheduleVolumeHoverPopupClose()
    {
        _volumeHoverCloseTimer.Stop();
        _volumeHoverCloseTimer.Start();
    }

    private void HideVolumeHoverPopup()
    {
        _volumeHoverCloseTimer.Stop();
        if (VolumeHoverPopup.IsOpen)
        {
            VolumeHoverPopup.IsOpen = false;
        }
    }

    private bool IsCursorInsideVolumeHoverArea()
    {
        if (!TryGetCursorPosition(out var cursorPosition))
        {
            return false;
        }

        return IsCursorInsideElement(VolumeHoverAnchor, cursorPosition)
               || (VolumeHoverPopup.IsOpen && IsCursorInsideElement(VolumeHoverPopupSurface, cursorPosition));
    }

    private void ShowInteractionFeedbackPopup()
    {
        if (InteractionFeedbackPopup.IsOpen)
        {
            return;
        }

        UpdateInteractionFeedbackPopupPlacement(moveNativePopup: false);
        InteractionFeedbackPopup.IsOpen = true;
        _ = Dispatcher.BeginInvoke(
            () => UpdateInteractionFeedbackPopupPlacement(moveNativePopup: true),
            DispatcherPriority.Loaded);
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

        EnterFullScreen();
    }

    private void EnterFullScreen()
    {
        _previousState = WindowState;
        _previousStyle = WindowStyle;
        _previousResizeMode = ResizeMode;
        _isFullScreen = true;
        _isFullScreenChromeVisible = true;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        WindowState = WindowState.Maximized;
        UpdateFullScreenToggleButton();
        UpdatePlayerChromeVisibility();
        _lastCursorPosition = null;
        MarkControlBarActivity();
        ShowControlBar();
        RestartControlBarTimer();
    }

    private void ExitFullScreen()
    {
        _isFullScreen = false;
        _isFullScreenChromeVisible = true;
        WindowStyle = _previousStyle;
        ResizeMode = _previousResizeMode;
        WindowState = WindowState.Normal;
        CenterWindowOnCurrentMonitor();
        _ = Dispatcher.BeginInvoke(CenterWindowOnCurrentMonitor, DispatcherPriority.Loaded);
        UpdateFullScreenToggleButton();
        UpdatePlayerChromeVisibility();
        UpdatePlayerMaximizeRestoreButton();
        _controlBarTimer.Stop();
        _lastCursorPosition = null;
        MarkControlBarActivity();
        ShowControlBar();
        RestartControlBarTimer();
    }

    private void RefreshControlBarAfterKeyboardInput(bool keepChromeHidden)
    {
        if (keepChromeHidden)
        {
            return;
        }

        ShowControlBar();
        RestartControlBarTimer();
    }

    private void UpdateFullScreenToggleButton()
    {
        if (FullscreenToggleButton is not null)
        {
            FullscreenToggleButton.Content = _isFullScreen ? "arrows-in-simple" : "arrows-out-simple";
        }
    }

    private bool ShouldKeepHiddenChromeForPassiveInput()
    {
        return _isFullScreen
               && !_isFullScreenChromeVisible
               && !ControlBarPopup.IsOpen;
    }

    private void RestartControlBarTimer()
    {
        if (IsAnyPlayerMenuOpen() || !IsActive)
        {
            _controlBarTimer.Stop();
            return;
        }

        if (!_controlBarTimer.IsEnabled)
        {
            StartControlBarTimer(ControlBarAutoHideMilliseconds);
        }
    }

    private void StartControlBarTimer(long dueMilliseconds)
    {
        _controlBarTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(100, dueMilliseconds));
        _controlBarTimer.Start();
    }

    private void MarkControlBarActivity()
    {
        _lastControlBarActivityTick = Environment.TickCount64;
    }

    private void ShowControlBar()
    {
        MarkControlBarActivity();
        ShowPlayerCursor();
        ShowFullScreenChrome();
        if (!IsActive)
        {
            HideControlBar();
            return;
        }

        if (!ControlBarPopup.IsOpen)
        {
            UpdateControlBarPopupPlacement(moveNativePopup: false);
            ControlBarPopup.IsOpen = true;
            _ = Dispatcher.BeginInvoke(
                () => UpdateControlBarPopupPlacement(moveNativePopup: true),
                DispatcherPriority.Loaded);
        }
        ControlBar.Opacity = 1d;
        ControlBar.IsHitTestVisible = true;
    }

    private void CenterWindowOnCurrentMonitor()
    {
        var handle = _windowSource?.Handle ?? new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var monitor = MonitorFromWindow(handle, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return;
        }

        var monitorInfo = new MONITORINFO
        {
            Size = Marshal.SizeOf<MONITORINFO>()
        };

        if (!GetMonitorInfo(monitor, ref monitorInfo))
        {
            return;
        }

        var source = PresentationSource.FromVisual(this);
        var topLeft = new Point(monitorInfo.WorkArea.Left, monitorInfo.WorkArea.Top);
        var bottomRight = new Point(monitorInfo.WorkArea.Right, monitorInfo.WorkArea.Bottom);
        if (source?.CompositionTarget is not null)
        {
            topLeft = source.CompositionTarget.TransformFromDevice.Transform(topLeft);
            bottomRight = source.CompositionTarget.TransformFromDevice.Transform(bottomRight);
        }

        var workAreaWidth = Math.Max(0d, bottomRight.X - topLeft.X);
        var workAreaHeight = Math.Max(0d, bottomRight.Y - topLeft.Y);
        var windowWidth = ActualWidth > 0 ? ActualWidth : Width;
        var windowHeight = ActualHeight > 0 ? ActualHeight : Height;
        if (double.IsNaN(windowWidth) || windowWidth <= 0 || double.IsNaN(windowHeight) || windowHeight <= 0)
        {
            return;
        }

        Left = topLeft.X + Math.Max(0d, (workAreaWidth - windowWidth) / 2d);
        Top = topLeft.Y + Math.Max(0d, (workAreaHeight - windowHeight) / 2d);
    }

    private void HideControlBar()
    {
        _controlBarTimer.Stop();
        HideVolumeHoverPopup();
        ControlBarPopup.IsOpen = false;
        ControlBar.IsHitTestVisible = false;
        _controlBarScreenBounds = Rect.Empty;
        HideFullScreenChrome();
        UpdatePlayerCursorForControlBarState();
    }

    private void UpdateControlBarPopupPlacement(bool moveNativePopup = true)
    {
        if (!IsLoaded)
        {
            return;
        }

        var resizeInset = GetControlBarResizeInset();
        var availableWidth = GetControlBarAvailableWidth(resizeInset);
        var controlBarWidth = GetControlBarWidth(availableWidth);
        ControlBar.Width = controlBarWidth;
        ControlBarPopup.HorizontalOffset = GetControlBarHorizontalOffset(resizeInset, availableWidth, controlBarWidth);
        ControlBarPopup.VerticalOffset = Math.Max(0d, RootLayout.ActualHeight - ControlBar.ActualHeight - ControlBarBottomLift);
        if (moveNativePopup)
        {
            MoveControlBarPopupWindow();
        }
    }

    private double GetControlBarAvailableWidth(double resizeInset)
    {
        return Math.Max(1d, RootLayout.ActualWidth - (resizeInset * 2d));
    }

    private static double GetControlBarWidth(double availableWidth)
    {
        return Math.Max(1d, availableWidth * ControlBarWidthRatio);
    }

    private static double GetControlBarHorizontalOffset(double resizeInset, double availableWidth, double controlBarWidth)
    {
        return resizeInset + Math.Max(0d, (availableWidth - controlBarWidth) / 2d);
    }

    private double GetControlBarResizeInset()
    {
        return !_isFullScreen && WindowState != WindowState.Maximized
            ? ResizeHitTestThickness
            : 0d;
    }

    private void UpdatePopupPlacements()
    {
        UpdatePlayerTitleBarPopupPlacement();
        UpdateControlBarPopupPlacement();
        UpdateInteractionFeedbackPopupPlacement();
        UpdatePromptOverlayPopupPlacement();
    }

    private void UpdatePlayerTitleBarPopupPlacement()
    {
        if (!IsLoaded || PlayerTitleBarPopup is null)
        {
            return;
        }

        PlayerTitleBarPopup.HorizontalOffset = 0d;
        PlayerTitleBarPopup.VerticalOffset = 0d;
    }

    private void UpdateInteractionFeedbackPopupPlacement(bool moveNativePopup = true)
    {
        if (!IsLoaded)
        {
            return;
        }

        InteractionFeedbackLayer.Width = Math.Max(1d, RootLayout.ActualWidth);
        InteractionFeedbackLayer.Height = Math.Max(1d, RootLayout.ActualHeight);
        InteractionFeedbackPopup.HorizontalOffset = 0d;
        InteractionFeedbackPopup.VerticalOffset = 0d;
        if (moveNativePopup)
        {
            MoveInteractionFeedbackPopupWindow();
        }
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
            _controlBarScreenBounds = Rect.Empty;
            return;
        }

        var popupSource = PresentationSource.FromVisual(ControlBar) as HwndSource;
        if (popupSource?.Handle is not { } popupHandle || popupHandle == IntPtr.Zero)
        {
            _ = Dispatcher.BeginInvoke(
                () => UpdateControlBarPopupPlacement(moveNativePopup: true),
                DispatcherPriority.Loaded);
            return;
        }

        var resizeInset = GetControlBarResizeInset();
        var availableWidth = GetControlBarAvailableWidth(resizeInset);
        var popupWidth = GetControlBarWidth(availableWidth);
        var horizontalOffset = GetControlBarHorizontalOffset(resizeInset, availableWidth, popupWidth);
        var popupTopLeft = RootLayout.PointToScreen(
            new Point(horizontalOffset, Math.Max(0d, RootLayout.ActualHeight - ControlBar.ActualHeight - ControlBarBottomLift)));
        var transformToDevice = PresentationSource.FromVisual(RootLayout)?.CompositionTarget?.TransformToDevice
                                ?? Matrix.Identity;
        var popupSize = transformToDevice.Transform(new Point(popupWidth, ControlBar.ActualHeight));

        _ = SetWindowPos(
            popupHandle,
            IntPtr.Zero,
            (int)Math.Round(popupTopLeft.X),
            (int)Math.Round(popupTopLeft.Y),
            Math.Max(1, (int)Math.Round(popupSize.X)),
            Math.Max(1, (int)Math.Round(popupSize.Y)),
            SwpNoZOrder | SwpNoActivate);
        CacheControlBarScreenBounds(popupTopLeft, popupSize);
    }

    private void RefreshControlBarScreenBoundsFromElement()
    {
        if (!ControlBarPopup.IsOpen || ControlBar.ActualWidth <= 0 || ControlBar.ActualHeight <= 0)
        {
            _controlBarScreenBounds = Rect.Empty;
            return;
        }

        var popupTopLeft = ControlBar.PointToScreen(new Point(0d, 0d));
        var transformToDevice = PresentationSource.FromVisual(ControlBar)?.CompositionTarget?.TransformToDevice
                                ?? Matrix.Identity;
        var popupSize = transformToDevice.Transform(new Point(ControlBar.ActualWidth, ControlBar.ActualHeight));
        CacheControlBarScreenBounds(popupTopLeft, popupSize);
    }

    private void CacheControlBarScreenBounds(Point popupTopLeft, Point popupSize)
    {
        const double hitTestPadding = 4d;
        _controlBarScreenBounds = new Rect(
            popupTopLeft.X - hitTestPadding,
            popupTopLeft.Y - hitTestPadding,
            Math.Max(1d, popupSize.X) + (hitTestPadding * 2d),
            Math.Max(1d, popupSize.Y) + (hitTestPadding * 2d));
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
            _ = Dispatcher.BeginInvoke(
                () => UpdateInteractionFeedbackPopupPlacement(moveNativePopup: true),
                DispatcherPriority.Loaded);
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
        MarkControlBarActivity();
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

        if (!_controlBarScreenBounds.IsEmpty
            && _controlBarScreenBounds.Contains(new Point(cursorPosition.X, cursorPosition.Y)))
        {
            return true;
        }

        var point = ControlBar.PointFromScreen(new Point(cursorPosition.X, cursorPosition.Y));
        return point.X >= 0
               && point.Y >= 0
               && point.X <= ControlBar.ActualWidth
               && point.Y <= ControlBar.ActualHeight;
    }

    private bool IsCursorInsidePlayerTitleBar(POINT cursorPosition)
    {
        if (!_isFullScreen && IsCursorInsideElement(PlayerTitleBar, cursorPosition))
        {
            return true;
        }

        return PlayerTitleBarPopup.IsOpen
               && IsCursorInsideElement(FullscreenPlayerTitleBar, cursorPosition);
    }

    private static bool IsCursorInsideElement(FrameworkElement element, POINT cursorPosition)
    {
        if (element.Visibility != Visibility.Visible
            || element.ActualWidth <= 0
            || element.ActualHeight <= 0)
        {
            return false;
        }

        var point = element.PointFromScreen(new Point(cursorPosition.X, cursorPosition.Y));
        return point.X >= 0
               && point.Y >= 0
               && point.X <= element.ActualWidth
               && point.Y <= element.ActualHeight;
    }

    private bool IsCursorInsideVideoInteractionArea(POINT cursorPosition)
    {
        return IsActive
               && !_closeRequested
               && !IsAnyPlayerMenuOpen()
               && IsCursorInsidePlayer(cursorPosition)
               && !IsCursorInsideControlBar(cursorPosition)
               && !IsCursorInsidePlayerTitleBar(cursorPosition);
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
        _isSourceMenuOpen = false;
        _isSubtitleMenuOpen = false;
        _isAudioTrackMenuOpen = false;
        _cursorPollTimer.Stop();
        UninstallMouseWheelHook();
        HideVolumeHoverPopup();
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
        if (msg == WmNcHitTest && !_isFullScreen && IsSideResizeHit(GetScreenPoint(lParam)))
        {
            handled = true;
            return new IntPtr(1);
        }

        if (msg == WmGetMinMaxInfo)
        {
            ApplyMonitorBounds(lParam, useFullMonitorArea: _isFullScreen);
            handled = true;
        }
        else if (msg == WmSizing && !_isFullScreen)
        {
            ApplyAspectRatioToSizingRect(wParam, lParam);
            handled = true;
        }
        else if ((msg == WmMouseWheel || msg == WmMouseHWheel)
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

    private bool IsSideResizeHit(POINT cursorPosition)
    {
        if (!IsLoaded
            || WindowState == WindowState.Maximized
            || ActualWidth <= 0
            || ActualHeight <= 0)
        {
            return false;
        }

        var point = PointFromScreen(new Point(cursorPosition.X, cursorPosition.Y));
        var onLeft = point.X >= 0 && point.X <= ResizeHitTestThickness;
        var onRight = point.X >= ActualWidth - ResizeHitTestThickness && point.X <= ActualWidth;
        var onTop = point.Y >= 0 && point.Y <= ResizeHitTestThickness;
        var onBottom = point.Y >= ActualHeight - ResizeHitTestThickness && point.Y <= ActualHeight;
        var onHorizontalEdge = onLeft || onRight;
        var onVerticalEdge = onTop || onBottom;

        return onHorizontalEdge ^ onVerticalEdge;
    }

    private void ApplyMonitorBounds(IntPtr lParam, bool useFullMonitorArea)
    {
        if (_windowSource?.Handle is not { } handle || handle == IntPtr.Zero)
        {
            return;
        }

        var monitor = MonitorFromWindow(handle, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return;
        }

        var monitorInfo = new MONITORINFO
        {
            Size = Marshal.SizeOf<MONITORINFO>()
        };

        if (!GetMonitorInfo(monitor, ref monitorInfo))
        {
            return;
        }

        var minMaxInfo = Marshal.PtrToStructure<MINMAXINFO>(lParam);
        var workArea = useFullMonitorArea ? monitorInfo.MonitorArea : monitorInfo.WorkArea;
        var monitorArea = monitorInfo.MonitorArea;

        minMaxInfo.MaxPosition.X = Math.Abs(workArea.Left - monitorArea.Left);
        minMaxInfo.MaxPosition.Y = Math.Abs(workArea.Top - monitorArea.Top);
        minMaxInfo.MaxSize.X = Math.Abs(workArea.Right - workArea.Left);
        minMaxInfo.MaxSize.Y = Math.Abs(workArea.Bottom - workArea.Top);

        var dpi = VisualTreeHelper.GetDpi(this);
        minMaxInfo.MinTrackSize.X = (int)Math.Ceiling(MinWidth * dpi.DpiScaleX);
        minMaxInfo.MinTrackSize.Y = (int)Math.Ceiling(MinHeight * dpi.DpiScaleY);

        Marshal.StructureToPtr(minMaxInfo, lParam, true);
    }

    private void ApplyAspectRatioToSizingRect(IntPtr edgeParam, IntPtr lParam)
    {
        var rect = Marshal.PtrToStructure<RECT>(lParam);
        var edge = edgeParam.ToInt32();
        var dpi = VisualTreeHelper.GetDpi(this);
        var minimumWidth = Math.Max(1, (int)Math.Ceiling(MinWidth * dpi.DpiScaleX));
        var minimumHeight = Math.Max(1, (int)Math.Ceiling(MinHeight * dpi.DpiScaleY));
        var width = Math.Max(minimumWidth, rect.Right - rect.Left);
        var height = Math.Max(minimumHeight, rect.Bottom - rect.Top);

        switch (edge)
        {
            case WmszLeft:
            case WmszRight:
                height = Math.Max(minimumHeight, (int)Math.Round(width / _windowAspectRatio));
                SetVerticalFromCenter(ref rect, height);
                break;
            case WmszTop:
            case WmszBottom:
                width = Math.Max(minimumWidth, (int)Math.Round(height * _windowAspectRatio));
                SetHorizontalFromCenter(ref rect, width);
                break;
            case WmszTopLeft:
            case WmszTopRight:
            case WmszBottomLeft:
            case WmszBottomRight:
                height = Math.Max(minimumHeight, (int)Math.Round(width / _windowAspectRatio));
                if (height == minimumHeight)
                {
                    width = Math.Max(minimumWidth, (int)Math.Round(height * _windowAspectRatio));
                }

                SetHorizontalFromSizingEdge(ref rect, width, edge);
                SetVerticalFromSizingEdge(ref rect, height, edge);
                break;
        }

        Marshal.StructureToPtr(rect, lParam, true);
    }

    private static void SetHorizontalFromCenter(ref RECT rect, int width)
    {
        var center = rect.Left + ((rect.Right - rect.Left) / 2);
        rect.Left = center - (width / 2);
        rect.Right = rect.Left + width;
    }

    private static void SetVerticalFromCenter(ref RECT rect, int height)
    {
        var center = rect.Top + ((rect.Bottom - rect.Top) / 2);
        rect.Top = center - (height / 2);
        rect.Bottom = rect.Top + height;
    }

    private static void SetHorizontalFromSizingEdge(ref RECT rect, int width, int edge)
    {
        if (edge is WmszLeft or WmszTopLeft or WmszBottomLeft)
        {
            rect.Left = rect.Right - width;
            return;
        }

        rect.Right = rect.Left + width;
    }

    private static void SetVerticalFromSizingEdge(ref RECT rect, int height, int edge)
    {
        if (edge is WmszTop or WmszTopLeft or WmszTopRight)
        {
            rect.Top = rect.Bottom - height;
            return;
        }

        rect.Bottom = rect.Top + height;
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
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO monitorInfo);

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
    private struct MINMAXINFO
    {
        public POINT Reserved;

        public POINT MaxSize;

        public POINT MaxPosition;

        public POINT MinTrackSize;

        public POINT MaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int Size;

        public RECT MonitorArea;

        public RECT WorkArea;

        public int Flags;
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

    private static bool IsInteractiveElement(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ButtonBase
                or TextBoxBase
                or PasswordBox
                or Selector
                or MenuItem
                or Thumb
                or Slider
                or ScrollBar)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source) ?? LogicalTreeHelper.GetParent(source);
        }

        return false;
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
