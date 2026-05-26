using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface IOpenSubtitlesClientService
{
    IReadOnlyList<OpenSubtitlesLanguageOption> SupportedLanguages { get; }

    Task<OpenSubtitlesProbeResult> ProbeAsync(
        OpenSubtitlesClientOptions options,
        CancellationToken cancellationToken = default);

    Task<OpenSubtitlesSearchPage> SearchAsync(
        OpenSubtitlesClientOptions options,
        OpenSubtitlesSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<OpenSubtitlesDownloadContractResult> CheckDownloadContractAsync(
        OpenSubtitlesClientOptions options,
        OpenSubtitlesDownloadContractRequest request,
        CancellationToken cancellationToken = default);

    Task<OpenSubtitlesDownloadResult> DownloadAsync(
        OpenSubtitlesClientOptions options,
        OpenSubtitlesDownloadContractRequest request,
        CancellationToken cancellationToken = default);
}
