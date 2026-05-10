using System.IO;
using System.Text.Json;
using MediaLibrary.App.Helpers;
using MediaLibrary.App.Models.Player;
using MediaLibrary.App.Services.Interfaces;
using MediaLibrary.Core.Helpers;

namespace MediaLibrary.App.Services.Implementations;

public sealed class PlayerPreferencesService : IPlayerPreferencesService
{
    private const int DefaultVolume = 80;
    private const int DefaultBrightness = 100;
    private const string FileName = "player-preferences.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly string _filePath;

    public PlayerPreferencesService()
    {
        _filePath = Path.Combine(AppPaths.GetAppDataDirectory(), FileName);
    }

    public async Task<PlayerPreferencesModel> LoadAsync(CancellationToken cancellationToken = default)
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
            var preferences = await JsonSerializer.DeserializeAsync<PlayerPreferencesModel>(
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
            MpvPlaybackDiagnostics.Write(
                $"player-ux1-preferences-load-failed errorType={exception.GetType().Name}");
            return CreateDefault();
        }
    }

    public async Task SaveAsync(PlayerPreferencesModel preferences, CancellationToken cancellationToken = default)
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

    private static PlayerPreferencesModel CreateDefault()
    {
        return new PlayerPreferencesModel
        {
            Volume = DefaultVolume,
            Muted = false,
            Brightness = DefaultBrightness
        };
    }

    private static PlayerPreferencesModel Normalize(PlayerPreferencesModel? preferences)
    {
        if (preferences is null)
        {
            return CreateDefault();
        }

        var volume = Math.Clamp(preferences.Volume, 0, 200);
        var muted = preferences.Muted || volume <= 0;
        if (muted && volume <= 0)
        {
            volume = DefaultVolume;
        }

        return new PlayerPreferencesModel
        {
            Volume = volume,
            Muted = muted,
            Brightness = Math.Clamp(preferences.Brightness, 0, 100)
        };
    }
}
