namespace MediaLibrary.Core.Models.Enums;

public enum WebDavRangeDownloadStatus
{
    Success = 0,
    RangeNotSupported = 1,
    Unauthorized = 2,
    RangeNotSatisfiable = 3,
    Failed = 4
}
