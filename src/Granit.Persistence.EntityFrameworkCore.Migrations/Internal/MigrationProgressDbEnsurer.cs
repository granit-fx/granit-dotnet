using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Granit.Persistence.EntityFrameworkCore.Migrations.Internal;

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

        // Query information_schema instead of probing with a SELECT that would generate
        // Npgsql error-level log noise when the table does not exist yet.
        Microsoft.EntityFrameworkCore.Metadata.IEntityType? entityType = db.Model
            .GetEntityTypes()
            .FirstOrDefault(e => e.GetTableName() is not null);

        if (entityType is null)
        {
            return true;
        }

        string tableName = entityType.GetTableName()!;
        string? schema = entityType.GetSchema();

        string sql = schema is not null
            ? string.Concat(
                "SELECT COUNT(1) AS \"Value\" FROM information_schema.tables WHERE table_schema = '", schema,
                "' AND table_name = '", tableName, "'")
            : string.Concat(
                "SELECT COUNT(1) AS \"Value\" FROM information_schema.tables WHERE table_name = '", tableName, "'");

        int count = await db.Database
            .SqlQueryRaw<int>(sql) // NOSONAR — schema/table from EF model metadata
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);

        return count > 0;
    }
}
