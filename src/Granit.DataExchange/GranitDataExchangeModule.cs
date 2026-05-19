using Granit.DataExchange.Extensions;
using Granit.Events;
using Granit.Guids;
using Granit.Modularity;
using Granit.QueryEngine;
using Granit.Timing;
using Granit.Validation;

namespace Granit.DataExchange;

/// <summary>
/// Granit module for the data import pipeline.
/// </summary>
/// <remarks>
/// Registers the core import infrastructure: mapping suggestion service, orchestrator,
/// and null-object defaults for optional services.
/// <para>
/// This module provides <strong>no</strong> file parser by default.
/// Add at least one parser package:
/// <list type="bullet">
///   <item><c>Granit.DataExchange.Csv</c> for CSV files (Sep).</item>
///   <item><c>Granit.DataExchange.Excel</c> for Excel files (Sylvan.Data.Excel).</item>
/// </list>
/// </para>
/// <para>
/// For persistence (executor, stores), add <c>Granit.DataExchange.EntityFrameworkCore</c>.
/// For REST endpoints, add <c>Granit.DataExchange.Endpoints</c>.
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitEventsModule),
    typeof(GranitGuidsModule),
    typeof(GranitQueryEngineModule),
    typeof(GranitTimingModule),
    typeof(GranitValidationModule))]
public sealed class GranitDataExchangeModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services
            .AddGranitDataImport()
            .AddGranitDataExport();
}
