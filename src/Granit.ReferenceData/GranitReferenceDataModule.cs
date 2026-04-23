using Granit.DataLookup;
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
/// Each registered reference data type is auto-exposed as a Granit.DataLookup source
/// under the <c>ref-{type-kebab-case}</c> name (e.g. <c>ref-country</c>).
/// </remarks>
[DependsOn(
    typeof(GranitDataLookupModule),
    typeof(GranitQueryEngineModule))]
public sealed class GranitReferenceDataModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitReferenceData();
}
