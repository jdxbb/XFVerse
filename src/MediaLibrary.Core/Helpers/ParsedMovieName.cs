namespace MediaLibrary.Core.Helpers;

public sealed class ParsedMovieName
{
    public string CleanTitle { get; set; } = string.Empty;

    public int? ReleaseYear { get; set; }

    public IReadOnlyList<string> RemovedNoiseCategories { get; set; } = [];
}
