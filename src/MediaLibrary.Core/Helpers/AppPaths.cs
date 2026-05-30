using System.IO;

namespace MediaLibrary.Core.Helpers;

public static class AppPaths
{
    private const string AppFolderName = "MediaLibrary";
    private const string AppDataDirectoryOverrideEnvironmentVariable = "XFVERSE_APPDATA_DIR";

    public static string GetAppDataDirectory()
    {
        var overrideDirectory = Environment.GetEnvironmentVariable(AppDataDirectoryOverrideEnvironmentVariable);
        var appDataDirectory = string.IsNullOrWhiteSpace(overrideDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolderName)
            : overrideDirectory;

        Directory.CreateDirectory(appDataDirectory);
        return appDataDirectory;
    }

    public static string GetDatabaseFilePath()
    {
        return Path.Combine(GetAppDataDirectory(), "media-library.db");
    }

    public static string GetVideoCacheDirectory()
    {
        var videoCacheDirectory = Path.Combine(GetAppDataDirectory(), "VideoCache");
        Directory.CreateDirectory(videoCacheDirectory);
        return videoCacheDirectory;
    }

    public static string GetPosterCacheDirectory()
    {
        var posterCacheDirectory = Path.Combine(GetAppDataDirectory(), "PosterCache");
        Directory.CreateDirectory(posterCacheDirectory);
        return posterCacheDirectory;
    }

    public static string GetOnlineSubtitleCacheDirectory()
    {
        var subtitleCacheDirectory = Path.Combine(GetAppDataDirectory(), "OnlineSubtitles");
        Directory.CreateDirectory(subtitleCacheDirectory);
        return subtitleCacheDirectory;
    }
}
