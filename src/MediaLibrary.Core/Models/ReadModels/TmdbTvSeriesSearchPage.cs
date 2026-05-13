namespace MediaLibrary.Core.Models.ReadModels;

public sealed class TmdbTvSeriesSearchPage
{
    public IReadOnlyList<TmdbTvSeriesSearchItem> Results { get; set; } = [];

    public int Page { get; set; }

    public int TotalPages { get; set; }

    public int TotalResults { get; set; }

    public string ResultMessage { get; set; } = string.Empty;
}
