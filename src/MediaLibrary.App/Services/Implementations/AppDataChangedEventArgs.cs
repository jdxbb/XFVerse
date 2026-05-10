namespace MediaLibrary.App.Services.Implementations;

public enum AppDataChangeReason
{
    LibraryChanged = 0,
    PlaybackHistoryChanged = 1,
    CollectionChanged = 2,
    RecommendationChanged = 3,
    ScanChanged = 4,
    MetadataChanged = 5,
    SettingsChanged = 6
}

public sealed class AppDataChangedEventArgs : EventArgs
{
    public AppDataChangedEventArgs(bool libraryChanged, bool playbackChanged)
        : this(
            libraryChanged,
            playbackChanged,
            libraryChanged ? AppDataChangeReason.LibraryChanged : AppDataChangeReason.PlaybackHistoryChanged)
    {
    }

    public AppDataChangedEventArgs(
        bool libraryChanged,
        bool playbackChanged,
        AppDataChangeReason reason)
    {
        LibraryChanged = libraryChanged;
        PlaybackChanged = playbackChanged;
        Reason = reason;
    }

    public bool LibraryChanged { get; }

    public bool PlaybackChanged { get; }

    public AppDataChangeReason Reason { get; }
}
