using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Models.Settings;

namespace MediaLibrary.Core.Services.Interfaces;

public interface IWebDavService
{
    Task<WebDavConnectionTestResult> TestConnectionAsync(
        WebDavConnectionModel connectionModel,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RemoteEntry>> ListDirectoryAsync(
        WebDavConnectionModel connectionModel,
        string directoryPath,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RemoteEntry>> ListDirectoryAsync(
        WebDavConnectionModel connectionModel,
        string directoryPath,
        string? directoryUri,
        CancellationToken cancellationToken = default);
}
