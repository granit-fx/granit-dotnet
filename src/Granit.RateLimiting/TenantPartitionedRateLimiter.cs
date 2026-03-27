using Granit.MultiTenancy;
using Granit.RateLimiting.Abstractions;
using Granit.RateLimiting.Diagnostics;
using Granit.RateLimiting.Internal;
using Granit.RateLimiting.Options;
using Granit.Users;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.RateLimiting;

/// <summary>
/// Core rate limiting logic shared between the ASP.NET Core endpoint filter and Wolverine behavior.
/// Partitions by tenant, resolves quotas dynamically, checks bypass roles, and records metrics.
/// </summary>
public sealed class TenantPartitionedRateLimiter(
    IRateLimitCounterStore counterStore,
    IRateLimitQuotaProvider quotaProvider,
    IOptions<GranitRateLimitingOptions> options,
    ICurrentTenant currentTenant,
    ICurrentUserService currentUser,
    RateLimitingMetrics metrics,
    ILogger<TenantPartitionedRateLimiter> logger)
{
    private readonly GranitRateLimitingOptions _options = options.Value;

    /// <summary>
    /// Checks whether the current request is within the rate limit for <paramref name="policyName"/>.
    /// </summary>
    /// <param name="policyName">Name of the rate limiting policy.</param>
    /// <param name="clientIp">Client IP address for IP-based partitioning. Pass <see langword="null"/> when not applicable (e.g., Wolverine messages).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rate limit result, or <see langword="null"/> if the policy is not configured.</returns>
    public async Task<RateLimitResult?> CheckAsync(string policyName, string? clientIp, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return null;
        }

        if (!_options.Policies.TryGetValue(policyName, out RateLimitPolicyOptions? policy))
        {
            return null;
        }

        // Check bypass claims
        if (TryBypass(policyName))
        {
            return null;
        }

        string? tenantId = currentTenant.IsAvailable ? currentTenant.Id?.ToString() : null;
        string key = BuildKey(policyName, policy.PartitionBy, tenantId, clientIp);

        int permitLimit = await quotaProvider.GetPermitLimitAsync(policyName, cancellationToken).ConfigureAwait(false)
                          ?? policy.PermitLimit;

        RateLimitResult result = await counterStore.CheckAndIncrementAsync(
            key, permitLimit, policy.Window, policy.Algorithm, policy, cancellationToken).ConfigureAwait(false);

        if (result.IsAllowed)
        {
            metrics.RecordAllowed(policyName, tenantId);
            RateLimitingLog.LogRateLimitChecked(logger, policyName, tenantId, result.Remaining, result.Limit);
        }
        else
        {
            metrics.RecordRejected(policyName, tenantId);
            RateLimitingLog.LogRateLimitExceeded(logger, policyName, tenantId, result.Remaining, result.RetryAfter.TotalSeconds);
        }

        return result;
    }

    private bool TryBypass(string policyName)
    {
        if (_options.BypassRoles.Length == 0 || !currentUser.IsAuthenticated)
        {
            return false;
        }

        string? matchedRole = _options.BypassRoles.FirstOrDefault(currentUser.IsInRole);
        if (matchedRole is not null)
        {
            RateLimitingLog.LogBypassApplied(logger, policyName, matchedRole, currentUser.UserId);
            return true;
        }

        return false;
    }

    private string BuildKey(string policyName, RateLimitPartition partition, string? tenantId, string? clientIp)
    {
        string tenant = tenantId ?? "global";
        string ip = clientIp ?? "unknown";
        string user = currentUser.IsAuthenticated ? currentUser.UserId ?? "anon" : "anon";

        // Hash tag {...} ensures all keys for the primary partition entity hash to the same Redis Cluster slot.
        return partition switch
        {
            RateLimitPartition.Tenant => $"{_options.KeyPrefix}:{{{tenant}}}:{policyName}",
            RateLimitPartition.TenantAndIp => $"{_options.KeyPrefix}:{{{tenant}}}:{ip}:{policyName}",
            RateLimitPartition.Ip => $"{_options.KeyPrefix}:{{{ip}}}:{policyName}",
            RateLimitPartition.User => $"{_options.KeyPrefix}:{{{user}}}:{policyName}",
            RateLimitPartition.TenantAndUser => $"{_options.KeyPrefix}:{{{tenant}}}:{user}:{policyName}",
            _ => $"{_options.KeyPrefix}:{{{tenant}}}:{policyName}",
        };
    }
}
