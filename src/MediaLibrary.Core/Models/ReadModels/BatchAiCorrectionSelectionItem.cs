using MediaLibrary.Core.Models.Enums;

namespace MediaLibrary.Core.Models.ReadModels;

public sealed class BatchAiCorrectionSelectionItem
{
    public string SelectionKey { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string SeriesTitle { get; set; } = string.Empty;

    public LibraryMediaItemKind ItemKind { get; set; }

    public int MovieId { get; set; }

    public int SeriesId { get; set; }

    public int SeasonId { get; set; }

    public int OrphanMediaFileId { get; set; }

    public IReadOnlyList<int> GroupedRangeMediaFileIds { get; set; } = [];

    public bool IsInLibrary { get; set; }

    public bool HasActiveSource { get; set; }
}
