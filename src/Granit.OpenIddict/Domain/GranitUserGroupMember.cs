using Granit.Domain;

namespace Granit.OpenIddict.Domain;

/// <summary>
/// Join entity linking a <see cref="GranitUser"/> to a <see cref="GranitUserGroup"/>.
/// </summary>
public class GranitUserGroupMember : AuditedEntity, IMultiTenant
{
    /// <summary>Gets or sets the group identifier.</summary>
    public Guid GroupId { get; set; }

    /// <summary>Gets or sets the user identifier.</summary>
    public Guid UserId { get; set; }

    /// <summary>Gets or sets the tenant identifier for multi-tenant isolation.</summary>
    public Guid? TenantId { get; set; }
}
