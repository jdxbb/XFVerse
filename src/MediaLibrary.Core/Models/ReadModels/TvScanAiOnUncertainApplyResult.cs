namespace MediaLibrary.Core.Models.ReadModels;

public sealed class TvScanAiOnUncertainApplyResult
{
    private readonly HashSet<int> _affectedMediaFileIds = [];

    public int AppliedFiles { get; set; }

    public int SuccessfulBatchCount { get; set; }

    public int FailedBatchCount { get; set; }

    public int FailedRangeCount { get; set; }

    public int ParsedHints { get; set; }

    public int AppliedHints { get; set; }

    public int IgnoredHints { get; set; }

    public bool HasBatchFailure => FailedBatchCount > 0;

    public IReadOnlyCollection<int> AffectedMediaFileIds => _affectedMediaFileIds;

    public void AddAffectedMediaFiles(IEnumerable<int> mediaFileIds)
    {
        foreach (var mediaFileId in mediaFileIds)
        {
            if (mediaFileId > 0)
            {
                _affectedMediaFileIds.Add(mediaFileId);
            }
        }
    }
}
