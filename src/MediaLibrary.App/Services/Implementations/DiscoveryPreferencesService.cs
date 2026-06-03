using System.IO;
using System.Text.Json;
using MediaLibrary.App.Models.Discovery;
using MediaLibrary.App.Services.Interfaces;
using MediaLibrary.Core.Diagnostics;
using MediaLibrary.Core.Helpers;

namespace MediaLibrary.App.Services.Implementations;

public sealed class DiscoveryPreferencesService : IDiscoveryPreferencesService
{
    private const string FileName = "discovery-preferences.json";
    private const string PosterLayoutMode = "poster";
    private const string ListLayoutMode = "list";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly string _filePath;

    public DiscoveryPreferencesService()
    {
        _filePath = Path.Combine(AppPaths.GetAppDataDirectory(), FileName);
    }

    public async Task<DiscoveryPreferencesModel> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return CreateDefault();
            }

            await using var stream = new FileStream(
                _filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);
            var preferences = await JsonSerializer.DeserializeAsync<DiscoveryPreferencesModel>(
                stream,
                JsonOptions,
                cancellationToken);
            return Normalize(preferences);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            AiPerfDiagnostics.WriteEvent(
                $"discovery-preferences-load-failed errorType={exception.GetType().Name}");
            return CreateDefault();
        }
    }

    public async Task SaveAsync(DiscoveryPreferencesModel preferences, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(preferences);
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = _filePath + ".tmp";
            await using (var stream = new FileStream(
                             tempPath,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 4096,
                             useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, normalized, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(tempPath, _filePath, overwrite: true);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private static DiscoveryPreferencesModel CreateDefault()
    {
        return new DiscoveryPreferencesModel
        {
            SearchLayoutMode = PosterLayoutMode
        };
    }

    private static DiscoveryPreferencesModel Normalize(DiscoveryPreferencesModel? preferences)
    {
        if (preferences is null)
        {
            return CreateDefault();
        }

        var layoutMode = string.Equals(preferences.SearchLayoutMode, ListLayoutMode, StringComparison.OrdinalIgnoreCase)
            ? ListLayoutMode
            : PosterLayoutMode;

        return new DiscoveryPreferencesModel
        {
            SearchLayoutMode = layoutMode
        };
    }
}
