using Granit.BackgroundJobs.Domain;
using Granit.BackgroundJobs.EntityFrameworkCore.Extensions;
using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.BackgroundJobs.EntityFrameworkCore.Internal;

/// <summary>
/// Dedicated EF Core DbContext for Granit background job administrative state.
/// </summary>
/// <remarks>
/// <para>
/// Isolated from the host application's DbContext to avoid coupling. Stores only
/// the administrative record (<see cref="BackgroundJobDefinition"/>) — Wolverine messages
/// and the Outbox live in the Wolverine transport schema.
/// </para>
/// <para>
/// Compatible with SQL Server and PostgreSQL.
/// </para>
/// </remarks>
internal sealed class BackgroundJobsDbContext(
    DbContextOptions<BackgroundJobsDbContext> options,
    ICurrentTenant currentTenant,
    IDataFilter? dataFilter = null)
    : GranitDbContext(options, currentTenant, dataFilter)
{
    /// <summary>Administrative records for all registered recurring jobs.</summary>
    public DbSet<BackgroundJobDefinition> Jobs { get; set; } = null!;

    /// <inheritdoc/>
    protected override void OnGranitModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ConfigureBackgroundJobsModule();
}
