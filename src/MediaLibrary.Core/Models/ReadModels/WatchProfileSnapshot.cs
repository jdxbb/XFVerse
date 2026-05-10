namespace MediaLibrary.Core.Models.ReadModels;

public sealed class WatchProfileSnapshot
{
    public WatchProfileMeta Meta { get; set; } = new();

    public bool LoadedFromCache { get; set; }

    public bool HasProfile { get; set; }

    public bool CanGenerateProfile { get; set; }

    public string InsufficientReason { get; set; } = string.Empty;

    public string ErrorMessage { get; set; } = string.Empty;

    public string StatusMessage { get; set; } = string.Empty;

    public bool WasAiCalled { get; set; }

    public bool IsCacheHit { get; set; }

    public bool IsUnchanged { get; set; }

    public List<string> WarningMessages { get; set; } = [];

    public WatchProfileSummary Summary { get; set; } = new();

    public WatchProfilePersona Persona { get; set; } = new();

    public List<WatchProfileDnaGene> DNA { get; set; } = [];

    public WatchProfileQuadrant Quadrant { get; set; } = new();

    public WatchProfileWatchVsLike WatchVsLike { get; set; } = new();

    public WatchProfileLikes Likes { get; set; } = new();

    public WatchProfileDislikes Dislikes { get; set; } = new();

    public WatchProfileFuturePreference FuturePreference { get; set; } = new();

    public List<string> Caveats { get; set; } = [];
}

public sealed class WatchProfileMeta
{
    public DateTime GeneratedAtUtc { get; set; }

    public string SourceFingerprint { get; set; } = string.Empty;

    public int ProfileSchemaVersion { get; set; }

    public string PromptVersion { get; set; } = string.Empty;

    public int SignalMovieCount { get; set; }

    public int Confidence { get; set; }

    public List<string> WarningMessages { get; set; } = [];
}

public sealed class WatchProfileSummary
{
    public string Text { get; set; } = string.Empty;

    public List<string> Keywords { get; set; } = [];
}

public sealed class WatchProfilePersona
{
    public string Type { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int Confidence { get; set; }
}

public sealed class WatchProfileDnaGene
{
    public string Gene { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = [];

    public int Score { get; set; }

    public string Description { get; set; } = string.Empty;

    public int Confidence { get; set; }
}

public sealed class WatchProfileQuadrant
{
    public int XAxisScore { get; set; }

    public int YAxisScore { get; set; }

    public string QuadrantName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}

public sealed class WatchProfileWatchVsLike
{
    public List<string> OftenWatchedTypes { get; set; } = [];

    public List<string> OftenLikedTypes { get; set; } = [];

    public List<string> OftenWantedTypes { get; set; } = [];

    public string Conclusion { get; set; } = string.Empty;
}

public sealed class WatchProfileLikes
{
    public List<string> PreferredGenres { get; set; } = [];

    public List<string> PreferredEmotions { get; set; } = [];

    public List<string> PreferredScenes { get; set; } = [];

    public List<string> PreferredCountries { get; set; } = [];

    public List<string> PreferredLanguages { get; set; } = [];
}

public sealed class WatchProfileDislikes
{
    public List<string> AvoidGenres { get; set; } = [];

    public List<string> AvoidEmotions { get; set; } = [];

    public List<string> AvoidScenes { get; set; } = [];

    public string NegativeSummary { get; set; } = string.Empty;
}

public sealed class WatchProfileFuturePreference
{
    public List<string> LikelyToEnjoy { get; set; } = [];

    public List<string> LessLikelyToEnjoy { get; set; } = [];
}
