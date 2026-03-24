using Granit.Domain;
using OpenIddict.EntityFrameworkCore.Models;

namespace Granit.OpenIddict.Entities.OpenIddict;

/// <summary>
/// Multi-tenant OpenIddict token entity.
/// </summary>
public class GranitOpenIddictToken : OpenIddictEntityFrameworkCoreToken<Guid, GranitOpenIddictApplication, GranitOpenIddictAuthorization>, IMultiTenant
{
    /// <summary>Gets or sets the tenant identifier.</summary>
    public Guid? TenantId { get; set; }
}
