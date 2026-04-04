using Granit.Invoicing.Odoo.Domain;

namespace Granit.Invoicing.Odoo;

/// <summary>Persistence for Odoo partner ↔ Granit tenant mappings.</summary>
public interface IOdooPartnerMappingStore
{
    /// <summary>Returns the Odoo partner mapping for a tenant.</summary>
    Task<OdooPartnerMapping?> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Persists a new partner mapping.</summary>
    Task AddAsync(OdooPartnerMapping mapping, CancellationToken cancellationToken = default);
}
