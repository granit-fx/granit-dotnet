using Granit.DataExchange.Extensions;
using Granit.Diagnostics;
using Granit.Metering.Diagnostics;
using Granit.Metering.Domain;
using Granit.Metering.Exports;
using Granit.Metering.Internal;
using Granit.Metering.Options;
using Granit.Metering.Queries;
using Granit.QueryEngine.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Metering.Extensions;

/// <summary>
/// Extension methods for registering the Granit metering infrastructure.
/// </summary>
public static class MeteringHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the Granit metering infrastructure.
    /// </summary>
    public static IHostApplicationBuilder AddGranitMetering(
        this IHostApplicationBuilder builder)
    {
        builder.Services.Configure<GranitMeteringOptions>(
            builder.Configuration.GetSection("Granit:Metering"));

        builder.Services.TryAddSingleton<MeteringMetrics>();
        builder.Services.TryAddSingleton<IQuotaLimitProvider, UnlimitedQuotaLimitProvider>();
        builder.Services.TryAddScoped<IBillingPeriodProvider, CalendarMonthBillingPeriodProvider>();
        GranitActivitySourceRegistry.Register(MeteringActivitySource.Name);

        // Query + Export definitions (ADR-020: owned by the base module).
        builder.Services.AddQueryDefinition<MeterDefinition, MeterDefinitionQueryDefinition>();
        builder.Services.AddQueryDefinition<UsageAggregate, UsageAggregateQueryDefinition>();
        builder.Services.AddExportDefinition<MeterDefinition, MeterDefinitionExportDefinition>();
        builder.Services.AddExportDefinition<UsageAggregate, UsageAggregateExportDefinition>();

        return builder;
    }
}
