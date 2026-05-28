using Granit.Identity.Federated.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Identity.Federated.EntityFrameworkCore.Internal;

/// <summary>
/// Common shape exposed by every Identity.Federated DbContext flavour so
/// <see cref="EfCoreUserCacheStore"/> can dispatch reads and writes regardless of whether
/// the storage layout is <c>Shared</c> (one context) or <c>Segregated</c> (host context
/// + tenant context).
/// </summary>
internal interface IIdentityFederatedDbContext : IAsyncDisposable, IDisposable
{
    /// <summary>Federated identity entries in the current scope.</summary>
    DbSet<FederatedIdentity> FederatedIdentities { get; }

    /// <summary>Persists pending changes.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
