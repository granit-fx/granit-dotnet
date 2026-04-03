using Granit.Metering.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;

namespace Granit.Metering.EntityFrameworkCore.Extensions;

/// <summary>
/// <see cref="ModelBuilder"/> extensions for including Granit metering entity configurations.
/// </summary>
public static class MeteringModelBuilderExtensions
{
    /// <summary>Applies all entity configurations for the Granit Metering module.</summary>
    public static ModelBuilder ConfigureMeteringModule(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new MeterDefinitionConfiguration());
        modelBuilder.ApplyConfiguration(new MeterEventConfiguration());
        modelBuilder.ApplyConfiguration(new UsageAggregateConfiguration());
        modelBuilder.ApplyConfiguration(new AggregationWatermarkConfiguration());
        return modelBuilder;
    }
}
