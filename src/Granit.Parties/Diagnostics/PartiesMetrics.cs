using System.Diagnostics;
using System.Diagnostics.Metrics;
using Granit.Parties.Domain;

namespace Granit.Parties.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the parties module.
/// Meter: <c>Granit.Parties</c>.
/// </summary>
public sealed class PartiesMetrics
{
    /// <summary>The meter name used for all party metrics.</summary>
    public const string MeterName = "Granit.Parties";

    private const string TenantIdTag = "tenant_id";
    private const string GlobalTenant = "global";

    private readonly Counter<long> _partiesCreated;
    private readonly Counter<long> _partiesUpdated;
    private readonly Counter<long> _partiesSuspended;
    private readonly Counter<long> _partiesActivated;
    private readonly Counter<long> _partiesArchived;
    private readonly Counter<long> _partiesPseudonymized;
    private readonly Counter<long> _externalMappingsAdded;
    private readonly Counter<long> _rolesChanged;
    private readonly Counter<long> _taxStatusChanged;

    /// <summary>Initializes party metrics using the specified meter factory.</summary>
    public PartiesMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _partiesCreated = meter.CreateCounter<long>(
            "granit.parties.party.created",
            description: "Number of parties created.");

        _partiesUpdated = meter.CreateCounter<long>(
            "granit.parties.party.updated",
            description: "Number of party identity updates (name / website / locale / timezone).");

        _partiesSuspended = meter.CreateCounter<long>(
            "granit.parties.party.suspended",
            description: "Number of parties suspended.");

        _partiesActivated = meter.CreateCounter<long>(
            "granit.parties.party.activated",
            description: "Number of parties reactivated from a suspended state.");

        _partiesArchived = meter.CreateCounter<long>(
            "granit.parties.party.archived",
            description: "Number of parties archived (terminal state).");

        _partiesPseudonymized = meter.CreateCounter<long>(
            "granit.parties.party.pseudonymized",
            description: "Number of parties whose personal data was pseudonymized (GDPR Art. 17).");

        _externalMappingsAdded = meter.CreateCounter<long>(
            "granit.parties.external_mapping.added",
            description: "Number of external provider mappings registered (Stripe / Mollie / Odoo / …).");

        _rolesChanged = meter.CreateCounter<long>(
            "granit.parties.role.changed",
            description: "Number of role flag changes (added or removed).");

        _taxStatusChanged = meter.CreateCounter<long>(
            "granit.parties.tax_status.changed",
            description: "Number of customer-specific tax status changes (exempt / reverse-charge / standard).");
    }

    /// <summary>Records a party creation.</summary>
    public void RecordCreated(string? tenantId)
    {
        var tags = new TagList { { TenantIdTag, tenantId ?? GlobalTenant } };
        _partiesCreated.Add(1, tags);
    }

    /// <summary>Records a party identity update.</summary>
    public void RecordUpdated(string? tenantId)
    {
        var tags = new TagList { { TenantIdTag, tenantId ?? GlobalTenant } };
        _partiesUpdated.Add(1, tags);
    }

    /// <summary>Records a party suspension.</summary>
    public void RecordSuspended(string? tenantId)
    {
        var tags = new TagList { { TenantIdTag, tenantId ?? GlobalTenant } };
        _partiesSuspended.Add(1, tags);
    }

    /// <summary>Records a party reactivation.</summary>
    public void RecordActivated(string? tenantId)
    {
        var tags = new TagList { { TenantIdTag, tenantId ?? GlobalTenant } };
        _partiesActivated.Add(1, tags);
    }

    /// <summary>Records a party archival.</summary>
    public void RecordArchived(string? tenantId)
    {
        var tags = new TagList { { TenantIdTag, tenantId ?? GlobalTenant } };
        _partiesArchived.Add(1, tags);
    }

    /// <summary>Records a personal-data pseudonymization (GDPR Art. 17).</summary>
    public void RecordPseudonymized(string? tenantId)
    {
        var tags = new TagList { { TenantIdTag, tenantId ?? GlobalTenant } };
        _partiesPseudonymized.Add(1, tags);
    }

    /// <summary>Records an external-mapping registration.</summary>
    /// <remarks>
    /// <paramref name="providerName"/> is folded to the reserved set declared in
    /// <see cref="PartyExternalProviderNames"/> — unrecognised values are mapped to the
    /// <c>"other"</c> bucket so an authenticated caller cannot inflate Prometheus
    /// cardinality by registering mappings with random distinct provider names
    /// (CWE-770).
    /// </remarks>
    public void RecordExternalMappingAdded(string? tenantId, string providerName)
    {
        string normalized = PartyExternalProviderNames.IsReserved(providerName)
            ? providerName.ToLowerInvariant()
            : "other";
        var tags = new TagList
        {
            { TenantIdTag, tenantId ?? GlobalTenant },
            { "provider_name", normalized },
        };
        _externalMappingsAdded.Add(1, tags);
    }

    /// <summary>Records a role flag change.</summary>
    public void RecordRoleChanged(string? tenantId)
    {
        var tags = new TagList { { TenantIdTag, tenantId ?? GlobalTenant } };
        _rolesChanged.Add(1, tags);
    }

    /// <summary>Records a customer-specific tax status change.</summary>
    public void RecordTaxStatusChanged(string? tenantId)
    {
        var tags = new TagList { { TenantIdTag, tenantId ?? GlobalTenant } };
        _taxStatusChanged.Add(1, tags);
    }
}
