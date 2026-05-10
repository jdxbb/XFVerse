namespace MediaLibrary.Core.Models.ReadModels;

public sealed class VideoCachePlaybackLease : IDisposable
{
    private readonly Action _release;
    private readonly Action<string>? _releaseWithReason;
    private int _disposed;

    public VideoCachePlaybackLease(string localFilePath, Action release)
    {
        LocalFilePath = localFilePath;
        _release = release;
    }

    public VideoCachePlaybackLease(string localFilePath, Action<string> release)
    {
        LocalFilePath = localFilePath;
        _release = () => release("lease-release");
        _releaseWithReason = release;
    }

    public string LocalFilePath { get; }

    public void Dispose()
    {
        Dispose("lease-release");
    }

    public void Dispose(string reason)
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (_releaseWithReason is not null)
        {
            _releaseWithReason(reason);
            return;
        }

        _release();
    }
}
