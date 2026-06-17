using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediaLibrary.Core.Diagnostics;
using MediaLibrary.Core.Helpers;
using MediaLibrary.Core.Models.Settings;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class RecommendationPreferenceService : IRecommendationPreferenceService
{
    private const string FileName = "recommendation-preferences.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly string _filePath;

    public RecommendationPreferenceService()
    {
        _filePath = Path.Combine(AppPaths.GetAppDataDirectory(), FileName);
    }

    public async Task<RecommendationPreferenceModel> GetAsync(CancellationToken cancellationToken = default)
    {
        return await LoadAsync(logFailures: true, cancellationToken);
    }

    public async Task<RecommendationPreferenceModel> SaveAsync(
        RecommendationPreferenceModel preference,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeForSave(preference);
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var existing = await LoadAsync(logFailures: false, cancellationToken);
            if (IsSameStoredPreference(existing, normalized))
            {
                return existing;
            }

            normalized.UpdatedAt = DateTimeOffset.UtcNow;
            await SaveCoreAsync(normalized, cancellationToken);
            AiPerfDiagnostics.WriteEvent(
                $"event=recommendation-preference-saved enabled={normalized.IsEnabled} length={normalized.Text.Length} hash={BuildTextHash(normalized.Text)}");
            return normalized;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<RecommendationPreferenceModel> ClearAsync(CancellationToken cancellationToken = default)
    {
        return await SaveAsync(
            new RecommendationPreferenceModel
            {
                IsEnabled = false,
                Text = string.Empty
            },
            cancellationToken);
    }

    public string NormalizeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();
    }

    public string BuildFingerprintPart(RecommendationPreferenceModel preference)
    {
        var normalized = NormalizeForLoad(preference);
        var isEffective = normalized.IsEnabled && normalized.Text.Length > 0;
        var textHash = isEffective ? BuildTextHash(normalized.Text) : "none";
        var updatedAt = isEffective ? normalized.UpdatedAt.UtcDateTime.Ticks : 0L;
        return $"custom-pref:{isEffective}:{textHash}:{updatedAt}";
    }

    private async Task<RecommendationPreferenceModel> LoadAsync(
        bool logFailures,
        CancellationToken cancellationToken)
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
            var preference = await JsonSerializer.DeserializeAsync<RecommendationPreferenceModel>(
                stream,
                JsonOptions,
                cancellationToken);
            return NormalizeForLoad(preference);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            if (logFailures)
            {
                AiPerfDiagnostics.WriteEvent(
                    $"event=recommendation-preference-load-failed errorType={exception.GetType().Name}");
            }

            return CreateDefault();
        }
    }

    private async Task SaveCoreAsync(
        RecommendationPreferenceModel preference,
        CancellationToken cancellationToken)
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
            await JsonSerializer.SerializeAsync(stream, preference, JsonOptions, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        File.Move(tempPath, _filePath, overwrite: true);
    }

    private RecommendationPreferenceModel NormalizeForSave(RecommendationPreferenceModel? preference)
    {
        var normalized = NormalizeForLoad(preference);
        if (normalized.Text.Length > RecommendationPreferenceModel.MaxTextLength)
        {
            throw new InvalidOperationException($"自定义推荐偏好不能超过 {RecommendationPreferenceModel.MaxTextLength} 个字符。");
        }

        return normalized;
    }

    private RecommendationPreferenceModel NormalizeForLoad(RecommendationPreferenceModel? preference)
    {
        if (preference is null)
        {
            return CreateDefault();
        }

        var text = NormalizeText(preference.Text);
        if (text.Length > RecommendationPreferenceModel.MaxTextLength)
        {
            text = text[..RecommendationPreferenceModel.MaxTextLength];
            AiPerfDiagnostics.WriteEvent(
                $"event=recommendation-preference-text-truncated length={preference.Text.Length} max={RecommendationPreferenceModel.MaxTextLength}");
        }

        return new RecommendationPreferenceModel
        {
            IsEnabled = preference.IsEnabled && text.Length > 0,
            Text = text,
            UpdatedAt = preference.UpdatedAt == default ? DateTimeOffset.MinValue : preference.UpdatedAt
        };
    }

    private static RecommendationPreferenceModel CreateDefault()
    {
        return new RecommendationPreferenceModel
        {
            IsEnabled = false,
            Text = string.Empty,
            UpdatedAt = DateTimeOffset.MinValue
        };
    }

    private static bool IsSameStoredPreference(
        RecommendationPreferenceModel existing,
        RecommendationPreferenceModel next)
    {
        return existing.IsEnabled == next.IsEnabled
               && string.Equals(existing.Text, next.Text, StringComparison.Ordinal);
    }

    private static string BuildTextHash(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "none";
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];
    }
}
