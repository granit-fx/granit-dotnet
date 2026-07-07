using Granit.AI.Tools;
using Granit.Modularity;

namespace Granit.QueryEngine.AI.Tools;

/// <summary>
/// Granit module exposing opted-in <c>QueryDefinition</c>s as ACL-bound <c>query_data</c>
/// agent tools.
/// </summary>
/// <remarks>
/// Unlike the NLQ translator (<c>Granit.QueryEngine.AI</c>, metadata-only), a
/// <c>query_data</c> tool returns the matching rows to the model. Results are bounded by the
/// caller-scoped <c>IQueryableSource</c> — the model never sees data the current user could
/// not read — but they ARE business data; keep this package out of hosts whose privacy
/// posture forbids row data in prompts.
/// </remarks>
[DependsOn(
    typeof(GranitAIToolsModule),
    typeof(GranitQueryEngineAbstractionsModule))]
public sealed class GranitQueryEngineAIToolsModule : GranitModule;
