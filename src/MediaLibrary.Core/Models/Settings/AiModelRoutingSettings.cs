using System.Text.Json;

namespace MediaLibrary.Core.Models.Settings;

public sealed class AiModelRoutingSettings
{
    public const string SingleSourceCorrectionKind = "single-source-correction";
    public const string BatchCorrectionKind = "batch-ai-correction";
    public const string ScanTvUncertainRangeKind = "tv-scan-uncertain-range";
    public const string ScanTvFullRangeKind = "tv-scan-full-range";
    public const string ScanMovieTaggingKind = "movie-tagging";
    public const string RecommendationKind = "recommendation";
    public const string WatchProfileKind = "watch-profile";

    private const int CurrentVersion = 1;

    public string DefaultModel { get; set; } = "deepseek-v4-flash";

    public AiModelRoute SingleSourceCorrection { get; set; } = new("deepseek-v4-pro", 90);

    public AiModelRoute BatchCorrection { get; set; } = new("deepseek-v4-pro", 75);

    public AiModelRoute ScanTvUncertainRange { get; set; } = new("deepseek-v4-flash", 300);

    public AiModelRoute ScanTvFullRange { get; set; } = new("deepseek-v4-flash", 18);

    public AiModelRoute ScanMovieTagging { get; set; } = new("deepseek-v4-flash", 45);

    public AiModelRoute Recommendation { get; set; } = new("deepseek-v4-flash", 90);

    public AiModelRoute WatchProfile { get; set; } = new("deepseek-v4-pro", 180);

