using Granit.DataExchange.Extensions;
using Granit.Diagnostics;
using Granit.Entities.Extensions;
using Granit.Events;
using Granit.Guids;
using Granit.Identity;
using Granit.Identity.Local.Diagnostics;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Entities;
using Granit.Identity.Local.Exports;
using Granit.Identity.Local.Queries;
using Granit.Modularity;
using Granit.QueryEngine.Extensions;
using Granit.Settings;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Identity.Local;

/// <summary>
/// Granit module for shared local identity abstractions.
/// </summary>
/// <remarks>
/// Provides <see cref="Entities.GranitUser"/>, <see cref="Entities.GranitRole"/>,
/// <see cref="Domain.GranitUserGroup"/>, provider-agnostic service interfaces,
/// integration events, and <see cref="Services.ILocalIdentityGroupStore"/>.
/// Used by both <c>Granit.OpenIddict.EntityFrameworkCore</c> and any future
/// self-hosted identity provider (e.g. Duende Identity Server).
/// </remarks>
[DependsOn(
    typeof(GranitEventsModule),
    typeof(GranitGuidsModule),
    typeof(GranitIdentityModule),
    typeof(GranitSettingsModule),
    typeof(GranitTimingModule))]
public sealed class GranitIdentityLocalModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        GranitActivitySourceRegistry.Register(IdentityLocalActivitySource.Name);

        context.Services.TryAddSingleton<IdentityLocalMetrics>();

        // Query + Export definitions (ADR-020: owned by the base module).
        context.Services.AddQueryDefinition<GranitRole, GranitRoleQueryDefinition>();
        context.Services.AddQueryDefinition<GranitUserGroup, GranitUserGroupQueryDefinition>();
        context.Services.AddExportDefinition<GranitRole, GranitRoleExportDefinition>();
        context.Services.AddExportDefinition<GranitUserGroup, GranitUserGroupExportDefinition>();

        // Phase 2 EntityDefinitions (ADR-050).
        context.Services.AddEntityDefinition<GranitRole, GranitRoleEntityDefinition>();
        context.Services.AddEntityDefinition<GranitUserGroup, GranitUserGroupEntityDefinition>();
    }
}
