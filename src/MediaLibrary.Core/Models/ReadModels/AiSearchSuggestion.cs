namespace MediaLibrary.Core.Models.ReadModels;

public sealed class AiSearchSuggestion
{
    public string Query { get; set; } = string.Empty;

    public int? ReleaseYear { get; set; }

    public int? SeasonNumber { get; set; }

    public int? EpisodeNumber { get; set; }
}
