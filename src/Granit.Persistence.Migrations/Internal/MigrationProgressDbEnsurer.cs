using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Granit.Persistence.Migrations.Internal;

/// <summary>
/// Creates the migration progress tracking table via <see cref="MigrationProgressDbContext"/>.
/// </summary>
/// <remarks>
/// <para>
/// <c>EnsureCreatedAsync()</c> is a no-op when the database already has tables
/// (e.g., from host application migrations). This implementation uses the
/// <see cref="IRelationalDatabaseCreator"/> to check table existence and create
/// only the tables defined in the <see cref="MigrationProgressDbContext"/> model.
/// </para>
/// </remarks>
internal sealed class MigrationProgressDbEnsurer(
    IDbContextFactory<MigrationProgressDbContext> factory) : IMigrationProgressDbEnsurer
{
    public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        await using MigrationProgressDbContext db = await factory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        IRelationalDatabaseCreator creator = db.GetService<IRelationalDatabaseCreator>();

        if (!await creator.HasTablesAsync(cancellationToken).ConfigureAwait(false))
        {
            // Database has no tables at all — safe to use EnsureCreated
            await creator.CreateTablesAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        // Database has tables (from host migrations) — EnsureCreated would be a no-op.
        // Try to query the progress table; if it fails, create it.
        try
        {
            await db.MigrationProgresses.AnyAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Table doesn't exist — create all tables from this model
            await creator.CreateTablesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
