using System.IO;
using System.Text.Json;
using MediaLibrary.App.Models.Profile;
using MediaLibrary.App.Services.Interfaces;
using MediaLibrary.Core.Diagnostics;
using MediaLibrary.Core.Helpers;

namespace MediaLibrary.App.Services.Implementations;

public sealed class UserProfileService : IUserProfileService
{
    private const string FileName = "user-profile.json";
    private const int MaxUserNameLength = 24;
    private const int MaxAccountLength = 32;
    private const int MaxPhoneLength = 24;
    private const int MaxEmailLength = 64;
    private const int MaxGenderLength = 16;
    private const int MaxAgeLength = 8;
    private const int MaxSignatureLength = 48;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly string _filePath;

    public UserProfileService()
    {
        _filePath = Path.Combine(AppPaths.GetAppDataDirectory(), FileName);
    }

    public event EventHandler<UserProfileModel>? ProfileChanged;

    public async Task<UserProfileModel> LoadAsync(CancellationToken cancellationToken = default)
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
            var profile = await JsonSerializer.DeserializeAsync<UserProfileModel>(
                stream,
                JsonOptions,
                cancellationToken);
            return Normalize(profile);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            AiPerfDiagnostics.WriteEvent($"user-profile-load-failed errorType={exception.GetType().Name}");
            return CreateDefault();
        }
    }

    public async Task SaveAsync(UserProfileModel profile, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(profile);
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

        ProfileChanged?.Invoke(this, Clone(normalized));
    }

    private static UserProfileModel CreateDefault()
    {
        return new UserProfileModel
        {
            UserName = "James",
            Account = "local_user"
        };
    }

    private static UserProfileModel Normalize(UserProfileModel? profile)
    {
        profile ??= CreateDefault();
        return new UserProfileModel
        {
            UserName = TrimToLength(profile.UserName, MaxUserNameLength),
            Account = TrimToLength(profile.Account, MaxAccountLength),
            PhoneNumber = TrimToLength(profile.PhoneNumber, MaxPhoneLength),
            Email = TrimToLength(profile.Email, MaxEmailLength),
            Gender = TrimToLength(profile.Gender, MaxGenderLength),
            Age = TrimToLength(profile.Age, MaxAgeLength),
            Signature = TrimToLength(profile.Signature, MaxSignatureLength),
            AvatarPath = NormalizeAvatarPath(profile.AvatarPath)
        };
    }

    private static UserProfileModel Clone(UserProfileModel profile)
    {
        return new UserProfileModel
        {
            UserName = profile.UserName,
            Account = profile.Account,
            PhoneNumber = profile.PhoneNumber,
            Email = profile.Email,
            Gender = profile.Gender,
            Age = profile.Age,
            Signature = profile.Signature,
            AvatarPath = profile.AvatarPath
        };
    }

    private static string TrimToLength(string? value, int maxLength)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string NormalizeAvatarPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var trimmed = path.Trim();
        return File.Exists(trimmed) ? trimmed : string.Empty;
    }
}
