using Granit.Modularity;

namespace Granit.DataExchange;

/// <summary>
/// Marker module for <c>Granit.DataExchange.Abstractions</c>.
/// </summary>
/// <remarks>
/// Modules that only declare <see cref="Export.ExportDefinition{TEntity}"/> instances
/// (without executing exports) reference <c>Granit.DataExchange.Abstractions</c> and
/// declare <c>[DependsOn(typeof(GranitDataExchangeAbstractionsModule))]</c>. Hosts that
/// run the export pipeline depend on <c>GranitDataExchangeModule</c> instead, which
/// transitively depends on this module.
/// </remarks>
public sealed class GranitDataExchangeAbstractionsModule : GranitModule
{
}
