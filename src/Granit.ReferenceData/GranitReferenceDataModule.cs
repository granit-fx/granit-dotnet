using Granit.Modularity;
using Granit.QueryEngine;
using Granit.ReferenceData.Extensions;

namespace Granit.ReferenceData;

/// <summary>
/// Granit module for generic reference data management.
/// Provides base entity, store/seeder abstractions, options, and memory cache.
/// </summary>
/// <remarks>
/// Register via:
/// <code>
/// services.AddGranitReferenceData();
/// </code>
/// </remarks>
[DependsOn(typeof(GranitQueryEngineAbstractionsModule))]
public sealed class GranitReferenceDataModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitReferenceData();
}
