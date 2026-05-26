namespace Granit.AI.Extraction.RateLimiting;

/// <summary>
/// Per-bucket sliding-window cap on outbound LLM calls. Bucket keys are caller-defined
/// strings (typically <c>"{consumer}:{tenant}"</c>); the limiter only knows how to
/// admit-or-deny based on the configured hourly cap.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why caller-supplied caps.</b> Different AI features have different cost profiles:
/// the language detector runs on every indexed entry (high volume, low token count),
/// the summarizer is gated by the consumer (lower volume, higher token count). Each
/// package owns its own option (typed <c>MaxAICallsPerHourPerTenant</c>) and the limiter
/// stays cap-agnostic — there's no global ceiling the framework forces on its consumers.
/// </para>
/// <para>
/// <b>Graceful failure.</b> Callers MUST treat a <c>false</c> return as "skip this call,
/// log a metric, fall back to a non-AI default". Never throw on rate-limit denial — the
/// indexing pipeline must keep flowing even when the cost ceiling is hit.
/// </para>
/// </remarks>
public interface IAICallRateLimiter
{
    /// <summary>
    /// Attempts to consume one token from <paramref name="bucketKey"/>. Returns
    /// <c>true</c> when the call is permitted, <c>false</c> when the bucket already
    /// holds <paramref name="maxCallsPerHour"/> entries within the trailing hour.
    /// </summary>
    ValueTask<bool> TryAcquireAsync(
        string bucketKey,
        int maxCallsPerHour,
        CancellationToken cancellationToken = default);
}
