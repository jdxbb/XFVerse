namespace MediaLibrary.Core.Models.ReadModels;

public sealed class TvScanAiOnUncertainApplyResult
{
    private readonly HashSet<int> _affectedMediaFileIds = [];

    public int AppliedFiles { get; set; }

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
