using System.Globalization;
using System.IO;
using System.Net;
using MediaLibrary.Core.Diagnostics;

namespace MediaLibrary.Core.Services.Implementations;

internal sealed class AdaptiveAiBatchExecutor
{
    private const int MinConcurrency = 1;
    private const int MidConcurrency = 3;
    private const int MaxConcurrency = 5;
    private const int SuccessUpgradeThreshold = 3;
    private const int MaxAttempts = 3;
    private static readonly TimeSpan BaseRetryDelay = TimeSpan.FromMilliseconds(600);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(15);

    private readonly object _gate = new();
    private readonly string _purpose;
    private TaskCompletionSource _waitSignal = NewWaitSignal();
    private int _activeCount;
    private int _currentConcurrency = MaxConcurrency;
    private int _successStreak;

    public AdaptiveAiBatchExecutor(string purpose, int itemCount)
    {
        _purpose = string.IsNullOrWhiteSpace(purpose) ? "unknown" : purpose.Trim();
        ItemCount = Math.Max(0, itemCount);
        ScanIdentificationDiagnostics.Write(
            $"event=ai-adaptive-concurrency-started purpose={ScanIdentificationDiagnostics.FormatValue(_purpose)} batchItemCount={ItemCount} currentConcurrency={_currentConcurrency} minConcurrency={MinConcurrency} midConcurrency={MidConcurrency} maxConcurrency={MaxConcurrency} successUpgradeThreshold={SuccessUpgradeThreshold} maxAttempts={MaxAttempts}");
    }

    public int ItemCount { get; }

    public int CurrentConcurrency
    {
        get
        {
            lock (_gate)
            {
                return _currentConcurrency;
            }
        }
    }

    public int RetryableErrorCount { get; private set; }

    public int RetryScheduledCount { get; private set; }

    public int RetryExhaustedCount { get; private set; }

    public int SuccessCount { get; private set; }

    public async Task<T> ExecuteAsync<T>(
        string itemKind,
        string itemKey,
        Func<int, CancellationToken, Task<T>> requestAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requestAsync);

        Exception? lastException = null;
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            await AcquireSlotAsync(cancellationToken);
            try
            {
                var result = await requestAsync(attempt, cancellationToken);
                RecordSuccess(itemKind, itemKey);
                return result;
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested && IsRetryable(exception, cancellationToken))
            {
                lastException = exception;
                RecordRetryableError(exception, itemKind, itemKey);
                if (attempt >= MaxAttempts)
                {
                    RetryExhaustedCount++;
                    ScanIdentificationDiagnostics.Write(
                        $"event=ai-request-retry-exhausted purpose={ScanIdentificationDiagnostics.FormatValue(_purpose)} itemKind={ScanIdentificationDiagnostics.FormatValue(itemKind)} itemKey={ScanIdentificationDiagnostics.FormatValue(itemKey)} retryCount={attempt - 1} currentConcurrency={CurrentConcurrency} retryableErrorCode={ScanIdentificationDiagnostics.FormatValue(GetRetryableErrorCode(exception))}");
                    throw;
                }
            }
            finally
            {
                ReleaseSlot();
            }

