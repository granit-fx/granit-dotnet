using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.CustomerBalance.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the customer balance module.
/// Meter: <c>Granit.CustomerBalance</c>.
/// </summary>
public sealed class CustomerBalanceMetrics
{
    /// <summary>The meter name used for all customer balance metrics.</summary>
    public const string MeterName = "Granit.CustomerBalance";

    private const string TenantIdTag = "tenant_id";
    private const string GlobalTenant = "global";
    private const string CurrencyTag = "currency";
    private const string SourceTag = "source";

    private readonly Counter<long> _credited;
    private readonly Counter<long> _debited;
    private readonly Counter<long> _expired;
    private readonly Counter<long> _expiringNotified;

    /// <summary>Initializes customer balance metrics using the specified meter factory.</summary>
    public CustomerBalanceMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _credited = meter.CreateCounter<long>(
            "granit.customer_balance.account.credited",
            description: "Number of credit transactions recorded.");

        _debited = meter.CreateCounter<long>(
            "granit.customer_balance.account.debited",
            description: "Number of debit transactions recorded.");

        _expired = meter.CreateCounter<long>(
            "granit.customer_balance.credit.expired",
            description: "Number of promotional credits expired.");

        _expiringNotified = meter.CreateCounter<long>(
            "granit.customer_balance.credit.expiring_notified",
            description: "Number of CreditExpiringEto notifications emitted by the daily scanner.");
    }

    /// <summary>Records a credit transaction.</summary>
    public void RecordCredited(string? tenantId, string currency, string source)
    {
        var tags = new TagList
        {
            { TenantIdTag, tenantId ?? GlobalTenant },
            { CurrencyTag, currency },
            { SourceTag, source },
        };
        _credited.Add(1, tags);
    }

    /// <summary>Records a debit transaction.</summary>
    public void RecordDebited(string? tenantId, string currency, string source)
    {
        var tags = new TagList
        {
            { TenantIdTag, tenantId ?? GlobalTenant },
            { CurrencyTag, currency },
            { SourceTag, source },
        };
        _debited.Add(1, tags);
    }

    /// <summary>Records a promotional credit expiration.</summary>
    public void RecordExpired(string? tenantId, string currency)
    {
        var tags = new TagList
        {
            { TenantIdTag, tenantId ?? GlobalTenant },
            { CurrencyTag, currency },
        };
        _expired.Add(1, tags);
    }

    /// <summary>Records a "credit expiring soon" notification emission.</summary>
    public void RecordExpiringNotified(string? tenantId, string currency)
    {
        var tags = new TagList
        {
            { TenantIdTag, tenantId ?? GlobalTenant },
            { CurrencyTag, currency },
        };
        _expiringNotified.Add(1, tags);
    }
}
