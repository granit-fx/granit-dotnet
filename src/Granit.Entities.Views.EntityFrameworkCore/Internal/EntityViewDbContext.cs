using Granit.DataFiltering;
using Granit.Entities.Views.Domain;
using Granit.Entities.Views.EntityFrameworkCore.Extensions;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.Entities.Views.EntityFrameworkCore.Internal;

/// <summary>Isolated EF Core <see cref="DbContext"/> for the <see cref="EntityView"/> aggregate.</summary>
internal sealed class EntityViewDbContext(
    DbContextOptions<EntityViewDbContext> options,
    ICurrentTenant? currentTenant = null,
    IDataFilter? dataFilter = null)
    : DbContext(options)
{
    public DbSet<EntityView> EntityViews { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureEntityViewsModule();
        modelBuilder.ApplyGranitConventions(currentTenant, dataFilter);
    }
}
