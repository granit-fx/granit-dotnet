using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Identity.Federated.EntityFrameworkCore;

/// <summary>
/// Granit module for EF Core identity user cache persistence.
/// Provides <see cref="Granit.Identity.Federated.Domain.FederatedIdentity"/> entity, <see cref="DbContext.IUserCacheDbContext"/>,
/// and <see cref="IUserLookupService"/> with cache-aside strategy and login-time sync.
/// </summary>
/// <remarks>
/// Registration of the generic store requires the application DbContext type. Call
/// <c>services.AddGranitIdentityEntityFrameworkCore&lt;TContext&gt;()</c>
/// in the host application's module or startup code.
/// </remarks>
[DependsOn(
    typeof(GranitIdentityFederatedModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitIdentityFederatedEntityFrameworkCoreModule : GranitModule;
