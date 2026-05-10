using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class AiService : IAiService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(45)
    };

    private readonly ISettingsService _settingsService;

    public AiService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task<string?> GenerateTextAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.GetApplicationSettingAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(settings.AiBaseUrl)
            || string.IsNullOrWhiteSpace(settings.AiApiKey)
            || string.IsNullOrWhiteSpace(settings.AiModel))
        {
            return null;
        }

        var baseUrl = settings.AiBaseUrl.TrimEnd('/');
        var endpoint = baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
            ? baseUrl + "/chat/completions"
            : baseUrl + "/v1/chat/completions";
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.AiApiKey);

        var payload = new
        {
            model = settings.AiModel,
            temperature = 0.4,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            }
        };

        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await HttpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();
    }
}
