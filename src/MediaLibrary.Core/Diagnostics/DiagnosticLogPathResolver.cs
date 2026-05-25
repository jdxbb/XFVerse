namespace MediaLibrary.Core.Diagnostics;

public static class DiagnosticLogPathResolver
{
    public static string Resolve(string fileName)
    {
        var safeFileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            safeFileName = "diagnostics.log";
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MediaLibrary.sln")))
            {
                return Path.Combine(directory.FullName, "logs", safeFileName);
            }

            directory = directory.Parent;
        }

        return Path.Combine(AppContext.BaseDirectory, "logs", safeFileName);
    }
}
