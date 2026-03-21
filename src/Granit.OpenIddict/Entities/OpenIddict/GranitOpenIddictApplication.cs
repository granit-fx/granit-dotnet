using Granit.Core.Domain;
using OpenIddict.EntityFrameworkCore.Models;

namespace Granit.OpenIddict.Entities.OpenIddict;

/// <summary>
/// Multi-tenant OpenIddict application entity.
/// </summary>
/// <remarks>
/// Extends the default OpenIddict application with <see cref="IMultiTenant"/> support.
/// Applications with <c>TenantId == null</c> are global (visible to all tenants).
/// </remarks>
public class GranitOpenIddictApplication : OpenIddictEntityFrameworkCoreApplication<Guid, GranitOpenIddictAuthorization, GranitOpenIddictToken>, IMultiTenant
{
    /// <summary>Gets or sets the tenant identifier. Null = global application.</summary>
    public Guid? TenantId { get; set; }
}
