using System.Collections.Concurrent;
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

        CancelPreviousRequest(image);
        var version = GetRequestVersion(image) + 1;
        SetRequestVersion(image, version);

        var source = e.NewValue as string;
        if (string.IsNullOrWhiteSpace(source))
        {
            PosterCacheDiagnostics.Write("source-empty", $"version={version}");
            image.SetCurrentValue(Image.SourceProperty, null);
            return;
        }

        var decodePixelWidth = NormalizeDecodePixelWidth(GetDecodePixelWidth(image));
        if (TrySetMemoryCachedSource(image, source, decodePixelWidth))
        {
            PosterCacheDiagnostics.Write(
                "memory-hit",
                $"source={PosterCacheDiagnostics.SourceId(source)} width={decodePixelWidth} version={version}");
            return;
        }

        PosterCacheDiagnostics.Write(
            "request-start",
            $"source={PosterCacheDiagnostics.SourceId(source)} width={decodePixelWidth} version={version}");
        image.SetCurrentValue(Image.SourceProperty, null);
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
            loadResult = await GetOrCreateImageSourceAsync(source, decodePixelWidth);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested || GetRequestVersion(image) != version)
        {
            PosterCacheDiagnostics.Write(
                "request-stale",
                $"source={PosterCacheDiagnostics.SourceId(source)} width={decodePixelWidth} version={version} currentVersion={GetRequestVersion(image)}");
            return;
        }

        var imageSource = loadResult.ImageSource
            ?? CreateRemoteFallbackImageSource(loadResult.RemoteFallbackSource, decodePixelWidth);
        if (imageSource is null)
        {
            PosterCacheDiagnostics.Write(
                "set-empty",
                $"source={PosterCacheDiagnostics.SourceId(source)} width={decodePixelWidth} result={loadResult.Kind}");
            return;
        }

        PosterCacheDiagnostics.Write(
            "set-image",
            $"source={PosterCacheDiagnostics.SourceId(source)} width={decodePixelWidth} result={loadResult.Kind}");
        image.SetCurrentValue(Image.SourceProperty, imageSource);
    }

    private static async Task<PosterImageLoadResult> GetOrCreateImageSourceAsync(string source, int decodePixelWidth)
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
                    () => LoadImageSourceAsync(source, decodePixelWidth, generation),
                    LazyThreadSafetyMode.ExecutionAndPublication);
            });
        PosterCacheDiagnostics.Write(
            created ? "load-task-create" : "load-task-join",
            $"source={PosterCacheDiagnostics.SourceId(source)} width={decodePixelWidth} generation={generation}");

        try
        {
            return await loader.Value.ConfigureAwait(false);
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
        int generation)
    {
        string displaySource;
        try
        {
            var posterCacheService = AppServiceProvider.GetRequiredService<IPosterCacheService>();
            displaySource = await Task.Run(
                    () => posterCacheService.GetCachedOrFallbackAsync(requestedSource, CancellationToken.None),
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            PosterCacheDiagnostics.Write(
                "resolve-error",
                $"source={PosterCacheDiagnostics.SourceId(requestedSource)} width={decodePixelWidth} error={exception.GetType().Name}");
            displaySource = requestedSource;
        }

        if (string.IsNullOrWhiteSpace(displaySource))
        {
            PosterCacheDiagnostics.Write(
                "resolve-empty",
                $"source={PosterCacheDiagnostics.SourceId(requestedSource)} width={decodePixelWidth}");
            return PosterImageLoadResult.Empty;
        }

        var uri = new Uri(displaySource, UriKind.RelativeOrAbsolute);
        if (!IsLocalFileSource(displaySource, uri))
        {
            PosterCacheDiagnostics.Write(
                "remote-fallback",
                $"source={PosterCacheDiagnostics.SourceId(requestedSource)} display={PosterCacheDiagnostics.SourceId(displaySource)} width={decodePixelWidth}");
            return PosterImageLoadResult.FromRemoteFallback(displaySource);
        }

        try
        {
            var imageSource = await Task.Run(() => LoadLocalBitmap(uri, decodePixelWidth), CancellationToken.None)
                .ConfigureAwait(false);
            TryAddMemoryCachedSource(requestedSource, decodePixelWidth, imageSource, generation);
            PosterCacheDiagnostics.Write(
                "local-decode-success",
                $"source={PosterCacheDiagnostics.SourceId(requestedSource)} display={PosterCacheDiagnostics.SourceId(displaySource)} width={decodePixelWidth}");
            return PosterImageLoadResult.FromImage(imageSource);
        }
        catch (Exception exception)
        {
            PosterCacheDiagnostics.Write(
                "local-decode-error",
                $"source={PosterCacheDiagnostics.SourceId(requestedSource)} display={PosterCacheDiagnostics.SourceId(displaySource)} width={decodePixelWidth} error={exception.GetType().Name}");
            return PosterImageLoadResult.FromRemoteFallback(requestedSource);
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

    private static ImageSource? CreateRemoteFallbackImageSource(string? source, int decodePixelWidth)
    {
        if (string.IsNullOrWhiteSpace(source)
            || !Uri.TryCreate(source, UriKind.RelativeOrAbsolute, out var uri)
            || IsLocalFileSource(source, uri))
        {
            return null;
        }

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        if (decodePixelWidth > 0)
        {
            bitmap.DecodePixelWidth = decodePixelWidth;
        }

        bitmap.UriSource = uri;
        bitmap.EndInit();
        return bitmap;
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

    private readonly record struct PosterImageLoadResult(ImageSource? ImageSource, string? RemoteFallbackSource, string Kind)
    {
        public static PosterImageLoadResult Empty => new(null, null, "empty");

        public static PosterImageLoadResult FromImage(ImageSource imageSource)
        {
            return new PosterImageLoadResult(imageSource, null, "local");
        }

        public static PosterImageLoadResult FromRemoteFallback(string? source)
        {
            return new PosterImageLoadResult(null, source, "remote-fallback");
        }
    }
}
