using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Testing.Persistence.Domain;
using Granit.Testing.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.Testing.Persistence;

/// <summary>
/// Isolated DbContext for the conformance suites. Inherits <see cref="GranitDbContext"/> —
/// mandatory here because <see cref="ConformanceOrder"/> is <c>IMultiTenant</c> (the base
/// class owns the parameterised tenant filter).
/// </summary>
public sealed class ConformanceDbContext(
    DbContextOptions<ConformanceDbContext> options,
    ICurrentTenant currentTenant,
    IDataFilter? dataFilter = null)
    : GranitDbContext(options, currentTenant, dataFilter)
{
    /// <summary>Tenant-scoped, audited, concurrency-aware orders.</summary>
    public DbSet<ConformanceOrder> Orders => Set<ConformanceOrder>();

    /// <summary>Activatable toggles (IActive filter).</summary>
    public DbSet<ConformanceToggle> Toggles => Set<ConformanceToggle>();

    /// <inheritdoc/>
    protected override void OnGranitModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ConfigureConformanceModule();
}
