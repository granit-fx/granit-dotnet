using Granit.Modularity;

namespace Granit.QueryEngine;

/// <summary>
/// Granit module for query engine contracts (PagedResult, QueryRequest, QueryMetadata).
/// </summary>
/// <remarks>
/// This module has no service registrations — it exists so that consumer modules
/// can declare <c>[DependsOn(typeof(GranitQueryEngineAbstractionsModule))]</c>
/// without pulling in the full query engine implementation.
/// </remarks>
public sealed class GranitQueryEngineAbstractionsModule : GranitModule;
