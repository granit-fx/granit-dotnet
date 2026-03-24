using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Persistence.Extensions;
using Granit.Querying.EntityFrameworkCore.Extensions;
using Granit.Querying.SavedViews;
using Granit.Querying.SavedViews.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Querying.EntityFrameworkCore.Internal;

/// <summary>
/// Isolated DbContext for the Querying persistence layer.
/// Owns <see cref="SavedView"/>.
/// </summary>
internal sealed class QueryingDbContext(
    DbContextOptions<QueryingDbContext> options,
    ICurrentTenant? currentTenant = null,
    IDataFilter? dataFilter = null)
    : DbContext(options)
{
    public DbSet<SavedView> SavedViews { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureQueryingModule();
        modelBuilder.ApplyGranitConventions(currentTenant, dataFilter);
    }
}
