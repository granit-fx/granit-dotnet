using System.Collections.Concurrent;
using Granit.AI.Mcp.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.AI.Mcp.Internal;

/// <summary>
/// Guards MCP sampling requests (external servers requesting LLM completions)
/// with permission checks, rate limiting, cost control, and audit logging.
/// </summary>
internal sealed partial class SamplingGuard(
    IOptions<GranitAIMcpOptions> options,
    TimeProvider timeProvider,
    ILogger<SamplingGuard> logger)
{
    private readonly ConcurrentDictionary<string, SlidingWindow> _windows = new(StringComparer.Ordinal);

    /// <summary>
    /// Validates a sampling request before forwarding to the AI workspace.
    /// </summary>
    /// <returns><see langword="true"/> if the request should be allowed.</returns>
    public bool TryValidate(int? requestedMaxTokens, string? serverOrigin, out string? rejectionReason)
    {
        GranitAIMcpOptions config = options.Value;

        if (!config.EnableSampling)
        {
            rejectionReason = "MCP sampling is disabled.";
            LogSamplingRejected(serverOrigin, rejectionReason);
            return false;
        }

        if (config.SamplingMaxTokensPerRequest > 0 &&
            requestedMaxTokens > config.SamplingMaxTokensPerRequest)
        {
            rejectionReason = $"Requested {requestedMaxTokens} tokens exceeds limit of {config.SamplingMaxTokensPerRequest}.";
            LogSamplingRejected(serverOrigin, rejectionReason);
            return false;
        }

        if (config.SamplingRateLimitPerMinute > 0 &&
            !TryAcquireRateLimit(serverOrigin ?? "unknown", config.SamplingRateLimitPerMinute))
        {
            rejectionReason = $"Rate limit exceeded: {config.SamplingRateLimitPerMinute} requests per minute.";
            LogSamplingRejected(serverOrigin, rejectionReason);
            return false;
        }

        rejectionReason = null;
        LogSamplingAllowed(serverOrigin, requestedMaxTokens);
        return true;
    }

    private bool TryAcquireRateLimit(string key, int maxPerMinute)
    {
        SlidingWindow window = _windows.GetOrAdd(key, _ => new SlidingWindow());
        return window.TryAcquire(timeProvider.GetUtcNow(), maxPerMinute);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "MCP sampling rejected from {ServerOrigin}: {Reason}")]
    private partial void LogSamplingRejected(string? serverOrigin, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "MCP sampling allowed from {ServerOrigin} (maxTokens={MaxTokens})")]
    private partial void LogSamplingAllowed(string? serverOrigin, int? maxTokens);

    /// <summary>
    /// Simple sliding window rate limiter. Tracks request timestamps and evicts
    /// entries older than 60 seconds.
    /// </summary>
    internal sealed class SlidingWindow
    {
        private readonly Lock _lock = new();
        private readonly Queue<DateTimeOffset> _timestamps = new();

        public bool TryAcquire(DateTimeOffset now, int maxPerMinute)
        {
            lock (_lock)
            {
                DateTimeOffset windowStart = now.AddMinutes(-1);

                while (_timestamps.Count > 0 && _timestamps.Peek() < windowStart)
                {
                    _timestamps.Dequeue();
                }

                if (_timestamps.Count >= maxPerMinute)
                {
                    return false;
                }

                _timestamps.Enqueue(now);
                return true;
            }
        }
    }
}
