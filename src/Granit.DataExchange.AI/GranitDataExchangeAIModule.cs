using Granit.AI;
using Granit.Core.Modularity;
using Granit.DataExchange;

namespace Granit.DataExchange.AI;

/// <summary>
/// Granit module that provides AI-powered semantic column mapping for data exchange.
/// </summary>
/// <remarks>
/// When this module is installed, the default <c>NullSemanticMappingService</c> is replaced
/// with an LLM-backed implementation that uses <see cref="IAIChatClientFactory"/> to suggest
/// column-to-property mappings during CSV/Excel import.
/// <para>
/// Only column headers and field metadata are sent to the LLM — never business data (GDPR safe).
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitAIModule),
    typeof(GranitDataExchangeModule))]
public sealed class GranitDataExchangeAIModule : GranitModule;
