using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http.Headers;
using MediaLibrary.Core.Diagnostics;

namespace MediaLibrary.Core.Services.Implementations;

internal sealed class ExternalApiAdaptiveThrottle
{
    private static readonly TimeSpan RateWindow = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan BaseRetryDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan MaxComputedRetryDelay = TimeSpan.FromSeconds(15);

    private readonly object _gate = new();
    private readonly object _rateGate = new();
    private readonly string _provider;
    private readonly string _concurrencySampleName;
    private readonly int[] _concurrencyLevels;
    private readonly int _observationWindowSize;
    private readonly int _maxAttempts;
    private readonly int _maxRequestsPerSecond;
    private readonly Queue<DateTimeOffset> _requestTimestamps = new();

    private TaskCompletionSource _waitSignal = NewWaitSignal();
    private int _activeCount;
    private int _levelIndex;
    private int _stableRequestCount;

    public ExternalApiAdaptiveThrottle(
        string provider,
        string concurrencySampleName,
        IReadOnlyList<int> concurrencyLevels,
        int maxRequestsPerSecond = 0,
        int observationWindowSize = 8,
        int maxAttempts = 3)
    {
        if (concurrencyLevels.Count == 0)
        {
            throw new ArgumentException("At least one concurrency level is required.", nameof(concurrencyLevels));
        }

        _provider = string.IsNullOrWhiteSpace(provider) ? "external-api" : provider.Trim();
        _concurrencySampleName = string.IsNullOrWhiteSpace(concurrencySampleName)
            ? "external-api-http"
            : concurrencySampleName.Trim();
        _concurrencyLevels = concurrencyLevels
            .Where(x => x > 0)
            .Distinct()
            .OrderByDescending(x => x)
            .ToArray();
        if (_concurrencyLevels.Length == 0)
        {
            throw new ArgumentException("At least one positive concurrency level is required.", nameof(concurrencyLevels));
        }

        _maxRequestsPerSecond = Math.Max(0, maxRequestsPerSecond);
        _observationWindowSize = Math.Max(1, observationWindowSize);
        _maxAttempts = Math.Max(1, maxAttempts);

        ScanIdentificationDiagnostics.Write(
            $"event=external-api-adaptive-throttle-started provider={ScanIdentificationDiagnostics.FormatValue(_provider)} currentConcurrency={CurrentConcurrency} maxConcurrency={MaxConcurrency} minConcurrency={MinConcurrency} rateLimitPerSecond={_maxRequestsPerSecond} observationWindowSize={_observationWindowSize} maxAttempts={_maxAttempts}");
    }

    public int CurrentConcurrency
    {
        get
        {
            lock (_gate)
            {
                return _concurrencyLevels[_levelIndex];
            }
        }
    }

    public int MaxConcurrency => _concurrencyLevels[0];

    public int MinConcurrency => _concurrencyLevels[^1];

    public async Task<HttpResponseMessage> SendAsync(
        string purpose,
        Func<CancellationToken, Task<HttpResponseMessage>> sendAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sendAsync);

