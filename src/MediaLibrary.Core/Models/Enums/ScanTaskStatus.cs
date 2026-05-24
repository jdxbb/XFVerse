namespace MediaLibrary.Core.Models.Enums;

public enum ScanTaskStatus
{
    Pending = 1,
    Running = 2,
    Success = 3,
    Failed = 4,
    PartialSuccess = 5,
    Cancelled = 6
}
