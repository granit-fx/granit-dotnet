using Granit.AI;
using Granit.Diagnostics;
using Granit.Modularity;
using Granit.QueryEngine.AI.Diagnostics;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.QueryEngine.AI;

/// <summary>
/// Granit module for Natural Language Query (NLQ) translation.
/// </summary>
/// <remarks>
/// Translates user natural language phrases into structured <see cref="QueryRequest"/> objects
/// using an LLM via <see cref="IAIChatClientFactory"/>. The translator sends only query metadata
/// (column names, filter types, operator codes) plus the user's own phrase to the LLM — never
/// stored business data. To expose query results (rows) to an agent, add the separate
/// <c>Granit.QueryEngine.AI.Tools</c> package, which documents its own privacy posture.
/// </remarks>
[DependsOn(
    typeof(GranitAIModule),
    typeof(GranitQueryEngineModule))]
public sealed class GranitQueryEngineAIModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.TryAddSingleton<QueryEngineAIMetrics>();
        GranitActivitySourceRegistry.Register(QueryEngineAIActivitySource.Name);
    }
}
