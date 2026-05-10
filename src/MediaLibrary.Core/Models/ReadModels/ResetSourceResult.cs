namespace MediaLibrary.Core.Models.ReadModels;

public sealed class ResetSourceResult
{
    public int OriginalMovieId { get; set; }

    public int PlaceholderMovieId { get; set; }

    public int RemainingLibrarySourceCount { get; set; }

    public int DetailMovieId { get; set; }

    public bool ShouldNavigateToPlaceholder => RemainingLibrarySourceCount == 0;
}
