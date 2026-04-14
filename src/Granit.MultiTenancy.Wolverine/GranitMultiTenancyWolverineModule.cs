using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore.Hosting;
using Granit.Wolverine;

namespace Granit.MultiTenancy.Wolverine;

/// <summary>
/// Wolverine integration for Granit.MultiTenancy.
/// Provides automatic runtime tenant provisioning via
/// <see cref="Handlers.TenantProvisioningHandler"/>, which listens to
/// <see cref="Events.TenantCreatedEvent"/> and delegates to <see cref="ITenantProvisioner"/>.
/// </summary>
/// <remarks>
/// <para>
/// During <c>--migrate</c> mode, Wolverine is not started and tenant provisioning is
/// handled by <c>GranitMigrationRunner</c>'s post-seed re-migration pass. This module
/// only activates for runtime tenant creation via the admin API.
/// </para>
/// <para>
/// The handler is auto-discovered by Wolverine's assembly scanning — no manual
/// registration in <see cref="ConfigureServices"/> is needed.
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitMultiTenancyModule),
    typeof(GranitPersistenceEntityFrameworkCoreHostingModule),
    typeof(GranitWolverineModule))]
public sealed class GranitMultiTenancyWolverineModule : GranitModule;
