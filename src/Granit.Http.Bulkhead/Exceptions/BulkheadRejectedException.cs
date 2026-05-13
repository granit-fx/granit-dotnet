using System.Globalization;
using Granit.Exceptions;

namespace Granit.Http.Bulkhead.Exceptions;

/// <summary>
/// Thrown when a bulkhead concurrency limit is exceeded and the queue is full.
/// Mapped to HTTP 503 Service Unavailable. Localized via error code
/// <c>Bulkhead:Rejected</c> in resource <c>Bulkhead</c>.
/// </summary>
public sealed class BulkheadRejectedException : BusinessException
{
    /// <summary>Stable error code used for localization lookup.</summary>
    public const string Code = "Bulkhead:Rejected";

    /// <summary>
    /// Initializes a new instance of <see cref="BulkheadRejectedException"/>.
    /// </summary>
    public BulkheadRejectedException(string policyName, int permitLimit, int queueLimit)
        : base(
            Code,
            string.Format(
                CultureInfo.InvariantCulture,
                "Bulkhead full for policy '{0}'. Concurrent permit limit ({1}) and queue limit ({2}) exhausted. Please retry shortly.",
                policyName,
                permitLimit,
                queueLimit))
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
