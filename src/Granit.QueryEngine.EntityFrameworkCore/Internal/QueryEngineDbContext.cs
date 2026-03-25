using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Persistence.Extensions;
using Granit.QueryEngine.EntityFrameworkCore.Extensions;
using Granit.QueryEngine.SavedViews;
using Granit.QueryEngine.SavedViews.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.QueryEngine.EntityFrameworkCore.Internal;

/// <summary>
/// Isolated DbContext for the QueryEngine persistence layer.
/// Owns <see cref="SavedView"/>.
/// </summary>
internal sealed class QueryEngineDbContext(
    DbContextOptions<QueryEngineDbContext> options,
    ICurrentTenant? currentTenant = null,
    IDataFilter? dataFilter = null)
    : DbContext(options)
{
    public DbSet<SavedView> SavedViews { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureQueryEngineModule();
        modelBuilder.ApplyGranitConventions(currentTenant, dataFilter);
    }
}
