using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MediaLibrary.Core.Models.Settings;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class AiService : IAiService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = Timeout.InfiniteTimeSpan
    };

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(45);

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
        return await GenerateTextAsync(systemPrompt, userPrompt, options: null, cancellationToken);
    }

    public async Task<string?> GenerateTextAsync(
        string systemPrompt,
        string userPrompt,
        AiRequestOptions? options,
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
        var isDeepSeek = IsDeepSeekEndpoint(baseUrl);
        var model = ResolveModel(settings.AiModel, options, isDeepSeek);
        var endpoint = baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
            ? baseUrl + "/chat/completions"
            : baseUrl + "/v1/chat/completions";
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.AiApiKey);

        var payload = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["messages"] = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            }
        };

        var thinkingEnabled = isDeepSeek && options?.ThinkingEnabled == true;
        if (thinkingEnabled)
        {
            payload["thinking"] = new { type = "enabled" };
            if (!string.IsNullOrWhiteSpace(options?.ReasoningEffort))
            {
                payload["reasoning_effort"] = options.ReasoningEffort.Trim();
            }
        }
        else
        {
            payload["temperature"] = options?.Temperature ?? 0.4;
        }

        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(options?.Timeout ?? DefaultTimeout);
        using var response = await HttpClient.SendAsync(request, timeoutCts.Token);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(timeoutCts.Token);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: timeoutCts.Token);
        return document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();
    }

    private static string ResolveModel(string configuredModel, AiRequestOptions? options, bool isDeepSeek)
    {
        if (isDeepSeek && !string.IsNullOrWhiteSpace(options?.DeepSeekModelOverride))
        {
            return options.DeepSeekModelOverride.Trim();
        }

        var model = configuredModel.Trim();
        if (!isDeepSeek)
        {
            return model;
        }

        return model switch
        {
            "deepseek-chat" => "deepseek-v4-flash",
            "deepseek-reasoner" => "deepseek-v4-flash",
            _ => model
        };
    }

    private static bool IsDeepSeekEndpoint(string baseUrl)
    {
        return Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
               && uri.Host.Contains("deepseek", StringComparison.OrdinalIgnoreCase);
    }
}