    public static AiModelRoutingSettings FromStoredValue(string? storedValue)
    {
        if (string.IsNullOrWhiteSpace(storedValue))
        {
            return CreateDefault();
        }

        var trimmed = storedValue.Trim();
        if (!trimmed.StartsWith('{'))
        {
            return CreateDefault(trimmed);
        }

        try
        {
            var document = JsonSerializer.Deserialize<AiModelRoutingDocument>(
                trimmed,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return FromDocument(document, fallbackDefaultModel: "deepseek-v4-flash");
        }
        catch
        {
            return CreateDefault(trimmed);
        }
    }

    public static AiModelRoutingSettings CreateDefault(string? defaultModel = null)
    {
        var settings = new AiModelRoutingSettings();
        if (!string.IsNullOrWhiteSpace(defaultModel))
        {
            settings.DefaultModel = defaultModel.Trim();
        }

        return settings;
    }

    public AiModelRoutingSettings Clone()
    {
        return new AiModelRoutingSettings
        {
            DefaultModel = DefaultModel,
            SingleSourceCorrection = SingleSourceCorrection.Clone(),
            BatchCorrection = BatchCorrection.Clone(),
            ScanTvUncertainRange = ScanTvUncertainRange.Clone(),
            ScanTvFullRange = ScanTvFullRange.Clone(),
            ScanMovieTagging = ScanMovieTagging.Clone(),
            Recommendation = Recommendation.Clone(),
            WatchProfile = WatchProfile.Clone()
        };
    }

    public AiModelRoute? ResolveRoute(string? requestKind)
    {
        return requestKind?.Trim() switch
        {
            SingleSourceCorrectionKind => SingleSourceCorrection,
            BatchCorrectionKind => BatchCorrection,
            ScanTvUncertainRangeKind => ScanTvUncertainRange,
            ScanTvFullRangeKind => ScanTvFullRange,
            ScanMovieTaggingKind => ScanMovieTagging,
            RecommendationKind => Recommendation,
            WatchProfileKind => WatchProfile,
            _ => null
        };
    }

    public string ToStoredValue()
    {
        var document = new AiModelRoutingDocument
        {
            Version = CurrentVersion,
            DefaultModel = NormalizeModel(DefaultModel, "deepseek-v4-flash"),
            SingleSourceCorrection = SingleSourceCorrection.ToDocument("deepseek-v4-pro", 90),
            BatchCorrection = BatchCorrection.ToDocument("deepseek-v4-pro", 75),
            ScanTvUncertainRange = ScanTvUncertainRange.ToDocument("deepseek-v4-flash", 300),
            ScanTvFullRange = ScanTvFullRange.ToDocument("deepseek-v4-flash", 18),
            ScanMovieTagging = ScanMovieTagging.ToDocument("deepseek-v4-flash", 45),
            Recommendation = Recommendation.ToDocument("deepseek-v4-flash", 90),
            WatchProfile = WatchProfile.ToDocument("deepseek-v4-pro", 180)
        };

        return JsonSerializer.Serialize(document);
    }

    private static AiModelRoutingSettings FromDocument(AiModelRoutingDocument? document, string fallbackDefaultModel)
    {
        var settings = CreateDefault(document?.DefaultModel ?? fallbackDefaultModel);
        settings.SingleSourceCorrection = AiModelRoute.FromDocument(document?.SingleSourceCorrection, "deepseek-v4-pro", 90);
        settings.BatchCorrection = AiModelRoute.FromDocument(document?.BatchCorrection, "deepseek-v4-pro", 75);
        settings.ScanTvUncertainRange = AiModelRoute.FromDocument(document?.ScanTvUncertainRange, "deepseek-v4-flash", 300);
        settings.ScanTvFullRange = AiModelRoute.FromDocument(document?.ScanTvFullRange, "deepseek-v4-flash", 18);
        settings.ScanMovieTagging = AiModelRoute.FromDocument(document?.ScanMovieTagging, "deepseek-v4-flash", 45);
        settings.Recommendation = AiModelRoute.FromDocument(document?.Recommendation, "deepseek-v4-flash", 90);
        settings.WatchProfile = AiModelRoute.FromDocument(document?.WatchProfile, "deepseek-v4-pro", 180);
        return settings;
    }

    private static string NormalizeModel(string? model, string fallback)
    {
        return string.IsNullOrWhiteSpace(model) ? fallback : model.Trim();
    }

    private sealed class AiModelRoutingDocument
    {
        public int Version { get; set; } = CurrentVersion;

        public string DefaultModel { get; set; } = "deepseek-v4-flash";

        public AiModelRouteDocument? SingleSourceCorrection { get; set; }

        public AiModelRouteDocument? BatchCorrection { get; set; }

        public AiModelRouteDocument? ScanTvUncertainRange { get; set; }

        public AiModelRouteDocument? ScanTvFullRange { get; set; }

        public AiModelRouteDocument? ScanMovieTagging { get; set; }

        public AiModelRouteDocument? Recommendation { get; set; }

        public AiModelRouteDocument? WatchProfile { get; set; }
    }

    internal sealed class AiModelRouteDocument
    {
        public string Model { get; set; } = string.Empty;

        public int TimeoutSeconds { get; set; }
    }

    public sealed class AiModelRoute
    {
        public AiModelRoute()
        {
        }

        public AiModelRoute(string model, int timeoutSeconds)
        {
            Model = model;
            TimeoutSeconds = timeoutSeconds;
        }

        public string Model { get; set; } = string.Empty;

        public int TimeoutSeconds { get; set; }

        public AiModelRoute Clone()
        {
            return new AiModelRoute(Model, TimeoutSeconds);
        }

        private static int NormalizeTimeout(int timeoutSeconds, int fallback)
        {
            return timeoutSeconds > 0 ? timeoutSeconds : fallback;
        }

        internal static AiModelRoute FromDocument(AiModelRouteDocument? document, string fallbackModel, int fallbackTimeoutSeconds)
        {
            return new AiModelRoute(
                NormalizeModel(document?.Model, fallbackModel),
                NormalizeTimeout(document?.TimeoutSeconds ?? 0, fallbackTimeoutSeconds));
        }

        internal AiModelRouteDocument ToDocument(string fallbackModel, int fallbackTimeoutSeconds)
        {
            return new AiModelRouteDocument
            {
                Model = NormalizeModel(Model, fallbackModel),
                TimeoutSeconds = NormalizeTimeout(TimeoutSeconds, fallbackTimeoutSeconds)
            };
        }
    }
}
