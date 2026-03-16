using Granit.AI;
using Granit.Core.Modularity;
using Granit.Querying;

namespace Granit.Querying.AI;

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
    typeof(GranitQueryingModule))]
public sealed class GranitQueryingAIModule : GranitModule;
