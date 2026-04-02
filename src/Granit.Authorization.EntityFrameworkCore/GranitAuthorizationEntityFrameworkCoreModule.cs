using Granit.Authorization;
using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Authorization.EntityFrameworkCore;

/// <summary>
/// Granit module for EF Core authorization grant persistence.
/// Provides <see cref="Entities.PermissionGrant"/> entity, <see cref="DbContext.IPermissionGrantDbContext"/>,
/// and <see cref="Abstractions.IPermissionGrantStore"/> implementation backed by EF Core.
/// </summary>
/// <remarks>
/// Registration of the generic store requires the application DbContext type. Call
/// <c>services.AddGranitAuthorizationEntityFrameworkCore&lt;TContext&gt;()</c>
/// in the host application's module or startup code.
/// </remarks>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitAuthorizationEntityFrameworkCoreModule : GranitModule;
