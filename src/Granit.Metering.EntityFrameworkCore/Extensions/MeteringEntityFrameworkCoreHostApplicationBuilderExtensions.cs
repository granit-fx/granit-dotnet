using Granit.Metering.Domain;
using Granit.Metering.EntityFrameworkCore.Internal;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Metering.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for registering EF Core persistence for Granit metering.
/// </summary>
public static class MeteringEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>Registers EF Core persistence for Granit metering.</summary>
    public static IHostApplicationBuilder AddGranitMeteringEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        builder.Services.AddGranitDbContext<MeteringDbContext>(configure);

        builder.Services.TryAddScoped<IMeterDefinitionReader, EfMeterDefinitionReader>();
        builder.Services.TryAddScoped<IMeterDefinitionWriter, EfMeterDefinitionWriter>();

        builder.Services.AddScoped<EfMeterEventStore>();
        builder.Services.TryAddScoped<IMeterEventRecorder>(sp => sp.GetRequiredService<EfMeterEventStore>());

        builder.Services.AddScoped<EfUsageAggregateStore>();
        builder.Services.TryAddScoped<IUsageReader>(sp => sp.GetRequiredService<EfUsageAggregateStore>());

        builder.Services.TryAddScoped<IAggregationRunner, EfAggregationRunner>();
        builder.Services.TryAddScoped<IQuotaChecker, EfQuotaChecker>();

        // Queryable sources for MapGranitQuery (host bypasses tenant filter for cross-tenant review).
        builder.Services.AddScoped<IQueryableSource<MeterDefinition>, EfMeterDefinitionQueryableSource>();
        builder.Services.AddScoped<IQueryableSource<UsageAggregate>, EfUsageAggregateQueryableSource>();

        return builder;
    }
}
