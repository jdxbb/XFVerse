using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using MediaLibrary.Core.Data;
using MediaLibrary.Core.Diagnostics;
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
    private const int DefaultDetailLazyProbeLimit = 10;
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(25);
    private static readonly TimeSpan RecentPendingThreshold = TimeSpan.FromMinutes(2);
    private readonly ConcurrentQueue<ProbeQueueItem> _queue = new();
    private readonly HashSet<int> _queuedMediaFileIds = [];
    private readonly object _queueLock = new();
    private readonly CancellationTokenSource _disposeTokenSource = new();
    private int _isWorkerRunning;
    private bool _disposed;

    public event EventHandler<MediaProbeStatusChangedEventArgs>? ProbeStatusChanged;

    public async Task<MediaProbeDetailLazyResult> EnqueueDetailSourcesAsync(
        IReadOnlyCollection<int> mediaFileIds,
        string contentKind,
        int contentId,
        int limit = DefaultDetailLazyProbeLimit,
        CancellationToken cancellationToken = default)
    {
        var normalizedContentKind = NormalizeContentKind(contentKind);
        var requestedIds = mediaFileIds
            .Where(id => id > 0)
            .Distinct()
            .ToArray();
        var effectiveLimit = Math.Clamp(limit, 0, DefaultDetailLazyProbeLimit);
        ScanIdentificationDiagnostics.Write(
            $"event=media-probe-detail-lazy-check-started contentKind={normalizedContentKind} contentId={contentId} sourceCount={requestedIds.Length} limit={effectiveLimit}");

        if (requestedIds.Length == 0 || effectiveLimit == 0 || cancellationToken.IsCancellationRequested || _disposed)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=media-probe-detail-lazy-skipped contentKind={normalizedContentKind} contentId={contentId} sourceCount={requestedIds.Length} skippedReason={ScanIdentificationDiagnostics.FormatValue(requestedIds.Length == 0 ? "no-sources" : "probe-unavailable")} limit={effectiveLimit}");
            return new MediaProbeDetailLazyResult(
                requestedIds.Length,
                0,
                0,
                requestedIds.Length,
                effectiveLimit,
                Array.Empty<int>());
        }

        var candidateResult = await LoadDetailLazyProbeCandidatesAsync(
            requestedIds,
            normalizedContentKind,
            contentId,
            effectiveLimit,
            cancellationToken);
        ScanIdentificationDiagnostics.Write(
            $"event=media-probe-detail-lazy-candidates contentKind={normalizedContentKind} contentId={contentId} sourceCount={candidateResult.SourceCount} candidateCount={candidateResult.CandidateIds.Count} skippedCount={candidateResult.SkippedCount} protocolLocalCount={candidateResult.LocalCandidateCount} protocolWebDavCount={candidateResult.WebDavCandidateCount} limit={effectiveLimit} skippedReasons={ScanIdentificationDiagnostics.FormatValue(candidateResult.SkippedReasonsText)}");

        if (candidateResult.CandidateIds.Count == 0)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=media-probe-detail-lazy-skipped contentKind={normalizedContentKind} contentId={contentId} sourceCount={candidateResult.SourceCount} candidateCount=0 skippedReason={ScanIdentificationDiagnostics.FormatValue("no-candidates")} limit={effectiveLimit}");
            return new MediaProbeDetailLazyResult(
                candidateResult.SourceCount,
                0,
                0,
                candidateResult.SkippedCount,
                effectiveLimit,
                Array.Empty<int>());
        }

        var enqueueResult = EnqueueMediaFilesCore(candidateResult.CandidateIds, force: false, cancellationToken);
        await WriteProbeQueueSummaryAsync(
            enqueueResult.RequestedCount,
            enqueueResult.EnqueuedIds,
            enqueueResult.DuplicateCount,
            cancellationToken);
        ScanIdentificationDiagnostics.Write(
            $"event=media-probe-detail-lazy-queued contentKind={normalizedContentKind} contentId={contentId} sourceCount={candidateResult.SourceCount} candidateCount={candidateResult.CandidateIds.Count} queuedCount={enqueueResult.EnqueuedIds.Count} duplicateQueuedCount={enqueueResult.DuplicateCount} limit={effectiveLimit}");
        StartWorkerIfNeeded();
        return new MediaProbeDetailLazyResult(
            candidateResult.SourceCount,
            candidateResult.CandidateIds.Count,
            enqueueResult.EnqueuedIds.Count,
            candidateResult.SkippedCount,
            effectiveLimit,
            candidateResult.CandidateIds);
    }

    public async Task EnqueueMediaFilesAsync(
        IReadOnlyCollection<int> mediaFileIds,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        var enqueueResult = EnqueueMediaFilesCore(mediaFileIds, force, cancellationToken);
        await WriteProbeQueueSummaryAsync(
            enqueueResult.RequestedCount,
            enqueueResult.EnqueuedIds,
            enqueueResult.DuplicateCount,
            cancellationToken);
        StartWorkerIfNeeded();
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

    private ProbeEnqueueResult EnqueueMediaFilesCore(
        IReadOnlyCollection<int> mediaFileIds,
        bool force,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested || mediaFileIds.Count == 0 || _disposed)
        {
            return new ProbeEnqueueResult(0, [], 0);
        }

        var requestedIds = mediaFileIds
            .Where(id => id > 0)
            .Distinct()
            .ToArray();
        var enqueuedIds = new List<int>(requestedIds.Length);
        var duplicateCount = 0;
        foreach (var mediaFileId in requestedIds)
        {
            lock (_queueLock)
            {
                if (!_queuedMediaFileIds.Add(mediaFileId))
                {
                    duplicateCount++;
                    continue;
                }

                _queue.Enqueue(new ProbeQueueItem(mediaFileId, force));
                enqueuedIds.Add(mediaFileId);
            }
        }

        return new ProbeEnqueueResult(requestedIds.Length, enqueuedIds, duplicateCount);
    }

    private static async Task<DetailLazyProbeCandidateResult> LoadDetailLazyProbeCandidatesAsync(
        IReadOnlyCollection<int> requestedIds,
        string contentKind,
        int contentId,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var mediaFiles = await dbContext.MediaFiles
            .AsNoTracking()
            .Include(x => x.SourceConnection)
            .Where(x => requestedIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
        var filesById = mediaFiles.ToDictionary(x => x.Id);
        var candidateRows = new List<MediaFile>(Math.Min(limit, requestedIds.Count));
        var skippedReasons = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var limitSkippedCount = 0;
        var now = DateTime.UtcNow;

        foreach (var mediaFileId in requestedIds)
        {
            if (!filesById.TryGetValue(mediaFileId, out var mediaFile))
            {
                AddSkippedReason(skippedReasons, "missing-media-file");
                continue;
            }

            var skippedReason = GetDetailLazyProbeSkippedReason(mediaFile, contentKind, contentId, now);
            if (!string.IsNullOrWhiteSpace(skippedReason))
            {
                AddSkippedReason(skippedReasons, skippedReason);
                continue;
            }

            if (candidateRows.Count >= limit)
            {
                limitSkippedCount++;
                AddSkippedReason(skippedReasons, "limit");
                continue;
            }

            candidateRows.Add(mediaFile);
        }

        return new DetailLazyProbeCandidateResult(
            requestedIds.Count,
            candidateRows.Select(x => x.Id).ToArray(),
            requestedIds.Count - candidateRows.Count,
            candidateRows.Count(x => x.SourceConnection?.ProtocolType == ProtocolType.Local),
            candidateRows.Count(x => x.SourceConnection?.ProtocolType != ProtocolType.Local),
            limitSkippedCount,
            FormatSkippedReasons(skippedReasons));
    }

    private static string? GetDetailLazyProbeSkippedReason(
        MediaFile mediaFile,
        string contentKind,
        int contentId,
        DateTime now)
    {
        if (mediaFile.IsDeleted)
        {
            return "deleted";
        }

        if (mediaFile.MediaType != MediaType.Video)
        {
            return "not-video";
        }

        if (MediaFileRules.IsIgnoredSystemFile(mediaFile.FileName))
        {
            return "ignored-system-file";
        }

        if (contentKind == "movie" && mediaFile.MovieId != contentId)
        {
            return "not-current-movie-source";
        }

        if (contentKind == "episode" && mediaFile.EpisodeId != contentId)
        {
            return "not-current-episode-source";
        }

        if (!HasProbeInputInformation(mediaFile))
        {
            return "missing-probe-input";
        }

        if (IsRecentlyPending(mediaFile, now))
        {
            return "pending";
        }

        return ShouldProbe(mediaFile) ? null : "current-or-attempt-limit";
    }

    private static bool HasProbeInputInformation(MediaFile mediaFile)
    {
        if (mediaFile.SourceConnection is null)
        {
            return false;
        }

        if (mediaFile.SourceConnection.ProtocolType == ProtocolType.Local)
        {
            return !string.IsNullOrWhiteSpace(mediaFile.FilePath);
        }

        return !string.IsNullOrWhiteSpace(mediaFile.FilePath)
               || !string.IsNullOrWhiteSpace(mediaFile.RemoteUri);
    }

    private static bool IsRecentlyPending(MediaFile mediaFile, DateTime now)
    {
        return mediaFile.MediaProbeStatus == MediaProbeStatus.Pending
               && now - mediaFile.UpdatedAt <= RecentPendingThreshold;
    }

    private static void AddSkippedReason(IDictionary<string, int> skippedReasons, string reason)
    {
        skippedReasons.TryGetValue(reason, out var count);
        skippedReasons[reason] = count + 1;
    }

    private static string FormatSkippedReasons(IReadOnlyDictionary<string, int> skippedReasons)
    {
        return skippedReasons.Count == 0
            ? "(none)"
            : string.Join(
                "|",
                skippedReasons
                    .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(x => $"{x.Key}:{x.Value}"));
    }

    private static string NormalizeContentKind(string contentKind)
    {
        return string.Equals(contentKind, "episode", StringComparison.OrdinalIgnoreCase)
            ? "episode"
            : "movie";
    }

    private static async Task WriteProbeQueueSummaryAsync(
        int requestedCount,
        IReadOnlyCollection<int> enqueuedIds,
        int duplicateCount,
        CancellationToken cancellationToken)
    {
        try
        {
            var rows = enqueuedIds.Count == 0
                ? []
                : await LoadProbeQueueRowsAsync(enqueuedIds, cancellationToken);
            var movieSourceCount = rows.Count(x => x.MovieId.HasValue);
            var episodeSourceCount = rows.Count(x => x.EpisodeId.HasValue);
            var orphanSourceCount = rows.Count(x => !x.MovieId.HasValue && !x.EpisodeId.HasValue);
            var webDavCount = rows.Count(x => x.ProtocolType == ProtocolType.WebDav);
            var localCount = rows.Count(x => x.ProtocolType == ProtocolType.Local);

            ScanIdentificationDiagnostics.Write(
                $"event=media-probe-queued mediaProbeCandidateCount={requestedCount} mediaProbeMovieSourceCount={movieSourceCount} mediaProbeEpisodeSourceCount={episodeSourceCount} mediaProbeOrphanSourceCount={orphanSourceCount} mediaProbeWebDavCount={webDavCount} mediaProbeLocalCount={localCount} mediaProbeEnqueuedCount={rows.Count} mediaProbeDuplicateQueuedCount={duplicateCount}");
        }
        catch
        {
            // Probe diagnostics are best-effort and must not affect scanning or playback.
        }
    }

    private static async Task<IReadOnlyList<ProbeQueueRow>> LoadProbeQueueRowsAsync(
        IReadOnlyCollection<int> enqueuedIds,
        CancellationToken cancellationToken)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        return await dbContext.MediaFiles
            .AsNoTracking()
            .Where(x => enqueuedIds.Contains(x.Id))
            .Select(
                x => new ProbeQueueRow(
                    x.MovieId,
                    x.EpisodeId,
                    x.SourceConnection == null ? ProtocolType.WebDav : x.SourceConnection.ProtocolType))
            .ToListAsync(cancellationToken);
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
        WriteProbeQueueAbandoned("service-disposed");
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
                    WriteProbeCanceled(item.MediaFileId, "service-disposed");
                    return;
                }
                catch (Exception exception)
                {
                    WriteProbeWorkerException(item.MediaFileId, exception);
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            throw;
        }

        string output;
        try
        {
            output = await outputTask;
            _ = await errorTask;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            throw;
        }

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
            ScanIdentificationDiagnostics.Write(
                $"event=media-probe-skipped mediaFileId={mediaFileId} mediaFileKind=missing protocolType=unknown probeSkippedReason=missing-media-file");
            return null;
        }

        var now = DateTime.UtcNow;
        if (mediaFile.IsDeleted)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=probe-skipped-deleted-mediafile mediaFileId={mediaFile.Id} mediaFileKind={ResolveMediaFileKind(mediaFile)} protocolType={FormatProtocol(mediaFile.SourceConnection?.ProtocolType ?? ProtocolType.WebDav)} file={ScanIdentificationDiagnostics.FormatFileNameFingerprint(mediaFile.FileName)}");
            WriteProbeSkipped(mediaFile, "deleted-media-file");
            return null;
        }

        if (mediaFile.MediaType != MediaType.Video)
        {
            mediaFile.MediaProbeStatus = MediaProbeStatus.Skipped;
            mediaFile.MediaProbeError = "not a video file";
            mediaFile.MediaProbedAt = now;
            mediaFile.UpdatedAt = now;
            await dbContext.SaveChangesAsync(cancellationToken);
            WriteProbeSkipped(mediaFile, "not-video");
            NotifyProbeStatusChanged(mediaFile, MediaProbeStatus.Skipped);
            return null;
        }

        if (!force && !ShouldProbe(mediaFile))
        {
            WriteProbeSkipped(mediaFile, "snapshot-current-or-attempt-limit");
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
            WriteProbeIssue(mediaFile, MediaProbeStatus.Failed, "probe-input-build-failed");
            NotifyProbeStatusChanged(mediaFile, MediaProbeStatus.Failed);
            return null;
        }

        mediaFile.MediaProbeStatus = MediaProbeStatus.Pending;
        mediaFile.MediaProbeAttemptCount++;
        mediaFile.MediaProbeError = null;
        mediaFile.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        WriteProbeStarted(mediaFile);
        NotifyProbeStatusChanged(mediaFile, MediaProbeStatus.Pending);

        return new ProbeStartState(
            mediaFile.Id,
            mediaFile.FileName,
            mediaFile.SourceConnectionId,
            mediaFile.SourceConnection?.ProtocolType ?? ProtocolType.WebDav,
            ResolveMediaFileKind(mediaFile),
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

    private static void WriteProbeStarted(MediaFile mediaFile)
    {
        ScanIdentificationDiagnostics.Write(
            $"event=media-probe-started mediaFileId={mediaFile.Id} mediaFileKind={ResolveMediaFileKind(mediaFile)} protocolType={FormatProtocol(mediaFile.SourceConnection?.ProtocolType ?? ProtocolType.WebDav)} file={ScanIdentificationDiagnostics.FormatFileNameFingerprint(mediaFile.FileName)} attempt={mediaFile.MediaProbeAttemptCount} probeStarted=true");
    }

    private static void WriteProbeSkipped(MediaFile mediaFile, string reason)
    {
        ScanIdentificationDiagnostics.Write(
            $"event=media-probe-skipped mediaFileId={mediaFile.Id} mediaFileKind={ResolveMediaFileKind(mediaFile)} protocolType={FormatProtocol(mediaFile.SourceConnection?.ProtocolType ?? ProtocolType.WebDav)} file={ScanIdentificationDiagnostics.FormatFileNameFingerprint(mediaFile.FileName)} probeSkippedReason={ScanIdentificationDiagnostics.FormatValue(reason)} status={mediaFile.MediaProbeStatus} attempts={mediaFile.MediaProbeAttemptCount}");
    }

    private static void WriteProbeSucceeded(ProbeStartState startState, ProbeResult result)
    {
        var hasDuration = result.DurationSeconds.HasValue.ToString().ToLowerInvariant();
        var hasResolution = (result.ResolutionWidth.HasValue && result.ResolutionHeight.HasValue)
            .ToString()
            .ToLowerInvariant();
        var hasBitrate = result.OverallBitrateKbps.HasValue.ToString().ToLowerInvariant();
        ScanIdentificationDiagnostics.Write(
            $"event=media-probe-succeeded mediaFileId={startState.MediaFileId} mediaFileKind={startState.MediaFileKind} protocolType={FormatProtocol(startState.ProtocolType)} file={ScanIdentificationDiagnostics.FormatFileNameFingerprint(startState.FileName)} probeSucceeded=true hasDuration={hasDuration} hasResolution={hasResolution} hasBitrate={hasBitrate}");
    }

    private static void WriteProbeIssue(MediaFile mediaFile, MediaProbeStatus status, string reason)
    {
        ScanIdentificationDiagnostics.Write(
            $"event=media-probe-failed mediaFileId={mediaFile.Id} mediaFileKind={ResolveMediaFileKind(mediaFile)} protocolType={FormatProtocol(mediaFile.SourceConnection?.ProtocolType ?? ProtocolType.WebDav)} file={ScanIdentificationDiagnostics.FormatFileNameFingerprint(mediaFile.FileName)} probeStatus={status} probeFailedReason={ScanIdentificationDiagnostics.FormatValue(reason, 220)}");
    }

    private static void WriteProbeIssue(ProbeStartState startState, MediaProbeStatus status, string reason)
    {
        ScanIdentificationDiagnostics.Write(
            $"event=media-probe-failed mediaFileId={startState.MediaFileId} mediaFileKind={startState.MediaFileKind} protocolType={FormatProtocol(startState.ProtocolType)} file={ScanIdentificationDiagnostics.FormatFileNameFingerprint(startState.FileName)} probeStatus={status} probeFailedReason={ScanIdentificationDiagnostics.FormatValue(reason, 220)}");
    }

    private void WriteProbeQueueAbandoned(string reason)
    {
        int abandonedCount;
        lock (_queueLock)
        {
            abandonedCount = _queue.Count;
        }

        if (abandonedCount <= 0)
        {
            return;
        }

        ScanIdentificationDiagnostics.Write(
            $"event=media-probe-abandoned queuedCount={abandonedCount} reason={ScanIdentificationDiagnostics.FormatValue(reason)}");
    }

    private static void WriteProbeCanceled(int mediaFileId, string reason)
    {
        ScanIdentificationDiagnostics.Write(
            $"event=media-probe-canceled mediaFileId={mediaFileId} reason={ScanIdentificationDiagnostics.FormatValue(reason)}");
    }

    private static void WriteProbeWorkerException(int mediaFileId, Exception exception)
    {
        ScanIdentificationDiagnostics.Write(
            $"event=media-probe-failed mediaFileId={mediaFileId} mediaFileKind=unknown protocolType=unknown probeStatus=Failed probeFailedReason={ScanIdentificationDiagnostics.FormatValue($"worker-exception:{exception.GetType().Name}", 120)}");
    }

    private static string ResolveMediaFileKind(MediaFile mediaFile)
    {
        if (mediaFile.EpisodeId.HasValue)
        {
            return "episode";
        }

        return mediaFile.MovieId.HasValue ? "movie" : "orphan";
    }

    private static string FormatProtocol(ProtocolType protocolType)
    {
        return protocolType == ProtocolType.Local ? "local" : "webdav";
    }

    private void NotifyProbeStatusChanged(MediaFile mediaFile, MediaProbeStatus status)
    {
        NotifyProbeStatusChanged(
            mediaFile.Id,
            status,
            ResolveMediaFileKind(mediaFile),
            mediaFile.SourceConnection?.ProtocolType ?? ProtocolType.WebDav);
    }

    private void NotifyProbeStatusChanged(ProbeStartState startState, MediaProbeStatus status)
    {
        NotifyProbeStatusChanged(
            startState.MediaFileId,
            status,
            startState.MediaFileKind,
            startState.ProtocolType);
    }

    private void NotifyProbeStatusChanged(
        int mediaFileId,
        MediaProbeStatus status,
        string mediaFileKind,
        ProtocolType protocolType)
    {
        var handler = ProbeStatusChanged;
        if (handler is null)
        {
            return;
        }

        try
        {
            handler(
                this,
                new MediaProbeStatusChangedEventArgs(mediaFileId, status, mediaFileKind, protocolType));
        }
        catch
        {
            // Probe status notifications are UI refresh hints and must not affect background probing.
        }
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
        WriteProbeSucceeded(startState, result);
        NotifyProbeStatusChanged(startState, MediaProbeStatus.Success);
    }

    private static string? BuildCodecInfo(ProbeResult result)
    {
        var parts = new[] { result.VideoCodec, result.AudioCodec }
            .Where(part => !string.IsNullOrWhiteSpace(part));
        var value = string.Join(" / ", parts);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private async Task MarkUnavailableAsync(
        ProbeStartState startState,
        string error,
        CancellationToken cancellationToken)
    {
        await MarkProbeIssueAsync(startState, MediaProbeStatus.Unavailable, error, cancellationToken);
    }

    private async Task MarkFailedAsync(
        ProbeStartState startState,
        string error,
        CancellationToken cancellationToken)
    {
        await MarkProbeIssueAsync(startState, MediaProbeStatus.Failed, error, cancellationToken);
    }

    private async Task MarkProbeIssueAsync(
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
        WriteProbeIssue(startState, status, error);
        NotifyProbeStatusChanged(startState, status);
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

    private sealed record ProbeEnqueueResult(
        int RequestedCount,
        IReadOnlyList<int> EnqueuedIds,
        int DuplicateCount);

    private sealed record ProbeQueueRow(int? MovieId, int? EpisodeId, ProtocolType ProtocolType);

    private sealed record DetailLazyProbeCandidateResult(
        int SourceCount,
        IReadOnlyList<int> CandidateIds,
        int SkippedCount,
        int LocalCandidateCount,
        int WebDavCandidateCount,
        int LimitSkippedCount,
        string SkippedReasonsText);

    private sealed record ProbeStartState(
        int MediaFileId,
        string FileName,
        int SourceConnectionId,
        ProtocolType ProtocolType,
        string MediaFileKind,
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
