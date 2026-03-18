using System.Data;
using System.Data.Common;
using Granit.Persistence.Migrations;
using Granit.Persistence.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Granit.Persistence.Postgres.Internal;

/// <summary>
/// PostgreSQL <see cref="ITenantDbIsolator"/> that executes <c>SET search_path</c> on
/// the <see cref="DbContext"/> connection before each batch migration delegate runs.
/// </summary>
/// <remarks>
/// Resolves the schema name via <see cref="ITenantSchemaProvider"/> (honours
/// <c>TenantSchemaOptions</c>) and delegates SQL execution to
/// <see cref="ITenantSchemaActivator"/> (validates and double-quotes the identifier).
/// Registered by <c>AddGranitPostgres()</c>.
/// </remarks>
internal sealed class NpgsqlTenantDbIsolator(
    ITenantSchemaProvider schemaProvider,
    ITenantSchemaActivator schemaActivator) : ITenantDbIsolator
{
    /// <inheritdoc/>
    public async Task IsolateAsync(DbContext context, Guid tenantId, CancellationToken cancellationToken)
    {
        string schemaName = await schemaProvider
            .GetSchemaNameAsync(tenantId, cancellationToken)
            .ConfigureAwait(false);

        DbConnection connection = context.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        await schemaActivator
            .ActivateSchemaAsync(connection, schemaName, cancellationToken)
            .ConfigureAwait(false);
    }
}
