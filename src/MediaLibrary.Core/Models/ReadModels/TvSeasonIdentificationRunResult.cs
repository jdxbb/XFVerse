namespace MediaLibrary.Core.Models.ReadModels;

public sealed class TvSeasonIdentificationRunResult
{
    private readonly HashSet<int> _handledMediaFileIds = [];

    public IdentificationRunResult Summary { get; } = new();

    public IReadOnlyCollection<int> HandledMediaFileIds => _handledMediaFileIds;

    public void AddHandledMediaFile(int mediaFileId)
    {
        if (mediaFileId > 0)
        {
            _handledMediaFileIds.Add(mediaFileId);
        }
    }

    public void AddHandledMediaFiles(IEnumerable<int> mediaFileIds)
    {
        foreach (var mediaFileId in mediaFileIds)
        {
            AddHandledMediaFile(mediaFileId);
        }
    }
}
