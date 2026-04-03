using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Payments.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the payments module.
/// Meter: <c>Granit.Payments</c>.
/// </summary>
public sealed class PaymentsMetrics
{
    /// <summary>The meter name used for all payment metrics.</summary>
    public const string MeterName = "Granit.Payments";

    private const string TenantIdTag = "tenant_id";
    private const string GlobalTenant = "global";

    private readonly Counter<long> _paymentsCreated;
    private readonly Counter<long> _paymentsSucceeded;
    private readonly Counter<long> _paymentsFailed;
    private readonly Counter<long> _refundsCompleted;
    private readonly Counter<long> _disputesOpened;

    /// <summary>Initializes payment metrics using the specified meter factory.</summary>
    public PaymentsMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _paymentsCreated = meter.CreateCounter<long>(
            "granit.payments.transaction.created",
            description: "Number of payment transactions created.");

        _paymentsSucceeded = meter.CreateCounter<long>(
            "granit.payments.transaction.succeeded",
            description: "Number of payment transactions succeeded.");

        _paymentsFailed = meter.CreateCounter<long>(
            "granit.payments.transaction.failed",
            description: "Number of payment transactions failed.");

        _refundsCompleted = meter.CreateCounter<long>(
            "granit.payments.refund.completed",
            description: "Number of refunds completed.");

        _disputesOpened = meter.CreateCounter<long>(
            "granit.payments.dispute.opened",
            description: "Number of disputes opened.");
    }

    /// <summary>Records a payment creation.</summary>
    public void RecordCreated(string? tenantId, string providerName)
    {
        var tags = new TagList
        {
            { TenantIdTag, tenantId ?? GlobalTenant },
            { "provider_name", providerName },
        };
        _paymentsCreated.Add(1, tags);
    }

    /// <summary>Records a successful payment.</summary>
    public void RecordSucceeded(string? tenantId, string providerName)
    {
        var tags = new TagList
        {
            { TenantIdTag, tenantId ?? GlobalTenant },
            { "provider_name", providerName },
        };
        _paymentsSucceeded.Add(1, tags);
    }

    /// <summary>Records a failed payment.</summary>
    public void RecordFailed(string? tenantId, string providerName, string? failureCode)
    {
        var tags = new TagList
        {
            { TenantIdTag, tenantId ?? GlobalTenant },
            { "provider_name", providerName },
            { "failure_code", failureCode ?? "unknown" },
        };
        _paymentsFailed.Add(1, tags);
    }

    /// <summary>Records a completed refund.</summary>
    public void RecordRefundCompleted(string? tenantId)
    {
        var tags = new TagList { { TenantIdTag, tenantId ?? GlobalTenant } };
        _refundsCompleted.Add(1, tags);
    }

    /// <summary>Records a dispute opened.</summary>
    public void RecordDisputeOpened(string? tenantId)
    {
        var tags = new TagList { { TenantIdTag, tenantId ?? GlobalTenant } };
        _disputesOpened.Add(1, tags);
    }
}
