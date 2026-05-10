using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using MediaLibrary.Core.Data;
using MediaLibrary.Core.Helpers;
using MediaLibrary.Core.Models.Entities;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class MediaProbeService : IMediaProbeService, IDisposable
{
    private const int MaxAutomaticAttempts = 2;
    private const int MaxErrorLength = 500;
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(25);
    private readonly ConcurrentQueue<ProbeQueueItem> _queue = new();
    private readonly HashSet<int> _queuedMediaFileIds = [];
    private readonly object _queueLock = new();
    private readonly CancellationTokenSource _disposeTokenSource = new();
    private int _isWorkerRunning;
    private bool _disposed;

    public Task EnqueueMediaFilesAsync(
        IReadOnlyCollection<int> mediaFileIds,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested || mediaFileIds.Count == 0 || _disposed)
        {
            return Task.CompletedTask;
        }

        foreach (var mediaFileId in mediaFileIds.Where(id => id > 0).Distinct())
        {
            lock (_queueLock)
            {
                if (!_queuedMediaFileIds.Add(mediaFileId))
                {
                    continue;
                }

                _queue.Enqueue(new ProbeQueueItem(mediaFileId, force));
            }
        }

        StartWorkerIfNeeded();
        return Task.CompletedTask;
    }

    public async Task EnqueueMovieSourcesAsync(
        int movieId,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var mediaFileIds = await dbContext.MediaFiles
            .AsNoTracking()
            .Where(x => x.MovieId == movieId && x.MediaType == MediaType.Video && !x.IsDeleted)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        await EnqueueMediaFilesAsync(mediaFileIds, force, cancellationToken);
    }

    public async Task ProbeMediaFileAsync(
        int mediaFileId,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        await ProbeMediaFileCoreAsync(mediaFileId, force, cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _disposeTokenSource.Cancel();
        _disposeTokenSource.Dispose();
    }

    private void StartWorkerIfNeeded()
    {
        if (_disposed || Interlocked.CompareExchange(ref _isWorkerRunning, 1, 0) != 0)
        {
            return;
        }

        _ = Task.Run(ProcessQueueAsync);
    }

    private async Task ProcessQueueAsync()
    {
        try
        {
            while (!_disposeTokenSource.IsCancellationRequested && _queue.TryDequeue(out var item))
            {
                lock (_queueLock)
                {
                    _queuedMediaFileIds.Remove(item.MediaFileId);
                }

                try
                {
                    await ProbeMediaFileCoreAsync(item.MediaFileId, item.Force, _disposeTokenSource.Token);
                }
                catch (OperationCanceledException) when (_disposeTokenSource.IsCancellationRequested)
                {
                    return;
                }
                catch
                {
                    // Background probing must never escape into scan, playback, or app shutdown flows.
                }
            }
        }
        finally
        {
            Interlocked.Exchange(ref _isWorkerRunning, 0);
            if (!_queue.IsEmpty)
            {
                StartWorkerIfNeeded();
            }
        }
    }

    private async Task ProbeMediaFileCoreAsync(
        int mediaFileId,
        bool force,
        CancellationToken cancellationToken)
    {
        var startState = await TryStartProbeAsync(mediaFileId, force, cancellationToken);
        if (startState is null)
        {
            return;
        }

        var ffprobe = ResolveFfprobePath();
        if (string.IsNullOrWhiteSpace(ffprobe.Path))
        {
            await MarkUnavailableAsync(startState, ffprobe.Error ?? "ffprobe unavailable", cancellationToken);
            return;
        }

        ProbeProcessResult processResult;
        try
        {
            processResult = await RunFfprobeAsync(ffprobe.Path, startState.Input, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            await MarkFailedAsync(startState, "ffprobe execution failed", cancellationToken);
            return;
        }

        if (!processResult.IsSuccess)
        {
            await MarkFailedAsync(startState, processResult.ErrorSummary, cancellationToken);
            return;
        }

        ProbeResult probeResult;
        try
        {
            probeResult = ParseProbeResult(processResult.Output);
        }
        catch (JsonException)
        {
            await MarkFailedAsync(startState, "ffprobe output parse failed", cancellationToken);
            return;
        }

        await MarkSuccessAsync(startState, probeResult, cancellationToken);
    }

    private static async Task<ProbeProcessResult> RunFfprobeAsync(
        string ffprobePath,
        string input,
        CancellationToken cancellationToken)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = ffprobePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        process.StartInfo.ArgumentList.Add("-v");
        process.StartInfo.ArgumentList.Add("error");
        process.StartInfo.ArgumentList.Add("-print_format");
        process.StartInfo.ArgumentList.Add("json");
        process.StartInfo.ArgumentList.Add("-show_format");
        process.StartInfo.ArgumentList.Add("-show_streams");
        process.StartInfo.ArgumentList.Add(input);

        if (!process.Start())
        {
            return ProbeProcessResult.Failed("ffprobe failed to start");
        }

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutTokenSource.CancelAfter(ProbeTimeout);

        try
        {
            await process.WaitForExitAsync(timeoutTokenSource.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            return ProbeProcessResult.Failed("ffprobe timed out");
        }

        var output = await outputTask;
        _ = await errorTask;

        return process.ExitCode == 0
            ? ProbeProcessResult.Success(output)
            : ProbeProcessResult.Failed($"ffprobe exited with code {process.ExitCode}");
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
    }

    private async Task<ProbeStartState?> TryStartProbeAsync(
        int mediaFileId,
        bool force,
        CancellationToken cancellationToken)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var mediaFile = await dbContext.MediaFiles
            .Include(x => x.SourceConnection)
            .FirstOrDefaultAsync(x => x.Id == mediaFileId, cancellationToken);

        if (mediaFile is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        if (mediaFile.MediaType != MediaType.Video)
        {
            mediaFile.MediaProbeStatus = MediaProbeStatus.Skipped;
            mediaFile.MediaProbeError = "not a video file";
            mediaFile.MediaProbedAt = now;
            mediaFile.UpdatedAt = now;
            await dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }

        if (!force && !ShouldProbe(mediaFile))
        {
            return null;
        }

        string input;
        try
        {
            input = BuildProbeInput(mediaFile);
        }
        catch
        {
            mediaFile.MediaProbeStatus = MediaProbeStatus.Failed;
            mediaFile.MediaProbeError = TrimError("probe input could not be built");
            mediaFile.MediaProbeAttemptCount++;
            mediaFile.MediaProbedAt = now;
            mediaFile.MediaProbeFileSize = mediaFile.FileSize;
            mediaFile.MediaProbeLastModifiedAt = mediaFile.LastModifiedAt;
            mediaFile.UpdatedAt = now;
            await dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }

        mediaFile.MediaProbeStatus = MediaProbeStatus.Pending;
        mediaFile.MediaProbeAttemptCount++;
        mediaFile.MediaProbeError = null;
        mediaFile.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ProbeStartState(
            mediaFile.Id,
            mediaFile.FileName,
            mediaFile.SourceConnectionId,
            mediaFile.SourceConnection?.ProtocolType ?? ProtocolType.WebDav,
            mediaFile.FileSize,
            mediaFile.LastModifiedAt,
            input);
    }

    private static bool ShouldProbe(MediaFile mediaFile)
    {
        var isCurrentProbeSnapshot =
            mediaFile.MediaProbeFileSize == mediaFile.FileSize
            && Nullable.Equals(mediaFile.MediaProbeLastModifiedAt, mediaFile.LastModifiedAt);

        if (mediaFile.MediaProbeStatus == MediaProbeStatus.Success && isCurrentProbeSnapshot)
        {
            return false;
        }

        return mediaFile.MediaProbeStatus != MediaProbeStatus.Failed
               || mediaFile.MediaProbeAttemptCount < MaxAutomaticAttempts
               || !isCurrentProbeSnapshot;
    }

    private static string BuildProbeInput(MediaFile mediaFile)
    {
        if (mediaFile.SourceConnection?.ProtocolType == ProtocolType.WebDav)
        {
            var playbackUrl = WebDavPathHelper.BuildPlaybackUrl(
                mediaFile.SourceConnection.BaseUrl,
                mediaFile.FilePath,
                mediaFile.RemoteUri);

            return BuildCredentialUri(
                    playbackUrl,
                    mediaFile.SourceConnection.Username,
                    SecretProtector.Unprotect(mediaFile.SourceConnection.PasswordEncrypted))
                .AbsoluteUri;
        }

        if (File.Exists(mediaFile.FilePath))
        {
            return mediaFile.FilePath;
        }

        throw new InvalidOperationException("Unsupported media source.");
    }

    private static Uri BuildCredentialUri(string playbackUrl, string username, string password)
    {
        var uriBuilder = new UriBuilder(playbackUrl);
        if (!string.IsNullOrWhiteSpace(username))
        {
            uriBuilder.UserName = username;
            uriBuilder.Password = password;
        }

        return uriBuilder.Uri;
    }

    private static FfprobeResolution ResolveFfprobePath()
    {
        var currentRid = NativeRuntimeResolver.GetCurrentRuntimeId();
        var expectedMachine = NativeRuntimeResolver.GetExpectedPeMachine();
        if (string.IsNullOrWhiteSpace(currentRid) || !expectedMachine.HasValue)
        {
            return FfprobeResolution.Unavailable("ffprobe unsupported process architecture");
        }

        var architectureCandidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "tools", "ffmpeg", currentRid, "ffprobe.exe"),
            Path.Combine(Environment.CurrentDirectory, "tools", "ffmpeg", currentRid, "ffprobe.exe")
        };

        foreach (var candidate in architectureCandidates)
        {
            var resolved = TryUseFfprobeCandidate(candidate, expectedMachine.Value);
            if (resolved.IsAvailable)
            {
                return resolved;
            }
        }

        var legacyCandidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "tools", "ffmpeg", "ffprobe.exe"),
            Path.Combine(Environment.CurrentDirectory, "tools", "ffmpeg", "ffprobe.exe")
        };

        foreach (var candidate in legacyCandidates)
        {
            var resolved = TryUseFfprobeCandidate(candidate, expectedMachine.Value);
            if (resolved.IsAvailable)
            {
                return resolved;
            }
        }

        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(pathValue))
        {
            foreach (var path in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = Path.Combine(path.Trim(), "ffprobe.exe");
                var resolved = TryUseFfprobeCandidate(candidate, expectedMachine.Value);
                if (resolved.IsAvailable)
                {
                    return resolved;
                }
            }
        }

        return FfprobeResolution.Unavailable($"ffprobe unavailable for current architecture: {currentRid}");
    }

    private static FfprobeResolution TryUseFfprobeCandidate(string candidate, ushort expectedMachine)
    {
        if (!File.Exists(candidate))
        {
            return FfprobeResolution.Unavailable("ffprobe candidate missing");
        }

        var machine = NativeRuntimeResolver.TryReadPeMachine(candidate);
        if (!machine.IsPe || !machine.IsValid || machine.Machine != expectedMachine)
        {
            return FfprobeResolution.Unavailable(
                $"ffprobe architecture mismatch: expected {NativeRuntimeResolver.FormatPeMachine(expectedMachine)}, actual {machine.Architecture}");
        }

        return FfprobeResolution.Available(candidate);
    }

    private static ProbeResult ParseProbeResult(string output)
    {
        using var document = JsonDocument.Parse(output);
        var root = document.RootElement;

        int? durationSeconds = null;
        int? overallBitrateKbps = null;
        if (root.TryGetProperty("format", out var format))
        {
            durationSeconds = ParseDurationSeconds(format);
            overallBitrateKbps = ParseBitrateKbps(format);
        }

        StreamProbeResult? video = null;
        StreamProbeResult? audio = null;
        if (root.TryGetProperty("streams", out var streams) && streams.ValueKind == JsonValueKind.Array)
        {
            foreach (var stream in streams.EnumerateArray())
            {
                var codecType = GetString(stream, "codec_type");
                if (video is null && string.Equals(codecType, "video", StringComparison.OrdinalIgnoreCase))
                {
                    video = ParseStream(stream);
                }
                else if (audio is null && string.Equals(codecType, "audio", StringComparison.OrdinalIgnoreCase))
                {
                    audio = ParseStream(stream);
                }

                if (video is not null && audio is not null)
                {
                    break;
                }
            }
        }

        return new ProbeResult(
            durationSeconds,
            video?.Width,
            video?.Height,
            NormalizeCodec(video?.CodecName),
            NormalizeCodec(audio?.CodecName),
            audio?.Channels,
            audio?.SampleRate,
            overallBitrateKbps,
            video?.BitrateKbps,
            audio?.BitrateKbps);
    }

    private static StreamProbeResult ParseStream(JsonElement stream)
    {
        return new StreamProbeResult(
            GetString(stream, "codec_name"),
            GetInt(stream, "width"),
            GetInt(stream, "height"),
            GetInt(stream, "channels"),
            GetInt(stream, "sample_rate"),
            ParseBitrateKbps(stream));
    }

    private static int? ParseDurationSeconds(JsonElement format)
    {
        var value = GetString(format, "duration");
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
            ? (int)Math.Round(seconds, MidpointRounding.AwayFromZero)
            : null;
    }

    private static int? ParseBitrateKbps(JsonElement element)
    {
        var value = GetString(element, "bit_rate");
        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bitsPerSecond)
            || bitsPerSecond <= 0)
        {
            return null;
        }

        return (int)Math.Min(int.MaxValue, Math.Round(bitsPerSecond / 1000d, MidpointRounding.AwayFromZero));
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            _ => null
        };
    }

    private static int? GetInt(JsonElement element, string propertyName)
    {
        var value = GetString(element, propertyName);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? parsed
            : null;
    }

    private static string? NormalizeCodec(string? codec)
    {
        if (string.IsNullOrWhiteSpace(codec))
        {
            return null;
        }

        return codec.Trim().Length > 80
            ? codec.Trim()[..80]
            : codec.Trim();
    }

    private async Task MarkSuccessAsync(
        ProbeStartState startState,
        ProbeResult result,
        CancellationToken cancellationToken)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var mediaFile = await dbContext.MediaFiles.FirstOrDefaultAsync(x => x.Id == startState.MediaFileId, cancellationToken);
        if (mediaFile is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        mediaFile.DurationSeconds = result.DurationSeconds;
        mediaFile.ResolutionWidth = result.ResolutionWidth;
        mediaFile.ResolutionHeight = result.ResolutionHeight;
        mediaFile.VideoCodec = result.VideoCodec;
        mediaFile.AudioCodec = result.AudioCodec;
        mediaFile.AudioChannels = result.AudioChannels;
        mediaFile.AudioSampleRate = result.AudioSampleRate;
        mediaFile.OverallBitrateKbps = result.OverallBitrateKbps;
        mediaFile.VideoBitrateKbps = result.VideoBitrateKbps;
        mediaFile.AudioBitrateKbps = result.AudioBitrateKbps;
        mediaFile.CodecInfo = BuildCodecInfo(result);
        mediaFile.MediaProbeStatus = MediaProbeStatus.Success;
        mediaFile.MediaProbeError = null;
        mediaFile.MediaProbedAt = now;
        mediaFile.MediaProbeFileSize = mediaFile.FileSize;
        mediaFile.MediaProbeLastModifiedAt = mediaFile.LastModifiedAt;
        mediaFile.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string? BuildCodecInfo(ProbeResult result)
    {
        var parts = new[] { result.VideoCodec, result.AudioCodec }
            .Where(part => !string.IsNullOrWhiteSpace(part));
        var value = string.Join(" / ", parts);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static async Task MarkUnavailableAsync(
        ProbeStartState startState,
        string error,
        CancellationToken cancellationToken)
    {
        await MarkProbeIssueAsync(startState, MediaProbeStatus.Unavailable, error, cancellationToken);
    }

    private static async Task MarkFailedAsync(
        ProbeStartState startState,
        string error,
        CancellationToken cancellationToken)
    {
        await MarkProbeIssueAsync(startState, MediaProbeStatus.Failed, error, cancellationToken);
    }

    private static async Task MarkProbeIssueAsync(
        ProbeStartState startState,
        MediaProbeStatus status,
        string error,
        CancellationToken cancellationToken)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var mediaFile = await dbContext.MediaFiles.FirstOrDefaultAsync(x => x.Id == startState.MediaFileId, cancellationToken);
        if (mediaFile is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        mediaFile.MediaProbeStatus = status;
        mediaFile.MediaProbeError = TrimError(error);
        mediaFile.MediaProbedAt = now;
        mediaFile.MediaProbeFileSize = mediaFile.FileSize;
        mediaFile.MediaProbeLastModifiedAt = mediaFile.LastModifiedAt;
        mediaFile.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string TrimError(string error)
    {
        var normalized = error
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();

        return normalized.Length <= MaxErrorLength
            ? normalized
            : normalized[..MaxErrorLength];
    }

    private sealed record ProbeQueueItem(int MediaFileId, bool Force);

    private sealed record ProbeStartState(
        int MediaFileId,
        string FileName,
        int SourceConnectionId,
        ProtocolType ProtocolType,
        long FileSize,
        DateTime? LastModifiedAt,
        string Input);

    private sealed record ProbeProcessResult(bool IsSuccess, string Output, string ErrorSummary)
    {
        public static ProbeProcessResult Success(string output)
        {
            return new ProbeProcessResult(true, output, string.Empty);
        }

        public static ProbeProcessResult Failed(string errorSummary)
        {
            return new ProbeProcessResult(false, string.Empty, TrimError(errorSummary));
        }
    }

    private sealed record FfprobeResolution(string? Path, string? Error)
    {
        public bool IsAvailable => !string.IsNullOrWhiteSpace(Path);

        public static FfprobeResolution Available(string path)
        {
            return new FfprobeResolution(path, null);
        }

        public static FfprobeResolution Unavailable(string error)
        {
            return new FfprobeResolution(null, error);
        }
    }

    private sealed record ProbeResult(
        int? DurationSeconds,
        int? ResolutionWidth,
        int? ResolutionHeight,
        string? VideoCodec,
        string? AudioCodec,
        int? AudioChannels,
        int? AudioSampleRate,
        int? OverallBitrateKbps,
        int? VideoBitrateKbps,
        int? AudioBitrateKbps);

    private sealed record StreamProbeResult(
        string? CodecName,
        int? Width,
        int? Height,
        int? Channels,
        int? SampleRate,
        int? BitrateKbps);
}
