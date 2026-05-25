using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MediaLibrary.Core.Diagnostics;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class OpenSubtitlesClientService : IOpenSubtitlesClientService
{
    private const string DefaultEndpoint = "https://api.opensubtitles.com/api/v1";
    private const string UserAgent = "XFVerse/5.1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    public IReadOnlyList<OpenSubtitlesLanguageOption> SupportedLanguages { get; } =
    [
        new("ab", "Abkhazian"),
        new("af", "Afrikaans"),
        new("sq", "Albanian"),
        new("am", "Amharic"),
        new("ar", "Arabic"),
        new("an", "Aragonese"),
        new("hy", "Armenian"),
        new("as", "Assamese"),
        new("at", "Asturian"),
        new("az-az", "Azerbaijani"),
        new("eu", "Basque"),
        new("be", "Belarusian"),
        new("bn", "Bengali"),
        new("bs", "Bosnian"),
        new("br", "Breton"),
        new("bg", "Bulgarian"),
        new("my", "Burmese"),
        new("ca", "Catalan"),
        new("ze", "Chinese bilingual"),
        new("zh-ca", "Chinese (Cantonese)"),
        new("zh-cn", "Chinese (simplified)"),
        new("zh-tw", "Chinese (traditional)"),
        new("hr", "Croatian"),
        new("cs", "Czech"),
        new("da", "Danish"),
        new("pr", "Dari"),
        new("nl", "Dutch"),
        new("en", "English"),
        new("eo", "Esperanto"),
        new("et", "Estonian"),
        new("ex", "Extremaduran"),
        new("fi", "Finnish"),
        new("fr", "French"),
        new("gd", "Gaelic"),
        new("gl", "Galician"),
        new("ka", "Georgian"),
        new("de", "German"),
        new("el", "Greek"),
        new("he", "Hebrew"),
        new("hi", "Hindi"),
        new("hu", "Hungarian"),
        new("is", "Icelandic"),
        new("ig", "Igbo"),
        new("id", "Indonesian"),
        new("ia", "Interlingua"),
        new("ga", "Irish"),
        new("it", "Italian"),
        new("ja", "Japanese"),
        new("kn", "Kannada"),
        new("kk", "Kazakh"),
        new("km", "Khmer"),
        new("ko", "Korean"),
        new("ku", "Kurdish"),
        new("lv", "Latvian"),
        new("lt", "Lithuanian"),
        new("lb", "Luxembourgish"),
        new("mk", "Macedonian"),
        new("ms", "Malay"),
        new("ml", "Malayalam"),
        new("ma", "Manipuri"),
        new("mr", "Marathi"),
        new("mn", "Mongolian"),
        new("me", "Montenegrin"),
        new("nv", "Navajo"),
        new("ne", "Nepali"),
        new("se", "Northern Sami"),
        new("no", "Norwegian"),
        new("oc", "Occitan"),
        new("or", "Odia"),
        new("fa", "Persian"),
        new("pl", "Polish"),
        new("pt-pt", "Portuguese"),
        new("pt-br", "Portuguese (BR)"),
        new("pm", "Portuguese (MZ)"),
        new("ps", "Pushto"),
        new("ro", "Romanian"),
        new("ru", "Russian"),
        new("sx", "Santali"),
        new("sr", "Serbian"),
        new("sd", "Sindhi"),
        new("si", "Sinhalese"),
        new("sk", "Slovak"),
        new("sl", "Slovenian"),
        new("so", "Somali"),
        new("az-zb", "South Azerbaijani"),
        new("es", "Spanish"),
        new("sp", "Spanish (EU)"),
        new("ea", "Spanish (LA)"),
        new("sw", "Swahili"),
        new("sv", "Swedish"),
        new("sy", "Syriac"),
        new("tl", "Tagalog"),
        new("ta", "Tamil"),
        new("tt", "Tatar"),
        new("te", "Telugu"),
        new("tm-td", "Tetum"),
        new("th", "Thai"),
        new("tp", "Toki Pona"),
        new("tr", "Turkish"),
        new("tk", "Turkmen"),
        new("uk", "Ukrainian"),
        new("ur", "Urdu"),
        new("uz", "Uzbek"),
        new("vi", "Vietnamese"),
        new("cy", "Welsh")
    ];

    public async Task<OpenSubtitlesProbeResult> ProbeAsync(
        OpenSubtitlesClientOptions options,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return new OpenSubtitlesProbeResult
            {
                IsApiKeyConfigured = false,
                Message = "OpenSubtitles API key is not configured.",
                ErrorKind = OpenSubtitlesErrorKind.NotConfigured
            };
        }

        string token = options.Token;
        var loginAttempted = false;
        var loginSucceeded = false;
        try
        {
            using var searchResponse = await SendAsync(
                options,
                HttpMethod.Get,
                "subtitles?languages=en&query=contract&page=1",
                token,
                body: null,
                "opensubtitles-api-key-probe",
                cancellationToken);

            if (!searchResponse.IsSuccessStatusCode)
            {
                return new OpenSubtitlesProbeResult
                {
                    IsApiKeyConfigured = true,
                    IsApiKeyAccepted = false,
                    Message = BuildFailureMessage(searchResponse.StatusCode),
                    ErrorKind = MapStatus(searchResponse.StatusCode)
                };
            }

            if (!string.IsNullOrWhiteSpace(options.Username) && !string.IsNullOrWhiteSpace(options.Password))
            {
                loginAttempted = true;
                var loginResult = await LoginAsync(options, cancellationToken);
                token = loginResult.Token;
                loginSucceeded = loginResult.Succeeded;
                if (!loginSucceeded)
                {
                    return new OpenSubtitlesProbeResult
                    {
                        IsApiKeyConfigured = true,
                        IsApiKeyAccepted = true,
                        LoginAttempted = true,
                        LoginSucceeded = false,
                        Message = loginResult.Message,
                        ErrorKind = loginResult.ErrorKind
                    };
                }
            }

            var quota = await TryProbeQuotaAsync(options, token, cancellationToken);
            return new OpenSubtitlesProbeResult
            {
                IsApiKeyConfigured = true,
                IsApiKeyAccepted = true,
                LoginAttempted = loginAttempted,
                LoginSucceeded = loginSucceeded,
                Token = token,
                QuotaProbeAttempted = quota.Attempted,
                QuotaProbeSucceeded = quota.Succeeded,
                RemainingDownloads = quota.RemainingDownloads,
                AllowedDownloads = quota.AllowedDownloads,
                Message = quota.Succeeded
                    ? "OpenSubtitles API key is accepted and quota information is available."
                    : "OpenSubtitles API key is accepted; quota information is not available before download."
            };
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new OpenSubtitlesProbeResult
            {
                IsApiKeyConfigured = true,
                Message = AiPerfDiagnostics.SanitizeMessage(exception.Message),
                ErrorKind = OpenSubtitlesErrorKind.Network
            };
        }
    }

    public async Task<OpenSubtitlesSearchPage> SearchAsync(
        OpenSubtitlesClientOptions options,
        OpenSubtitlesSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return new OpenSubtitlesSearchPage { ResultMessage = "OpenSubtitles API key is not configured." };
        }

        var queryString = BuildSearchQuery(request);
        using var response = await SendAsync(
            options,
            HttpMethod.Get,
            $"subtitles?{queryString}",
            options.Token,
            body: null,
            "opensubtitles-search",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new OpenSubtitlesSearchPage { ResultMessage = BuildFailureMessage(response.StatusCode) };
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);
        return BuildSearchPage(document.RootElement, request.Page);
    }

    public async Task<OpenSubtitlesDownloadContractResult> CheckDownloadContractAsync(
        OpenSubtitlesClientOptions options,
        OpenSubtitlesDownloadContractRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return new OpenSubtitlesDownloadContractResult
            {
                Succeeded = false,
                Message = "OpenSubtitles API key is not configured.",
                ErrorKind = OpenSubtitlesErrorKind.NotConfigured
            };
        }

        if (string.IsNullOrWhiteSpace(request.FileId))
        {
            return new OpenSubtitlesDownloadContractResult
            {
                Succeeded = false,
                Message = "OpenSubtitles file id is required.",
                ErrorKind = OpenSubtitlesErrorKind.InvalidResponse
            };
        }

        var payload = new Dictionary<string, object>
        {
            ["file_id"] = request.FileId.Trim()
        };
        if (!string.IsNullOrWhiteSpace(request.SubFormat))
        {
            payload["sub_format"] = request.SubFormat.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.FileName))
        {
            payload["file_name"] = Path.GetFileName(request.FileName);
        }

        if (request.ForceDownload)
        {
            payload["force_download"] = true;
        }

        using var response = await SendAsync(
            options,
            HttpMethod.Post,
            "download",
            options.Token,
            JsonSerializer.Serialize(payload, JsonOptions),
            "opensubtitles-download-contract",
            cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new OpenSubtitlesDownloadContractResult
            {
                Succeeded = false,
                Message = BuildFailureMessage(response.StatusCode),
                ErrorKind = MapStatus(response.StatusCode)
            };
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        return new OpenSubtitlesDownloadContractResult
        {
            Succeeded = true,
            DownloadUrl = GetString(root, "link"),
            FileName = GetString(root, "file_name"),
            Requests = GetInt(root, "requests"),
            Remaining = GetInt(root, "remaining"),
            ResetTime = GetString(root, "reset_time_utc"),
            Message = GetString(root, "message")
        };
    }

    private async Task<LoginResult> LoginAsync(OpenSubtitlesClientOptions options, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(
            new
            {
                username = options.Username.Trim(),
                password = options.Password
            },
            JsonOptions);

        using var response = await SendAsync(
            options,
            HttpMethod.Post,
            "login",
            token: string.Empty,
            payload,
            "opensubtitles-login",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new LoginResult(false, string.Empty, BuildFailureMessage(response.StatusCode), MapStatus(response.StatusCode));
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var token = GetString(document.RootElement, "token");
        return string.IsNullOrWhiteSpace(token)
            ? new LoginResult(false, string.Empty, "OpenSubtitles login response did not include a token.", OpenSubtitlesErrorKind.InvalidResponse)
            : new LoginResult(true, token, "OpenSubtitles login succeeded.", OpenSubtitlesErrorKind.None);
    }

    private async Task<QuotaProbeResult> TryProbeQuotaAsync(
        OpenSubtitlesClientOptions options,
        string token,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            options,
            HttpMethod.Get,
            "infos/user",
            token,
            body: null,
            "opensubtitles-quota-probe",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new QuotaProbeResult(true, false, null, null);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement.TryGetProperty("data", out var data) ? data : document.RootElement;
        return new QuotaProbeResult(
            true,
            true,
            GetNestedInt(root, "remaining_downloads") ?? GetNestedInt(root, "downloads_remaining"),
            GetNestedInt(root, "allowed_downloads") ?? GetNestedInt(root, "downloads_count"));
    }

    private async Task<HttpResponseMessage> SendAsync(
        OpenSubtitlesClientOptions options,
        HttpMethod method,
        string relativeUri,
        string token,
        string? body,
        string purpose,
        CancellationToken cancellationToken)
    {
        var requestUri = new Uri(new Uri(NormalizeEndpoint(options.Endpoint) + "/", UriKind.Absolute), relativeUri);
        using var request = new HttpRequestMessage(method, requestUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.UserAgent.ParseAdd(UserAgent);
        request.Headers.TryAddWithoutValidation("Api-Key", options.ApiKey.Trim());
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
        }

        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        var stopwatch = Stopwatch.StartNew();
        var isError = false;
        try
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);
            isError = !response.IsSuccessStatusCode;
            return response;
        }
        catch
        {
            isError = true;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            AiPerfDiagnostics.RecordExternalCall(purpose, stopwatch.Elapsed, isError);
        }
    }

    private static string BuildSearchQuery(OpenSubtitlesSearchRequest request)
    {
        var parameters = new List<KeyValuePair<string, string>>();
        Add(parameters, "query", request.Query);
        Add(parameters, "imdb_id", NormalizeImdbId(request.ImdbId));
        if (request.TmdbId is > 0)
        {
            Add(parameters, "tmdb_id", request.TmdbId.Value.ToString(CultureInfo.InvariantCulture));
        }

        Add(parameters, "parent_imdb_id", NormalizeImdbId(request.ParentImdbId));
        if (request.ParentTmdbId is > 0)
        {
            Add(parameters, "parent_tmdb_id", request.ParentTmdbId.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (request.SeasonNumber.HasValue)
        {
            Add(parameters, "season_number", request.SeasonNumber.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (request.EpisodeNumber.HasValue)
        {
            Add(parameters, "episode_number", request.EpisodeNumber.Value.ToString(CultureInfo.InvariantCulture));
        }

        Add(parameters, "languages", request.Languages);
        Add(parameters, "moviehash", request.MovieHash);
        if (request.FileSize.HasValue)
        {
            Add(parameters, "file_size", request.FileSize.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (request.Year.HasValue)
        {
            Add(parameters, "year", request.Year.Value.ToString(CultureInfo.InvariantCulture));
        }

        Add(parameters, "type", request.Type);
        Add(parameters, "page", Math.Max(1, request.Page).ToString(CultureInfo.InvariantCulture));
        Add(parameters, "order_by", request.OrderBy);
        Add(parameters, "order_direction", request.OrderDirection);

        return string.Join(
            "&",
            parameters.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
    }

    private static OpenSubtitlesSearchPage BuildSearchPage(JsonElement root, int requestedPage)
    {
        var results = new List<OpenSubtitlesSearchItem>();
        if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
            {
                results.Add(BuildSearchItem(item));
            }
        }

        var totalPages = GetNestedInt(root, "total_pages") ?? GetNestedInt(root, "last_page") ?? 0;
        var totalCount = GetNestedInt(root, "total_count") ?? GetNestedInt(root, "total") ?? results.Count;
        return new OpenSubtitlesSearchPage
        {
            Results = results,
            Page = Math.Max(1, requestedPage),
            TotalPages = totalPages,
            TotalCount = totalCount,
            ResultMessage = results.Count == 0 ? "No subtitles returned." : string.Empty
        };
    }

    private static OpenSubtitlesSearchItem BuildSearchItem(JsonElement item)
    {
        var attributes = item.TryGetProperty("attributes", out var attr) ? attr : item;
        var feature = attributes.TryGetProperty("feature_details", out var featureDetails) ? featureDetails : default;
        var uploader = attributes.TryGetProperty("uploader", out var uploaderDetails) ? uploaderDetails : default;
        var file = GetFirstFile(attributes);

        return new OpenSubtitlesSearchItem
        {
            SubtitleId = GetString(attributes, "subtitle_id", GetString(item, "id")),
            LanguageCode = GetString(attributes, "language"),
            LanguageName = GetString(attributes, "language"),
            ReleaseName = GetString(attributes, "release"),
            FileName = GetString(file, "file_name"),
            ProviderFileId = GetString(file, "file_id"),
            DownloadCount = GetInt(attributes, "download_count"),
            Rating = GetDouble(attributes, "ratings"),
            Votes = GetInt(attributes, "votes"),
            IsHearingImpaired = GetBool(attributes, "hearing_impaired"),
            IsMachineTranslated = GetBool(attributes, "machine_translated"),
            IsAiTranslated = GetBool(attributes, "ai_translated"),
            IsTrustedUploader = GetBool(attributes, "from_trusted"),
            Fps = GetDouble(attributes, "fps"),
            UploadedAt = ParseDateTime(GetString(attributes, "upload_date")),
            FeatureTitle = GetString(feature, "title"),
            FeatureYear = GetInt(feature, "year"),
            SeasonNumber = GetInt(feature, "season_number"),
            EpisodeNumber = GetInt(feature, "episode_number"),
            UploaderName = GetString(uploader, "name"),
            RawMetadataJson = attributes.GetRawText()
        };
    }

    private static JsonElement GetFirstFile(JsonElement attributes)
    {
        if (attributes.ValueKind != JsonValueKind.Undefined
            && attributes.TryGetProperty("files", out var files)
            && files.ValueKind == JsonValueKind.Array)
        {
            foreach (var file in files.EnumerateArray())
            {
                return file;
            }
        }

        return default;
    }

    private static void Add(List<KeyValuePair<string, string>> parameters, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parameters.Add(new KeyValuePair<string, string>(key, value.Trim()));
        }
    }

    private static string NormalizeEndpoint(string? endpoint)
    {
        return string.IsNullOrWhiteSpace(endpoint) ? DefaultEndpoint : endpoint.Trim().TrimEnd('/');
    }

    private static string NormalizeImdbId(string? imdbId)
    {
        var normalized = imdbId?.Trim() ?? string.Empty;
        return normalized.StartsWith("tt", StringComparison.OrdinalIgnoreCase)
            ? normalized[2..]
            : normalized;
    }

    private static string BuildFailureMessage(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.Unauthorized => "OpenSubtitles authentication failed.",
            HttpStatusCode.Forbidden => "OpenSubtitles access was forbidden.",
            (HttpStatusCode)429 => "OpenSubtitles rate limit or quota was reached.",
            _ when (int)statusCode >= 500 => "OpenSubtitles server error.",
            _ => $"OpenSubtitles request failed with HTTP {(int)statusCode}."
        };
    }

    private static OpenSubtitlesErrorKind MapStatus(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.Unauthorized => OpenSubtitlesErrorKind.Unauthorized,
            HttpStatusCode.Forbidden => OpenSubtitlesErrorKind.Forbidden,
            (HttpStatusCode)429 => OpenSubtitlesErrorKind.RateLimited,
            _ when (int)statusCode >= 500 => OpenSubtitlesErrorKind.ServerError,
            _ => OpenSubtitlesErrorKind.Unknown
        };
    }

    private static DateTime? ParseDateTime(string value)
    {
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed.ToUniversalTime()
            : null;
    }

    private static int? GetNestedInt(JsonElement element, string propertyName)
    {
        var direct = GetInt(element, propertyName);
        if (direct.HasValue)
        {
            return direct;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Object)
                {
                    var nested = GetNestedInt(property.Value, propertyName);
                    if (nested.HasValue)
                    {
                        return nested;
                    }
                }
            }
        }

        return null;
    }

    private static string GetString(JsonElement element, string propertyName, string fallback = "")
    {
        if (element.ValueKind == JsonValueKind.Undefined
            || !element.TryGetProperty(propertyName, out var property)
            || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return fallback;
        }

        return property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? fallback
            : property.ToString();
    }

    private static int? GetInt(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Undefined || !element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value))
        {
            return value;
        }

        return int.TryParse(property.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
            ? value
            : null;
    }

    private static double? GetDouble(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Undefined || !element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out var value))
        {
            return value;
        }

        return double.TryParse(property.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value)
            ? value
            : null;
    }

    private static bool GetBool(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Undefined || !element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        if (property.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return property.GetBoolean();
        }

        return bool.TryParse(property.ToString(), out var parsed) && parsed;
    }

    private sealed record LoginResult(bool Succeeded, string Token, string Message, OpenSubtitlesErrorKind ErrorKind);

    private sealed record QuotaProbeResult(bool Attempted, bool Succeeded, int? RemainingDownloads, int? AllowedDownloads);
}
