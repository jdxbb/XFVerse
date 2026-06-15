using System.Diagnostics;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using MediaLibrary.App.Helpers;
using MediaLibrary.App.ViewModels.Pages;
using MediaLibrary.Core.Diagnostics;

namespace MediaLibrary.App.Views.Pages;

public partial class WatchInsightsPage : UserControl
{
    private const int ScrollRestoreMaxAttempts = 16;
    private const double BubbleRippleDistanceThreshold = 25d;
    private const double BubbleFlowWaveLifetimeSeconds = 1.85d;
    private static readonly Dictionary<string, Dictionary<string, PreferenceBubbleState>> PreferenceBubbleStateCache = new(StringComparer.Ordinal);
    private INotifyPropertyChanged? _propertyChangedSource;
    private bool _isRestoringScrollOffset;
    private int _scrollApplyVersion;
    private int _layoutDiagnosticsVersion;
    private long _scrollRestoreStartedAt;
    private int _scrollRestoreAttempts;
    private string _scrollRestoreReason = string.Empty;
    private long _lastFrameTimestamp;
    private long _frameSummaryStartedAt;
    private int _slowFrameCount;
    private double _slowFrameTotalMs;
    private double _slowFrameMaxMs;
    private bool _isFrameDiagnosticsActive;
    private INotifyCollectionChanged? _bubbleCollectionChangedSource;
    private readonly List<PreferenceBubbleParticle> _preferenceBubbleParticles = [];
    private readonly List<BubbleFlowWave> _bubbleFlowWaves = [];
    private int _bubbleRebuildVersion;
    private long _lastBubblePhysicsTimestamp;
    private bool _isBubbleCanvasInViewport;
    private string _preferenceBubbleStateKey = string.Empty;
    private double _bubbleStateCanvasWidth;
    private double _bubbleStateCanvasHeight;
    private Point _bubblePointerPosition;
    private Point _lastBubblePointerSamplePosition;
    private Vector _bubblePointerVelocity;
    private long _lastBubblePointerTimestamp;
    private bool _isBubblePointerActive;
    private Point _lastBubbleRipplePosition;
    private long _lastBubbleRippleTimestamp;
    private bool _hasBubbleRippleAnchor;
    private int _bubbleRippleSequence;

