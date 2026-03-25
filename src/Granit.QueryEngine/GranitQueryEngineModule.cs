using Granit.Modularity;
using Granit.QueryEngine.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.QueryEngine;

/// <summary>
/// Granit module for declarative query building.
/// </summary>
/// <remarks>
/// Registers the core QueryEngine infrastructure: query definition descriptors
/// and null-object defaults for optional services (saved view store).
/// <para>
/// For the EF Core query engine, add <c>Granit.QueryEngine.EntityFrameworkCore</c>.
/// For REST endpoints, add <c>Granit.QueryEngine.Endpoints</c>.
/// </para>
/// </remarks>
public sealed class GranitQueryEngineModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitQueryEngine();
}
