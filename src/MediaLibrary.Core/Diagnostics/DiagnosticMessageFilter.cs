namespace MediaLibrary.Core.Diagnostics;

public static class DiagnosticMessageFilter
{
    private static readonly string[] ReleaseKeywords =
    [
        "error",
        "failed",
        "failure",
        "exception",
        "timeout",
        "retry-exhausted",
        "parse-failed",
        "invalid",
        "slow",
        "ui-recover",
        "no-generated-candidates"
    ];

    public static bool IsEnabledByEnvironment(string variableName)
    {
        return string.Equals(
            Environment.GetEnvironmentVariable(variableName),
            "1",
            StringComparison.Ordinal);
    }

    public static bool ShouldWriteReleaseMessage(string? message)
    {
        return !string.IsNullOrWhiteSpace(message)
               && ReleaseKeywords.Any(keyword => message.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    public static bool ShouldWrite(string? message, string verboseEnvironmentVariable)
    {
        return IsEnabledByEnvironment(verboseEnvironmentVariable)
               || ShouldWriteReleaseMessage(message);
    }
}
