using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MediaLibrary.App.Services;

namespace MediaLibrary.App.Helpers;

public static class PosterCacheImageBehavior
{
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
            image.SetCurrentValue(Image.SourceProperty, null);
            return;
        }

        image.SetCurrentValue(Image.SourceProperty, null);
        var cancellation = new CancellationTokenSource();
        SetRequestCancellation(image, cancellation);
        _ = ApplySourceAsync(image, source, version, GetDecodePixelWidth(image), cancellation.Token);
    }

    private static async Task ApplySourceAsync(
        Image image,
        string source,
        int version,
        int decodePixelWidth,
        CancellationToken cancellationToken)
    {
        string displaySource;
        try
        {
            var posterCacheService = AppServiceProvider.GetRequiredService<IPosterCacheService>();
            displaySource = await posterCacheService.GetCachedOrFallbackAsync(source, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch
        {
            displaySource = source;
        }

        if (cancellationToken.IsCancellationRequested || GetRequestVersion(image) != version)
        {
            return;
        }

        await SetImageSourceAsync(image, displaySource, decodePixelWidth, version, cancellationToken);
    }

    private static async Task SetImageSourceAsync(
        Image image,
        string source,
        int decodePixelWidth,
        int version,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            image.SetCurrentValue(Image.SourceProperty, null);
            return;
        }

        try
        {
            var uri = new Uri(source, UriKind.RelativeOrAbsolute);
            var imageSource = IsLocalFileSource(source, uri)
                ? await Task.Run(() => LoadLocalBitmap(uri, decodePixelWidth), cancellationToken)
                : new BitmapImage(uri);
            if (cancellationToken.IsCancellationRequested || GetRequestVersion(image) != version)
            {
                return;
            }

            image.SetCurrentValue(Image.SourceProperty, imageSource);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            image.SetCurrentValue(Image.SourceProperty, null);
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
}
