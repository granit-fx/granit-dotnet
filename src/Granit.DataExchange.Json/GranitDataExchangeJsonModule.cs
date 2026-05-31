using Granit.DataExchange.Json.Extensions;
using Granit.Modularity;

namespace Granit.DataExchange.Json;

/// <summary>
/// Registers the JSON export writer for the data export pipeline.
/// Supports hierarchical/complex fields via <c>ExportFormatCapabilities.Structured</c>.
/// </summary>
[DependsOn(typeof(GranitDataExchangeModule))]
public sealed class GranitDataExchangeJsonModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitDataExchangeJson();
}
