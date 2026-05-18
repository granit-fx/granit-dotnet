using Granit.DataExchange.EntityFrameworkCore.Extensions;
using Granit.DataExchange.EntityFrameworkCore.Internal.Export.Entities;
using Granit.DataExchange.EntityFrameworkCore.Internal.Import.Entities;
using Granit.DataExchange.Export;
using Granit.DataExchange.Export.Domain;
using Granit.DataExchange.Import.Domain;
using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.DataExchange.EntityFrameworkCore.Internal;

/// <summary>
/// Isolated DbContext for the DataExchange persistence layer.
/// Owns <see cref="ImportJob"/>, <see cref="SavedMappingEntity"/>, <see cref="ExternalIdMappingEntity"/>,
/// <see cref="ExportJob"/>, and <see cref="ExportPresetEntity"/>.
/// </summary>
internal sealed class DataExchangeDbContext(
    DbContextOptions<DataExchangeDbContext> options,
    ICurrentTenant currentTenant,
    IDataFilter? dataFilter = null)
    : GranitDbContext(options, currentTenant, dataFilter)
{
    public DbSet<ImportJob> ImportJobs { get; set; } = null!;
    public DbSet<SavedMappingEntity> SavedMappings { get; set; } = null!;
    public DbSet<ExternalIdMappingEntity> ExternalIdMappings { get; set; } = null!;
    public DbSet<ExportJob> ExportJobs { get; set; } = null!;
    public DbSet<ExportPresetEntity> ExportPresets { get; set; } = null!;

    protected override void OnGranitModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ConfigureDataExchangeModule();
}
