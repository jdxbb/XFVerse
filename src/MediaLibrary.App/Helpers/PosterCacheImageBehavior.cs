using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MediaLibrary.App.Services;

namespace MediaLibrary.App.Helpers;

public static class PosterCacheImageBehavior
{
    private const int DefaultDecodePixelWidth = 240;
    private const int MaxMemoryCacheEntries = 768;
    public const string LoadStateLoading = "Loading";
    public const string LoadStateLoaded = "Loaded";
    public const string LoadStateEmpty = "Empty";
    public const string LoadStateFailed = "Failed";
    private static readonly ConcurrentDictionary<string, ImageSource> MemoryCache = new(StringComparer.Ordinal);
    private static readonly ConcurrentQueue<string> MemoryCacheOrder = new();
    private static readonly ConcurrentDictionary<string, Lazy<Task<PosterImageLoadResult>>> ImageLoadTasks = new(StringComparer.Ordinal);
    private static int MemoryCacheGeneration;

    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.RegisterAttached(
            "Source",
            typeof(string),
            typeof(PosterCacheImageBehavior),
            new PropertyMetadata(null, OnSourceChanged));

    public static readonly DependencyProperty DecodePixelWidthProperty =
        DependencyProperty.RegisterAttached(
            "DecodePixelWidth",
            typeof(int),
            typeof(PosterCacheImageBehavior),
            new PropertyMetadata(0));

    public static readonly DependencyProperty UseOriginalTmdbSizeProperty =
        DependencyProperty.RegisterAttached(
            "UseOriginalTmdbSize",
            typeof(bool),
            typeof(PosterCacheImageBehavior),
            new PropertyMetadata(false, OnImageRequestOptionChanged));

    public static readonly DependencyProperty PreferredTmdbImageSizeProperty =
        DependencyProperty.RegisterAttached(
            "PreferredTmdbImageSize",
            typeof(string),
            typeof(PosterCacheImageBehavior),
            new PropertyMetadata(string.Empty, OnImageRequestOptionChanged));

    public static readonly DependencyProperty LoadStateProperty =
        DependencyProperty.RegisterAttached(
            "LoadState",
            typeof(string),
            typeof(PosterCacheImageBehavior),
            new FrameworkPropertyMetadata(
                LoadStateLoading,
                FrameworkPropertyMetadataOptions.Inherits));

    private static readonly DependencyProperty RequestVersionProperty =
        DependencyProperty.RegisterAttached(
            "RequestVersion",
            typeof(int),
            typeof(PosterCacheImageBehavior),
            new PropertyMetadata(0));

    private static readonly DependencyProperty RequestCancellationProperty =
        DependencyProperty.RegisterAttached(
            "RequestCancellation",
            typeof(CancellationTokenSource),
            typeof(PosterCacheImageBehavior),
            new PropertyMetadata(null));

    private static readonly DependencyProperty SyncLoadStateOnLoadedProperty =
        DependencyProperty.RegisterAttached(
            "SyncLoadStateOnLoaded",
            typeof(bool),
            typeof(PosterCacheImageBehavior),
            new PropertyMetadata(false));

    public static string? GetSource(DependencyObject target)
    {
        return (string?)target.GetValue(SourceProperty);
    }

    public static void SetSource(DependencyObject target, string? value)
    {
        target.SetValue(SourceProperty, value);
    }

    public static int GetDecodePixelWidth(DependencyObject target)
    {
        return (int)target.GetValue(DecodePixelWidthProperty);
    }

    public static void SetDecodePixelWidth(DependencyObject target, int value)
    {
        target.SetValue(DecodePixelWidthProperty, value);
    }

    public static bool GetUseOriginalTmdbSize(DependencyObject target)
    {
        return (bool)target.GetValue(UseOriginalTmdbSizeProperty);
    }

    public static void SetUseOriginalTmdbSize(DependencyObject target, bool value)
    {
        target.SetValue(UseOriginalTmdbSizeProperty, value);
    }

    public static string GetPreferredTmdbImageSize(DependencyObject target)
    {
        return (string)target.GetValue(PreferredTmdbImageSizeProperty);
    }

    public static void SetPreferredTmdbImageSize(DependencyObject target, string value)
    {
        target.SetValue(PreferredTmdbImageSizeProperty, value);
    }

    public static string GetLoadState(DependencyObject target)
    {
        return (string)target.GetValue(LoadStateProperty);
    }

