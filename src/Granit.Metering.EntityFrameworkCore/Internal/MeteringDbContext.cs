using Granit.DataFiltering;
using Granit.Metering.Domain;
using Granit.Metering.EntityFrameworkCore.Extensions;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.Metering.EntityFrameworkCore.Internal;

/// <summary>
/// Dedicated EF Core DbContext for Granit metering data.
/// </summary>
internal sealed class MeteringDbContext(
    DbContextOptions<MeteringDbContext> options,
    ICurrentTenant? currentTenant = null,
    IDataFilter? dataFilter = null)
    : DbContext(options)
{
    public DbSet<MeterDefinition> MeterDefinitions { get; set; } = null!;

    public DbSet<MeterEvent> MeterEvents { get; set; } = null!;

    public DbSet<UsageAggregate> UsageAggregates { get; set; } = null!;

    public DbSet<AggregationWatermark> AggregationWatermarks { get; set; } = null!;

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureMeteringModule();
        modelBuilder.ApplyGranitConventions(currentTenant, dataFilter);
    }
}
