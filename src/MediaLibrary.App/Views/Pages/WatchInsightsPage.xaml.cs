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
    private const double BubbleRippleDistanceThreshold = 24d;
    private const double BubbleRippleMinimumIntervalMilliseconds = 64d;
    private const double BubbleSweepIntervalSeconds = 9.4d;
    private const double BubbleSweepDurationSeconds = 8.6d;
    private const string InitialLoadingNone = "none";
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
    private string _activeInitialLoadingTab = InitialLoadingNone;
    private long _initialLoadingStartedAt;
    private int _initialLoadingSlowFrameCount;
    private double _initialLoadingSlowFrameTotalMs;
    private double _initialLoadingSlowFrameMaxMs;
    private INotifyCollectionChanged? _bubbleCollectionChangedSource;
    private readonly List<PreferenceBubbleParticle> _preferenceBubbleParticles = [];
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
    private long _nextBubbleSweepTimestamp;
    private long _activeBubbleSweepStartedAt;
    private Point _activeBubbleSweepOrigin;
    private Vector _activeBubbleSweepDirection = new(1d, 0d);
    private double _activeBubbleSweepReach;
    private double _activeBubbleSweepInfluenceRadius;
    private double _activeBubbleSweepLength;
    private double _activeBubbleSweepAmplitude;
    private readonly Canvas _unrealizedBubbleCanvas = new();
    private Canvas? _bubbleCanvas;
    private ContentControl? _watchLikeLeftCard;
    private ContentControl? _watchLikeCenterCard;
    private ContentControl? _watchLikeRightCard;
    private string? _hoveredTasteGraphNodeId;
    private string? _selectedTasteGraphNodeId;
    private bool _isProfileDnaScrollableStateUpdateQueued;

    private Canvas BubbleCanvas
    {
        get
        {
            _bubbleCanvas = FindNamedVisual<Canvas>(this, "BubbleCanvas") ?? _bubbleCanvas;
            return _bubbleCanvas ?? _unrealizedBubbleCanvas;
        }
    }

    private ContentControl WatchLikeLeftCard =>
        _watchLikeLeftCard ??= FindRequiredNamedVisual<ContentControl>("WatchLikeLeftCard");

    private ContentControl WatchLikeCenterCard =>
        _watchLikeCenterCard ??= FindRequiredNamedVisual<ContentControl>("WatchLikeCenterCard");

    private ContentControl WatchLikeRightCard =>
        _watchLikeRightCard ??= FindRequiredNamedVisual<ContentControl>("WatchLikeRightCard");

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
        UpdateInitialLoadingDiagnostics("loaded");
        QueueApplyScrollOffset("loaded");
        QueueLayoutDiagnostics("loaded");
        QueuePreferenceBubbleRebuild();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        StoreCurrentScrollOffset();
        _hoveredTasteGraphNodeId = null;
        _selectedTasteGraphNodeId = null;
        DetachState();
        AttachState();
        UpdateInitialLoadingDiagnostics("data-context-changed");
        QueueApplyScrollOffset("data-context-changed");
        QueueLayoutDiagnostics("data-context-changed");
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StoreCurrentScrollOffset();
        _hoveredTasteGraphNodeId = null;
        _selectedTasteGraphNodeId = null;
        SetInitialLoadingDiagnosticsState(InitialLoadingNone, "unloaded");
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
            SetInitialLoadingDiagnosticsState(InitialLoadingNone, "hidden");
            StopFrameDiagnostics("hidden");
            _lastBubblePhysicsTimestamp = 0;
            _isBubbleCanvasInViewport = false;
            ResetBubbleSweepSchedule();
            return;
        }

        StartFrameDiagnostics();
        UpdateInitialLoadingDiagnostics("visible");
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
            UpdateInitialLoadingDiagnostics("tab-changed");
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

        if (e.PropertyName is nameof(WatchInsightsViewModel.IsLoadingProfile)
            or nameof(WatchInsightsViewModel.IsLoadingStatistics)
            or nameof(WatchInsightsViewModel.IsProfileInitialLoading)
            or nameof(WatchInsightsViewModel.IsStatisticsInitialLoading))
        {
            UpdateInitialLoadingDiagnostics(e.PropertyName);
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
        if (rippleDistance >= BubbleRippleDistanceThreshold
            && rippleElapsed >= TimeSpan.FromMilliseconds(BubbleRippleMinimumIntervalMilliseconds))
        {
            CreateBubbleRipple(position, _bubblePointerVelocity);
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

    private void CreateBubbleRipple(Point position, Vector pointerVelocity)
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
            width: 28d,
            height: 19d,
            maximumScaleX: 9d + speedRatio * 1.6d,
            maximumScaleY: 5.9d + speedRatio * 0.9d,
            initialOpacity: 0.62d,
            duration: TimeSpan.FromMilliseconds(1040d + speedRatio * 180d),
            beginTime: TimeSpan.Zero,
            strokeThickness: 1.5d);
    }

    private BubbleSweepSample? UpdateBubbleSweep(long now)
    {
        if (_activeBubbleSweepStartedAt > 0)
        {
            var activeElapsed = Stopwatch.GetElapsedTime(_activeBubbleSweepStartedAt, now).TotalSeconds;
            if (activeElapsed < BubbleSweepDurationSeconds)
            {
                return CreateBubbleSweepSample(activeElapsed / BubbleSweepDurationSeconds);
            }

            _activeBubbleSweepStartedAt = 0;
        }

        if (_nextBubbleSweepTimestamp == 0)
        {
            _nextBubbleSweepTimestamp = now + (long)((BubbleSweepIntervalSeconds * 0.72d) * Stopwatch.Frequency);
            return null;
        }

        if (now < _nextBubbleSweepTimestamp)
        {
            return null;
        }

        StartBubbleSweep(now);
        if (_activeBubbleSweepStartedAt == 0)
        {
            return null;
        }

        return CreateBubbleSweepSample(0d);
    }

    private void StartBubbleSweep(long now)
    {
        var canvasWidth = BubbleCanvas.ActualWidth;
        var canvasHeight = BubbleCanvas.ActualHeight;
        if (canvasWidth <= 0d || canvasHeight <= 0d)
        {
            return;
        }

        var placement = CreateBubbleSweepPlacement(canvasWidth, canvasHeight, Random.Shared.Next(8));
        var diagonal = Math.Sqrt((canvasWidth * canvasWidth) + (canvasHeight * canvasHeight));

        _activeBubbleSweepStartedAt = now;
        _activeBubbleSweepOrigin = placement.Origin;
        _activeBubbleSweepDirection = placement.Direction;
        _activeBubbleSweepReach = placement.TravelDistance;
        _activeBubbleSweepLength = placement.Length;
        _activeBubbleSweepInfluenceRadius = Math.Clamp(diagonal * 0.16d, 120d, 190d);
        _activeBubbleSweepAmplitude = Math.Clamp(diagonal * 0.018d, 10d, 18d);
        _nextBubbleSweepTimestamp = now + (long)(RandomBetween(Random.Shared, BubbleSweepIntervalSeconds * 0.9d, BubbleSweepIntervalSeconds * 1.25d) * Stopwatch.Frequency);
        CreateBubbleSweepVisual();
    }

    private BubbleSweepSample CreateBubbleSweepSample(double progress)
    {
        var normalizedProgress = Math.Clamp(progress, 0d, 1d);
        var travel = _activeBubbleSweepReach * normalizedProgress;
        var strength = 980d * Math.Pow(1d - normalizedProgress, 1.32d);
        return new BubbleSweepSample(
            _activeBubbleSweepOrigin,
            _activeBubbleSweepDirection,
            travel,
            _activeBubbleSweepReach,
            _activeBubbleSweepInfluenceRadius,
            normalizedProgress,
            strength,
            _activeBubbleSweepLength);
    }

    private void CreateBubbleSweepVisual()
    {
        if (_activeBubbleSweepReach <= 0d || _activeBubbleSweepLength <= 0d)
        {
            return;
        }

        var accent = ResolveBubbleRippleColor();
        var host = new Canvas
        {
            Width = BubbleCanvas.ActualWidth,
            Height = BubbleCanvas.ActualHeight,
            Opacity = 0d,
            IsHitTestVisible = false
        };
        var normal = NormalizeOrDefault(_activeBubbleSweepDirection, new Vector(1d, 0d));
        var tangent = new Vector(-normal.Y, normal.X);
        foreach (var (normalOffset, amplitudeScale, phase, thickness, alpha) in new[]
                 {
                     (-8d, 0.62d, 0.65d, 1.1d, (byte)62),
                     (0d, 1d, 0d, 2.05d, (byte)148),
                     (9d, 0.72d, 1.35d, 1.25d, (byte)84)
                 })
        {
            var stroke = new SolidColorBrush(Color.FromArgb(alpha, accent.R, accent.G, accent.B));
            stroke.Freeze();
            host.Children.Add(new System.Windows.Shapes.Path
            {
                Data = CreateBubbleSweepGeometry(
                    _activeBubbleSweepOrigin + (normal * normalOffset),
                    normal,
                    tangent,
                    _activeBubbleSweepLength,
                    _activeBubbleSweepAmplitude * amplitudeScale,
                    phase),
                Stroke = stroke,
                StrokeThickness = thickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                IsHitTestVisible = false
            });
        }

        Panel.SetZIndex(host, 3);
        BubbleCanvas.Children.Add(host);

        var translation = new TranslateTransform();
        host.RenderTransform = translation;
        var duration = TimeSpan.FromSeconds(BubbleSweepDurationSeconds);
        translation.BeginAnimation(
            TranslateTransform.XProperty,
            new DoubleAnimation(0d, normal.X * _activeBubbleSweepReach, duration));
        translation.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation(0d, normal.Y * _activeBubbleSweepReach, duration));

        var opacity = new DoubleAnimationUsingKeyFrames();
        opacity.KeyFrames.Add(new LinearDoubleKeyFrame(0d, KeyTime.FromPercent(0d)));
        opacity.KeyFrames.Add(new LinearDoubleKeyFrame(0.64d, KeyTime.FromPercent(0.10d)));
        opacity.KeyFrames.Add(new LinearDoubleKeyFrame(0.38d, KeyTime.FromPercent(0.54d)));
        opacity.KeyFrames.Add(new LinearDoubleKeyFrame(0.16d, KeyTime.FromPercent(0.82d)));
        opacity.KeyFrames.Add(new LinearDoubleKeyFrame(0d, KeyTime.FromPercent(1d)));
        opacity.Duration = duration;
        opacity.Completed += (_, _) => BubbleCanvas.Children.Remove(host);
        host.BeginAnimation(OpacityProperty, opacity);
    }

    private static BubbleSweepPlacement CreateBubbleSweepPlacement(double width, double height, int index)
    {
        var diagonal = Math.Sqrt((width * width) + (height * height));
        const double padding = 96d;
        return index switch
        {
            0 => new BubbleSweepPlacement(new Point(width / 2d, -padding), new Vector(0d, 1d), height + (padding * 2d), width + (padding * 2.4d)),
            1 => new BubbleSweepPlacement(new Point(width + padding, -padding), NormalizeOrDefault(new Vector(-1d, 1d), new Vector(-1d, 1d)), diagonal + (padding * 2.6d), diagonal + (padding * 2.8d)),
            2 => new BubbleSweepPlacement(new Point(width + padding, height / 2d), new Vector(-1d, 0d), width + (padding * 2d), height + (padding * 2.4d)),
            3 => new BubbleSweepPlacement(new Point(width + padding, height + padding), NormalizeOrDefault(new Vector(-1d, -1d), new Vector(-1d, -1d)), diagonal + (padding * 2.6d), diagonal + (padding * 2.8d)),
            4 => new BubbleSweepPlacement(new Point(width / 2d, height + padding), new Vector(0d, -1d), height + (padding * 2d), width + (padding * 2.4d)),
            5 => new BubbleSweepPlacement(new Point(-padding, height + padding), NormalizeOrDefault(new Vector(1d, -1d), new Vector(1d, -1d)), diagonal + (padding * 2.6d), diagonal + (padding * 2.8d)),
            6 => new BubbleSweepPlacement(new Point(-padding, height / 2d), new Vector(1d, 0d), width + (padding * 2d), height + (padding * 2.4d)),
            _ => new BubbleSweepPlacement(new Point(-padding, -padding), NormalizeOrDefault(new Vector(1d, 1d), new Vector(1d, 1d)), diagonal + (padding * 2.6d), diagonal + (padding * 2.8d))
        };
    }

    private static Geometry CreateBubbleSweepGeometry(
        Point center,
        Vector normal,
        Vector tangent,
        double length,
        double amplitude,
        double phase)
    {
        const int segmentCount = 72;
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            for (var index = 0; index <= segmentCount; index++)
            {
                var progress = index / (double)segmentCount;
                var local = (progress - 0.5d) * length;
                var wave = Math.Sin((progress * Math.PI * 9d) + phase) * amplitude;
                var point = center + (tangent * local) + (normal * wave);
                if (index == 0)
                {
                    context.BeginFigure(point, false, false);
                }
                else
                {
                    context.LineTo(point, true, false);
                }
            }
        }

        geometry.Freeze();
        return geometry;
    }

    private static Vector NormalizeOrDefault(Vector vector, Vector fallback)
    {
        if (vector.Length <= 0.0001d)
        {
            vector = fallback;
        }

        if (vector.Length <= 0.0001d)
        {
            return new Vector(1d, 0d);
        }

        vector.Normalize();
        return vector;
    }

    private static double RandomBetween(Random random, double minimum, double maximum)
    {
        return minimum + ((maximum - minimum) * random.NextDouble());
    }

    private void ResetBubbleSweepSchedule()
    {
        _nextBubbleSweepTimestamp = 0;
        _activeBubbleSweepStartedAt = 0;
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
        directionalStroke.GradientStops.Add(new GradientStop(Color.FromArgb(10, accentColor.R, accentColor.G, accentColor.B), 0d));
        directionalStroke.GradientStops.Add(new GradientStop(Color.FromArgb(34, accentColor.R, accentColor.G, accentColor.B), 0.34d));
        directionalStroke.GradientStops.Add(new GradientStop(Color.FromArgb(232, accentColor.R, accentColor.G, accentColor.B), 0.78d));
        directionalStroke.GradientStops.Add(new GradientStop(Color.FromArgb(166, accentColor.R, accentColor.G, accentColor.B), 1d));
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
        Panel.SetZIndex(ripple, 3);
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
        bubbleSurface.Children.Add(content);
        bubble.Child = bubbleSurface;
        host.Children.Add(depthShadow);
        host.Children.Add(bubble);

        var particle = new PreferenceBubbleParticle(
            host,
            bubble,
            depthShadow,
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
        Panel.SetZIndex(host, 1);
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
        Panel.SetZIndex(particle.Host, isHovered ? 4 : 1);
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

    private void ClearPreferenceBubbles()
    {
        SavePreferenceBubbleState();
        _lastBubblePhysicsTimestamp = 0;
        _isBubbleCanvasInViewport = false;
        _preferenceBubbleParticles.Clear();
        BubbleCanvas.Children.Clear();
        _preferenceBubbleStateKey = string.Empty;
        _bubbleStateCanvasWidth = 0d;
        _bubbleStateCanvasHeight = 0d;
        _isBubblePointerActive = false;
        _bubblePointerVelocity = default;
        _lastBubblePointerTimestamp = 0;
        _hasBubbleRippleAnchor = false;
        ResetBubbleSweepSchedule();
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
        if (IsInitialLoadingActive(out var activeTab))
        {
            WatchInsightsDiagnostics.Write(
                "layer=view event=layout-idle-skipped "
                + $"reason={reason} tab={activeTab} initialLoading=true");
            return;
        }

        var version = ++_layoutDiagnosticsVersion;
        var startedAt = Stopwatch.GetTimestamp();
        _ = Dispatcher.InvokeAsync(
            () => CaptureLayoutDiagnostics(version, reason, startedAt),
            DispatcherPriority.ContextIdle);
    }

    private bool IsInitialLoadingActive(out string activeTab)
    {
        activeTab = InitialLoadingNone;
        if (DataContext is not WatchInsightsViewModel viewModel)
        {
            return false;
        }

        if (viewModel.SelectedTabIndex == 1)
        {
            activeTab = "statistics";
            return viewModel.IsStatisticsInitialLoading;
        }

        activeTab = "profile";
        return viewModel.IsProfileInitialLoading;
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

    private void UpdateInitialLoadingDiagnostics(string reason)
    {
        if (!IsVisible || DataContext is not WatchInsightsViewModel viewModel)
        {
            SetInitialLoadingDiagnosticsState(InitialLoadingNone, reason);
            return;
        }

        var activeTab = viewModel.SelectedTabIndex == 1
            ? viewModel.IsStatisticsInitialLoading ? "statistics" : InitialLoadingNone
            : viewModel.IsProfileInitialLoading ? "profile" : InitialLoadingNone;
        SetInitialLoadingDiagnosticsState(activeTab, reason);
    }

    private void SetInitialLoadingDiagnosticsState(string activeTab, string reason)
    {
        if (string.Equals(_activeInitialLoadingTab, activeTab, StringComparison.Ordinal))
        {
            return;
        }

        if (!string.Equals(_activeInitialLoadingTab, InitialLoadingNone, StringComparison.Ordinal))
        {
            var elapsed = _initialLoadingStartedAt == 0
                ? TimeSpan.Zero
                : Stopwatch.GetElapsedTime(_initialLoadingStartedAt);
            var averageSlowMs = _initialLoadingSlowFrameCount == 0
                ? 0d
                : _initialLoadingSlowFrameTotalMs / _initialLoadingSlowFrameCount;
            WatchInsightsDiagnostics.Write(
                "layer=view event=initial-loading-hidden "
                + $"tab={_activeInitialLoadingTab} reason={reason} elapsedMs={elapsed.TotalMilliseconds:0} "
                + $"slowFrames={_initialLoadingSlowFrameCount} averageSlowMs={averageSlowMs:0} "
                + $"maxSlowMs={_initialLoadingSlowFrameMaxMs:0} renderTier={RenderCapability.Tier >> 16}");
            _initialLoadingSlowFrameCount = 0;
            _initialLoadingSlowFrameTotalMs = 0d;
            _initialLoadingSlowFrameMaxMs = 0d;
            _initialLoadingStartedAt = 0;
        }

        _activeInitialLoadingTab = activeTab;
        if (!string.Equals(_activeInitialLoadingTab, InitialLoadingNone, StringComparison.Ordinal))
        {
            _initialLoadingStartedAt = Stopwatch.GetTimestamp();
            _initialLoadingSlowFrameCount = 0;
            _initialLoadingSlowFrameTotalMs = 0d;
            _initialLoadingSlowFrameMaxMs = 0d;
            WatchInsightsDiagnostics.Write(
                "layer=view event=initial-loading-visible "
                + $"tab={_activeInitialLoadingTab} reason={reason} renderTier={RenderCapability.Tier >> 16}");
        }
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
                if (!string.Equals(_activeInitialLoadingTab, InitialLoadingNone, StringComparison.Ordinal))
                {
                    _initialLoadingSlowFrameCount++;
                    _initialLoadingSlowFrameTotalMs += frameMs;
                    _initialLoadingSlowFrameMaxMs = Math.Max(_initialLoadingSlowFrameMaxMs, frameMs);
                }

                if (frameMs >= 100d)
                {
                    var viewModel = DataContext as WatchInsightsViewModel;
                    WatchInsightsDiagnostics.Write(
                        "layer=view event=slow-frame "
                        + $"tab={(viewModel?.SelectedTabIndex == 1 ? "statistics" : "profile")} "
                        + $"initialLoading={_activeInitialLoadingTab} frameMs={frameMs:0}");
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

        var sweepWave = UpdateBubbleSweep(now);

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
                particle.AccelerationX += (edgeInset - particle.X) * 0.62d;
            }
            else if (particle.X > BubbleCanvas.ActualWidth - edgeInset)
            {
                particle.AccelerationX -= (particle.X - (BubbleCanvas.ActualWidth - edgeInset)) * 0.62d;
            }

            if (particle.Y < edgeInset)
            {
                particle.AccelerationY += (edgeInset - particle.Y) * 0.62d;
            }
            else if (particle.Y > BubbleCanvas.ActualHeight - edgeInset)
            {
                particle.AccelerationY -= (particle.Y - (BubbleCanvas.ActualHeight - edgeInset)) * 0.62d;
            }

            ApplyBubbleCornerEscape(particle, BubbleCanvas.ActualWidth, BubbleCanvas.ActualHeight, elapsedSeconds);

            var flowAngle = Math.Sin((particle.X * 0.006d) + (elapsedSeconds * 0.11d) + particle.Phase)
                            + Math.Cos((particle.Y * 0.007d) - (elapsedSeconds * 0.09d) + (particle.Phase * 1.83d));
            particle.AccelerationX += Math.Cos(flowAngle * Math.PI) * 17d;
            particle.AccelerationY += Math.Sin(flowAngle * Math.PI) * 17d;
            particle.AccelerationX += Math.Sin((elapsedSeconds * 0.17d) + (particle.Phase * 2.17d)) * 6d;
            particle.AccelerationY += Math.Cos((elapsedSeconds * 0.15d) + (particle.Phase * 2.63d)) * 6d;

            if (sweepWave is { } wave)
            {
                var normal = NormalizeOrDefault(wave.Direction, new Vector(1d, 0d));
                var tangent = new Vector(-normal.Y, normal.X);
                var frontCenter = wave.Origin + (normal * wave.Travel);
                var delta = new Vector(particle.X - frontCenter.X, particle.Y - frontCenter.Y);
                var normalDistance = (delta.X * normal.X) + (delta.Y * normal.Y);
                var tangentDistance = (delta.X * tangent.X) + (delta.Y * tangent.Y);
                var halfLength = wave.Length / 2d;

                if (Math.Abs(tangentDistance) <= halfLength + wave.InfluenceRadius)
                {
                    var normalInfluence = Math.Exp(
                        -(normalDistance * normalDistance)
                        / (2d * wave.InfluenceRadius * wave.InfluenceRadius));
                    var overflow = Math.Max(0d, Math.Abs(tangentDistance) - halfLength);
                    var lengthInfluence = Math.Exp(
                        -(overflow * overflow)
                        / (2d * wave.InfluenceRadius * wave.InfluenceRadius));
                    var fade = Math.Pow(1d - wave.Progress, 1.22d);
                    var wavePhase = (tangentDistance / Math.Max(42d, wave.Length / 9d))
                                    + (wave.Progress * Math.PI * 2.2d);
                    var pulse = 0.82d + (0.18d * Math.Sin(wavePhase));
                    var push = wave.Strength * normalInfluence * lengthInfluence * fade * pulse;
                    var side = wave.Strength
                               * 0.07d
                               * normalInfluence
                               * lengthInfluence
                               * fade
                               * Math.Cos(wavePhase);
                    particle.AccelerationX += (normal.X * push) + (tangent.X * side);
                    particle.AccelerationY += (normal.Y * push) + (tangent.Y * side);
                }
            }

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
                var alongRadius = Math.Max(320d, Math.Min(590d, canvasDiagonal * 0.62d));
                var sideRadius = Math.Max(185d, Math.Min(350d, canvasDiagonal * 0.37d));
                var directionalDistance = Math.Sqrt(
                    (along * along) / (alongRadius * alongRadius)
                    + (side * side) / (sideRadius * sideRadius));
                var directionalInfluence = Math.Exp(-2.1d * directionalDistance * directionalDistance);
                var nearInfluence = Math.Exp(-pointerDistanceSquared / (2d * 164d * 164d));
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

        for (var leftIndex = 0; leftIndex < _preferenceBubbleParticles.Count; leftIndex++)
        {
            var left = _preferenceBubbleParticles[leftIndex];
            for (var rightIndex = leftIndex + 1; rightIndex < _preferenceBubbleParticles.Count; rightIndex++)
            {
                var right = _preferenceBubbleParticles[rightIndex];
                var deltaX = right.X - left.X;
                var deltaY = right.Y - left.Y;
                var distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);
                var minimumDistance = left.Radius + right.Radius + 6d;
                var comfortDistance = minimumDistance + 38d;
                if (distanceSquared >= comfortDistance * comfortDistance)
                {
                    continue;
                }

                var distance = Math.Sqrt(Math.Max(distanceSquared, 0.0001d));
                var normalX = distance > 0.01d ? deltaX / distance : Math.Cos(left.Phase + right.Phase);
                var normalY = distance > 0.01d ? deltaY / distance : Math.Sin(left.Phase + right.Phase);
                var comfortOverlap = comfortDistance - distance;
                var separationForce = comfortOverlap * comfortOverlap / Math.Max(comfortDistance, 1d) * 2.75d;
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
                var correction = overlap * 0.98d;
                var leftCorrection = correction * left.Mobility / mobilityTotal;
                var rightCorrection = correction * right.Mobility / mobilityTotal;
                left.X -= normalX * leftCorrection;
                left.Y -= normalY * leftCorrection;
                right.X += normalX * rightCorrection;
                right.Y += normalY * rightCorrection;

                var collisionForce = overlap * 74d;
                left.AccelerationX -= normalX * collisionForce;
                left.AccelerationY -= normalY * collisionForce;
                right.AccelerationX += normalX * collisionForce;
                right.AccelerationY += normalY * collisionForce;

                var relativeNormalVelocity = ((right.VelocityX - left.VelocityX) * normalX)
                                             + ((right.VelocityY - left.VelocityY) * normalY);
                if (relativeNormalVelocity < 0d)
                {
                    var impulse = -relativeNormalVelocity * 0.72d;
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

        const double restitution = 0.68d;
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

    private void TasteGraphNode_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is Border { DataContext: TasteGraphNodeItem node })
        {
            _hoveredTasteGraphNodeId = node.Id;
            UpdateTasteGraphFocus();
        }
    }

    private void TasteGraphNode_MouseLeave(object sender, MouseEventArgs e)
    {
        _hoveredTasteGraphNodeId = null;
        UpdateTasteGraphFocus();
    }

    private void TasteGraphNode_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { DataContext: TasteGraphNodeItem node })
        {
            return;
        }

        _selectedTasteGraphNodeId = string.Equals(
            _selectedTasteGraphNodeId,
            node.Id,
            StringComparison.OrdinalIgnoreCase)
            ? null
            : node.Id;
        UpdateTasteGraphFocus();
        e.Handled = true;
    }

    private void UpdateTasteGraphFocus()
    {
        var activeNodeId = _hoveredTasteGraphNodeId ?? _selectedTasteGraphNodeId;
        foreach (var path in FindVisualChildren<Path>(this))
        {
            if ((!Equals(path.Tag, "TasteGraphLink") && !Equals(path.Tag, "TasteGraphLinkGlow"))
                || path.DataContext is not TasteGraphLinkItem link)
            {
                continue;
            }

            var isConnected = !string.IsNullOrEmpty(activeNodeId) && link.IsRelatedTo(activeNodeId);
            if (Equals(path.Tag, "TasteGraphLinkGlow"))
            {
                path.Opacity = isConnected ? 0.16d : 0d;
                continue;
            }

            path.Opacity = isConnected ? 1d : link.BaseOpacity;
            path.StrokeThickness = isConnected ? link.StrokeThickness + 0.5d : link.StrokeThickness;
        }

        foreach (var border in FindVisualChildren<Border>(this))
        {
            if (!Equals(border.Tag, "TasteGraphNode")
                || border.DataContext is not TasteGraphNodeItem node)
            {
                continue;
            }

            var isActive = !string.IsNullOrEmpty(activeNodeId)
                           && string.Equals(node.Id, activeNodeId, StringComparison.OrdinalIgnoreCase);
            border.BorderThickness = new Thickness(isActive ? 2d : 1.2d);
        }
    }

    private void WriteFrameSummary(string reason)
    {
        var viewModel = DataContext as WatchInsightsViewModel;
        var averageMs = _slowFrameCount == 0 ? 0d : _slowFrameTotalMs / _slowFrameCount;
        WatchInsightsDiagnostics.Write(
            "layer=view event=frame-summary "
            + $"reason={reason} tab={(viewModel?.SelectedTabIndex == 1 ? "statistics" : "profile")} "
            + $"initialLoading={_activeInitialLoadingTab} "
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
        QueueProfileDnaScrollableStateUpdate();
    }

    private void ProfileDnaDescriptionScrollViewer_Loaded(object sender, RoutedEventArgs e)
    {
        QueueProfileDnaScrollableStateUpdate();
    }

    private void ProfileDnaDescriptionScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        QueueProfileDnaScrollableStateUpdate();
    }

    private void QueueProfileDnaScrollableStateUpdate()
    {
        if (_isProfileDnaScrollableStateUpdateQueued)
        {
            return;
        }

        _isProfileDnaScrollableStateUpdateQueued = true;
        Dispatcher.BeginInvoke(
            () =>
            {
                _isProfileDnaScrollableStateUpdateQueued = false;
                UpdateProfileDnaScrollableState();
            },
            DispatcherPriority.Loaded);
    }

    private void UpdateProfileDnaScrollableState()
    {
        if (DataContext is not WatchInsightsViewModel viewModel)
        {
            return;
        }

        var descriptionScrollViewers = FindVisualChildren<ScrollViewer>(this)
            .Where(static scrollViewer => string.Equals(scrollViewer.Tag as string, "ProfileDnaDescription", StringComparison.Ordinal))
            .ToList();
        var hasScrollableDescription = descriptionScrollViewers.Any(NeedsInternalScroll);
        if (viewModel.IsProfileDnaTextScrollable != hasScrollableDescription)
        {
            viewModel.IsProfileDnaTextScrollable = hasScrollableDescription;
        }
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

        ApplyWatchLikeFocus(activeCard);
    }

    private void WatchLikeTriptych_MouseLeave(object sender, MouseEventArgs e)
    {
        ApplyWatchLikeFocus(WatchLikeCenterCard);
    }

    private void ApplyWatchLikeFocus(ContentControl activeCard)
    {
        if (ReferenceEquals(activeCard, WatchLikeLeftCard))
        {
            SetWatchLikeZOrder(left: 3, center: 2, right: 1);
            AnimateWatchLikeCard(WatchLikeLeftCard, 1.04d, 8d, -8d, 1d, 1d);
            AnimateWatchLikeCard(WatchLikeCenterCard, 0.98d, 3d, 8d, 0.74d, 0.66d);
            AnimateWatchLikeCard(WatchLikeRightCard, 0.95d, -1d, 13d, 0.62d, 0.34d);
            return;
        }

        if (ReferenceEquals(activeCard, WatchLikeRightCard))
        {
            SetWatchLikeZOrder(left: 1, center: 2, right: 3);
            AnimateWatchLikeCard(WatchLikeLeftCard, 0.95d, 1d, 13d, 0.62d, 0.34d);
            AnimateWatchLikeCard(WatchLikeCenterCard, 0.98d, -3d, 8d, 0.74d, 0.66d);
            AnimateWatchLikeCard(WatchLikeRightCard, 1.04d, -8d, -8d, 1d, 1d);
            return;
        }

        SetWatchLikeZOrder(left: 1, center: 3, right: 1);
        AnimateWatchLikeCard(WatchLikeLeftCard, 0.96d, -4d, 12d, 0.68d, 0.48d);
        AnimateWatchLikeCard(WatchLikeCenterCard, 1.04d, 0d, -6d, 1d, 1d);
        AnimateWatchLikeCard(WatchLikeRightCard, 0.96d, 4d, 12d, 0.68d, 0.48d);
    }

    private void SetWatchLikeZOrder(int left, int center, int right)
    {
        Panel.SetZIndex(WatchLikeLeftCard, left);
        Panel.SetZIndex(WatchLikeCenterCard, center);
        Panel.SetZIndex(WatchLikeRightCard, right);
    }

    private void AnimateWatchLikeCard(
        ContentControl card,
        double scale,
        double translateX,
        double translateY,
        double opacity,
        double shadowOpacity)
    {
        var targets = GetWatchLikeTransformTargets(card);
        if (targets is null)
        {
            return;
        }

        var (scaleTransform, translateTransform) = targets.Value;
        var duration = TimeSpan.FromMilliseconds(220);
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        scaleTransform.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            new DoubleAnimation(scale, duration) { EasingFunction = easing },
            HandoffBehavior.SnapshotAndReplace);
        scaleTransform.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            new DoubleAnimation(scale, duration) { EasingFunction = easing },
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
    }

    private (ScaleTransform Scale, TranslateTransform Translation)?
        GetWatchLikeTransformTargets(ContentControl card)
    {
        if (card.RenderTransform is not TransformGroup transforms)
        {
            return null;
        }

        var scale = transforms.Children.OfType<ScaleTransform>().FirstOrDefault();
        var translation = transforms.Children.OfType<TranslateTransform>().FirstOrDefault();
        return scale is not null && translation is not null
            ? (scale, translation)
            : null;
    }

    private T FindRequiredNamedVisual<T>(string name)
        where T : FrameworkElement
    {
        return FindNamedVisual<T>(this, name)
               ?? throw new InvalidOperationException($"Deferred Watch Insights element '{name}' is not materialized.");
    }

    private static T? FindNamedVisual<T>(DependencyObject root, string name)
        where T : FrameworkElement
    {
        if (root is T candidate && string.Equals(candidate.Name, name, StringComparison.Ordinal))
        {
            return candidate;
        }

        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < childCount; index++)
        {
            var match = FindNamedVisual<T>(VisualTreeHelper.GetChild(root, index), name);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
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

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject element)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++)
        {
            var child = VisualTreeHelper.GetChild(element, index);
            if (child is T typedChild)
            {
                yield return typedChild;
            }

            foreach (var descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private readonly record struct VisualMetrics(
        int VisualCount,
        int EffectCount,
        int DropShadowCount,
        int AnimatedCount);

    private readonly record struct BubbleSweepSample(
        Point Origin,
        Vector Direction,
        double Travel,
        double Reach,
        double InfluenceRadius,
        double Progress,
        double Strength,
        double Length);

    private readonly record struct BubbleSweepPlacement(
        Point Origin,
        Vector Direction,
        double TravelDistance,
        double Length);

    private sealed record PreferenceBubbleState(
        double XRatio,
        double YRatio,
        double VelocityX,
        double VelocityY,
        double RadiusRatio);

    private sealed class PreferenceBubbleParticle(
        Grid host,
        Border bubble,
        Ellipse depthShadow,
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