    public static void SetLoadState(DependencyObject target, string value)
    {
        target.SetValue(LoadStateProperty, value);
    }

    public static void ClearMemoryCache()
    {
        MemoryCache.Clear();
        ImageLoadTasks.Clear();
        Interlocked.Increment(ref MemoryCacheGeneration);
        PosterCacheDiagnostics.Write("memory-clear", "reason=poster-cache-clear");
        while (MemoryCacheOrder.TryDequeue(out _))
        {
        }
    }

    private static void OnSourceChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is not Image image)
        {
            return;
        }

        ApplySource(image, e.NewValue as string);
    }

    private static void OnImageRequestOptionChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is Image image)
        {
            ApplySource(image, GetSource(image));
        }
    }

    private static void ApplySource(Image image, string? requestedSource)
    {
        CancelPreviousRequest(image);
        var version = GetRequestVersion(image) + 1;
        SetRequestVersion(image, version);

        var source = NormalizeRequestSource(
            requestedSource,
            GetUseOriginalTmdbSize(image),
            GetPreferredTmdbImageSize(image));
        if (string.IsNullOrWhiteSpace(source))
        {
            PosterCacheDiagnostics.Write("source-empty", $"version={version}");
            image.SetCurrentValue(Image.SourceProperty, null);
            SetImageLoadState(image, LoadStateEmpty);
            return;
        }

        var decodePixelWidth = NormalizeDecodePixelWidth(GetDecodePixelWidth(image));
        if (TrySetMemoryCachedSource(image, source, decodePixelWidth))
        {
            SetImageLoadState(image, LoadStateLoaded);
            PosterCacheDiagnostics.Write(
                "memory-hit",
                $"source={PosterCacheDiagnostics.SourceId(source)} width={decodePixelWidth} version={version}");
            return;
        }

        PosterCacheDiagnostics.Write(
            "request-start",
            $"source={PosterCacheDiagnostics.SourceId(source)} width={decodePixelWidth} version={version}");
        image.SetCurrentValue(Image.SourceProperty, null);
        SetImageLoadState(image, LoadStateLoading);
        var cancellation = new CancellationTokenSource();
        SetRequestCancellation(image, cancellation);
        _ = ApplySourceAsync(image, source, version, decodePixelWidth, cancellation.Token);
    }

    private static async Task ApplySourceAsync(
        Image image,
        string source,
        int version,
        int decodePixelWidth,
        CancellationToken cancellationToken)
    {
        PosterImageLoadResult loadResult;
        try
        {
            loadResult = await GetOrCreateImageSourceAsync(source, decodePixelWidth, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch
        {
            if (!cancellationToken.IsCancellationRequested && GetRequestVersion(image) == version)
            {
                SetImageLoadState(image, LoadStateFailed);
            }

            return;
        }

        if (cancellationToken.IsCancellationRequested || GetRequestVersion(image) != version)
        {
            PosterCacheDiagnostics.Write(
                "request-stale",
                $"source={PosterCacheDiagnostics.SourceId(source)} width={decodePixelWidth} version={version} currentVersion={GetRequestVersion(image)}");
            return;
        }

        var imageSource = loadResult.ImageSource;
        if (imageSource is null)
        {
            PosterCacheDiagnostics.Write(
                "set-empty",
                $"source={PosterCacheDiagnostics.SourceId(source)} width={decodePixelWidth} result={loadResult.Kind}");
            SetImageLoadState(
                image,
                string.Equals(loadResult.Kind, "empty", StringComparison.Ordinal)
                    ? LoadStateEmpty
                    : LoadStateFailed);
            return;
        }

        PosterCacheDiagnostics.Write(
            "set-image",
            $"source={PosterCacheDiagnostics.SourceId(source)} width={decodePixelWidth} result={loadResult.Kind}");
        image.SetCurrentValue(Image.SourceProperty, imageSource);
        SetImageLoadState(image, LoadStateLoaded);
    }

    private static async Task<PosterImageLoadResult> GetOrCreateImageSourceAsync(
        string source,
        int decodePixelWidth,
        CancellationToken cancellationToken)
    {
        var key = BuildMemoryCacheKey(source, decodePixelWidth);
        if (MemoryCache.TryGetValue(key, out var cachedImageSource))
        {
            PosterCacheDiagnostics.Write(
                "memory-hit-late",
                $"source={PosterCacheDiagnostics.SourceId(source)} width={decodePixelWidth}");
            return PosterImageLoadResult.FromImage(cachedImageSource);
        }

        var generation = Volatile.Read(ref MemoryCacheGeneration);
        var created = false;
        var loader = ImageLoadTasks.GetOrAdd(
            key,
            _ =>
            {
                created = true;
                return new Lazy<Task<PosterImageLoadResult>>(
                    () => LoadImageSourceAsync(source, decodePixelWidth, generation, cancellationToken),
                    LazyThreadSafetyMode.ExecutionAndPublication);
            });
        PosterCacheDiagnostics.Write(
            created ? "load-task-create" : "load-task-join",
            $"source={PosterCacheDiagnostics.SourceId(source)} width={decodePixelWidth} generation={generation}");

        try
        {
            return await loader.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (loader.IsValueCreated && loader.Value.IsCompleted)
            {
                ImageLoadTasks.TryRemove(key, out _);
                PosterCacheDiagnostics.Write(
                    "load-task-remove",
                    $"source={PosterCacheDiagnostics.SourceId(source)} width={decodePixelWidth}");
            }
        }
    }

    private static async Task<PosterImageLoadResult> LoadImageSourceAsync(
        string requestedSource,
        int decodePixelWidth,
        int generation,
        CancellationToken cancellationToken)
    {
        string displaySource;
        var resolveStartedAt = Stopwatch.GetTimestamp();
        var resolveElapsed = TimeSpan.Zero;
        try
        {
            var posterCacheService = AppServiceProvider.GetRequiredService<IPosterCacheService>();
            displaySource = await Task.Run(
                    () => posterCacheService.GetCachedOrFallbackAsync(requestedSource, cancellationToken),
                    cancellationToken)
                .ConfigureAwait(false);
            resolveElapsed = Stopwatch.GetElapsedTime(resolveStartedAt);
        }
        catch (Exception exception)
        {
            resolveElapsed = Stopwatch.GetElapsedTime(resolveStartedAt);
            PosterCacheDiagnostics.Write(
                "resolve-error",
                $"source={PosterCacheDiagnostics.SourceId(requestedSource)} width={decodePixelWidth} resolveMs={resolveElapsed.TotalMilliseconds:0} error={exception.GetType().Name}");
            displaySource = requestedSource;
        }

        if (string.IsNullOrWhiteSpace(displaySource))
        {
            PosterCacheDiagnostics.Write(
                "resolve-empty",
                $"source={PosterCacheDiagnostics.SourceId(requestedSource)} width={decodePixelWidth} resolveMs={resolveElapsed.TotalMilliseconds:0}");
            return PosterImageLoadResult.Empty;
        }

        var uri = new Uri(displaySource, UriKind.RelativeOrAbsolute);
        if (!IsLocalFileSource(displaySource, uri))
        {
            PosterCacheDiagnostics.Write(
                "remote-fallback",
                $"source={PosterCacheDiagnostics.SourceId(requestedSource)} display={PosterCacheDiagnostics.SourceId(displaySource)} width={decodePixelWidth} resolveMs={resolveElapsed.TotalMilliseconds:0}");
            return PosterImageLoadResult.Failed("remote-fallback");
        }

        try
        {
            var decodeStartedAt = Stopwatch.GetTimestamp();
            var imageSource = await Task.Run(() => LoadLocalBitmap(uri, decodePixelWidth), cancellationToken)
                .ConfigureAwait(false);
            var decodeElapsed = Stopwatch.GetElapsedTime(decodeStartedAt);
            TryAddMemoryCachedSource(requestedSource, decodePixelWidth, imageSource, generation);
            PosterCacheDiagnostics.Write(
                "local-decode-success",
                $"source={PosterCacheDiagnostics.SourceId(requestedSource)} display={PosterCacheDiagnostics.SourceId(displaySource)} width={decodePixelWidth} resolveMs={resolveElapsed.TotalMilliseconds:0} decodeMs={decodeElapsed.TotalMilliseconds:0}");
            return PosterImageLoadResult.FromImage(imageSource);
        }
        catch (Exception exception)
        {
            PosterCacheDiagnostics.Write(
                "local-decode-error",
                $"source={PosterCacheDiagnostics.SourceId(requestedSource)} display={PosterCacheDiagnostics.SourceId(displaySource)} width={decodePixelWidth} resolveMs={resolveElapsed.TotalMilliseconds:0} error={exception.GetType().Name}");
            return PosterImageLoadResult.Failed("local-decode-error");
        }
    }

    private static ImageSource LoadLocalBitmap(Uri uri, int decodePixelWidth)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        if (decodePixelWidth > 0)
        {
            bitmap.DecodePixelWidth = decodePixelWidth;
        }

        bitmap.UriSource = uri;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static bool IsLocalFileSource(string source, Uri uri)
    {
        return uri.IsAbsoluteUri
            ? uri.IsFile
            : Path.IsPathRooted(source);
    }

    private static void SetImageLoadState(Image image, string state)
    {
        image.SetCurrentValue(LoadStateProperty, state);

        var parent = GetParent(image);
        if (parent is not null)
        {
            parent.SetCurrentValue(LoadStateProperty, state);
            SetDescendantControlLoadState(parent, state);
            return;
        }

        EnsureLoadStateSyncOnLoaded(image);
    }

    private static void EnsureLoadStateSyncOnLoaded(Image image)
    {
        if ((bool)image.GetValue(SyncLoadStateOnLoadedProperty))
        {
            return;
        }

        image.SetValue(SyncLoadStateOnLoadedProperty, true);
        image.Loaded += OnImageLoadedSyncLoadState;
    }

    private static void OnImageLoadedSyncLoadState(object sender, RoutedEventArgs e)
    {
        if (sender is not Image image)
        {
            return;
        }

        image.Loaded -= OnImageLoadedSyncLoadState;
        image.SetValue(SyncLoadStateOnLoadedProperty, false);
        SetImageLoadState(image, GetLoadState(image));
    }

    private static void SetDescendantControlLoadState(DependencyObject root, string state)
    {
        foreach (var descendant in EnumerateDescendants(root))
        {
            if (descendant is Control control)
            {
                control.SetCurrentValue(LoadStateProperty, state);
            }
        }
    }

    private static DependencyObject? GetParent(DependencyObject current)
    {
        return current is Visual
            ? VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current)
            : LogicalTreeHelper.GetParent(current);
    }

    private static IEnumerable<DependencyObject> EnumerateDescendants(DependencyObject root)
    {
        var seen = new HashSet<DependencyObject>();
        var queue = new Queue<DependencyObject>();
        EnqueueChildren(root, queue, seen);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            yield return current;
            EnqueueChildren(current, queue, seen);
        }
    }

    private static void EnqueueChildren(
        DependencyObject root,
        Queue<DependencyObject> queue,
        ISet<DependencyObject> seen)
    {
        if (root is Visual)
        {
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                Enqueue(VisualTreeHelper.GetChild(root, index), queue, seen);
            }
        }

        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            Enqueue(child, queue, seen);
        }
    }

    private static void Enqueue(
        DependencyObject child,
        Queue<DependencyObject> queue,
        ISet<DependencyObject> seen)
    {
        if (seen.Add(child))
        {
            queue.Enqueue(child);
        }
    }

    private static bool TrySetMemoryCachedSource(Image image, string source, int decodePixelWidth)
    {
        if (!MemoryCache.TryGetValue(BuildMemoryCacheKey(source, decodePixelWidth), out var imageSource))
        {
            return false;
        }

        image.SetCurrentValue(Image.SourceProperty, imageSource);
        return true;
    }

    private static void TryAddMemoryCachedSource(
        string source,
        int decodePixelWidth,
        ImageSource imageSource,
        int generation)
    {
        if (!imageSource.IsFrozen || generation != Volatile.Read(ref MemoryCacheGeneration))
        {
            PosterCacheDiagnostics.Write(
                "memory-skip",
                $"source={PosterCacheDiagnostics.SourceId(source)} width={decodePixelWidth} frozen={imageSource.IsFrozen} generation={generation} currentGeneration={Volatile.Read(ref MemoryCacheGeneration)}");
            return;
        }

        var key = BuildMemoryCacheKey(source, decodePixelWidth);
        if (MemoryCache.TryAdd(key, imageSource))
        {
            MemoryCacheOrder.Enqueue(key);
            TrimMemoryCache();
            PosterCacheDiagnostics.Write(
                "memory-add",
                $"source={PosterCacheDiagnostics.SourceId(source)} width={decodePixelWidth} count={MemoryCache.Count}");
        }
    }

    private static void TrimMemoryCache()
    {
        while (MemoryCache.Count > MaxMemoryCacheEntries && MemoryCacheOrder.TryDequeue(out var oldestKey))
        {
            MemoryCache.TryRemove(oldestKey, out _);
        }
    }

    private static string BuildMemoryCacheKey(string source, int decodePixelWidth)
    {
        return $"{decodePixelWidth}:{source.Trim()}";
    }

    private static int NormalizeDecodePixelWidth(int decodePixelWidth)
    {
        return decodePixelWidth > 0 ? decodePixelWidth : DefaultDecodePixelWidth;
    }

    private static string? NormalizeRequestSource(
        string? source,
        bool useOriginalTmdbSize,
        string? preferredTmdbImageSize)
    {
        if ((!useOriginalTmdbSize && string.IsNullOrWhiteSpace(preferredTmdbImageSize))
            || string.IsNullOrWhiteSpace(source))
        {
            return source;
        }

        var trimmed = source.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Host, "image.tmdb.org", StringComparison.OrdinalIgnoreCase))
        {
            return source;
        }

        const string tmdbImagePrefix = "/t/p/";
        var path = uri.AbsolutePath;
        if (!path.StartsWith(tmdbImagePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return source;
        }

        var sizeStart = tmdbImagePrefix.Length;
        var sizeEnd = path.IndexOf('/', sizeStart);
        if (sizeEnd <= sizeStart)
        {
            return source;
        }

        var sizeSegment = path[sizeStart..sizeEnd];
        if (!IsTmdbResizableImageSize(sizeSegment)
            && !string.Equals(sizeSegment, "original", StringComparison.OrdinalIgnoreCase))
        {
            return source;
        }

        var targetSizeSegment = NormalizePreferredTmdbImageSize(preferredTmdbImageSize);
        if (string.IsNullOrWhiteSpace(targetSizeSegment))
        {
            targetSizeSegment = useOriginalTmdbSize ? "original" : sizeSegment;
        }

        if (string.Equals(sizeSegment, targetSizeSegment, StringComparison.OrdinalIgnoreCase))
        {
            return source;
        }

        var builder = new UriBuilder(uri)
        {
            Path = $"{path[..sizeStart]}{targetSizeSegment}{path[sizeEnd..]}"
        };
        return builder.Uri.AbsoluteUri;
    }

    private static string NormalizePreferredTmdbImageSize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return string.Equals(trimmed, "original", StringComparison.OrdinalIgnoreCase) || IsTmdbResizableImageSize(trimmed)
            ? trimmed
            : string.Empty;
    }

    private static bool IsTmdbResizableImageSize(string value)
    {
        if (value.Length < 2 || value[0] is not ('w' or 'h'))
        {
            return false;
        }

        for (var index = 1; index < value.Length; index++)
        {
            if (!char.IsDigit(value[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static void CancelPreviousRequest(Image image)
    {
        var cancellation = GetRequestCancellation(image);
        if (cancellation is null)
        {
            return;
        }

        cancellation.Cancel();
        cancellation.Dispose();
        SetRequestCancellation(image, null);
        PosterCacheDiagnostics.Write("request-cancel", "reason=source-changed-or-virtualized");
    }

    private static int GetRequestVersion(DependencyObject target)
    {
        return (int)target.GetValue(RequestVersionProperty);
    }

    private static void SetRequestVersion(DependencyObject target, int value)
    {
        target.SetValue(RequestVersionProperty, value);
    }

    private static CancellationTokenSource? GetRequestCancellation(DependencyObject target)
    {
        return (CancellationTokenSource?)target.GetValue(RequestCancellationProperty);
    }

    private static void SetRequestCancellation(DependencyObject target, CancellationTokenSource? value)
    {
        target.SetValue(RequestCancellationProperty, value);
    }

    private readonly record struct PosterImageLoadResult(ImageSource? ImageSource, string Kind)
    {
        public static PosterImageLoadResult Empty => new(null, "empty");

        public static PosterImageLoadResult FromImage(ImageSource imageSource)
        {
            return new PosterImageLoadResult(imageSource, "local");
        }

        public static PosterImageLoadResult Failed(string kind)
        {
            return new PosterImageLoadResult(null, kind);
        }
    }
}