            var delay = ComputeRetryDelay(attempt, lastException);
            RetryScheduledCount++;
            ScanIdentificationDiagnostics.Write(
                $"event=ai-request-retry-scheduled purpose={ScanIdentificationDiagnostics.FormatValue(_purpose)} itemKind={ScanIdentificationDiagnostics.FormatValue(itemKind)} itemKey={ScanIdentificationDiagnostics.FormatValue(itemKey)} retryAttempt={attempt + 1} retryCount={attempt} retryDelayMs={(int)delay.TotalMilliseconds} currentConcurrency={CurrentConcurrency} retryableErrorCode={ScanIdentificationDiagnostics.FormatValue(GetRetryableErrorCode(lastException))}");
            await Task.Delay(delay, cancellationToken);
        }

        throw lastException ?? new InvalidOperationException("AI request retry loop exited without a result.");
    }

    private async Task AcquireSlotAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            Task waitTask;
            lock (_gate)
            {
                if (_activeCount < _currentConcurrency)
                {
                    _activeCount++;
                    return;
                }

                waitTask = _waitSignal.Task;
            }

            await waitTask.WaitAsync(cancellationToken);
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

    private void RecordSuccess(string itemKind, string itemKey)
    {
        int? oldConcurrency = null;
        int? newConcurrency = null;
        int successStreak;
        lock (_gate)
        {
            SuccessCount++;
            _successStreak++;
            successStreak = _successStreak;
            if (_successStreak >= SuccessUpgradeThreshold && _currentConcurrency < MaxConcurrency)
            {
                oldConcurrency = _currentConcurrency;
                _currentConcurrency = _currentConcurrency <= MinConcurrency ? MidConcurrency : MaxConcurrency;
                newConcurrency = _currentConcurrency;
                _successStreak = 0;
                successStreak = 0;
                PulseWaiters();
            }
        }

        if (oldConcurrency.HasValue && newConcurrency.HasValue)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=ai-adaptive-concurrency-changed purpose={ScanIdentificationDiagnostics.FormatValue(_purpose)} itemKind={ScanIdentificationDiagnostics.FormatValue(itemKind)} itemKey={ScanIdentificationDiagnostics.FormatValue(itemKey)} oldConcurrency={oldConcurrency.Value} newConcurrency={newConcurrency.Value} successStreak={successStreak} upgradeReason=success-streak");
        }
    }

    private void RecordRetryableError(Exception exception, string itemKind, string itemKey)
    {
        int oldConcurrency;
        int newConcurrency;
        lock (_gate)
        {
            RetryableErrorCount++;
            _successStreak = 0;
            oldConcurrency = _currentConcurrency;
            _currentConcurrency = _currentConcurrency switch
            {
                > MidConcurrency => MidConcurrency,
                > MinConcurrency => MinConcurrency,
                _ => MinConcurrency
            };
            newConcurrency = _currentConcurrency;
            PulseWaiters();
        }

        if (oldConcurrency != newConcurrency)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=ai-adaptive-concurrency-changed purpose={ScanIdentificationDiagnostics.FormatValue(_purpose)} itemKind={ScanIdentificationDiagnostics.FormatValue(itemKind)} itemKey={ScanIdentificationDiagnostics.FormatValue(itemKey)} oldConcurrency={oldConcurrency} newConcurrency={newConcurrency} retryableErrorCode={ScanIdentificationDiagnostics.FormatValue(GetRetryableErrorCode(exception))} downgradeReason=retryable-error");
        }
    }

    private void PulseWaiters()
    {
        var signal = _waitSignal;
        _waitSignal = NewWaitSignal();
        signal.TrySetResult();
    }

    private static TaskCompletionSource NewWaitSignal()
    {
        return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private static TimeSpan ComputeRetryDelay(int failedAttempt, Exception? exception)
    {
        var retryAfter = GetRetryAfter(exception);
        if (retryAfter.HasValue && retryAfter.Value > TimeSpan.Zero)
        {
            return retryAfter.Value <= MaxRetryDelay ? retryAfter.Value : MaxRetryDelay;
        }

        var multiplier = Math.Pow(2, Math.Max(0, failedAttempt - 1));
        var delayMs = BaseRetryDelay.TotalMilliseconds * multiplier;
        delayMs += Random.Shared.Next(120, 420);
        return TimeSpan.FromMilliseconds(Math.Min(delayMs, MaxRetryDelay.TotalMilliseconds));
    }

    private static TimeSpan? GetRetryAfter(Exception? exception)
    {
        return exception switch
        {
            AiRequestException aiRequestException => aiRequestException.RetryAfter,
            { InnerException: not null } => GetRetryAfter(exception.InnerException),
            _ => null
        };
    }

    private static bool IsRetryable(Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException)
        {
            return !cancellationToken.IsCancellationRequested;
        }

        if (exception is AiRequestException aiRequestException)
        {
            return IsRetryableStatusCode(aiRequestException.AiStatusCode);
        }

        if (exception is HttpRequestException httpRequestException)
        {
            return !httpRequestException.StatusCode.HasValue
                   || IsRetryableStatusCode(httpRequestException.StatusCode.Value);
        }

        if (exception is IOException)
        {
            return true;
        }

        return exception.InnerException is not null && IsRetryable(exception.InnerException, cancellationToken);
    }

    private static bool IsRetryableStatusCode(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;
    }

    private static string GetRetryableErrorCode(Exception? exception)
    {
        return exception switch
        {
            null => "unknown",
            AiRequestException aiRequestException => ((int)aiRequestException.AiStatusCode).ToString(CultureInfo.InvariantCulture),
            HttpRequestException { StatusCode: { } statusCode } => ((int)statusCode).ToString(CultureInfo.InvariantCulture),
            OperationCanceledException => "timeout",
            IOException => "network-io",
            { InnerException: not null } => GetRetryableErrorCode(exception.InnerException),
            _ => exception.GetType().Name
        };
    }
}
