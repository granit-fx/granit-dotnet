using Microsoft.Extensions.Logging;

namespace Granit.RateLimiting.Internal;

/// <summary>
/// Source-generated logger messages for the rate limiting module.
/// </summary>
internal static partial class RateLimitingLog
{
    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Rate limit exceeded for policy '{PolicyName}' (tenant: {TenantId}). Remaining: {Remaining}, RetryAfter: {RetryAfterSeconds}s.")]
    public static partial void LogRateLimitExceeded(ILogger logger, string policyName, string? tenantId, int remaining, double retryAfterSeconds);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Rate limiting bypassed for policy '{PolicyName}' (claim: {Claim}, user: {UserId}).")]
    public static partial void LogBypassApplied(ILogger logger, string policyName, string claim, string? userId);

    [LoggerMessage(Level = LogLevel.Trace,
        Message = "Rate limit checked for policy '{PolicyName}' (tenant: {TenantId}). Remaining: {Remaining}/{Limit}.")]
    public static partial void LogRateLimitChecked(ILogger logger, string policyName, string? tenantId, int remaining, int limit);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Rate limiting using in-memory counter store. Counters are NOT shared across instances. Register IConnectionMultiplexer for production use.")]
    public static partial void LogInMemoryFallback(ILogger logger);
}
