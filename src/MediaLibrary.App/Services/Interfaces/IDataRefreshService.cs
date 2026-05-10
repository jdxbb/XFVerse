using MediaLibrary.App.Services.Implementations;

namespace MediaLibrary.App.Services.Interfaces;

public interface IDataRefreshService
{
    event EventHandler<AppDataChangedEventArgs>? DataChanged;

    void NotifyLibraryChanged();

    void NotifyPlaybackChanged();

    void NotifyCollectionChanged();

    void NotifyRecommendationChanged();

    void NotifyScanChanged();

    void NotifyMetadataChanged();
}
