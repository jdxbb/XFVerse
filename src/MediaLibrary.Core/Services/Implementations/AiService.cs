using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MediaLibrary.Core.Diagnostics;
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
        var route = settings.AiRouting.ResolveRoute(options?.RequestKind);
        var configuredModel = string.IsNullOrWhiteSpace(route?.Model)
            ? settings.AiModel
            : route!.Model;
        var modelResolution = ResolveModel(configuredModel, settings.AiModel, options, isDeepSeek, route is not null);
        var model = modelResolution.ResolvedModel;
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
        LogRouting(options, modelResolution, isDeepSeek, thinkingEnabled);
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
        timeoutCts.CancelAfter(ResolveTimeout(route, options));
        using var response = await HttpClient.SendAsync(request, timeoutCts.Token);
        if (!response.IsSuccessStatusCode)
        {
            throw new AiRequestException(
                response.StatusCode,
                response.ReasonPhrase,
                ResolveRetryAfter(response));
        }

        await using var stream = await response.Content.ReadAsStreamAsync(timeoutCts.Token);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: timeoutCts.Token);
        return document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();
    }

    private static ModelResolution ResolveModel(
        string configuredModel,
        string defaultModel,
        AiRequestOptions? options,
        bool isDeepSeek,
        bool hasConfiguredRoute)
    {
        var model = configuredModel.Trim();
        if (hasConfiguredRoute)
        {
            var resolved = isDeepSeek ? NormalizeDeepSeekLegacyModel(model) : model;
            var reason = isDeepSeek && IsDeepSeekLegacyModel(model)
                ? "legacy-model-migrated-to-flash"
                : FirstNonEmpty(options?.OverrideReason, "configured-route-model");
            return new ModelResolution(defaultModel.Trim(), resolved, reason);
        }

        if (isDeepSeek && !string.IsNullOrWhiteSpace(options?.DeepSeekModelOverride))
        {
            return new ModelResolution(
                model,
                NormalizeDeepSeekLegacyModel(options.DeepSeekModelOverride.Trim()),
                FirstNonEmpty(options.OverrideReason, "deepseek-model-override"));
        }

        if (!isDeepSeek)
        {
            return new ModelResolution(model, model, "configured-model");
        }

        return IsDeepSeekLegacyModel(model)
            ? new ModelResolution(model, "deepseek-v4-flash", "legacy-model-migrated-to-flash")
            : new ModelResolution(model, model, "configured-model");
    }

    private static TimeSpan ResolveTimeout(AiModelRoutingSettings.AiModelRoute? route, AiRequestOptions? options)
    {
        if (route?.TimeoutSeconds is > 0)
        {
            return TimeSpan.FromSeconds(route.TimeoutSeconds);
        }

        return options?.Timeout ?? DefaultTimeout;
    }

    private static bool IsDeepSeekLegacyModel(string model)
    {
        return string.Equals(model, "deepseek-chat", StringComparison.OrdinalIgnoreCase)
               || string.Equals(model, "deepseek-reasoner", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeDeepSeekLegacyModel(string model)
    {
        return IsDeepSeekLegacyModel(model) ? "deepseek-v4-flash" : model;
    }

    private static void LogRouting(
        AiRequestOptions? options,
        ModelResolution modelResolution,
        bool isDeepSeek,
        bool thinkingEnabled)
    {
        var provider = isDeepSeek ? "deepseek" : "custom";
        var purpose = FirstNonEmpty(options?.RequestKind, "default");
        var reasoningMode = thinkingEnabled
            ? FirstNonEmpty(options?.ReasoningEffort, "default")
            : "off";
        AiPerfDiagnostics.WriteEvent(
            $"event=ai-model-routing provider={FormatLogValue(provider)} purpose={FormatLogValue(purpose)} requestedModel={FormatLogValue(modelResolution.RequestedModel)} resolvedModel={FormatLogValue(modelResolution.ResolvedModel)} thinkingEnabled={thinkingEnabled.ToString().ToLowerInvariant()} reasoningMode={FormatLogValue(reasoningMode)} overrideReason={FormatLogValue(modelResolution.OverrideReason)}");
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private static string FormatLogValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "\"\"";
        }

        return "\"" + value.Trim().Replace("\\", "\\\\").Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    private static bool IsDeepSeekEndpoint(string baseUrl)
    {
        return Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
            && uri.Host.Contains("deepseek", StringComparison.OrdinalIgnoreCase);
    }

    private static TimeSpan? ResolveRetryAfter(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter is null)
        {
            return null;
        }

        if (retryAfter.Delta.HasValue)
        {
            return retryAfter.Delta.Value;
        }

        if (retryAfter.Date.HasValue)
        {
            var delay = retryAfter.Date.Value - DateTimeOffset.UtcNow;
            return delay > TimeSpan.Zero ? delay : null;
        }

        return null;
    }

    private sealed record ModelResolution(string RequestedModel, string ResolvedModel, string OverrideReason);
}
