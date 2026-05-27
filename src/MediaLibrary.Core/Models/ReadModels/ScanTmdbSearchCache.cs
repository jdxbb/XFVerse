namespace MediaLibrary.Core.Models.ReadModels;

public sealed class ScanTmdbSearchCache
{
    private readonly Dictionary<string, TmdbTvSeriesSearchPage> _tvSearchResults = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<MetadataSearchCandidate>> _movieSearchResults = new(StringComparer.Ordinal);

    public int TvSearchCacheHits { get; private set; }

    public int TvSearchCacheMisses { get; private set; }

    public int MovieSearchCacheHits { get; private set; }

    public int MovieSearchCacheMisses { get; private set; }

    public int TvSearchCacheEntries => _tvSearchResults.Count;

    public int MovieSearchCacheEntries => _movieSearchResults.Count;

    public int DuplicateSearchAvoided => TvSearchCacheHits + MovieSearchCacheHits;

    public bool TryGetTvSearch(
        string query,
        int page,
        string language,
        out TmdbTvSeriesSearchPage result)
    {
        var key = BuildTvKey(query, page, language);
        if (_tvSearchResults.TryGetValue(key, out var cached))
        {
            TvSearchCacheHits++;
            result = Clone(cached);
            return true;
        }

        TvSearchCacheMisses++;
        result = new TmdbTvSeriesSearchPage();
        return false;
    }

    public void SetTvSearch(
        string query,
        int page,
        string language,
        TmdbTvSeriesSearchPage result)
    {
        var key = BuildTvKey(query, page, language);
        _tvSearchResults[key] = Clone(result);
    }

    public bool TryGetMovieSearch(
        string query,
        int? releaseYear,
        out IReadOnlyList<MetadataSearchCandidate> result)
    {
        var key = BuildMovieKey(query, releaseYear);
        if (_movieSearchResults.TryGetValue(key, out var cached))
        {
            MovieSearchCacheHits++;
            result = Clone(cached);
            return true;
        }

        MovieSearchCacheMisses++;
        result = [];
        return false;
    }

    public void SetMovieSearch(
        string query,
        int? releaseYear,
        IReadOnlyList<MetadataSearchCandidate> result)
    {
        var key = BuildMovieKey(query, releaseYear);
        _movieSearchResults[key] = Clone(result);
    }

    private static string BuildTvKey(string query, int page, string language)
    {
        return string.Join(
            "|",
            "tv",
            Normalize(query),
            Math.Max(1, page),
            Normalize(string.IsNullOrWhiteSpace(language) ? "zh-CN" : language));
    }

    private static string BuildMovieKey(string query, int? releaseYear)
    {
        return string.Join(
            "|",
            "movie",
            Normalize(query),
            releaseYear?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "");
    }

    private static string Normalize(string value)
    {
        return (value ?? string.Empty).Trim().ToUpperInvariant();
    }

    private static TmdbTvSeriesSearchPage Clone(TmdbTvSeriesSearchPage page)
    {
        return new TmdbTvSeriesSearchPage
        {
            Page = page.Page,
            TotalPages = page.TotalPages,
            TotalResults = page.TotalResults,
            ResultMessage = page.ResultMessage,
            Results = page.Results
                .Select(
                    x => new TmdbTvSeriesSearchItem
                    {
                        TmdbId = x.TmdbId,
                        Name = x.Name,
                        OriginalName = x.OriginalName,
                        Overview = x.Overview,
                        PosterRemoteUrl = x.PosterRemoteUrl,
                        BackdropRemoteUrl = x.BackdropRemoteUrl,
                        FirstAirDate = x.FirstAirDate,
                        FirstAirYear = x.FirstAirYear,
                        GenreIds = x.GenreIds.ToArray(),
                        OriginalLanguage = x.OriginalLanguage,
                        OriginCountries = x.OriginCountries.ToArray(),
                        TmdbRating = x.TmdbRating,
                        TmdbVoteCount = x.TmdbVoteCount,
                        Popularity = x.Popularity
                    })
                .ToArray()
        };
    }

    private static IReadOnlyList<MetadataSearchCandidate> Clone(IReadOnlyList<MetadataSearchCandidate> candidates)
    {
        return candidates
            .Select(
                x => new MetadataSearchCandidate
                {
                    TmdbId = x.TmdbId,
                    Title = x.Title,
                    OriginalTitle = x.OriginalTitle,
                    ReleaseYear = x.ReleaseYear,
                    ReleaseDate = x.ReleaseDate,
                    Overview = x.Overview,
                    PosterRemoteUrl = x.PosterRemoteUrl,
                    GenresText = x.GenresText,
                    Country = x.Country,
                    Language = x.Language,
                    RuntimeMinutes = x.RuntimeMinutes,
                    ImdbId = x.ImdbId,
                    Confidence = x.Confidence,
                    TmdbRating = x.TmdbRating,
                    TmdbVoteCount = x.TmdbVoteCount
                })
            .ToArray();
    }
}
