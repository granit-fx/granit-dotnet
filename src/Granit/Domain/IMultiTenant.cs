namespace Granit.Domain;

/// <summary>
/// Interface for entities that belong to a specific tenant.
/// The <see cref="TenantId"/> is automatically set by
/// <c>AuditedEntityInterceptor</c> during persistence (SaveChanges).
/// </summary>
public interface IMultiTenant
{
    /// <summary>
    /// Identifier of the tenant that owns this entity.
    /// <c>null</c> indicates global data shared across all tenants.
    /// </summary>
    Guid? TenantId { get; set; }
}
