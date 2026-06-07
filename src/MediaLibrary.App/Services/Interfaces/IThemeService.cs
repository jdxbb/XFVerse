namespace MediaLibrary.App.Services.Interfaces;

public interface IThemeService
{
    event EventHandler<string>? ThemeChanged;

    IReadOnlyList<string> ThemeModes { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task ApplyAndSaveAsync(string themeMode, CancellationToken cancellationToken = default);

    void Apply(string themeMode);
}
