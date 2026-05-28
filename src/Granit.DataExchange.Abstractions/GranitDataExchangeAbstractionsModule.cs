using Granit.Modularity;

namespace Granit.DataExchange;

/// <summary>
/// Marker module for <c>Granit.DataExchange.Abstractions</c>.
/// </summary>
/// <remarks>
/// Modules that only declare <see cref="Export.ExportDefinition{TEntity}"/> or
/// <see cref="Import.ImportDefinition{TEntity}"/> instances (without executing
/// the pipelines) reference <c>Granit.DataExchange.Abstractions</c> and declare
/// <c>[DependsOn(typeof(GranitDataExchangeAbstractionsModule))]</c>. Hosts that run the
/// export or import pipelines depend on <c>GranitDataExchangeModule</c> instead, which
/// transitively depends on this module.
/// </remarks>
public sealed class GranitDataExchangeAbstractionsModule : GranitModule
{
}
