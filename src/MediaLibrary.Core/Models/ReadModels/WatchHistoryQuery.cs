namespace MediaLibrary.Core.Models.ReadModels;

public sealed class WatchHistoryQuery
{
    public DateTime? StartedAtUtc { get; set; }

    public DateTime? EndedBeforeUtc { get; set; }

    public int Take { get; set; } = 100;
}
