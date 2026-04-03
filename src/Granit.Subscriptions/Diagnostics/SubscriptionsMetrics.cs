using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Subscriptions.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the subscriptions module.
/// Meter: <c>Granit.Subscriptions</c>.
/// </summary>
public sealed class SubscriptionsMetrics
{
    /// <summary>The meter name used for all subscription metrics.</summary>
    public const string MeterName = "Granit.Subscriptions";

    private const string TenantIdTag = "tenant_id";
    private const string GlobalTenant = "global";

    private readonly Counter<long> _subscriptionsCreated;
    private readonly Counter<long> _subscriptionsActivated;
    private readonly Counter<long> _subscriptionsCancelled;
    private readonly Counter<long> _subscriptionsExpired;
    private readonly Counter<long> _planChanges;
    private readonly Counter<long> _periodAdvances;

    /// <summary>Initializes subscription metrics using the specified meter factory.</summary>
    public SubscriptionsMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _subscriptionsCreated = meter.CreateCounter<long>(
            "granit.subscriptions.subscription.created",
            description: "Number of subscriptions created.");

        _subscriptionsActivated = meter.CreateCounter<long>(
            "granit.subscriptions.subscription.activated",
            description: "Number of subscriptions activated.");

        _subscriptionsCancelled = meter.CreateCounter<long>(
            "granit.subscriptions.subscription.cancelled",
            description: "Number of subscriptions cancelled.");

        _subscriptionsExpired = meter.CreateCounter<long>(
            "granit.subscriptions.subscription.expired",
            description: "Number of subscriptions expired.");

        _planChanges = meter.CreateCounter<long>(
            "granit.subscriptions.plan.changed",
            description: "Number of plan changes.");

        _periodAdvances = meter.CreateCounter<long>(
            "granit.subscriptions.period.advanced",
            description: "Number of billing period advances.");
    }

    /// <summary>Records a subscription creation.</summary>
    public void RecordCreated(string? tenantId)
    {
        var tags = new TagList { { TenantIdTag, tenantId ?? GlobalTenant } };
        _subscriptionsCreated.Add(1, tags);
    }

    /// <summary>Records a subscription activation.</summary>
    public void RecordActivated(string? tenantId)
    {
        var tags = new TagList { { TenantIdTag, tenantId ?? GlobalTenant } };
        _subscriptionsActivated.Add(1, tags);
    }

    /// <summary>Records a subscription cancellation.</summary>
    public void RecordCancelled(string? tenantId)
    {
        var tags = new TagList { { TenantIdTag, tenantId ?? GlobalTenant } };
        _subscriptionsCancelled.Add(1, tags);
    }

    /// <summary>Records a subscription expiration.</summary>
    public void RecordExpired(string? tenantId)
    {
        var tags = new TagList { { TenantIdTag, tenantId ?? GlobalTenant } };
        _subscriptionsExpired.Add(1, tags);
    }

    /// <summary>Records a plan change.</summary>
    public void RecordPlanChanged(string? tenantId)
    {
        var tags = new TagList { { TenantIdTag, tenantId ?? GlobalTenant } };
        _planChanges.Add(1, tags);
    }

    /// <summary>Records a billing period advance.</summary>
    public void RecordPeriodAdvanced(string? tenantId)
    {
        var tags = new TagList { { TenantIdTag, tenantId ?? GlobalTenant } };
        _periodAdvances.Add(1, tags);
    }
}
