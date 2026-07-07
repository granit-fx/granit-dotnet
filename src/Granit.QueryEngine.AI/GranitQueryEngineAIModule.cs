using Granit.AI;
using Granit.AI.Tools;
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
/// using an LLM via <see cref="IAIChatClientFactory"/>. Only query metadata (column names,
/// filter types, operator codes) is sent to the LLM — never business data.
/// </remarks>
[DependsOn(
    typeof(GranitAIModule),
    typeof(GranitAIToolsModule),
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
