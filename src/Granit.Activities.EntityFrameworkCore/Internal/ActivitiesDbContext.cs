using Granit.Activities.Domain;
using Granit.Activities.EntityFrameworkCore.Extensions;
using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.Activities.EntityFrameworkCore.Internal;

/// <summary>
/// Isolated EF Core DbContext for activity persistence. Constructor-injects the
/// optional <see cref="ICurrentTenant"/> + <see cref="IDataFilter"/> so
/// <c>ApplyGranitConventions</c> wires the standard tenant filter, soft-delete
/// filter, and audited-entity / lifecycle-event interceptors.
/// </summary>
internal sealed class ActivitiesDbContext(
    DbContextOptions<ActivitiesDbContext> options,
    ICurrentTenant? currentTenant = null,
    IDataFilter? dataFilter = null)
    : DbContext(options)
{
    /// <summary>Cross-entity activities (polymorphic to-do).</summary>
    public DbSet<Activity> Activities => Set<Activity>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureActivitiesModule();
        modelBuilder.ApplyGranitConventions(currentTenant, dataFilter);
    }
}
