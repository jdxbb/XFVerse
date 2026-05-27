using System.IO;
using System.Text.Json;
using MediaLibrary.App.Models.Library;
using MediaLibrary.App.Services.Interfaces;
using MediaLibrary.Core.Diagnostics;
using MediaLibrary.Core.Helpers;

namespace MediaLibrary.App.Services.Implementations;

public sealed class LibraryPreferencesService : ILibraryPreferencesService
{
    private const string FileName = "library-preferences.json";
    private const string PosterLayoutMode = "poster";
    private const string ListLayoutMode = "list";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly string _filePath;

    public LibraryPreferencesService()
    {
        _filePath = Path.Combine(AppPaths.GetAppDataDirectory(), FileName);
    }

    public async Task<LibraryPreferencesModel> LoadAsync(CancellationToken cancellationToken = default)
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
            var preferences = await JsonSerializer.DeserializeAsync<LibraryPreferencesModel>(
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
                $"library-preferences-load-failed errorType={exception.GetType().Name}");
            return CreateDefault();
        }
    }

    public async Task SaveAsync(LibraryPreferencesModel preferences, CancellationToken cancellationToken = default)
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

    private static LibraryPreferencesModel CreateDefault()
    {
        return new LibraryPreferencesModel
        {
            LayoutMode = PosterLayoutMode
        };
    }

    private static LibraryPreferencesModel Normalize(LibraryPreferencesModel? preferences)
    {
        if (preferences is null)
        {
            return CreateDefault();
        }

        var layoutMode = string.Equals(preferences.LayoutMode, ListLayoutMode, StringComparison.OrdinalIgnoreCase)
            ? ListLayoutMode
            : PosterLayoutMode;

        return new LibraryPreferencesModel
        {
            LayoutMode = layoutMode
        };
    }
}
