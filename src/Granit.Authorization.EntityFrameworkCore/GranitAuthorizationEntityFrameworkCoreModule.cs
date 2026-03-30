using Granit.Authorization;
using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Authorization.EntityFrameworkCore;

/// <summary>
/// Granit module for EF Core authorization grant persistence.
/// Provides <see cref="Entities.PermissionGrant"/> entity, <see cref="DbContext.IPermissionGrantDbContext"/>,
/// and <see cref="Abstractions.IPermissionManagerReader"/>/<see cref="Abstractions.IPermissionManagerWriter"/> with ISO 27001 audit logging.
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
