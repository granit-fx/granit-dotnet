using Granit.DataLookup.Diagnostics;
using Granit.DataLookup.Extensions;
using Granit.Diagnostics;
using Granit.Localization;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.DataLookup;

/// <summary>
/// Granit module for the unified data-lookup runtime.
/// </summary>
/// <remarks>
/// Registers the <see cref="Registry.ILookupRegistry"/>, metrics, and the
/// <see cref="DataLookupActivitySource"/> source. Adapters (QueryDefinition, Enum,
/// ReferenceData) register themselves via <c>IServiceCollection</c> extensions.
/// </remarks>
[DependsOn(
    typeof(GranitDataLookupAbstractionsModule),
    typeof(GranitLocalizationModule))]
public sealed class GranitDataLookupModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddGranitDataLookup();
        GranitActivitySourceRegistry.Register(DataLookupActivitySource.Name);
    }
}
