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
