using Granit.Core.Domain;
using OpenIddict.EntityFrameworkCore.Models;

namespace Granit.OpenIddict.Entities.OpenIddict;

/// <summary>
/// Multi-tenant OpenIddict authorization entity.
/// </summary>
public class GranitOpenIddictAuthorization : OpenIddictEntityFrameworkCoreAuthorization<Guid, GranitOpenIddictApplication, GranitOpenIddictToken>, IMultiTenant
{
    /// <summary>Gets or sets the tenant identifier.</summary>
    public Guid? TenantId { get; set; }
}
