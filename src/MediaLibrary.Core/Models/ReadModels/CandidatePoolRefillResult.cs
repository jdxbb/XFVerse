namespace MediaLibrary.Core.Models.ReadModels;

public enum CandidatePoolRefillOutcome
{
    Skipped,
    Success,
    Failed,
    NoGeneratedCandidates,
    Canceled,
    Discarded
}

public sealed record CandidatePoolRefillResult(
    CandidatePoolRefillOutcome Outcome,
    string Reason = "",
    int BeforeAvailableCount = 0,
    int GeneratedCandidateCount = 0,
    int AddedCount = 0,
    int AfterAvailableCount = 0,
    string Message = "",
    string Fingerprint = "")
{
    public bool Succeeded => Outcome == CandidatePoolRefillOutcome.Success;

    public static CandidatePoolRefillResult Skipped(
        string reason,
        int beforeAvailableCount = 0)
    {
        return new CandidatePoolRefillResult(CandidatePoolRefillOutcome.Skipped, reason, beforeAvailableCount);
    }

    public static CandidatePoolRefillResult Success(
        int beforeAvailableCount,
        int generatedCandidateCount,
        int addedCount,
        int afterAvailableCount)
    {
        return new CandidatePoolRefillResult(
            CandidatePoolRefillOutcome.Success,
            string.Empty,
            beforeAvailableCount,
            generatedCandidateCount,
            addedCount,
            afterAvailableCount);
    }

    public static CandidatePoolRefillResult Failed(
        string reason = "failed",
        string message = "",
        string fingerprint = "")
    {
        return new CandidatePoolRefillResult(
            CandidatePoolRefillOutcome.Failed,
            reason,
            Message: message,
            Fingerprint: fingerprint);
    }

    public static CandidatePoolRefillResult NoGeneratedCandidates(
        string reason,
        int beforeAvailableCount,
        int generatedCandidateCount)
    {
        return new CandidatePoolRefillResult(
            CandidatePoolRefillOutcome.NoGeneratedCandidates,
            reason,
            beforeAvailableCount,
            generatedCandidateCount);
    }

    public static CandidatePoolRefillResult Canceled(string reason)
    {
        return new CandidatePoolRefillResult(CandidatePoolRefillOutcome.Canceled, reason);
    }

    public static CandidatePoolRefillResult Discarded(
        string reason,
        int beforeAvailableCount,
        int generatedCandidateCount)
    {
        return new CandidatePoolRefillResult(
            CandidatePoolRefillOutcome.Discarded,
            reason,
            beforeAvailableCount,
            generatedCandidateCount);
    }
}
