using Granit.DataExchange.Excel.Extensions;
using Granit.Modularity;

namespace Granit.DataExchange.Excel;

/// <summary>
/// Registers the Sylvan-based Excel file parser for the data import pipeline.
/// </summary>
[DependsOn(typeof(GranitDataExchangeModule))]
public sealed class GranitDataExchangeExcelModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitDataExchangeExcel();
}
