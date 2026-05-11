using MediaLibrary.Core.Models.Settings;

namespace MediaLibrary.Core.Services.Interfaces;

public interface IAiService
{
    Task<string?> GenerateTextAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default);

    Task<string?> GenerateTextAsync(
        string systemPrompt,
        string userPrompt,
        AiRequestOptions? options,
        CancellationToken cancellationToken = default);
}
