using Granit.Domain;

namespace Granit.Localization.EntityFrameworkCore.Entities;

/// <summary>
/// Represents a per-resource, per-culture, per-key translation override stored in PostgreSQL.
/// </summary>
/// <remarks>
/// Each row represents a single translation override identified by the triple
/// (<see cref="ResourceName"/>, <see cref="CultureName"/>, <see cref="Key"/>) and,
/// when multi-tenancy is active, scoped by <see cref="TenantId"/>.
/// <para>
/// Audit fields (<c>CreatedAt</c>, <c>CreatedBy</c>, <c>ModifiedAt</c>, <c>ModifiedBy</c>) are
/// populated automatically by <c>AuditedEntityInterceptor</c> from <c>Granit.Persistence</c>,
/// satisfying the ISO 27001 3-year audit trail requirement.
/// </para>
/// </remarks>
public sealed class LocalizationOverride : AuditedEntity, IMultiTenant, IEmitEntityLifecycleEvents
{
    /// <summary>Tenant scope. <c>null</c> = host-level override (applies to all tenants).</summary>
    public Guid? TenantId { get; set; }

    /// <summary>Logical name of the localization resource (e.g. <c>"Acme"</c>). Max 200 characters.</summary>
    public string ResourceName { get; set; } = string.Empty;

    /// <summary>BCP 47 culture tag (e.g. <c>"fr"</c>, <c>"en-US"</c>). Max 20 characters.</summary>
    public string CultureName { get; set; } = string.Empty;

    /// <summary>Translation key to override. Max 500 characters.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Override value. Max 4 000 characters.</summary>
    public string Value { get; set; } = string.Empty;
}
