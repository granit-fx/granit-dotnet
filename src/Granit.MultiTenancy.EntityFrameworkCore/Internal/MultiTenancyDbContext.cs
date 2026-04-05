using Granit.DataFiltering;
using Granit.MultiTenancy.EntityFrameworkCore.Entities;
using Granit.MultiTenancy.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.MultiTenancy.EntityFrameworkCore.Internal;

/// <summary>
/// Isolated DbContext for multi-tenancy persistence.
/// Stores the <see cref="Tenant"/> aggregate root at the host level.
/// </summary>
internal sealed class MultiTenancyDbContext(
    DbContextOptions<MultiTenancyDbContext> options,
    ICurrentTenant? currentTenant = null,
    IDataFilter? dataFilter = null)
    : DbContext(options)
{
    public DbSet<Tenant> Tenants { get; set; } = null!;

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureMultiTenancyModule();
        modelBuilder.ApplyGranitConventions(currentTenant, dataFilter);
    }
}
