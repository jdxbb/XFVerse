namespace MediaLibrary.Core.Models.Enums;

public enum VideoCacheStatus
{
    NotCached = 0,
    Downloading = 1,
    Cached = 2,
    Failed = 3,
    Canceled = 4,
    NotCacheable = 5,
    InUse = 6
}
