using Granit.Domain;
using OpenIddict.EntityFrameworkCore.Models;

namespace Granit.OpenIddict.EntityFrameworkCore.Entities;

/// <summary>
/// Multi-tenant OpenIddict scope entity.
/// </summary>
public class GranitOpenIddictScope : OpenIddictEntityFrameworkCoreScope<Guid>, IMultiTenant
{
    /// <summary>Gets or sets the tenant identifier. Null = global scope.</summary>
    public Guid? TenantId { get; set; }
}
