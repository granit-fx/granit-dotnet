using Granit.DataExchange.Csv.Extensions;
using Granit.Modularity;

namespace Granit.DataExchange.Csv;

/// <summary>
/// Registers the Sep-based CSV file parser for the data import pipeline.
/// </summary>
[DependsOn(typeof(GranitDataExchangeModule))]
public sealed class GranitDataExchangeCsvModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitDataExchangeCsv();
}
