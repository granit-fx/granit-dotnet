using Granit.DataExchange.Definitions.Extensions;
using Granit.Modularity;

namespace Granit.DataExchange.Definitions;

/// <summary>
/// Module providing pre-built <c>ExportDefinition&lt;T&gt;</c> implementations
/// for all Granit framework entities visible in admin panels.
/// </summary>
/// <remarks>
/// <para>
/// Each definition uses a security-by-design whitelist: only explicitly declared
/// fields are exported. Sensitive fields (<c>[SensitiveData]</c>) are excluded.
/// Infrastructure fields (<c>ConcurrencyStamp</c>, <c>SecurityStamp</c>) are excluded.
/// </para>
/// <para>
/// Applications can override any definition by registering their own
/// <c>ExportDefinition&lt;T&gt;</c> — explicit registrations always take precedence.
/// </para>
/// </remarks>
[DependsOn(typeof(GranitDataExchangeModule))]
public sealed class GranitDataExchangeDefinitionsModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitExportDefinitions();
}
