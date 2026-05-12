namespace MediaLibrary.Core.Models.ReadModels;

public sealed class TmdbPersonSearchItem
{
    public int TmdbId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string OriginalName { get; set; } = string.Empty;

    public double Popularity { get; set; }
}