    public WatchInsightsPage()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnIsVisibleChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        WatchInsightsDiagnostics.Write("layer=view event=loaded");
        AttachState();
        StartFrameDiagnostics();
        QueueApplyScrollOffset("loaded");
        QueueLayoutDiagnostics("loaded");
        QueuePreferenceBubbleRebuild();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        StoreCurrentScrollOffset();
        DetachState();
        AttachState();
        QueueApplyScrollOffset("data-context-changed");
        QueueLayoutDiagnostics("data-context-changed");
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StoreCurrentScrollOffset();
        StopFrameDiagnostics("unloaded");
        DetachState();
        ClearPreferenceBubbles();
        WatchInsightsDiagnostics.Write("layer=view event=unloaded");
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is false)
        {
            StoreCurrentScrollOffset();
            StopFrameDiagnostics("hidden");
            _lastBubblePhysicsTimestamp = 0;
            _isBubbleCanvasInViewport = false;
            return;
        }

        StartFrameDiagnostics();
        QueueApplyScrollOffset("visible");
        QueueLayoutDiagnostics("visible");
        QueuePreferenceBubbleRebuild();
    }

    private void InsightsScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_isRestoringScrollOffset
            || sender is not ScrollViewer scrollViewer
            || !scrollViewer.IsVisible
            || DataContext is not WatchInsightsViewModel viewModel)
        {
            return;
        }

        if (ReferenceEquals(scrollViewer, ProfileInsightsScrollViewer))
        {
            viewModel.ProfileScrollOffset = e.VerticalOffset;
        }
        else if (ReferenceEquals(scrollViewer, StatisticsInsightsScrollViewer))
        {
            viewModel.StatisticsScrollOffset = e.VerticalOffset;
            UpdatePreferenceBubbleViewportState();
        }
    }

    private void AttachState()
    {
        if (_propertyChangedSource is not null)
        {
            return;
        }

        if (DataContext is INotifyPropertyChanged source)
        {
            _propertyChangedSource = source;
            source.PropertyChanged += OnViewModelPropertyChanged;
        }

        if (DataContext is WatchInsightsViewModel viewModel)
        {
            _bubbleCollectionChangedSource = viewModel.PreferenceBubbles;
            _bubbleCollectionChangedSource.CollectionChanged += PreferenceBubbles_CollectionChanged;
        }
    }

    private void DetachState()
    {
        if (_propertyChangedSource is null)
        {
            return;
        }

        _propertyChangedSource.PropertyChanged -= OnViewModelPropertyChanged;
        _propertyChangedSource = null;

        if (_bubbleCollectionChangedSource is not null)
        {
            _bubbleCollectionChangedSource.CollectionChanged -= PreferenceBubbles_CollectionChanged;
            _bubbleCollectionChangedSource = null;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (DataContext is not WatchInsightsViewModel viewModel)
        {
            return;
        }

        if (e.PropertyName is nameof(WatchInsightsViewModel.SelectedTabIndex))
        {
            QueueApplyScrollOffset("tab-changed");
            QueueLayoutDiagnostics("tab-changed");
            QueuePreferenceBubbleRebuild();
            return;
        }

        if (e.PropertyName is nameof(WatchInsightsViewModel.IsLoadingProfile) && !viewModel.IsLoadingProfile)
        {
            QueueApplyScrollOffset("profile-load-complete");
            QueueLayoutDiagnostics("profile-load-complete");
        }
        else if (e.PropertyName is nameof(WatchInsightsViewModel.IsLoadingStatistics) && !viewModel.IsLoadingStatistics)
        {
            QueueApplyScrollOffset("statistics-load-complete");
            QueueLayoutDiagnostics("statistics-load-complete");
            QueuePreferenceBubbleRebuild();
        }
    }

    private void PreferenceBubbles_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        QueuePreferenceBubbleRebuild();
    }

    private void BubbleCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged || e.HeightChanged)
        {
            QueuePreferenceBubbleRebuild();
            UpdatePreferenceBubbleViewportState();
        }
    }

    private void BubbleCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        var position = e.GetPosition(BubbleCanvas);
        var now = Stopwatch.GetTimestamp();
        if (_isBubblePointerActive && _lastBubblePointerTimestamp > 0)
        {
            var elapsedSeconds = Math.Max(
                Stopwatch.GetElapsedTime(_lastBubblePointerTimestamp, now).TotalSeconds,
                1d / 240d);
            var velocity = position - _lastBubblePointerSamplePosition;
            velocity /= elapsedSeconds;
            var speed = velocity.Length;
            if (speed > 1200d)
            {
                velocity *= 1200d / speed;
            }

            _bubblePointerVelocity = (_bubblePointerVelocity * 0.12d) + (velocity * 0.88d);
        }

        _bubblePointerPosition = position;
        _lastBubblePointerSamplePosition = position;
        _lastBubblePointerTimestamp = now;
        _isBubblePointerActive = true;

        if (!_hasBubbleRippleAnchor)
        {
            _lastBubbleRipplePosition = position;
            _lastBubbleRippleTimestamp = now;
            _hasBubbleRippleAnchor = true;
            return;
        }

        var rippleDistance = (position - _lastBubbleRipplePosition).Length;
        var rippleElapsed = Stopwatch.GetElapsedTime(_lastBubbleRippleTimestamp, now);
        if (rippleDistance >= BubbleRippleDistanceThreshold && rippleElapsed >= TimeSpan.FromMilliseconds(70))
        {
            CreateBubbleRipple(position, _bubblePointerVelocity, now);
            _lastBubbleRipplePosition = position;
            _lastBubbleRippleTimestamp = now;
        }
    }

    private void BubbleCanvas_MouseLeave(object sender, MouseEventArgs e)
    {
        _isBubblePointerActive = false;
        _bubblePointerVelocity = default;
        _lastBubblePointerTimestamp = 0;
        _hasBubbleRippleAnchor = false;
    }

    private void CreateBubbleRipple(Point position, Vector pointerVelocity, long createdAt)
    {
        var speed = pointerVelocity.Length;
        var direction = speed > 1d
            ? pointerVelocity / speed
            : new Vector(1d, 0d);
        var speedRatio = Math.Clamp(speed / 1200d, 0d, 1d);
        var angle = Math.Atan2(direction.Y, direction.X) * 180d / Math.PI;
        var origin = position - (direction * (10d + speedRatio * 12d));
        var accentColor = ResolveBubbleRippleColor();
        CreateBubbleRippleVisual(
            origin,
            angle,
            accentColor,
            width: 34d,
            height: 22d,
            maximumScaleX: 13.5d + speedRatio * 3d,
            maximumScaleY: 7.2d + speedRatio * 1.8d,
            initialOpacity: 0.58d,
            duration: TimeSpan.FromMilliseconds(1200d + speedRatio * 220d),
            beginTime: TimeSpan.Zero,
            strokeThickness: 1.5d);
        if (++_bubbleRippleSequence % 2 == 0 || speedRatio >= 0.72d)
        {
            CreateBubbleRippleVisual(
                origin - (direction * 8d),
                angle,
                accentColor,
                width: 38d,
                height: 25d,
                maximumScaleX: 21d + speedRatio * 4d,
                maximumScaleY: 11.5d + speedRatio * 2d,
                initialOpacity: 0.34d,
                duration: TimeSpan.FromMilliseconds(1650d + speedRatio * 260d),
                beginTime: TimeSpan.FromMilliseconds(120d),
                strokeThickness: 1.1d);
        }
        _bubbleFlowWaves.Add(new BubbleFlowWave(position, direction, speed, createdAt));
    }

    private void CreateBubbleRippleVisual(
        Point origin,
        double angle,
        Color accentColor,
        double width,
        double height,
        double maximumScaleX,
        double maximumScaleY,
        double initialOpacity,
        TimeSpan duration,
        TimeSpan beginTime,
        double strokeThickness)
    {
        var scale = new ScaleTransform(0.2d, 0.2d);
        var transforms = new TransformGroup();
        transforms.Children.Add(scale);
        transforms.Children.Add(new RotateTransform(angle));
        var directionalStroke = new LinearGradientBrush
        {
            StartPoint = new Point(0d, 0.5d),
            EndPoint = new Point(1d, 0.5d),
            MappingMode = BrushMappingMode.RelativeToBoundingBox
        };
        directionalStroke.GradientStops.Add(new GradientStop(Color.FromArgb(28, accentColor.R, accentColor.G, accentColor.B), 0d));
        directionalStroke.GradientStops.Add(new GradientStop(Color.FromArgb(70, accentColor.R, accentColor.G, accentColor.B), 0.36d));
        directionalStroke.GradientStops.Add(new GradientStop(Color.FromArgb(205, accentColor.R, accentColor.G, accentColor.B), 0.76d));
        directionalStroke.GradientStops.Add(new GradientStop(Color.FromArgb(104, accentColor.R, accentColor.G, accentColor.B), 1d));
        directionalStroke.Freeze();
        var ripple = new Ellipse
        {
            Width = width,
            Height = height,
            Fill = Brushes.Transparent,
            Stroke = directionalStroke,
            StrokeThickness = strokeThickness,
            Opacity = 0d,
            IsHitTestVisible = false,
            RenderTransformOrigin = new Point(0.5d, 0.5d),
            RenderTransform = transforms
        };
        Canvas.SetLeft(ripple, origin.X - width / 2d);
        Canvas.SetTop(ripple, origin.Y - height / 2d);
        Panel.SetZIndex(ripple, 0);
        BubbleCanvas.Children.Add(ripple);

        var easing = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        scale.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            new DoubleAnimation(0.2d, maximumScaleX, duration)
            {
                BeginTime = beginTime,
                EasingFunction = easing
            });
        scale.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            new DoubleAnimation(0.2d, maximumScaleY, duration)
            {
                BeginTime = beginTime,
                EasingFunction = easing
            });
        var fade = new DoubleAnimation(initialOpacity, 0d, duration)
        {
            BeginTime = beginTime,
            EasingFunction = easing
        };
        fade.Completed += (_, _) => BubbleCanvas.Children.Remove(ripple);
        ripple.BeginAnimation(OpacityProperty, fade);
    }

    private static Color ResolveBubbleRippleColor()
    {
        return Application.Current?.TryFindResource("BrushAccent") is SolidColorBrush accentBrush
            ? accentBrush.Color
            : Color.FromRgb(91, 124, 250);
    }

    private void QueuePreferenceBubbleRebuild()
    {
        var version = ++_bubbleRebuildVersion;
        _ = Dispatcher.InvokeAsync(
            () =>
            {
                if (version == _bubbleRebuildVersion)
                {
                    RebuildPreferenceBubbles();
                }
            },
            DispatcherPriority.ContextIdle);
    }

    private void RebuildPreferenceBubbles()
    {
        ClearPreferenceBubbles();
        if (!IsLoaded
            || DataContext is not WatchInsightsViewModel { SelectedTabIndex: 1 } viewModel
            || !BubbleCanvas.IsVisible
            || BubbleCanvas.ActualWidth < 120d
            || BubbleCanvas.ActualHeight < 120d)
        {
            return;
        }

        var items = viewModel.PreferenceBubbles.ToList();
        _preferenceBubbleStateKey = BuildPreferenceBubbleStateKey(viewModel.StatisticsRangeText, items);
        _bubbleStateCanvasWidth = BubbleCanvas.ActualWidth;
        _bubbleStateCanvasHeight = BubbleCanvas.ActualHeight;
        PreferenceBubbleStateCache.TryGetValue(_preferenceBubbleStateKey, out var restoredStates);
        for (var index = 0; index < items.Count; index++)
        {
            var stateId = BuildPreferenceBubbleStateId(items[index]);
            PreferenceBubbleState? restoredState = null;
            if (restoredStates is not null)
            {
                restoredStates.TryGetValue(stateId, out restoredState);
            }

            var particle = CreatePreferenceBubbleParticle(
                items[index],
                index,
                items.Count,
                BubbleCanvas.ActualWidth,
                BubbleCanvas.ActualHeight,
                stateId,
                restoredState);
            _preferenceBubbleParticles.Add(particle);
            BubbleCanvas.Children.Add(particle.Host);
            UpdatePreferenceBubbleVisual(particle);
        }

        _lastBubblePhysicsTimestamp = 0;
        UpdatePreferenceBubbleViewportState();
    }

    private PreferenceBubbleParticle CreatePreferenceBubbleParticle(
        BubbleTagItem item,
        int index,
        int count,
        double canvasWidth,
        double canvasHeight,
        string stateId,
        PreferenceBubbleState? restoredState)
    {
        var baseRadius = item.Size / 2d;
        var hoverRadius = Math.Min(baseRadius + 18d, baseRadius * 1.28d);
        var hostDiameter = (hoverRadius + 6d) * 2d;
        var phase = (index * 1.61803398875d) + 0.7d;
        var initialPosition = CreateInitialBubblePosition(index, count, canvasWidth, canvasHeight, hoverRadius, phase);
        var position = restoredState is null
            ? initialPosition
            : new Point(
                Math.Clamp(restoredState.XRatio * canvasWidth, hoverRadius + 8d, Math.Max(hoverRadius + 8d, canvasWidth - hoverRadius - 8d)),
                Math.Clamp(restoredState.YRatio * canvasHeight, hoverRadius + 8d, Math.Max(hoverRadius + 8d, canvasHeight - hoverRadius - 8d)));

        var host = new Grid
        {
            Width = hostDiameter,
            Height = hostDiameter,
            IsHitTestVisible = true
        };
        var scale = new ScaleTransform(1d, 1d);
        var depthShadow = new Ellipse
        {
            Width = item.Size * 0.94d,
            Height = item.Size * 0.94d,
            Margin = new Thickness(0d, 9d, 0d, -9d),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Fill = CreateBubbleDepthBrush(),
            IsHitTestVisible = false,
            Opacity = 0.3d
        };
        var bubble = new Border
        {
            Width = item.Size,
            Height = item.Size,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            CornerRadius = new CornerRadius(baseRadius),
            BorderThickness = new Thickness(1.25d),
            Opacity = 0.96d,
            Padding = new Thickness(10d),
            RenderTransformOrigin = new Point(0.5d, 0.5d),
            RenderTransform = scale,
            ToolTip = $"{item.Kind} · {item.Label} · {item.CountText}"
        };
        ApplyPreferenceBubbleColors(bubble, item.Kind);

        var bubbleSurface = new Grid();
        var lowerShade = new Ellipse
        {
            Margin = new Thickness(-3d),
            Fill = CreateBubbleLowerShadeBrush(),
            IsHitTestVisible = false,
            Opacity = 0.72d
        };
        var innerRim = new Ellipse
        {
            Margin = new Thickness(2.5d),
            Stroke = CreateBubbleRimBrush(),
            StrokeThickness = 1.4d,
            IsHitTestVisible = false,
            Opacity = 0.82d
        };
        var highlight = new Ellipse
        {
            Width = Math.Max(18d, item.Size * 0.34d),
            Height = Math.Max(11d, item.Size * 0.2d),
            Margin = new Thickness(item.Size * 0.16d, item.Size * 0.12d, 0d, 0d),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Fill = CreateBubbleHighlightBrush(),
            IsHitTestVisible = false,
            Opacity = 0.64d,
            RenderTransform = new RotateTransform(-18d),
            RenderTransformOrigin = new Point(0.5d, 0.5d)
        };
        var content = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var label = new TextBlock
        {
            MaxWidth = Math.Max(54d, item.Size - 22d),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Text = item.Label,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.Wrap,
            FontSize = baseRadius >= 60d ? 18d : 16d,
            FontWeight = FontWeights.SemiBold
        };
        label.SetResourceReference(TextBlock.ForegroundProperty, "BrushForegroundPrimary");
        var countText = new TextBlock
        {
            Margin = new Thickness(0d, 5d, 0d, 0d),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Text = item.CountText,
            FontSize = 13d
        };
        countText.SetResourceReference(TextBlock.ForegroundProperty, "BrushForegroundMuted");
        content.Children.Add(label);
        content.Children.Add(countText);
        bubbleSurface.Children.Add(lowerShade);
        bubbleSurface.Children.Add(innerRim);
        bubbleSurface.Children.Add(highlight);
        bubbleSurface.Children.Add(content);
        bubble.Child = bubbleSurface;
        host.Children.Add(depthShadow);
        host.Children.Add(bubble);

        var particle = new PreferenceBubbleParticle(
            host,
            bubble,
            depthShadow,
            highlight,
            scale,
            position.X,
            position.Y,
            baseRadius,
            hoverRadius,
            phase,
            stateId)
        {
            VelocityX = restoredState?.VelocityX ?? Math.Cos(phase) * 4d,
            VelocityY = restoredState?.VelocityY ?? Math.Sin(phase) * 4d,
            Radius = restoredState is null
                ? baseRadius
                : Math.Clamp(baseRadius * restoredState.RadiusRatio, baseRadius, hoverRadius)
        };
        particle.TargetRadius = particle.BaseRadius;
        bubble.MouseEnter += (_, _) => SetPreferenceBubbleHover(particle, true);
        bubble.MouseLeave += (_, _) => SetPreferenceBubbleHover(particle, false);
        return particle;
    }

    private static Point CreateInitialBubblePosition(
        int index,
        int count,
        double width,
        double height,
        double radius,
        double phase)
    {
        var aspect = Math.Max(0.5d, width / Math.Max(1d, height));
        var columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(count * aspect)));
        var rows = Math.Max(1, (int)Math.Ceiling(count / (double)columns));
        var column = index % columns;
        var row = index / columns;
        var cellWidth = width / columns;
        var cellHeight = height / rows;
        var jitterX = Math.Sin(phase * 1.7d) * Math.Min(18d, cellWidth * 0.12d);
        var jitterY = Math.Cos(phase * 1.3d) * Math.Min(16d, cellHeight * 0.12d);
        var x = ((column + 0.5d) * cellWidth) + jitterX;
        var y = ((row + 0.5d) * cellHeight) + jitterY;
        return new Point(
            Math.Clamp(x, radius + 8d, Math.Max(radius + 8d, width - radius - 8d)),
            Math.Clamp(y, radius + 8d, Math.Max(radius + 8d, height - radius - 8d)));
    }

    private static void ApplyPreferenceBubbleColors(Border bubble, string kind)
    {
        var (backgroundKey, borderKey) = kind switch
        {
            "类型" => ("BrushInfoBackground", "BrushInfoBorder"),
            "情绪" => ("BrushAccentSoft", "BrushAccent"),
            "场景" => ("BrushWatchInsightsDnaSceneBackground", "BrushWatchInsightsDnaSceneBorder"),
            _ => ("BrushSurfaceAlt", "BrushBorder")
        };
        bubble.SetResourceReference(Border.BackgroundProperty, backgroundKey);
        bubble.SetResourceReference(Border.BorderBrushProperty, borderKey);
    }

    private static void SetPreferenceBubbleHover(PreferenceBubbleParticle particle, bool isHovered)
    {
        particle.IsHovered = isHovered;
        particle.TargetRadius = isHovered ? particle.HoverRadius : particle.BaseRadius;
        particle.Bubble.Opacity = isHovered ? 1d : 0.96d;
        particle.Bubble.BorderThickness = new Thickness(isHovered ? 2d : 1.25d);
        particle.DepthShadow.Opacity = isHovered ? 0.42d : 0.3d;
        particle.Highlight.Opacity = isHovered ? 0.82d : 0.64d;
        Panel.SetZIndex(particle.Host, isHovered ? 2 : 1);
    }

    private static Brush CreateBubbleDepthBrush()
    {
        var brush = new RadialGradientBrush
        {
            Center = new Point(0.5d, 0.5d),
            GradientOrigin = new Point(0.5d, 0.42d),
            RadiusX = 0.5d,
            RadiusY = 0.5d
        };
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(82, 18, 24, 34), 0d));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(44, 18, 24, 34), 0.62d));
        brush.GradientStops.Add(new GradientStop(Colors.Transparent, 1d));
        brush.Freeze();
        return brush;
    }

    private static Brush CreateBubbleLowerShadeBrush()
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0.5d, 0d),
            EndPoint = new Point(0.5d, 1d)
        };
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0d));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.5d));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(20, 8, 12, 20), 0.72d));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(58, 8, 12, 20), 1d));
        brush.Freeze();
        return brush;
    }

    private static Brush CreateBubbleRimBrush()
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0.3d, 0d),
            EndPoint = new Point(0.7d, 1d)
        };
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(220, 255, 255, 255), 0d));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(88, 255, 255, 255), 0.42d));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(24, 255, 255, 255), 0.68d));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(62, 12, 18, 28), 1d));
        brush.Freeze();
        return brush;
    }

    private static Brush CreateBubbleHighlightBrush()
    {
        var brush = new RadialGradientBrush
        {
            Center = new Point(0.35d, 0.35d),
            GradientOrigin = new Point(0.28d, 0.28d),
            RadiusX = 0.68d,
            RadiusY = 0.68d
        };
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(236, 255, 255, 255), 0d));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(112, 255, 255, 255), 0.46d));
        brush.GradientStops.Add(new GradientStop(Colors.Transparent, 1d));
        brush.Freeze();
        return brush;
    }

    private void ClearPreferenceBubbles()
    {
        SavePreferenceBubbleState();
        _lastBubblePhysicsTimestamp = 0;
        _isBubbleCanvasInViewport = false;
        _preferenceBubbleParticles.Clear();
        _bubbleFlowWaves.Clear();
        BubbleCanvas.Children.Clear();
        _preferenceBubbleStateKey = string.Empty;
        _bubbleStateCanvasWidth = 0d;
        _bubbleStateCanvasHeight = 0d;
        _isBubblePointerActive = false;
        _bubblePointerVelocity = default;
        _lastBubblePointerTimestamp = 0;
        _hasBubbleRippleAnchor = false;
    }

    private void SavePreferenceBubbleState()
    {
        if (string.IsNullOrEmpty(_preferenceBubbleStateKey)
            || _preferenceBubbleParticles.Count == 0
            || _bubbleStateCanvasWidth <= 0d
            || _bubbleStateCanvasHeight <= 0d)
        {
            return;
        }

        PreferenceBubbleStateCache[_preferenceBubbleStateKey] = _preferenceBubbleParticles.ToDictionary(
            particle => particle.StateId,
            particle => new PreferenceBubbleState(
                Math.Clamp(particle.X / _bubbleStateCanvasWidth, 0d, 1d),
                Math.Clamp(particle.Y / _bubbleStateCanvasHeight, 0d, 1d),
                particle.VelocityX,
                particle.VelocityY,
                particle.BaseRadius <= 0d ? 1d : particle.Radius / particle.BaseRadius),
            StringComparer.Ordinal);
    }

    private static string BuildPreferenceBubbleStateKey(string rangeKey, IReadOnlyList<BubbleTagItem> items)
    {
        return $"{rangeKey.Length}:{rangeKey}|" + string.Concat(items.Select(item =>
            $"{item.Kind.Length}:{item.Kind}{item.Label.Length}:{item.Label}:{item.Count};"));
    }

    private static string BuildPreferenceBubbleStateId(BubbleTagItem item)
    {
        return $"{item.Kind}\u001F{item.Label}";
    }

    private void UpdatePreferenceBubbleViewportState()
    {
        if (!BubbleCanvas.IsVisible
            || !StatisticsInsightsScrollViewer.IsVisible
            || BubbleCanvas.ActualWidth <= 0d
            || BubbleCanvas.ActualHeight <= 0d
            || StatisticsInsightsScrollViewer.ViewportHeight <= 0d)
        {
            _isBubbleCanvasInViewport = false;
            return;
        }

        try
        {
            var bounds = BubbleCanvas
                .TransformToAncestor(StatisticsInsightsScrollViewer)
                .TransformBounds(new Rect(0d, 0d, BubbleCanvas.ActualWidth, BubbleCanvas.ActualHeight));
            var viewport = new Rect(
                0d,
                0d,
                StatisticsInsightsScrollViewer.ActualWidth,
                StatisticsInsightsScrollViewer.ViewportHeight);
            _isBubbleCanvasInViewport = bounds.IntersectsWith(viewport);
            if (!_isBubbleCanvasInViewport)
            {
                _lastBubblePhysicsTimestamp = 0;
            }
        }
        catch (InvalidOperationException)
        {
            _isBubbleCanvasInViewport = false;
        }
    }

    private void StoreCurrentScrollOffset()
    {
        if (_isRestoringScrollOffset || DataContext is not WatchInsightsViewModel viewModel)
        {
            return;
        }

        var scrollViewer = viewModel.SelectedTabIndex == 1
            ? StatisticsInsightsScrollViewer
            : ProfileInsightsScrollViewer;
        if (!scrollViewer.IsVisible)
        {
            return;
        }

        if (viewModel.SelectedTabIndex == 1)
        {
            viewModel.StatisticsScrollOffset = scrollViewer.VerticalOffset;
        }
        else
        {
            viewModel.ProfileScrollOffset = scrollViewer.VerticalOffset;
        }
    }

    private void QueueApplyScrollOffset(string reason)
    {
        if (DataContext is not WatchInsightsViewModel viewModel)
        {
            return;
        }

        if (!NeedsScrollRestore(viewModel))
        {
            return;
        }

        _isRestoringScrollOffset = true;
        _scrollRestoreStartedAt = Stopwatch.GetTimestamp();
        _scrollRestoreAttempts = 0;
        _scrollRestoreReason = reason;
        var applyVersion = ++_scrollApplyVersion;
        if (TryApplyScrollOffset(viewModel))
        {
            FinishScrollRestore(applyVersion);
            return;
        }

        _ = Dispatcher.InvokeAsync(
            () => ApplyScrollOffset(0, applyVersion),
            DispatcherPriority.Loaded);
    }

    private void ApplyScrollOffset(int attempt, int applyVersion)
    {
        if (applyVersion != _scrollApplyVersion)
        {
            return;
        }

        if (DataContext is not WatchInsightsViewModel viewModel)
        {
            FinishScrollRestore(applyVersion);
            return;
        }

        _scrollRestoreAttempts = Math.Max(_scrollRestoreAttempts, attempt + 1);
        if (TryApplyScrollOffset(viewModel) || attempt >= ScrollRestoreMaxAttempts)
        {
            FinishScrollRestore(applyVersion);
            return;
        }

        _ = Dispatcher.InvokeAsync(
            () => ApplyScrollOffset(attempt + 1, applyVersion),
            DispatcherPriority.ContextIdle);
    }

    private bool TryApplyScrollOffset(WatchInsightsViewModel viewModel)
    {
        var (scrollViewer, targetOffset) = viewModel.SelectedTabIndex == 1
            ? (StatisticsInsightsScrollViewer, viewModel.StatisticsScrollOffset)
            : (ProfileInsightsScrollViewer, viewModel.ProfileScrollOffset);
        targetOffset = Math.Max(0d, targetOffset);
        if (!scrollViewer.IsVisible)
        {
            return targetOffset <= 0d;
        }

        if (targetOffset > 0d && scrollViewer.ScrollableHeight <= 0d)
        {
            return false;
        }

        var clampedOffset = Math.Min(targetOffset, scrollViewer.ScrollableHeight);
        if (Math.Abs(scrollViewer.VerticalOffset - clampedOffset) > 0.5d)
        {
            scrollViewer.ScrollToVerticalOffset(clampedOffset);
        }

        return true;
    }

    private void FinishScrollRestore(int applyVersion)
    {
        if (applyVersion == _scrollApplyVersion)
        {
            _isRestoringScrollOffset = false;
            var elapsed = _scrollRestoreStartedAt == 0
                ? TimeSpan.Zero
                : Stopwatch.GetElapsedTime(_scrollRestoreStartedAt);
            var viewModel = DataContext as WatchInsightsViewModel;
            var scrollViewer = viewModel?.SelectedTabIndex == 1
                ? StatisticsInsightsScrollViewer
                : ProfileInsightsScrollViewer;
            WatchInsightsDiagnostics.Write(
                "layer=view event=scroll-restore-complete "
                + $"reason={_scrollRestoreReason} tab={(viewModel?.SelectedTabIndex == 1 ? "statistics" : "profile")} "
                + $"elapsedMs={elapsed.TotalMilliseconds:0} attempts={_scrollRestoreAttempts} "
                + $"offset={scrollViewer.VerticalOffset:0} extent={scrollViewer.ExtentHeight:0} viewport={scrollViewer.ViewportHeight:0}");
        }
    }

    private bool NeedsScrollRestore(WatchInsightsViewModel viewModel)
    {
        var scrollViewer = viewModel.SelectedTabIndex == 1
            ? StatisticsInsightsScrollViewer
            : ProfileInsightsScrollViewer;
        if (!scrollViewer.IsVisible)
        {
            return true;
        }

        var targetOffset = Math.Max(
            0d,
            viewModel.SelectedTabIndex == 1 ? viewModel.StatisticsScrollOffset : viewModel.ProfileScrollOffset);
        if (targetOffset > 0d && scrollViewer.ScrollableHeight <= 0d)
        {
            return true;
        }

        return Math.Abs(scrollViewer.VerticalOffset - Math.Min(targetOffset, scrollViewer.ScrollableHeight)) > 0.5d;
    }

    private void QueueLayoutDiagnostics(string reason)
    {
        var version = ++_layoutDiagnosticsVersion;
        var startedAt = Stopwatch.GetTimestamp();
        _ = Dispatcher.InvokeAsync(
            () => CaptureLayoutDiagnostics(version, reason, startedAt),
            DispatcherPriority.ContextIdle);
    }

    private void CaptureLayoutDiagnostics(int version, string reason, long startedAt)
    {
        if (version != _layoutDiagnosticsVersion || !IsVisible)
        {
            return;
        }

        var captureStartedAt = Stopwatch.GetTimestamp();
        var viewModel = DataContext as WatchInsightsViewModel;
        var scrollViewer = viewModel?.SelectedTabIndex == 1
            ? StatisticsInsightsScrollViewer
            : ProfileInsightsScrollViewer;
        var metrics = CountVisualMetrics(scrollViewer);
        var captureElapsed = Stopwatch.GetElapsedTime(captureStartedAt);
        var idleElapsed = Stopwatch.GetElapsedTime(startedAt);
        WatchInsightsDiagnostics.Write(
            "layer=view event=layout-idle "
            + $"reason={reason} tab={(viewModel?.SelectedTabIndex == 1 ? "statistics" : "profile")} "
            + $"idleMs={idleElapsed.TotalMilliseconds:0} captureMs={captureElapsed.TotalMilliseconds:0} "
            + $"visuals={metrics.VisualCount} effects={metrics.EffectCount} dropShadows={metrics.DropShadowCount} "
            + $"animated={metrics.AnimatedCount} extent={scrollViewer.ExtentHeight:0} viewport={scrollViewer.ViewportHeight:0} "
            + $"renderTier={RenderCapability.Tier >> 16}");
    }

    private static VisualMetrics CountVisualMetrics(DependencyObject root)
    {
        var visualCount = 1;
        var effectCount = root is UIElement { Effect: not null } ? 1 : 0;
        var dropShadowCount = root is UIElement { Effect: DropShadowEffect } ? 1 : 0;
        var animatedCount = HasActiveTransformOrOpacity(root) ? 1 : 0;
        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < childCount; index++)
        {
            var childMetrics = CountVisualMetrics(VisualTreeHelper.GetChild(root, index));
            visualCount += childMetrics.VisualCount;
            effectCount += childMetrics.EffectCount;
            dropShadowCount += childMetrics.DropShadowCount;
            animatedCount += childMetrics.AnimatedCount;
        }

        return new VisualMetrics(visualCount, effectCount, dropShadowCount, animatedCount);
    }

    private static bool HasActiveTransformOrOpacity(DependencyObject root)
    {
        return root is UIElement element
               && (element.RenderTransform is not null && element.RenderTransform != Transform.Identity
                   || element.Opacity < 1d);
    }

    private void StartFrameDiagnostics()
    {
        if (_isFrameDiagnosticsActive)
        {
            return;
        }

        _isFrameDiagnosticsActive = true;
        _lastFrameTimestamp = 0;
        _frameSummaryStartedAt = Stopwatch.GetTimestamp();
        _slowFrameCount = 0;
        _slowFrameTotalMs = 0d;
        _slowFrameMaxMs = 0d;
        CompositionTarget.Rendering += OnCompositionTargetRendering;
    }

    private void StopFrameDiagnostics(string reason)
    {
        if (!_isFrameDiagnosticsActive)
        {
            return;
        }

        CompositionTarget.Rendering -= OnCompositionTargetRendering;
        _isFrameDiagnosticsActive = false;
        WriteFrameSummary(reason);
        _lastFrameTimestamp = 0;
    }

    private void OnCompositionTargetRendering(object? sender, EventArgs e)
    {
        var now = Stopwatch.GetTimestamp();
        UpdatePreferenceBubblePhysics(now);
        if (_lastFrameTimestamp > 0)
        {
            var frameMs = Stopwatch.GetElapsedTime(_lastFrameTimestamp, now).TotalMilliseconds;
            if (frameMs >= 50d)
            {
                _slowFrameCount++;
                _slowFrameTotalMs += frameMs;
                _slowFrameMaxMs = Math.Max(_slowFrameMaxMs, frameMs);
                if (frameMs >= 100d)
                {
                    var viewModel = DataContext as WatchInsightsViewModel;
                    WatchInsightsDiagnostics.Write(
                        "layer=view event=slow-frame "
                        + $"tab={(viewModel?.SelectedTabIndex == 1 ? "statistics" : "profile")} frameMs={frameMs:0}");
                }
            }
        }

        _lastFrameTimestamp = now;
        if (Stopwatch.GetElapsedTime(_frameSummaryStartedAt, now) >= TimeSpan.FromSeconds(5))
        {
            WriteFrameSummary("interval");
            _frameSummaryStartedAt = now;
        }
    }

    private void UpdatePreferenceBubblePhysics(long now)
    {
        if (_preferenceBubbleParticles.Count == 0
            || DataContext is not WatchInsightsViewModel { SelectedTabIndex: 1 }
            || !_isBubbleCanvasInViewport)
        {
            _lastBubblePhysicsTimestamp = 0;
            return;
        }

        if (_lastBubblePhysicsTimestamp == 0)
        {
            _lastBubblePhysicsTimestamp = now;
            return;
        }

        var deltaSeconds = Math.Clamp(
            Stopwatch.GetElapsedTime(_lastBubblePhysicsTimestamp, now).TotalSeconds,
            1d / 240d,
            1d / 30d);
        _lastBubblePhysicsTimestamp = now;
        var elapsedSeconds = now / (double)Stopwatch.Frequency;
        if (_isBubblePointerActive
            && _lastBubblePointerTimestamp > 0
            && Stopwatch.GetElapsedTime(_lastBubblePointerTimestamp, now) > TimeSpan.FromMilliseconds(120))
        {
            var pointerDamping = Math.Pow(0.94d, deltaSeconds * 60d);
            _bubblePointerVelocity *= pointerDamping;
        }

        foreach (var particle in _preferenceBubbleParticles)
        {
            var radiusEase = 1d - Math.Exp(-10d * deltaSeconds);
            particle.Radius += (particle.TargetRadius - particle.Radius) * radiusEase;
            var roamFrequencyX = 0.035d + ((particle.Phase * 0.013d) % 0.026d);
            var roamFrequencyY = 0.031d + ((particle.Phase * 0.017d) % 0.029d);
            var roamTargetX = BubbleCanvas.ActualWidth
                              * (0.14d + 0.72d * (0.5d + 0.5d * Math.Sin((elapsedSeconds * roamFrequencyX) + particle.Phase)));
            var roamTargetY = BubbleCanvas.ActualHeight
                              * (0.14d + 0.72d * (0.5d + 0.5d * Math.Cos((elapsedSeconds * roamFrequencyY) + (particle.Phase * 1.37d))));
            particle.AccelerationX = (roamTargetX - particle.X) * 0.014d;
            particle.AccelerationY = (roamTargetY - particle.Y) * 0.014d;

            var edgeInset = particle.Radius + 34d;
            if (particle.X < edgeInset)
            {
                particle.AccelerationX += (edgeInset - particle.X) * 0.48d;
            }
            else if (particle.X > BubbleCanvas.ActualWidth - edgeInset)
            {
                particle.AccelerationX -= (particle.X - (BubbleCanvas.ActualWidth - edgeInset)) * 0.48d;
            }

            if (particle.Y < edgeInset)
            {
                particle.AccelerationY += (edgeInset - particle.Y) * 0.48d;
            }
            else if (particle.Y > BubbleCanvas.ActualHeight - edgeInset)
            {
                particle.AccelerationY -= (particle.Y - (BubbleCanvas.ActualHeight - edgeInset)) * 0.48d;
            }

            ApplyBubbleCornerEscape(particle, BubbleCanvas.ActualWidth, BubbleCanvas.ActualHeight, elapsedSeconds);

            var flowAngle = Math.Sin((particle.X * 0.006d) + (elapsedSeconds * 0.11d) + particle.Phase)
                            + Math.Cos((particle.Y * 0.007d) - (elapsedSeconds * 0.09d) + (particle.Phase * 1.83d));
            particle.AccelerationX += Math.Cos(flowAngle * Math.PI) * 17d;
            particle.AccelerationY += Math.Sin(flowAngle * Math.PI) * 17d;
            particle.AccelerationX += Math.Sin((elapsedSeconds * 0.17d) + (particle.Phase * 2.17d)) * 6d;
            particle.AccelerationY += Math.Cos((elapsedSeconds * 0.15d) + (particle.Phase * 2.63d)) * 6d;

            if (_isBubblePointerActive)
            {
                var pointerDeltaX = particle.X - _bubblePointerPosition.X;
                var pointerDeltaY = particle.Y - _bubblePointerPosition.Y;
                var pointerDistanceSquared = (pointerDeltaX * pointerDeltaX) + (pointerDeltaY * pointerDeltaY);
                var pointerDistance = Math.Sqrt(Math.Max(pointerDistanceSquared, 0.0001d));
                var pointerNormalX = pointerDistance > 0.01d ? pointerDeltaX / pointerDistance : Math.Cos(particle.Phase);
                var pointerNormalY = pointerDistance > 0.01d ? pointerDeltaY / pointerDistance : Math.Sin(particle.Phase);
                var pointerSpeed = _bubblePointerVelocity.Length;
                var pointerSpeedRatio = Math.Clamp(pointerSpeed / 1200d, 0d, 1d);
                var pointerDirection = pointerSpeed > 1d
                    ? _bubblePointerVelocity / pointerSpeed
                    : new Vector(1d, 0d);
                var pointerSide = new Vector(-pointerDirection.Y, pointerDirection.X);
                var along = (pointerDeltaX * pointerDirection.X) + (pointerDeltaY * pointerDirection.Y);
                var side = (pointerDeltaX * pointerSide.X) + (pointerDeltaY * pointerSide.Y);
                var canvasDiagonal = Math.Sqrt(
                    (BubbleCanvas.ActualWidth * BubbleCanvas.ActualWidth)
                    + (BubbleCanvas.ActualHeight * BubbleCanvas.ActualHeight));
                var alongRadius = Math.Max(360d, Math.Min(680d, canvasDiagonal * 0.72d));
                var sideRadius = Math.Max(220d, Math.Min(420d, canvasDiagonal * 0.42d));
                var directionalDistance = Math.Sqrt(
                    (along * along) / (alongRadius * alongRadius)
                    + (side * side) / (sideRadius * sideRadius));
                var directionalInfluence = Math.Exp(-2.1d * directionalDistance * directionalDistance);
                var nearInfluence = Math.Exp(-pointerDistanceSquared / (2d * 155d * 155d));
                var radialForce = nearInfluence * (88d + pointerSpeedRatio * 450d)
                                  + directionalInfluence * pointerSpeedRatio * 44d;
                particle.AccelerationX += pointerNormalX * radialForce;
                particle.AccelerationY += pointerNormalY * radialForce;
                particle.AccelerationX += _bubblePointerVelocity.X * directionalInfluence * 0.34d;
                particle.AccelerationY += _bubblePointerVelocity.Y * directionalInfluence * 0.34d;
                var sideSign = side >= 0d ? 1d : -1d;
                var sideForce = directionalInfluence * pointerSpeedRatio * 145d * sideSign;
                particle.AccelerationX += pointerSide.X * sideForce;
                particle.AccelerationY += pointerSide.Y * sideForce;
            }
        }

        ApplyBubbleFlowWaves(now);

        for (var leftIndex = 0; leftIndex < _preferenceBubbleParticles.Count; leftIndex++)
        {
            var left = _preferenceBubbleParticles[leftIndex];
            for (var rightIndex = leftIndex + 1; rightIndex < _preferenceBubbleParticles.Count; rightIndex++)
            {
                var right = _preferenceBubbleParticles[rightIndex];
                var deltaX = right.X - left.X;
                var deltaY = right.Y - left.Y;
                var distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);
                var minimumDistance = left.Radius + right.Radius + 5d;
                var comfortDistance = minimumDistance + 34d;
                if (distanceSquared >= comfortDistance * comfortDistance)
                {
                    continue;
                }

                var distance = Math.Sqrt(Math.Max(distanceSquared, 0.0001d));
                var normalX = distance > 0.01d ? deltaX / distance : Math.Cos(left.Phase + right.Phase);
                var normalY = distance > 0.01d ? deltaY / distance : Math.Sin(left.Phase + right.Phase);
                var comfortOverlap = comfortDistance - distance;
                var separationForce = comfortOverlap * comfortOverlap / Math.Max(comfortDistance, 1d) * 2.2d;
                left.AccelerationX -= normalX * separationForce;
                left.AccelerationY -= normalY * separationForce;
                right.AccelerationX += normalX * separationForce;
                right.AccelerationY += normalY * separationForce;
                if (distance >= minimumDistance)
                {
                    continue;
                }

                var overlap = minimumDistance - distance;
                var mobilityTotal = left.Mobility + right.Mobility;
                var correction = overlap * 0.94d;
                var leftCorrection = correction * left.Mobility / mobilityTotal;
                var rightCorrection = correction * right.Mobility / mobilityTotal;
                left.X -= normalX * leftCorrection;
                left.Y -= normalY * leftCorrection;
                right.X += normalX * rightCorrection;
                right.Y += normalY * rightCorrection;

                var collisionForce = overlap * 58d;
                left.AccelerationX -= normalX * collisionForce;
                left.AccelerationY -= normalY * collisionForce;
                right.AccelerationX += normalX * collisionForce;
                right.AccelerationY += normalY * collisionForce;

                var relativeNormalVelocity = ((right.VelocityX - left.VelocityX) * normalX)
                                             + ((right.VelocityY - left.VelocityY) * normalY);
                if (relativeNormalVelocity < 0d)
                {
                    var impulse = -relativeNormalVelocity * 0.58d;
                    left.VelocityX -= normalX * impulse * left.Mobility;
                    left.VelocityY -= normalY * impulse * left.Mobility;
                    right.VelocityX += normalX * impulse * right.Mobility;
                    right.VelocityY += normalY * impulse * right.Mobility;
                }
            }
        }

        var damping = Math.Pow(0.993d, deltaSeconds * 60d);
        foreach (var particle in _preferenceBubbleParticles)
        {
            particle.VelocityX = (particle.VelocityX + (particle.AccelerationX * particle.Mobility * deltaSeconds)) * damping;
            particle.VelocityY = (particle.VelocityY + (particle.AccelerationY * particle.Mobility * deltaSeconds)) * damping;
            var speed = Math.Sqrt((particle.VelocityX * particle.VelocityX) + (particle.VelocityY * particle.VelocityY));
            if (speed > 170d)
            {
                var speedScale = 170d / speed;
                particle.VelocityX *= speedScale;
                particle.VelocityY *= speedScale;
            }

            particle.X += particle.VelocityX * deltaSeconds;
            particle.Y += particle.VelocityY * deltaSeconds;
            ConstrainPreferenceBubble(particle, BubbleCanvas.ActualWidth, BubbleCanvas.ActualHeight);
            UpdatePreferenceBubbleVisual(particle);
        }
    }

    private static void ApplyBubbleCornerEscape(
        PreferenceBubbleParticle particle,
        double width,
        double height,
        double elapsedSeconds)
    {
        var cornerRange = particle.Radius + 92d;
        var leftInfluence = Math.Clamp((cornerRange - particle.X) / cornerRange, 0d, 1d);
        var rightInfluence = Math.Clamp((particle.X - (width - cornerRange)) / cornerRange, 0d, 1d);
        var topInfluence = Math.Clamp((cornerRange - particle.Y) / cornerRange, 0d, 1d);
        var bottomInfluence = Math.Clamp((particle.Y - (height - cornerRange)) / cornerRange, 0d, 1d);
        var horizontalInfluence = Math.Max(leftInfluence, rightInfluence);
        var verticalInfluence = Math.Max(topInfluence, bottomInfluence);
        var cornerInfluence = horizontalInfluence * verticalInfluence;
        if (cornerInfluence <= 0d)
        {
            return;
        }

        var inwardX = leftInfluence >= rightInfluence ? 1d : -1d;
        var inwardY = topInfluence >= bottomInfluence ? 1d : -1d;
        var pulse = 0.82d + 0.18d * Math.Sin((elapsedSeconds * 0.7d) + particle.Phase);
        var escapeForce = cornerInfluence * pulse * 46d;
        particle.AccelerationX += inwardX * escapeForce;
        particle.AccelerationY += inwardY * escapeForce;
        particle.AccelerationX += -inwardY * Math.Sin(particle.Phase * 1.7d) * escapeForce * 0.28d;
        particle.AccelerationY += inwardX * Math.Cos(particle.Phase * 1.3d) * escapeForce * 0.28d;
    }

    private void ApplyBubbleFlowWaves(long now)
    {
        if (_bubbleFlowWaves.Count == 0)
        {
            return;
        }

        var canvasDiagonal = Math.Sqrt(
            (BubbleCanvas.ActualWidth * BubbleCanvas.ActualWidth)
            + (BubbleCanvas.ActualHeight * BubbleCanvas.ActualHeight));
        var maximumTravel = Math.Max(520d, Math.Min(960d, canvasDiagonal * 1.08d));
        for (var waveIndex = _bubbleFlowWaves.Count - 1; waveIndex >= 0; waveIndex--)
        {
            var wave = _bubbleFlowWaves[waveIndex];
            var ageSeconds = Stopwatch.GetElapsedTime(wave.CreatedAt, now).TotalSeconds;
            if (ageSeconds >= BubbleFlowWaveLifetimeSeconds)
            {
                _bubbleFlowWaves.RemoveAt(waveIndex);
                continue;
            }

            var progress = Math.Clamp(ageSeconds / BubbleFlowWaveLifetimeSeconds, 0d, 1d);
            var waveRadius = 24d + maximumTravel * progress;
            var waveThickness = 72d + progress * 118d;
            var decay = Math.Pow(1d - progress, 0.92d);
            var speedRatio = Math.Clamp(wave.Speed / 1200d, 0.18d, 1d);
            var sideDirection = new Vector(-wave.Direction.Y, wave.Direction.X);
            foreach (var particle in _preferenceBubbleParticles)
            {
                var delta = new Vector(particle.X - wave.Origin.X, particle.Y - wave.Origin.Y);
                var distance = Math.Max(delta.Length, 0.001d);
                var along = Vector.Multiply(delta, wave.Direction);
                var side = Vector.Multiply(delta, sideDirection);
                var directionalDistance = Math.Sqrt((along * along * 0.62d) + (side * side * 1.28d));
                var shellOffset = (directionalDistance - waveRadius) / waveThickness;
                var frontRatio = Math.Clamp(along / Math.Max(waveRadius, 1d), -1d, 1d);
                var directionalDepth = 0.58d + ((frontRatio + 1d) * 0.21d);
                var shellInfluence = Math.Exp(-shellOffset * shellOffset * 1.7d) * decay * directionalDepth;
                if (shellInfluence < 0.008d)
                {
                    continue;
                }

                var radialDirection = delta / distance;
                var waveForce = shellInfluence * (115d + speedRatio * 245d);
                particle.AccelerationX += radialDirection.X * waveForce;
                particle.AccelerationY += radialDirection.Y * waveForce;
                particle.AccelerationX += wave.Direction.X * waveForce * 0.48d;
                particle.AccelerationY += wave.Direction.Y * waveForce * 0.48d;
                var sideSign = side >= 0d ? 1d : -1d;
                particle.AccelerationX += sideDirection.X * waveForce * sideSign * 0.24d;
                particle.AccelerationY += sideDirection.Y * waveForce * sideSign * 0.24d;
            }
        }
    }

    private static void ConstrainPreferenceBubble(PreferenceBubbleParticle particle, double width, double height)
    {
        var inset = particle.Radius + 8d;
        var maximumX = Math.Max(inset, width - inset);
        var maximumY = Math.Max(inset, height - inset);
        if (particle.X < inset)
        {
            particle.X = inset;
            ApplyBubbleBoundaryImpulse(particle, 1d, 0d);
        }
        else if (particle.X > maximumX)
        {
            particle.X = maximumX;
            ApplyBubbleBoundaryImpulse(particle, -1d, 0d);
        }

        if (particle.Y < inset)
        {
            particle.Y = inset;
            ApplyBubbleBoundaryImpulse(particle, 0d, 1d);
        }
        else if (particle.Y > maximumY)
        {
            particle.Y = maximumY;
            ApplyBubbleBoundaryImpulse(particle, 0d, -1d);
        }
    }

    private static void ApplyBubbleBoundaryImpulse(
        PreferenceBubbleParticle particle,
        double normalX,
        double normalY)
    {
        var normalVelocity = (particle.VelocityX * normalX) + (particle.VelocityY * normalY);
        if (normalVelocity >= 0d)
        {
            return;
        }

        const double restitution = 0.52d;
        var impulseMagnitude = -(1d + restitution) * normalVelocity * particle.Mass;
        particle.VelocityX += impulseMagnitude * normalX / particle.Mass;
        particle.VelocityY += impulseMagnitude * normalY / particle.Mass;

        var tangentX = -normalY;
        var tangentY = normalX;
        var tangentVelocity = (particle.VelocityX * tangentX) + (particle.VelocityY * tangentY);
        particle.VelocityX -= tangentX * tangentVelocity * 0.015d;
        particle.VelocityY -= tangentY * tangentVelocity * 0.015d;
    }

    private static void UpdatePreferenceBubbleVisual(PreferenceBubbleParticle particle)
    {
        Canvas.SetLeft(particle.Host, particle.X - (particle.Host.Width / 2d));
        Canvas.SetTop(particle.Host, particle.Y - (particle.Host.Height / 2d));
        var scale = particle.Radius / particle.BaseRadius;
        particle.Scale.ScaleX = scale;
        particle.Scale.ScaleY = scale;
    }

    private void WriteFrameSummary(string reason)
    {
        var viewModel = DataContext as WatchInsightsViewModel;
        var averageMs = _slowFrameCount == 0 ? 0d : _slowFrameTotalMs / _slowFrameCount;
        WatchInsightsDiagnostics.Write(
            "layer=view event=frame-summary "
            + $"reason={reason} tab={(viewModel?.SelectedTabIndex == 1 ? "statistics" : "profile")} "
            + $"slowFrames={_slowFrameCount} averageSlowMs={averageMs:0} maxSlowMs={_slowFrameMaxMs:0}");
        _slowFrameCount = 0;
        _slowFrameTotalMs = 0d;
        _slowFrameMaxMs = 0d;
    }

    private void ProfileSummaryScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        if (!NeedsInternalScroll(scrollViewer))
        {
            ForwardMouseWheelToParent(scrollViewer, e);
            return;
        }

        if (CanScrollVertically(scrollViewer, e.Delta))
        {
            const double pixelStep = 36d;
            var direction = e.Delta < 0 ? 1d : -1d;
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + (direction * pixelStep));
        }

        e.Handled = true;
    }

    private void ProfileDnaCard_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not CachedShadowBorder card
            || GetMutableDnaTransforms(card) is not { Children.Count: >= 2 } transforms
            || transforms.Children[0] is not TranslateTransform ambientTransform
            || transforms.Children[1] is not ScaleTransform depthTransform)
        {
            return;
        }

        var presenter = FindVisualParent<ContentPresenter>(card);
        var index = presenter is null ? 0 : Math.Max(0, ItemsControl.GetAlternationIndex(presenter));
        var row = Math.Clamp(index / 3, 0, 1);
        var column = index % 3;
        var phase = (column * Math.PI * 2d / 3d) + (row * Math.PI);
        var duration = TimeSpan.FromSeconds(5.8d);

        ambientTransform.BeginAnimation(
            TranslateTransform.YProperty,
            CreateDnaWaveAnimation(duration, phase, angle => Math.Sin(angle) * 7.2d),
            HandoffBehavior.SnapshotAndReplace);
        ambientTransform.BeginAnimation(
            TranslateTransform.XProperty,
            CreateDnaWaveAnimation(duration, phase, angle => Math.Cos(angle) * 1.8d),
            HandoffBehavior.SnapshotAndReplace);
        depthTransform.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            CreateDnaWaveAnimation(duration, phase, angle => 1d + (Math.Cos(angle) * 0.012d)),
            HandoffBehavior.SnapshotAndReplace);
        depthTransform.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            CreateDnaWaveAnimation(duration, phase, angle => 1d + (Math.Cos(angle) * 0.012d)),
            HandoffBehavior.SnapshotAndReplace);
        card.BeginAnimation(
            CachedShadowBorder.ShadowVisualOpacityProperty,
            CreateDnaWaveAnimation(duration, phase, angle => 0.82d + (Math.Cos(angle) * 0.12d)),
            HandoffBehavior.SnapshotAndReplace);
    }

    private static DoubleAnimationUsingKeyFrames CreateDnaWaveAnimation(
        TimeSpan duration,
        double phase,
        Func<double, double> valueFactory)
    {
        const int sampleCount = 36;
        var animation = new DoubleAnimationUsingKeyFrames
        {
            Duration = duration,
            RepeatBehavior = RepeatBehavior.Forever
        };

        for (var sample = 0; sample <= sampleCount; sample++)
        {
            var progress = sample / (double)sampleCount;
            var angle = phase + (progress * Math.PI * 2d);
            animation.KeyFrames.Add(
                new LinearDoubleKeyFrame(
                    valueFactory(angle),
                    KeyTime.FromTimeSpan(TimeSpan.FromTicks((long)(duration.Ticks * progress)))));
        }

        return animation;
    }

    private static TransformGroup? GetMutableDnaTransforms(CachedShadowBorder? card)
    {
        if (card?.RenderTransform is not TransformGroup { Children.Count: >= 1 } transforms)
        {
            return null;
        }

        if (!transforms.IsFrozen && transforms.Children.All(transform => !transform.IsFrozen))
        {
            return transforms;
        }

        var mutableTransforms = transforms.CloneCurrentValue();
        card.RenderTransform = mutableTransforms;
        return mutableTransforms;
    }

    private void WatchLikeCard_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is not ContentControl activeCard)
        {
            return;
        }

        foreach (var card in GetWatchLikeCards())
        {
            var isActive = ReferenceEquals(card, activeCard);
            Panel.SetZIndex(card, isActive ? 3 : ReferenceEquals(card, WatchLikeCenterCard) ? 2 : 1);
            AnimateWatchLikeCard(
                card,
                isActive ? 0d : GetWatchLikeFoldAngle(card, activeCard),
                isActive ? 1.02d : 0.9d,
                isActive ? 1.02d : 0.94d,
                isActive ? GetWatchLikeActiveX(card) : 0d,
                isActive ? -4d : 5d,
                isActive ? 1d : 0.72d,
                isActive ? 1d : 0.48d,
                isActive ? 0d : 1d);
        }
    }

    private void WatchLikeTriptych_MouseLeave(object sender, MouseEventArgs e)
    {
        Panel.SetZIndex(WatchLikeLeftCard, 1);
        Panel.SetZIndex(WatchLikeCenterCard, 2);
        Panel.SetZIndex(WatchLikeRightCard, 1);
        AnimateWatchLikeCard(WatchLikeLeftCard, -5d, 0.9d, 0.94d, 0d, 4d, 0.92d, 0.72d, 1d);
        AnimateWatchLikeCard(WatchLikeCenterCard, 0d, 1d, 1d, 0d, -2d, 1d, 1d, 0d);
        AnimateWatchLikeCard(WatchLikeRightCard, 5d, 0.9d, 0.94d, 0d, 4d, 0.92d, 0.72d, 1d);
    }

    private IEnumerable<ContentControl> GetWatchLikeCards()
    {
        yield return WatchLikeLeftCard;
        yield return WatchLikeCenterCard;
        yield return WatchLikeRightCard;
    }

    private double GetWatchLikeActiveX(ContentControl card)
    {
        if (ReferenceEquals(card, WatchLikeLeftCard))
        {
            return 8d;
        }

        return ReferenceEquals(card, WatchLikeRightCard) ? -8d : 0d;
    }

    private double GetWatchLikeFoldAngle(ContentControl card, ContentControl activeCard)
    {
        if (ReferenceEquals(card, WatchLikeLeftCard))
        {
            return -6d;
        }

        if (ReferenceEquals(card, WatchLikeRightCard))
        {
            return 6d;
        }

        return ReferenceEquals(activeCard, WatchLikeLeftCard) ? 4d : -4d;
    }

    private void AnimateWatchLikeCard(
        ContentControl card,
        double skewAngleY,
        double scaleX,
        double scaleY,
        double translateX,
        double translateY,
        double opacity,
        double shadowOpacity,
        double shadeOpacity)
    {
        var targets = GetWatchLikeTransformTargets(card);
        if (targets is null)
        {
            return;
        }

        var (skewTransform, scaleTransform, translateTransform) = targets.Value;
        var duration = TimeSpan.FromMilliseconds(260);
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        skewTransform.BeginAnimation(
            SkewTransform.AngleYProperty,
            new DoubleAnimation(skewAngleY, duration) { EasingFunction = easing },
            HandoffBehavior.SnapshotAndReplace);
        scaleTransform.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            new DoubleAnimation(scaleX, duration) { EasingFunction = easing },
            HandoffBehavior.SnapshotAndReplace);
        scaleTransform.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            new DoubleAnimation(scaleY, duration) { EasingFunction = easing },
            HandoffBehavior.SnapshotAndReplace);
        translateTransform.BeginAnimation(
            TranslateTransform.XProperty,
            new DoubleAnimation(translateX, duration) { EasingFunction = easing },
            HandoffBehavior.SnapshotAndReplace);
        translateTransform.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation(translateY, duration) { EasingFunction = easing },
            HandoffBehavior.SnapshotAndReplace);
        card.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(opacity, duration) { EasingFunction = easing },
            HandoffBehavior.SnapshotAndReplace);

        var shadowCard = FindVisualChild<CachedShadowBorder>(card);
        shadowCard?.BeginAnimation(
            CachedShadowBorder.ShadowVisualOpacityProperty,
            new DoubleAnimation(shadowOpacity, duration) { EasingFunction = easing },
            HandoffBehavior.SnapshotAndReplace);

        card.ApplyTemplate();
        if (card.Template.FindName("PART_FoldShade", card) is Border foldShade)
        {
            foldShade.BeginAnimation(
                OpacityProperty,
                new DoubleAnimation(shadeOpacity, duration) { EasingFunction = easing },
                HandoffBehavior.SnapshotAndReplace);
        }
    }

    private (SkewTransform Skew, ScaleTransform Scale, TranslateTransform Translation)?
        GetWatchLikeTransformTargets(ContentControl card)
    {
        if (ReferenceEquals(card, WatchLikeLeftCard))
        {
            return (WatchLikeLeftSkew, WatchLikeLeftScale, WatchLikeLeftTranslation);
        }

        if (ReferenceEquals(card, WatchLikeCenterCard))
        {
            return (WatchLikeCenterSkew, WatchLikeCenterScale, WatchLikeCenterTranslation);
        }

        return ReferenceEquals(card, WatchLikeRightCard)
            ? (WatchLikeRightSkew, WatchLikeRightScale, WatchLikeRightTranslation)
            : null;
    }

    private static bool NeedsInternalScroll(ScrollViewer scrollViewer)
    {
        return scrollViewer.IsVisible
               && scrollViewer.ActualHeight > 0d
               && scrollViewer.ViewportHeight > 0d
               && scrollViewer.ComputedVerticalScrollBarVisibility == Visibility.Visible
               && scrollViewer.ExtentHeight > scrollViewer.ViewportHeight + 1d
               && scrollViewer.ScrollableHeight > 1d;
    }

    private static void ForwardMouseWheelToParent(ScrollViewer scrollViewer, MouseWheelEventArgs e)
    {
        var parent = FindVisualParent<UIElement>(scrollViewer);
        if (parent is null)
        {
            return;
        }

        e.Handled = true;
        var forwardedArgs = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = MouseWheelEvent,
            Source = scrollViewer
        };
        parent.RaiseEvent(forwardedArgs);
    }

    private static bool CanScrollVertically(ScrollViewer scrollViewer, int wheelDelta)
    {
        return wheelDelta < 0
            ? scrollViewer.VerticalOffset < scrollViewer.ScrollableHeight
            : scrollViewer.VerticalOffset > 0;
    }

    private static T? FindVisualParent<T>(DependencyObject element)
        where T : DependencyObject
    {
        var current = VisualTreeHelper.GetParent(element);
        while (current is not null)
        {
            if (current is T typedParent)
            {
                return typedParent;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static T? FindVisualChild<T>(DependencyObject element)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++)
        {
            var child = VisualTreeHelper.GetChild(element, index);
            if (child is T typedChild)
            {
                return typedChild;
            }

            var descendant = FindVisualChild<T>(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private readonly record struct VisualMetrics(
        int VisualCount,
        int EffectCount,
        int DropShadowCount,
        int AnimatedCount);

    private sealed record PreferenceBubbleState(
        double XRatio,
        double YRatio,
        double VelocityX,
        double VelocityY,
        double RadiusRatio);

    private sealed record BubbleFlowWave(
        Point Origin,
        Vector Direction,
        double Speed,
        long CreatedAt);

    private sealed class PreferenceBubbleParticle(
        Grid host,
        Border bubble,
        Ellipse depthShadow,
        Ellipse highlight,
        ScaleTransform scale,
        double x,
        double y,
        double baseRadius,
        double hoverRadius,
        double phase,
        string stateId)
    {
        public Grid Host { get; } = host;

        public Border Bubble { get; } = bubble;

        public Ellipse DepthShadow { get; } = depthShadow;

        public Ellipse Highlight { get; } = highlight;

        public ScaleTransform Scale { get; } = scale;

        public double X { get; set; } = x;

        public double Y { get; set; } = y;

        public double VelocityX { get; set; }

        public double VelocityY { get; set; }

        public double AccelerationX { get; set; }

        public double AccelerationY { get; set; }

        public double BaseRadius { get; } = baseRadius;

        public double HoverRadius { get; } = hoverRadius;

        public double Radius { get; set; } = baseRadius;

        public double TargetRadius { get; set; } = baseRadius;

        public double Mobility { get; } = Math.Clamp(Math.Pow(48d / Math.Max(baseRadius, 1d), 0.28d), 0.84d, 1.12d);

        public double Mass => 1d / Mobility;

        public double Phase { get; } = phase;

        public string StateId { get; } = stateId;

        public bool IsHovered { get; set; }
    }
}
