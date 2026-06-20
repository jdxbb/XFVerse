using MediaLibrary.Core.Helpers;

namespace MediaLibrary.Core.Diagnostics;

public static class DiagnosticLogPathResolver
{
    /// <summary>
    /// Resolves a safe file name under the current user's XFVerse log directory.
    /// </summary>
    public static string Resolve(string fileName)
    {
        var safeFileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            safeFileName = "diagnostics.log";
        }

        return Path.Combine(AppPaths.GetLogsDirectory(), safeFileName);
    }
}
