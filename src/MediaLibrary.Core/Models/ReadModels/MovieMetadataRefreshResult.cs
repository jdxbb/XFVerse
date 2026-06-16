namespace MediaLibrary.Core.Models.ReadModels;

public sealed record MovieMetadataRefreshResult(
    bool Success,
    bool HasChanges,
    bool SkippedByCooldown,
    int MovieId,
    int? TmdbId,
    string Status)
{
    public static MovieMetadataRefreshResult Failed(int movieId, int? tmdbId, string status)
    {
        return new MovieMetadataRefreshResult(false, false, false, movieId, tmdbId, status);
    }

    public static MovieMetadataRefreshResult Cooldown(int movieId, int? tmdbId)
    {
        return new MovieMetadataRefreshResult(false, false, true, movieId, tmdbId, "cooldown");
    }

    public static MovieMetadataRefreshResult Succeeded(int movieId, int? tmdbId, bool hasChanges)
    {
        return new MovieMetadataRefreshResult(true, hasChanges, false, movieId, tmdbId, "success");
    }
}
