using MediaLibrary.App.Services.Interfaces;

namespace MediaLibrary.App.Services.Implementations;

public sealed class DataRefreshService : IDataRefreshService
{
    public event EventHandler<AppDataChangedEventArgs>? DataChanged;

    public void NotifyLibraryChanged()
    {
        Notify(AppDataChangeReason.LibraryChanged, libraryChanged: true, playbackChanged: false);
    }

    public void NotifyPlaybackChanged()
    {
        Notify(AppDataChangeReason.PlaybackHistoryChanged, libraryChanged: false, playbackChanged: true);
    }

    public void NotifyCollectionChanged()
    {
        Notify(AppDataChangeReason.CollectionChanged, libraryChanged: false, playbackChanged: false);
    }

    public void NotifyRecommendationChanged()
    {
        Notify(AppDataChangeReason.RecommendationChanged, libraryChanged: false, playbackChanged: false);
    }

    public void NotifyScanChanged()
    {
        Notify(AppDataChangeReason.ScanChanged, libraryChanged: true, playbackChanged: false);
    }

    public void NotifyMetadataChanged()
    {
        Notify(AppDataChangeReason.MetadataChanged, libraryChanged: true, playbackChanged: false);
    }

    private void Notify(AppDataChangeReason reason, bool libraryChanged, bool playbackChanged)
    {
        var args = new AppDataChangedEventArgs(libraryChanged, playbackChanged, reason);
        var handlers = DataChanged?.GetInvocationList();
        if (handlers is null)
        {
            return;
        }

        foreach (EventHandler<AppDataChangedEventArgs> handler in handlers)
        {
            try
            {
                handler(this, args);
            }
            catch
            {
                // Data refresh notifications are best-effort and must not crash the shell.
            }
        }
    }
}
