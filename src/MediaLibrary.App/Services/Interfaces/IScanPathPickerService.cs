using MediaLibrary.Core.Models.Settings;

namespace MediaLibrary.App.Services.Interfaces;

public interface IScanPathPickerService
{
    Task<string?> PickLocalDirectoryAsync(string? initialPath = null);

    Task<IReadOnlyList<string>> PickLocalDirectoriesAsync(string? initialPath = null);

    Task<string?> PickWebDavDirectoryAsync(WebDavConnectionModel connection, string? initialPath = null);

    Task<IReadOnlyList<string>> PickWebDavDirectoriesAsync(WebDavConnectionModel connection, string? initialPath = null);
}
