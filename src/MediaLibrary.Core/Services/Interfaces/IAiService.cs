namespace MediaLibrary.Core.Services.Interfaces;

public interface IAiService
{
    Task<string?> GenerateTextAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default);
}
