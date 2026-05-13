namespace MediaLibrary.Core.Models.ReadModels;

public sealed class TmdbTvSeriesExternalIdsResult
{
    public int TmdbId { get; set; }

    public string ImdbId { get; set; } = string.Empty;

    public int? TvdbId { get; set; }

    public string WikidataId { get; set; } = string.Empty;

    public string FacebookId { get; set; } = string.Empty;

    public string InstagramId { get; set; } = string.Empty;

    public string TwitterId { get; set; } = string.Empty;
}
