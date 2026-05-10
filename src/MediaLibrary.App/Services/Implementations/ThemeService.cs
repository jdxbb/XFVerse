using System.Windows;
using MediaLibrary.App.Services.Interfaces;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.App.Services.Implementations;

public sealed class ThemeService : IThemeService
{
    private const string LightTheme = "Light";
    private const string DarkTheme = "Dark";
    private readonly ISettingsService _settingsService;

    public ThemeService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public IReadOnlyList<string> ThemeModes { get; } = [LightTheme, DarkTheme];

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.GetApplicationSettingAsync(cancellationToken);
        Apply(settings.ThemeMode);
    }

    public async Task ApplyAndSaveAsync(string themeMode, CancellationToken cancellationToken = default)
    {
        var normalizedTheme = NormalizeTheme(themeMode);
        Apply(normalizedTheme);

        var settings = await _settingsService.GetApplicationSettingAsync(cancellationToken);
        settings.ThemeMode = normalizedTheme;
        await _settingsService.SaveApplicationSettingAsync(settings, cancellationToken);
    }

    public void Apply(string themeMode)
    {
        var normalizedTheme = NormalizeTheme(themeMode);
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var themeUri = new Uri($"Resources/Styles/Colors.{normalizedTheme}.xaml", UriKind.Relative);
        var replacement = new ResourceDictionary { Source = themeUri };

        for (var i = 0; i < dictionaries.Count; i++)
        {
            var source = dictionaries[i].Source?.OriginalString ?? string.Empty;
            if (source.Contains("Colors.", StringComparison.OrdinalIgnoreCase)
                || source.EndsWith("Colors.xaml", StringComparison.OrdinalIgnoreCase))
            {
                dictionaries[i] = replacement;
                return;
            }
        }

        dictionaries.Insert(0, replacement);
    }

    private static string NormalizeTheme(string? themeMode)
    {
        return string.Equals(themeMode, DarkTheme, StringComparison.OrdinalIgnoreCase)
            ? DarkTheme
            : LightTheme;
    }
}
