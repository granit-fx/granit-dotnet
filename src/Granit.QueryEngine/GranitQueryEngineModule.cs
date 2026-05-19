using Granit.Modularity;
using Granit.QueryEngine.Extensions;

namespace Granit.QueryEngine;

/// <summary>
/// Granit module for declarative query building.
/// </summary>
/// <remarks>
/// Registers the core QueryEngine infrastructure: query definition descriptors
/// and null-object defaults for optional services (saved view store).
/// <para>
/// For the EF Core query engine, add <c>Granit.QueryEngine.EntityFrameworkCore</c>.
/// For REST endpoints, add <c>Granit.QueryEngine.AspNetCore</c>.
/// </para>
/// </remarks>
[DependsOn(typeof(GranitQueryEngineAbstractionsModule))]
public sealed class GranitQueryEngineModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitQueryEngine();
}
