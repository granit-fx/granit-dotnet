using Granit.Events;
using Granit.Guids;
using Granit.Identity;
using Granit.Identity.Local.Diagnostics;
using Granit.Modularity;
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
    typeof(GranitTimingModule))]
public sealed class GranitIdentityLocalModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.TryAddSingleton<IdentityLocalMetrics>();
    }
}