        var safePurpose = string.IsNullOrWhiteSpace(purpose) ? "unknown" : purpose.Trim();
        Exception? lastException = null;
        var downgradedThisRequest = false;
        for (var attempt = 1; attempt <= _maxAttempts; attempt++)
        {
            await WaitForRateLimitAsync(safePurpose, cancellationToken).ConfigureAwait(false);
            await AcquireSlotAsync(cancellationToken).ConfigureAwait(false);

            HttpResponseMessage? responseToReturn = null;
            HttpResponseMessage? responseToDispose = null;
            Exception? exceptionToThrow = null;
            TimeSpan? retryDelay = null;
            var retryAfterUsed = false;
            try
            {
                var response = await sendAsync(cancellationToken).ConfigureAwait(false);
                if (IsRetryableStatusCode(response.StatusCode))
                {
                    var downgraded = RecordRetryableFailure(
                        safePurpose,
                        statusCode: response.StatusCode,
                        errorType: "http-status",
                        allowDowngrade: !downgradedThisRequest);
                    downgradedThisRequest |= downgraded;

                    if (attempt >= _maxAttempts)
                    {
                        ScanIdentificationDiagnostics.Write(
                            $"event=external-api-retry-exhausted provider={ScanIdentificationDiagnostics.FormatValue(_provider)} purpose={ScanIdentificationDiagnostics.FormatValue(safePurpose)} retryCount={attempt - 1} currentConcurrency={CurrentConcurrency} statusCode={(int)response.StatusCode} errorType=http-status");
                        responseToReturn = response;
                    }
                    else
                    {
                        responseToDispose = response;
                        retryDelay = ComputeRetryDelay(attempt, GetRetryAfter(response.Headers), out retryAfterUsed);
                        LogRetryScheduled(safePurpose, attempt, retryDelay.Value, retryAfterUsed, response.StatusCode, "http-status");
                    }
                }
                else
                {
                    RecordStableRequest(safePurpose, response.StatusCode);
                    responseToReturn = response;
                }
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested && IsRetryableException(exception))
            {
                lastException = exception;
                var downgraded = RecordRetryableFailure(
                    safePurpose,
                    statusCode: GetStatusCode(exception),
                    errorType: GetRetryableErrorType(exception),
                    allowDowngrade: !downgradedThisRequest);
                downgradedThisRequest |= downgraded;

                if (attempt >= _maxAttempts)
                {
                    ScanIdentificationDiagnostics.Write(
                        $"event=external-api-retry-exhausted provider={ScanIdentificationDiagnostics.FormatValue(_provider)} purpose={ScanIdentificationDiagnostics.FormatValue(safePurpose)} retryCount={attempt - 1} currentConcurrency={CurrentConcurrency} statusCode={FormatStatusCode(GetStatusCode(exception))} errorType={ScanIdentificationDiagnostics.FormatValue(GetRetryableErrorType(exception))}");
                    exceptionToThrow = exception;
                }
                else
                {
                    retryDelay = ComputeRetryDelay(attempt, retryAfter: null, out retryAfterUsed);
                    LogRetryScheduled(safePurpose, attempt, retryDelay.Value, retryAfterUsed, GetStatusCode(exception), GetRetryableErrorType(exception));
                }
            }
            finally
            {
                ReleaseSlot();
            }

            if (responseToReturn is not null)
            {
                return responseToReturn;
            }

            responseToDispose?.Dispose();

            if (exceptionToThrow is not null)
            {
                throw exceptionToThrow;
            }

            if (retryDelay.HasValue)
            {
                await Task.Delay(retryDelay.Value, cancellationToken).ConfigureAwait(false);
            }
        }

