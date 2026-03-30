using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Granit.Persistence.EntityFrameworkCore.Internal;

/// <summary>
/// Generic <see cref="IInternalDbContextEnsurer"/> that creates tables for any isolated
/// Granit <see cref="DbContext"/> that is not host-migratable.
/// </summary>
/// <remarks>
/// <para>
/// Uses two separate <see cref="DbContext"/> instances to avoid PostgreSQL connection-state
/// contamination: one for probing table existence (which may throw) and a fresh one for
/// <see cref="IRelationalDatabaseCreator.CreateTablesAsync"/>. This avoids the "current
/// transaction is aborted" error that occurs when a failed query and a DDL command share
/// the same connection.
/// </para>
/// </remarks>
/// <typeparam name="TContext">The isolated DbContext type whose tables should be created.</typeparam>
internal sealed partial class InternalDbContextEnsurer<TContext>(
    IDbContextFactory<TContext> factory,
    ILogger<InternalDbContextEnsurer<TContext>> logger) : IInternalDbContextEnsurer
    where TContext : DbContext
{
    /// <inheritdoc/>
    public string ContextName => typeof(TContext).Name;

    /// <inheritdoc/>
    public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        if (await ProbeTableExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        // Use a FRESH context — the probe context's connection may be in a failed state
        // after the unsuccessful SELECT.
        await using TContext db = await factory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        IRelationalDatabaseCreator creator = db.GetService<IRelationalDatabaseCreator>();

        LogCreatingTables(ContextName);
        await creator.CreateTablesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Probes whether the first table in the model already exists by executing a
    /// lightweight <c>SELECT 1 FROM {table} WHERE 1=0</c> query.
    /// </summary>
    /// <returns><c>true</c> if the table exists; <c>false</c> if the query throws.</returns>
    private async Task<bool> ProbeTableExistsAsync(CancellationToken cancellationToken)
    {
        await using TContext db = await factory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        // Pick the first mapped table from the model as a representative probe target.
        Microsoft.EntityFrameworkCore.Metadata.IEntityType? entityType = db.Model
            .GetEntityTypes()
            .FirstOrDefault(e => e.GetTableName() is not null);

        if (entityType is null)
        {
            return true; // No tables in model — nothing to create
        }

        string tableName = entityType.GetTableName()!;
        string? schema = entityType.GetSchema();
        ISqlGenerationHelper helper = db.GetService<ISqlGenerationHelper>();

        string qualifiedName = schema is not null
            ? $"{helper.DelimitIdentifier(schema)}.{helper.DelimitIdentifier(tableName)}"
            : helper.DelimitIdentifier(tableName);

        try
        {
            // Provider-agnostic probe — WHERE 1=0 returns zero rows without scanning.
            // Table name is from the EF model (not user input) and delimiter-escaped via
            // ISqlGenerationHelper, so string concatenation is safe here.
            string probeSql = string.Concat("SELECT 1 FROM ", qualifiedName, " WHERE 1=0");
            await db.Database
                .ExecuteSqlRawAsync(probeSql, cancellationToken) // NOSONAR S2077 — table name from EF model (not user input), delimiter-escaped via ISqlGenerationHelper
                .ConfigureAwait(false);

            return true;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            LogTableNotFound(ContextName, qualifiedName);
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Probe for '{ContextName}' table '{TableName}' returned not found — will create tables.")]
    private partial void LogTableNotFound(string contextName, string tableName);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Creating tables for internal DbContext '{ContextName}'.")]
    private partial void LogCreatingTables(string contextName);
}
