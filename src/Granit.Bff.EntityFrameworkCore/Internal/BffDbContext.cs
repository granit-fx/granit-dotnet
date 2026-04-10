using Granit.Bff.EntityFrameworkCore.Extensions;
using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.Bff.EntityFrameworkCore.Internal;

/// <summary>
/// Isolated DbContext for BFF session persistence. Stores token sets (encrypted
/// when <c>IStringEncryptionService</c> is registered) as an alternative to
/// <c>IDistributedCache</c>-backed storage.
/// </summary>
internal sealed class BffDbContext(
    DbContextOptions<BffDbContext> options,
    ICurrentTenant? currentTenant = null,
    IDataFilter? dataFilter = null)
    : DbContext(options)
{
    /// <summary>BFF sessions.</summary>
    public DbSet<BffSessionEntity> Sessions => Set<BffSessionEntity>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureBffModule();
        modelBuilder.ApplyGranitConventions(currentTenant, dataFilter);
    }
}
