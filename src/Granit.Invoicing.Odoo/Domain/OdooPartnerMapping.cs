using Granit.Domain;

namespace Granit.Invoicing.Odoo.Domain;

/// <summary>
/// Maps a Granit tenant to an Odoo <c>res.partner</c> record.
/// </summary>
// TODO(audit/A3): Persistence is not yet implemented. A dedicated
// Granit.Invoicing.Odoo.EntityFrameworkCore package is required to back this
// aggregate with a DbContext, configurations, and migrations. Tracked in the
// Invoicing audit findings.
public sealed class OdooPartnerMapping : Entity
{
    private OdooPartnerMapping() { }

    /// <summary>Creates a new tenant → Odoo partner mapping.</summary>
    public static OdooPartnerMapping Create(Guid id, Guid tenantId, int odooPartnerId)
    {
        return new OdooPartnerMapping
        {
            Id = id,
            TenantId = tenantId,
            OdooPartnerId = odooPartnerId,
        };
    }

    /// <summary>The Granit tenant ID.</summary>
    public Guid TenantId { get; private set; }

    /// <summary>The Odoo res.partner ID.</summary>
    public int OdooPartnerId { get; private set; }
}
