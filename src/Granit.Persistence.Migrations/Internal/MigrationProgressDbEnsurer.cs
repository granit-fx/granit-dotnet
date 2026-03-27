using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Granit.Persistence.Migrations.Internal;

/// <summary>
/// Creates the migration progress tracking table via <see cref="MigrationProgressDbContext"/>.
/// </summary>
/// <remarks>
/// <para>
/// Uses two separate <see cref="MigrationProgressDbContext"/> instances to avoid PostgreSQL
/// connection-state contamination: one for probing table existence (which may throw) and a
/// fresh one for <see cref="IRelationalDatabaseCreator.CreateTablesAsync"/>. This avoids the
/// "current transaction is aborted" error that occurs when a failed query and a DDL command
/// share the same connection.
/// </para>
/// </remarks>
internal sealed class MigrationProgressDbEnsurer(
    IDbContextFactory<MigrationProgressDbContext> factory) : IMigrationProgressDbEnsurer
{
    public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        if (await ProbeTableExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        // Use a FRESH context — the probe context's connection may be in a failed state
        // after the unsuccessful SELECT.
        await using MigrationProgressDbContext db = await factory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        IRelationalDatabaseCreator creator = db.GetService<IRelationalDatabaseCreator>();
        await creator.CreateTablesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> ProbeTableExistsAsync(CancellationToken cancellationToken)
    {
        await using MigrationProgressDbContext db = await factory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!db.Database.IsRelational())
        {
            return true; // Non-relational providers don't need table creation
        }

        IRelationalDatabaseCreator creator = db.GetService<IRelationalDatabaseCreator>();

        if (!await creator.HasTablesAsync(cancellationToken).ConfigureAwait(false))
        {
            return false; // Database has no tables at all
        }

        // Database has tables (from host migrations) — probe the specific table.
        try
        {
            await db.MigrationProgresses.AnyAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }
}
