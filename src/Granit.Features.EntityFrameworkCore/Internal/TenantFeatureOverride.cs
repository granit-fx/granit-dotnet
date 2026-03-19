using Granit.Core.Domain;

namespace Granit.Features.EntityFrameworkCore.Internal;

/// <summary>
/// Represents a tenant-level override for a feature value.
/// Used to grant commercial exceptions (e.g. an Enterprise tenant allowed 5 000 patients
/// instead of the 200 defined by the plan).
/// </summary>
/// <remarks>
/// Audit fields (<c>CreatedAt</c>, <c>CreatedBy</c>, <c>ModifiedAt</c>, <c>ModifiedBy</c>) are
/// populated automatically by <c>AuditedEntityInterceptor</c> from <c>Granit.Persistence</c>,
/// satisfying the ISO 27001 3-year audit trail requirement.
/// <c>TenantId</c> is injected automatically by the same interceptor on insert.
/// </remarks>
internal sealed class TenantFeatureOverride : AuditedEntity, IMultiTenant, IEmitEntityLifecycleEvents
{
    /// <summary>Tenant scope. Never null for a tenant override (enforced at the store level).</summary>
    public Guid? TenantId { get; set; }

    /// <summary>Feature name, e.g. <c>"Acme.MaxUsersCount"</c>. Max 200 characters.</summary>
    public string FeatureName { get; set; } = string.Empty;

    /// <summary>Serialized feature value, e.g. <c>"true"</c> or <c>"5000"</c>. Max 2 000 characters.</summary>
    public string Value { get; set; } = string.Empty;
}
