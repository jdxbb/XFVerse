namespace MediaLibrary.Core.Models.ReadModels;

public sealed class MoviePlaceholderGroupingRunResult
{
    public int CandidateFiles { get; set; }

    public int PersistedRanges { get; set; }

    public int PersistedFiles { get; set; }

    public int HiddenPlaceholderSkippedCount { get; set; }

    public string SkippedReasons { get; set; } = string.Empty;

    public static MoviePlaceholderGroupingRunResult Empty { get; } = new();
}
