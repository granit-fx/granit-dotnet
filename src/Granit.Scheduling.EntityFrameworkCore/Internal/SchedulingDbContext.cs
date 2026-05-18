using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Scheduling.Domain;
using Granit.Scheduling.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.Scheduling.EntityFrameworkCore.Internal;

/// <summary>
/// Dedicated EF Core DbContext for Granit scheduled actions.
/// </summary>
/// <remarks>
/// Isolated from the host application's DbContext to avoid coupling.
/// Compatible with SQL Server and PostgreSQL.
/// </remarks>
internal sealed class SchedulingDbContext(
    DbContextOptions<SchedulingDbContext> options,
    ICurrentTenant currentTenant,
    IDataFilter? dataFilter = null)
    : GranitDbContext(options, currentTenant, dataFilter)
{
    /// <summary>Scheduled actions awaiting execution, cancelled, or completed.</summary>
    public DbSet<ScheduledAction> ScheduledActions { get; set; } = null!;

    /// <inheritdoc/>
    protected override void OnGranitModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ConfigureSchedulingModule();
}
