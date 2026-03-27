using Granit.Domain;

namespace Granit.Identity.Local.Domain;

/// <summary>
/// User group entity for organizing users.
/// </summary>
/// <remarks>
/// ASP.NET Core Identity has no native group concept. This entity provides group management
/// backed by EF Core directly, following the <see cref="AuditedEntity"/> + <see cref="IMultiTenant"/> pattern.
/// </remarks>
public class GranitUserGroup : AuditedEntity, IMultiTenant
{
    /// <summary>Gets or sets the group name (unique per tenant, max 256 characters).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets a human-readable description (max 512 characters).</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the tenant identifier for multi-tenant isolation.</summary>
    public Guid? TenantId { get; set; }
}
