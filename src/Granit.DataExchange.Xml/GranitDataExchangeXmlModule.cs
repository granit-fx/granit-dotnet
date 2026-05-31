using Granit.DataExchange.Xml.Extensions;
using Granit.Modularity;

namespace Granit.DataExchange.Xml;

/// <summary>
/// Registers the XML export writer for the data export pipeline.
/// Supports hierarchical/complex fields via <c>ExportFormatCapabilities.Structured</c>.
/// </summary>
[DependsOn(typeof(GranitDataExchangeModule))]
public sealed class GranitDataExchangeXmlModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitDataExchangeXml();
}
