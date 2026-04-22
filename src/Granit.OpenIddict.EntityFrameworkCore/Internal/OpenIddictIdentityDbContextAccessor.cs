using Granit.Persistence.EntityFrameworkCore.SharedConnection;
using Microsoft.EntityFrameworkCore;

namespace Granit.OpenIddict.EntityFrameworkCore.Internal;

/// <summary>
/// Exposes the scoped <see cref="OpenIddictDbContext"/> through the neutral
/// <see cref="IIdentityDbContextAccessor"/> contract, so consumers in sibling
/// modules (<c>Granit.Identity.Local.AspNetIdentity</c>) can reach the Identity
/// DbContext without taking a hard reference to this package's internal types.
/// </summary>
internal sealed class OpenIddictIdentityDbContextAccessor(OpenIddictDbContext context)
    : IIdentityDbContextAccessor
{
    public DbContext DbContext => context;
}