        throw lastException ?? new HttpRequestException($"{_provider} request retry loop exited without a response.");
    }

    private async Task WaitForRateLimitAsync(string purpose, CancellationToken cancellationToken)
    {
        if (_maxRequestsPerSecond <= 0)
        {
            return;
        }

        var totalWait = TimeSpan.Zero;
        while (true)
        {
            TimeSpan wait;
            lock (_rateGate)
            {
                var now = DateTimeOffset.UtcNow;
                while (_requestTimestamps.Count > 0 && now - _requestTimestamps.Peek() >= RateWindow)
                {
                    _requestTimestamps.Dequeue();
                }

                if (_requestTimestamps.Count < _maxRequestsPerSecond)
                {
                    _requestTimestamps.Enqueue(now);
                    if (totalWait > TimeSpan.Zero)
                    {
                        ScanIdentificationDiagnostics.Write(
                            $"event=external-api-rate-limit-wait provider={ScanIdentificationDiagnostics.FormatValue(_provider)} purpose={ScanIdentificationDiagnostics.FormatValue(purpose)} waitMs={(long)Math.Round(totalWait.TotalMilliseconds)} currentConcurrency={CurrentConcurrency} rateLimitPerSecond={_maxRequestsPerSecond}");
                    }

                    return;
                }

                wait = _requestTimestamps.Peek() + RateWindow - now;
            }

            if (wait <= TimeSpan.Zero)
            {
                continue;
            }

            totalWait += wait;
            await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task AcquireSlotAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            Task waitTask;
            int activeCount;
            lock (_gate)
            {
                var currentConcurrency = _concurrencyLevels[_levelIndex];
                if (_activeCount < currentConcurrency)
                {
                    _activeCount++;
                    activeCount = _activeCount;
                    AiPerfDiagnostics.RecordConcurrencySample(_concurrencySampleName, activeCount);
                    return;
                }

                waitTask = _waitSignal.Task;
            }

            await waitTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private void ReleaseSlot()
    {
        lock (_gate)
        {
            _activeCount = Math.Max(0, _activeCount - 1);
            PulseWaiters();
        }
    }

    private bool RecordRetryableFailure(
        string purpose,
        HttpStatusCode? statusCode,
        string errorType,
        bool allowDowngrade)
    {
        int oldConcurrency;
        int newConcurrency;
        lock (_gate)
        {
            oldConcurrency = _concurrencyLevels[_levelIndex];
            if (allowDowngrade && _levelIndex < _concurrencyLevels.Length - 1)
            {
                _levelIndex++;
            }

            newConcurrency = _concurrencyLevels[_levelIndex];
            _stableRequestCount = 0;
            PulseWaiters();
        }

        if (oldConcurrency != newConcurrency)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=external-api-adaptive-concurrency-changed provider={ScanIdentificationDiagnostics.FormatValue(_provider)} purpose={ScanIdentificationDiagnostics.FormatValue(purpose)} oldConcurrency={oldConcurrency} newConcurrency={newConcurrency} statusCode={FormatStatusCode(statusCode)} transientErrorType={ScanIdentificationDiagnostics.FormatValue(errorType)} downgradeReason=retryable-error observationWindowProgress=0/{_observationWindowSize}");
            return true;
        }

        ScanIdentificationDiagnostics.Write(
            $"event=external-api-observation-reset provider={ScanIdentificationDiagnostics.FormatValue(_provider)} purpose={ScanIdentificationDiagnostics.FormatValue(purpose)} currentConcurrency={newConcurrency} statusCode={FormatStatusCode(statusCode)} transientErrorType={ScanIdentificationDiagnostics.FormatValue(errorType)} downgradeSuppressed={(!allowDowngrade).ToString().ToLowerInvariant()} observationWindowProgress=0/{_observationWindowSize}");
        return false;
    }

    private void RecordStableRequest(string purpose, HttpStatusCode statusCode)
    {
        int? oldConcurrency = null;
        int? newConcurrency = null;
        int stableRequestCount;
        var isDowngraded = false;
        lock (_gate)
        {
            isDowngraded = _levelIndex > 0;
            if (isDowngraded)
            {
                _stableRequestCount++;
                stableRequestCount = _stableRequestCount;
                if (_stableRequestCount >= _observationWindowSize)
                {
                    oldConcurrency = _concurrencyLevels[_levelIndex];
                    _levelIndex--;
                    newConcurrency = _concurrencyLevels[_levelIndex];
                    _stableRequestCount = 0;
                    stableRequestCount = 0;
                    PulseWaiters();
                }
            }
            else
            {
                stableRequestCount = 0;
            }
        }

        if (!isDowngraded)
        {
            return;
        }

        if (oldConcurrency.HasValue && newConcurrency.HasValue)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=external-api-adaptive-concurrency-changed provider={ScanIdentificationDiagnostics.FormatValue(_provider)} purpose={ScanIdentificationDiagnostics.FormatValue(purpose)} oldConcurrency={oldConcurrency.Value} newConcurrency={newConcurrency.Value} statusCode={(int)statusCode} upgradeReason=observation-window-stable observationWindowProgress={_observationWindowSize}/{_observationWindowSize}");
            return;
        }

        ScanIdentificationDiagnostics.Write(
            $"event=external-api-observation-progress provider={ScanIdentificationDiagnostics.FormatValue(_provider)} purpose={ScanIdentificationDiagnostics.FormatValue(purpose)} currentConcurrency={CurrentConcurrency} statusCode={(int)statusCode} observationWindowProgress={stableRequestCount}/{_observationWindowSize}");
    }

    private void PulseWaiters()
    {
        var waitSignal = _waitSignal;
        _waitSignal = NewWaitSignal();
        waitSignal.TrySetResult();
    }

    private void LogRetryScheduled(
        string purpose,
        int failedAttempt,
        TimeSpan retryDelay,
        bool retryAfterUsed,
        HttpStatusCode? statusCode,
        string errorType)
    {
        ScanIdentificationDiagnostics.Write(
            $"event=external-api-retry-scheduled provider={ScanIdentificationDiagnostics.FormatValue(_provider)} purpose={ScanIdentificationDiagnostics.FormatValue(purpose)} retryAttempt={failedAttempt + 1} retryCount={failedAttempt} retryDelayMs={(long)Math.Round(retryDelay.TotalMilliseconds)} retryAfterUsed={retryAfterUsed.ToString().ToLowerInvariant()} currentConcurrency={CurrentConcurrency} statusCode={FormatStatusCode(statusCode)} transientErrorType={ScanIdentificationDiagnostics.FormatValue(errorType)}");
    }

    private static TaskCompletionSource NewWaitSignal()
    {
        return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private static TimeSpan ComputeRetryDelay(int failedAttempt, TimeSpan? retryAfter, out bool retryAfterUsed)
    {
        if (retryAfter.HasValue && retryAfter.Value > TimeSpan.Zero)
        {
            retryAfterUsed = true;
            return retryAfter.Value;
        }

        retryAfterUsed = false;
        var multiplier = Math.Pow(2, Math.Max(0, failedAttempt - 1));
        var delayMs = BaseRetryDelay.TotalMilliseconds * multiplier;
        delayMs += Random.Shared.Next(100, 450);
        return TimeSpan.FromMilliseconds(Math.Min(delayMs, MaxComputedRetryDelay.TotalMilliseconds));
    }

    private static TimeSpan? GetRetryAfter(HttpResponseHeaders headers)
    {
        var retryAfter = headers.RetryAfter;
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
            var delta = retryAfter.Date.Value - DateTimeOffset.UtcNow;
            return delta > TimeSpan.Zero ? delta : TimeSpan.Zero;
        }

        return null;
    }

    private static bool IsRetryableStatusCode(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;
    }

    private static bool IsRetryableException(Exception exception)
    {
        if (exception is OperationCanceledException)
        {
            return true;
        }

        if (exception is TimeoutException or IOException)
        {
            return true;
        }

        if (exception is HttpRequestException httpRequestException)
        {
            return !httpRequestException.StatusCode.HasValue
                   || IsRetryableStatusCode(httpRequestException.StatusCode.Value);
        }

        return exception.InnerException is not null && IsRetryableException(exception.InnerException);
    }

    private static HttpStatusCode? GetStatusCode(Exception exception)
    {
        return exception switch
        {
            HttpRequestException { StatusCode: { } statusCode } => statusCode,
            { InnerException: not null } => GetStatusCode(exception.InnerException),
            _ => null
        };
    }

    private static string GetRetryableErrorType(Exception exception)
    {
        return exception switch
        {
            OperationCanceledException => "timeout",
            TimeoutException => "timeout",
            IOException => "network-io",
            HttpRequestException { StatusCode: { } statusCode } => $"http-{(int)statusCode}",
            HttpRequestException => "network-http",
            { InnerException: not null } => GetRetryableErrorType(exception.InnerException),
            _ => exception.GetType().Name
        };
    }

    private static string FormatStatusCode(HttpStatusCode? statusCode)
    {
        return statusCode.HasValue
            ? ((int)statusCode.Value).ToString(CultureInfo.InvariantCulture)
            : "(none)";
    }
}
