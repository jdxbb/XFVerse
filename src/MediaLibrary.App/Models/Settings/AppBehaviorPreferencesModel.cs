namespace MediaLibrary.App.Models.Settings;

public sealed class AppBehaviorPreferencesModel
{
    public string CloseWindowBehavior { get; set; } = "exit";

    public bool StartPlayerFullscreenOnPlay { get; set; } = true;

    public bool AutoScanWebDavOnStartup { get; set; }
}
