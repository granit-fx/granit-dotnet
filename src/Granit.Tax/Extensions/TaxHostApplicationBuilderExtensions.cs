using Granit.DataExchange.Extensions;
using Granit.Diagnostics;
using Granit.QueryEngine;
using Granit.QueryEngine.Extensions;
using Granit.Tax.Diagnostics;
using Granit.Tax.Domain;
using Granit.Tax.Exports;
using Granit.Tax.Internal;
using Granit.Tax.Options;
using Granit.Tax.Queries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Tax.Extensions;

/// <summary>
/// Extension methods for registering the Granit tax infrastructure.
/// </summary>
public static class TaxHostApplicationBuilderExtensions
{
    /// <summary>Adds the Granit tax infrastructure.</summary>
    public static IHostApplicationBuilder AddGranitTax(
        this IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<TaxOptions>()
            .BindConfiguration(TaxOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.TryAddSingleton<TaxMetrics>();
        GranitActivitySourceRegistry.Register(TaxActivitySource.Name);

        // Query + Export definitions (ADR-020: owned by the base module).
        builder.Services.AddQueryDefinition<TaxRateOverride, TaxRateOverrideQueryDefinition>();
        builder.Services.AddExportDefinition<TaxRateOverride, TaxRateOverrideExportDefinition>();
        builder.Services.AddQueryDefinition<TaxRateEntry, TaxRateEntryQueryDefinition>();
        builder.Services.AddExportDefinition<TaxRateEntry, TaxRateEntryExportDefinition>();

        // Queryable source backing MapGranitQuery<TaxRateEntry>() — wraps ITaxRateProvider.
        builder.Services.AddScoped<IQueryableSource<TaxRateEntry>, TaxRateEntryQueryableSource>();

        return builder;
    }
}
