namespace MediaLibrary.App.Services;

public interface IPosterCacheService
{
    Task<string> GetCachedOrFallbackAsync(
        string? source,
        CancellationToken cancellationToken = default);

    Task<string> RefreshAsync(
        string source,
        CancellationToken cancellationToken = default);
}
