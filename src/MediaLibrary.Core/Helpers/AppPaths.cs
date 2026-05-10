using System.IO;

namespace MediaLibrary.Core.Helpers;

public static class AppPaths
{
    private const string AppFolderName = "MediaLibrary";

    public static string GetAppDataDirectory()
    {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appDataDirectory = Path.Combine(basePath, AppFolderName);
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
}
