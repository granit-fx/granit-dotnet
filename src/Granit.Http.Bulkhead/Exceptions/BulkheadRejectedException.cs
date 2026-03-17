using Granit.Core.Exceptions;

namespace Granit.Http.Bulkhead.Exceptions;

/// <summary>
/// Thrown when a bulkhead concurrency limit is exceeded and the queue is full.
/// Mapped to HTTP 503 Service Unavailable.
/// </summary>
public sealed class BulkheadRejectedException : BusinessException
{
    /// <summary>
    /// Initializes a new instance of <see cref="BulkheadRejectedException"/>.
    /// </summary>
    public BulkheadRejectedException(string policyName, int permitLimit, int queueLimit)
        : base($"Bulkhead full for policy '{policyName}'. PermitLimit: {permitLimit}, QueueLimit: {queueLimit}.")
    {
        PolicyName = policyName;
        PermitLimit = permitLimit;
        QueueLimit = queueLimit;
    }

    /// <summary>Name of the bulkhead policy.</summary>
    public string PolicyName { get; }

    /// <summary>Maximum concurrent permits for this policy per tenant.</summary>
    public int PermitLimit { get; }

    /// <summary>Maximum queued requests for this policy per tenant.</summary>
    public int QueueLimit { get; }
}
