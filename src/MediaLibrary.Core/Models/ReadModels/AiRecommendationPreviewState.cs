namespace MediaLibrary.Core.Models.ReadModels;

public sealed class AiRecommendationPreviewState
{
    public IReadOnlyList<AiRecommendationItem> Items { get; set; } = [];

    public bool HasRequested { get; set; }

    public bool IsPending { get; set; }

    public bool IsUpdating { get; set; }

    public int CandidatePoolCount { get; set; }

    public int CandidatePoolRawCount { get; set; }

    public string Fingerprint { get; set; } = string.Empty;

    public bool CanRequest { get; set; } = true;

    public string Status { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}
