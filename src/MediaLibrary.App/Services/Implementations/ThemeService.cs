using System.Windows;
using MediaLibrary.App.Services.Interfaces;
using MediaLibrary.Core.Services.Interfaces;
using Microsoft.Win32;

namespace MediaLibrary.App.Services.Implementations;

public sealed class ThemeService : IThemeService
{
    private const string SystemTheme = "System";
    private const string LightTheme = "Light";
    private const string DarkTheme = "Dark";
    private readonly ISettingsService _settingsService;

    public ThemeService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public event EventHandler<string>? ThemeChanged;

    public IReadOnlyList<string> ThemeModes { get; } = [SystemTheme, LightTheme, DarkTheme];

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
        var appliedTheme = ResolveTheme(normalizedTheme);
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var themeUri = new Uri($"Resources/Styles/Colors.{appliedTheme}.xaml", UriKind.Relative);
        var replacement = new ResourceDictionary { Source = themeUri };

        for (var i = 0; i < dictionaries.Count; i++)
        {
            var source = dictionaries[i].Source?.OriginalString ?? string.Empty;
            if (source.Contains("Colors.", StringComparison.OrdinalIgnoreCase)
                || source.EndsWith("Colors.xaml", StringComparison.OrdinalIgnoreCase))
            {
                dictionaries[i] = replacement;
                ThemeChanged?.Invoke(this, appliedTheme);
                return;
            }
        }

        dictionaries.Insert(0, replacement);
        ThemeChanged?.Invoke(this, appliedTheme);
    }

    private static string NormalizeTheme(string? themeMode)
    {
        if (string.Equals(themeMode, DarkTheme, StringComparison.OrdinalIgnoreCase))
        {
            return DarkTheme;
        }

        return string.Equals(themeMode, SystemTheme, StringComparison.OrdinalIgnoreCase)
            ? SystemTheme
            : LightTheme;
    }

    private static string ResolveTheme(string themeMode)
    {
        return string.Equals(themeMode, SystemTheme, StringComparison.OrdinalIgnoreCase)
            ? ResolveSystemTheme()
            : themeMode;
    }

    private static string ResolveSystemTheme()
    {
        try
        {
            using var personalizeKey = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var appsUseLightTheme = personalizeKey?.GetValue("AppsUseLightTheme");
            return appsUseLightTheme is int value && value == 0 ? DarkTheme : LightTheme;
        }
        catch
        {
            return LightTheme;
        }
    }
}
