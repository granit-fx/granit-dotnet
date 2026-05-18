using Granit.DataFiltering;
using Granit.MultiTenancy.Domain;
using Granit.MultiTenancy.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.MultiTenancy.EntityFrameworkCore.Internal;

/// <summary>
/// Isolated DbContext for multi-tenancy persistence.
/// Stores the <see cref="Tenant"/> aggregate root at the host level.
/// </summary>
internal sealed class MultiTenancyDbContext(
    DbContextOptions<MultiTenancyDbContext> options,
    ICurrentTenant currentTenant,
    IDataFilter? dataFilter = null)
    : GranitDbContext(options, currentTenant, dataFilter)
{
    public DbSet<Tenant> Tenants { get; set; } = null!;

    /// <inheritdoc/>
    protected override void OnGranitModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ConfigureMultiTenancyModule();
}
