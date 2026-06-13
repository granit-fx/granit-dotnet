using Granit.AI.VectorData;
using Granit.Indexing;
using Granit.Modularity;

namespace Granit.AI.Tools.Search;

/// <summary>
/// Granit module for RAG search tools (ADR-067). Exposes opted-in semantic collections and
/// full-text indexes as ACL-bound <c>search</c> tools. Tools are added by application code via
/// <c>AddGranitAITools(t =&gt; t.AddSearch(...))</c>; this module only wires the dependency chain.
/// </summary>
[DependsOn(
    typeof(GranitAIToolsModule),
    typeof(GranitAIVectorDataModule),
    typeof(GranitIndexingModule))]
public sealed class GranitAIToolsSearchModule : GranitModule;
