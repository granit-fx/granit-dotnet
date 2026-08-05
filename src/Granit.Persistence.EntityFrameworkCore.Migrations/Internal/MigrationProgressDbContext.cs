using Microsoft.EntityFrameworkCore;

namespace Granit.Persistence.EntityFrameworkCore.Migrations.Internal;

/// <summary>
/// System <see cref="DbContext"/> that tracks the progress of migration cycles.
/// Uses its own connection, independent of any tenant schema switch.
/// Table: <c>data_migration_progress</c>.
/// </summary>
/// <remarks>
/// This is an internal system DbContext — intentionally skips <c>ApplyGranitConventions()</c>
/// and Granit interceptors. Registered via <c>AddDbContextFactory</c> (not <c>AddGranitDbContext</c>)
/// so progress commits are independent from the tenant data transaction.
/// </remarks>
// Deliberately a plain DbContext, NOT GranitDbContext (Phase 3 exemption, #3161): this is
// tenant-AGNOSTIC system infrastructure — MigrationProgress.TenantId is an orchestration
// column, not IMultiTenant (the runner must enumerate progress across every tenant), no
// entity uses the convention interfaces, and the factory is registered Singleton (a
// GranitDbContext ctor requires the scoped ICurrentTenant). No ApplyGranitConventions call
// → no legacy-filter surface. Enum columns are configured explicitly in
// MigrationProgressConfiguration.
internal sealed class MigrationProgressDbContext(DbContextOptions<MigrationProgressDbContext> options)
    : DbContext(options)
{
    /// <summary>Access to the migration progress tracking table.</summary>
    public DbSet<MigrationProgress> MigrationProgresses => Set<MigrationProgress>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new MigrationProgressConfiguration());
    }
}
