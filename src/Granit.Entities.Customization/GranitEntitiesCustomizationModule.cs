using Granit.Entities.Customization.Internal;
using Granit.Entities.Internal;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Entities.Customization;

/// <summary>
/// Module marker for <c>Granit.Entities.Customization</c> — the Layer 1
/// tenant-customization runtime per ADR-053. Pull this from hosts that
/// resolve and persist customization deltas; loading the module replaces
/// the manifest pipeline's no-op
/// <see cref="IManifestCustomizationApplier"/> with the real
/// <see cref="EntityCustomizationManifestApplier"/> so subsequent manifest
/// reads honour the tenant's customization deltas.
/// </summary>
[DependsOn(
    typeof(GranitEntitiesAbstractionsModule),
    typeof(GranitEntitiesModule))]
public sealed class GranitEntitiesCustomizationModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.TryAddScoped<DescriptorDeltaValidator>();
        context.Services.Replace(
            ServiceDescriptor.Scoped<IManifestCustomizationApplier, EntityCustomizationManifestApplier>());
    }
}
