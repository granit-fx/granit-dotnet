using System.Diagnostics;
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
            // Fail-loud: the caller referenced a policy that is not configured. We still return
            // NoOp (the caller cannot infer protection from a non-existent policy), but operators
            // get a warning + counter so misconfiguration is detected before it matters under load.
            BulkheadLog.LogUnknownPolicy(logger, policyName);
            metrics.RecordUnknownPolicy(policyName);
            return BulkheadLease.NoOp;
        }

        if (TryBypass(policyName))
        {
            return BulkheadLease.NoOp;
        }

        string? tenantId = currentTenant.IsAvailable ? currentTenant.Id?.ToString() : null;
        string key = BuildKey(policyName, tenantId);

        using Activity? activity = BulkheadActivitySource.Source.StartActivity(
            "Granit.Http.Bulkhead.Acquire",
            ActivityKind.Internal);
        activity?.SetTag("bulkhead.policy", policyName);
        activity?.SetTag("bulkhead.tenant_id", tenantId ?? "global");

        int permitLimit = await quotaProvider.GetPermitLimitAsync(policyName, cancellationToken).ConfigureAwait(false)
                          ?? policy.PermitLimit;
        activity?.SetTag("bulkhead.permit_limit", permitLimit);
        activity?.SetTag("bulkhead.queue_limit", policy.QueueLimit);

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
            activity?.SetStatus(ActivityStatusCode.Error, "queue_timeout");
            metrics.RecordRejected(policyName, tenantId);
            BulkheadLog.LogBulkheadRejected(logger, policyName, tenantId, permitLimit, policy.QueueLimit);
            throw new BulkheadRejectedException(policyName, permitLimit, policy.QueueLimit);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Caller abandoned (client disconnect or upstream cancellation) — not a bulkhead rejection.
            // Record separately so operators can distinguish saturation from client churn.
            activity?.SetStatus(ActivityStatusCode.Error, "caller_cancelled");
            metrics.RecordAbandoned(policyName, tenantId);
            BulkheadLog.LogAcquireAbandoned(logger, policyName, tenantId);
            throw;
        }

        if (!innerLease.IsAcquired)
        {
            innerLease.Dispose();
            activity?.SetStatus(ActivityStatusCode.Error, "rejected");
            metrics.RecordRejected(policyName, tenantId);
            BulkheadLog.LogBulkheadRejected(logger, policyName, tenantId, permitLimit, policy.QueueLimit);
            throw new BulkheadRejectedException(policyName, permitLimit, policy.QueueLimit);
        }

        activity?.SetStatus(ActivityStatusCode.Ok);
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
            metrics.RecordBypassed(policyName, "machine");
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
            // Reason tag is the fixed enum "role" — never the role name (unbounded operator config
            // would expand metric cardinality). The role name remains in the log for forensics.
            metrics.RecordBypassed(policyName, "role");
            BulkheadLog.LogBypassApplied(logger, policyName, $"Role:{matchedRole}", currentUser.UserId);
            return true;
        }

        return false;
    }

    private static string BuildKey(string policyName, string? tenantId) =>
        $"{policyName}:{tenantId ?? "global"}";
}
