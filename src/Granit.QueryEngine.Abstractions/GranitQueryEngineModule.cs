using Granit.Modularity;
using Granit.QueryEngine.Extensions;

namespace Granit.QueryEngine;

/// <summary>
/// Granit module for declarative query building: contracts (PagedResult, QueryRequest,
/// QueryMetadata), the QueryDefinition fluent API, and the shared runtime seams
/// (<c>QueryEngineOptions</c>, <c>QueryEngineMetrics</c>).
/// </summary>
/// <remarks>
/// Declarations are pure — modules that only declare query definitions pay no runtime
/// cost beyond two <c>TryAddSingleton</c> registrations.
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
