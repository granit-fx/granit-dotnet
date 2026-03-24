using System.Threading.RateLimiting;
using Granit.Http.Bulkhead.Abstractions;
using Granit.Http.Bulkhead.Diagnostics;
using Granit.Http.Bulkhead.Exceptions;
using Granit.Http.Bulkhead.Internal;
using Granit.Http.Bulkhead.Options;
using Granit.MultiTenancy;
using Granit.Users;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Http.Bulkhead;

/// <summary>
/// Core bulkhead logic shared between the ASP.NET Core endpoint filter and Wolverine middleware.
/// Partitions by tenant, resolves quotas dynamically, checks bypass (machine actors + roles),
/// and records metrics.
/// </summary>
public sealed class TenantPartitionedBulkhead(
    ConcurrencyLimiterRegistry registry,
    IBulkheadQuotaProvider quotaProvider,
    IOptions<GranitBulkheadOptions> options,
    ICurrentTenant currentTenant,
    ICurrentUserService currentUser,
    BulkheadMetrics metrics,
    ILogger<TenantPartitionedBulkhead> logger)
{
    private readonly GranitBulkheadOptions _options = options.Value;

    /// <summary>
    /// Acquires a concurrency permit for <paramref name="policyName"/> and the current tenant.
    /// </summary>
    /// <returns>
    /// A <see cref="BulkheadLease"/> that must be disposed when the operation completes.
    /// Throws <see cref="BulkheadRejectedException"/> if the bulkhead is full.
    /// Returns <see cref="BulkheadLease.NoOp"/> when disabled, bypassed, or policy not found.
    /// </returns>
    public async Task<BulkheadLease> AcquireAsync(string policyName, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return BulkheadLease.NoOp;
        }

        if (!_options.Policies.TryGetValue(policyName, out BulkheadPolicyOptions? policy))
        {
            return BulkheadLease.NoOp;
        }

        if (TryBypass(policyName))
        {
            return BulkheadLease.NoOp;
        }

        string? tenantId = currentTenant.IsAvailable ? currentTenant.Id?.ToString() : null;
        string key = BuildKey(policyName, tenantId);

        int permitLimit = await quotaProvider.GetPermitLimitAsync(policyName, cancellationToken).ConfigureAwait(false)
                          ?? policy.PermitLimit;

        // Create a linked token with queue timeout when queuing is enabled.
        using CancellationTokenSource? timeoutCts = policy.QueueLimit > 0
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : null;

        if (timeoutCts is not null)
        {
            timeoutCts.CancelAfter(policy.QueueTimeout);
        }

        CancellationToken effectiveToken = timeoutCts?.Token ?? cancellationToken;

        RateLimitLease innerLease;
        try
        {
            innerLease = await registry.AcquireAsync(key, permitLimit, policy.QueueLimit, effectiveToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCts is not null && timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // Queue timeout expired — treat as rejection.
            metrics.RecordRejected(policyName, tenantId);
            BulkheadLog.LogBulkheadRejected(logger, policyName, tenantId, permitLimit, policy.QueueLimit);
            throw new BulkheadRejectedException(policyName, permitLimit, policy.QueueLimit);
        }

        if (!innerLease.IsAcquired)
        {
            innerLease.Dispose();
            metrics.RecordRejected(policyName, tenantId);
            BulkheadLog.LogBulkheadRejected(logger, policyName, tenantId, permitLimit, policy.QueueLimit);
            throw new BulkheadRejectedException(policyName, permitLimit, policy.QueueLimit);
        }

        metrics.RecordAcquired(policyName, tenantId);
        BulkheadLog.LogLeaseAcquired(logger, policyName, tenantId);

        return new BulkheadLease(innerLease, () =>
        {
            metrics.RecordReleased(policyName, tenantId);
            BulkheadLog.LogLeaseReleased(logger, policyName, tenantId);
        });
    }

    private bool TryBypass(string policyName)
    {
        // Machine actors (System, ExternalSystem) always bypass.
        if (currentUser.IsMachine)
        {
            BulkheadLog.LogBypassApplied(logger, policyName, "IsMachine", currentUser.UserId);
            return true;
        }

        if (_options.BypassRoles.Length == 0 || !currentUser.IsAuthenticated)
        {
            return false;
        }

        string? matchedRole = _options.BypassRoles.FirstOrDefault(currentUser.IsInRole);
        if (matchedRole is not null)
        {
            BulkheadLog.LogBypassApplied(logger, policyName, $"Role:{matchedRole}", currentUser.UserId);
            return true;
        }

        return false;
    }

    private static string BuildKey(string policyName, string? tenantId) =>
        $"{policyName}:{tenantId ?? "global"}";
}
