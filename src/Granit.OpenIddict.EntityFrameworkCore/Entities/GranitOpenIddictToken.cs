using Granit.Domain;
using OpenIddict.EntityFrameworkCore.Models;

namespace Granit.OpenIddict.EntityFrameworkCore.Entities;

/// <summary>
/// Multi-tenant OpenIddict token entity.
/// </summary>
public class GranitOpenIddictToken : OpenIddictEntityFrameworkCoreToken<Guid, GranitOpenIddictApplication, GranitOpenIddictAuthorization>, IMultiTenant
{
    /// <summary>Gets or sets the tenant identifier.</summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp of the last observed activity on this session, maintained by
    /// the session heartbeat on the refresh token. Mirrors the BFF's <c>BffTokenSet.LastAccessedAt</c>
    /// and Keycloak's server-side <c>session.LastAccess</c>: a durable last-access the session API
    /// surfaces as <c>UserSessionDescriptor.LastAccessedAt</c> and the idle-session job enforces on.
    /// </summary>
    public DateTimeOffset? LastActivityAt { get; set; }
}
