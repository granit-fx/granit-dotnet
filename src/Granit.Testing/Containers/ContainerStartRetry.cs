using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Granit.Testing.Containers;

/// <summary>
/// Retry helper around Testcontainers-style <c>StartAsync</c> invocations. Mitigates
/// transient image-registry failures (Docker Hub 429s, MCR/Azure-Front-Door blocks,
/// DNS blips) so a single network glitch does not fail the whole CI job.
/// </summary>
/// <remarks>
/// <para>
/// Wrap container start-up with <see cref="RunWithRetryAsync"/>:
/// <code>
/// await ContainerStartRetry.RunWithRetryAsync(
///     ct => Task.WhenAll(_containerA.StartAsync(ct), _containerB.StartAsync(ct)),
///     label: "mssql fixture",
///     logger: _logger,
///     cancellationToken: ct);
/// </code>
/// </para>
/// <para>
/// Exponential backoff (1s / 2s / 4s by default, 3 attempts). Each failure before
/// the last is logged at <see cref="LogLevel.Warning"/> with the full exception so
/// chronic registry issues remain visible in CI logs. The final attempt re-throws
/// the underlying exception unchanged — callers see the true failure cause, not a
/// wrapper.
/// </para>
/// </remarks>
public static partial class ContainerStartRetry
{
    private static readonly TimeSpan[] DefaultBackoff =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4),
    ];

    /// <summary>
    /// Invokes <paramref name="start"/> up to <paramref name="maxAttempts"/> times
    /// with exponential backoff between attempts. The final attempt propagates its
    /// exception to the caller.
    /// </summary>
    public static async Task RunWithRetryAsync(
        Func<CancellationToken, Task> start,
        string label,
        ILogger? logger = null,
        int maxAttempts = 3,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);

        ILogger effectiveLogger = logger ?? NullLogger.Instance;

        for (int attempt = 1; attempt < maxAttempts; attempt++)
        {
            try
            {
                await start(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                TimeSpan delay = DefaultBackoff[Math.Min(attempt - 1, DefaultBackoff.Length - 1)];
                Log.StartFailedRetrying(effectiveLogger, ex, label, attempt, maxAttempts, delay);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }

        // Final attempt: let the exception bubble up with the original stack trace.
        await start(cancellationToken).ConfigureAwait(false);
    }

    private static partial class Log
    {
        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Container start failed for '{Label}' (attempt {Attempt}/{MaxAttempts}); retrying after {Delay}.")]
        public static partial void StartFailedRetrying(
            ILogger logger, Exception exception,
            string label, int attempt, int maxAttempts, TimeSpan delay);
    }
}
