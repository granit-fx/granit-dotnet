using System.Diagnostics.Metrics;

namespace Granit.Documents.PublicLinks.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the public-links module. Meter:
/// <c>Granit.Documents.PublicLinks</c>.
/// </summary>
public sealed class DocumentsPublicLinksMetrics
{
    /// <summary>Meter name.</summary>
    public const string MeterName = "Granit.Documents.PublicLinks";

    private const string TagTenantId = "tenant_id";
    private const string TagScope = "scope";
    private const string TagReason = "reason";
    private const string DefaultTenant = "global";

    private readonly Counter<long> _created;
    private readonly Counter<long> _revoked;
    private readonly Counter<long> _consumed;
    private readonly Counter<long> _concurrencyConflicts;

    /// <summary>Initialises the meter and instruments.</summary>
    public DocumentsPublicLinksMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        Meter meter = meterFactory.Create(MeterName);

        _created = meter.CreateCounter<long>(
            "granit.documents.public_links.created.count",
            description: "Number of public links minted.");
        _revoked = meter.CreateCounter<long>(
            "granit.documents.public_links.revoked.count",
            description: "Number of public links revoked by an operator.");
        _consumed = meter.CreateCounter<long>(
            "granit.documents.public_links.consumed.count",
            description: "Number of successful public-link redemptions.");
        _concurrencyConflicts = meter.CreateCounter<long>(
            "granit.documents.public_links.concurrency_conflicts.count",
            description: "Number of public-link redemptions that lost the optimistic concurrency race.");
    }

    /// <summary>Records a freshly minted link.</summary>
    public void RecordCreated(string? tenantId, string scope) =>
        _created.Add(1,
            new KeyValuePair<string, object?>(TagTenantId, tenantId ?? DefaultTenant),
            new KeyValuePair<string, object?>(TagScope, scope));

    /// <summary>Records an operator-initiated revocation.</summary>
    public void RecordRevoked(string? tenantId, string reason) =>
        _revoked.Add(1,
            new KeyValuePair<string, object?>(TagTenantId, tenantId ?? DefaultTenant),
            new KeyValuePair<string, object?>(TagReason, reason));

    /// <summary>Records a successful redemption.</summary>
    public void RecordConsumed(string? tenantId, string scope) =>
        _consumed.Add(1,
            new KeyValuePair<string, object?>(TagTenantId, tenantId ?? DefaultTenant),
            new KeyValuePair<string, object?>(TagScope, scope));

    /// <summary>Records a redemption that lost the optimistic concurrency race against another consumer.</summary>
    public void RecordConcurrencyConflict(string? tenantId) =>
        _concurrencyConflicts.Add(1,
            new KeyValuePair<string, object?>(TagTenantId, tenantId ?? DefaultTenant));
}
