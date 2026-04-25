using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace Granit.Persistence.EntityFrameworkCore;

/// <summary>
/// Creates database schemas idempotently before EF Core migrations or <c>EnsureCreated</c>.
/// </summary>
/// <remarks>
/// EF Core / Npgsql never creates custom schemas automatically. If a module configures
/// a schema (e.g. <c>"host"</c>) and migrations run before the schema exists, the
/// provider throws a fatal error. Call <c>EnsureSchemasAsync</c> at application
/// startup, before any migration or <c>EnsureCreated</c> call.
/// </remarks>
public static partial class SchemaEnsurer
{
    /// <summary>
    /// Creates the specified schemas if they do not already exist.
    /// </summary>
    /// <param name="context">Any <see cref="DbContext"/> connected to the target database.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="schemas">Schema names to create (e.g. <c>"host"</c>, <c>"tenant_abc"</c>).</param>
    public static async Task EnsureSchemasAsync(
        DbContext context,
        CancellationToken cancellationToken = default,
        params string[] schemas)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (schemas.Length == 0)
        {
            return;
        }

        DbConnection connection = context.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        foreach (string schema in schemas)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(schema);

            if (!SafeSchemaNameRegex().IsMatch(schema))
            {
                throw new ArgumentException(
                    $"Schema name '{schema}' contains unsafe characters. " +
                    "Only letters, digits, underscores, and hyphens are allowed.",
                    nameof(schemas));
            }

            await using DbCommand cmd = connection.CreateCommand();
            // Schema names cannot be parameterized in SQL DDL; input is validated
            // above against a strict allowlist to prevent injection.
#pragma warning disable S2077 // Formatting SQL queries: validated against SafeSchemaNameRegex allowlist above
            cmd.CommandText = $"CREATE SCHEMA IF NOT EXISTS \"{schema}\"";
#pragma warning restore S2077
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Creates the specified schemas using a raw connection string and a
    /// <see cref="DbProviderFactory"/> resolved from <see cref="DbProviderFactories"/>.
    /// Use when no <see cref="DbContext"/> is available (e.g. in the migration runner
    /// before any DbContext is resolved).
    /// </summary>
    /// <param name="connectionString">Database connection string.</param>
    /// <param name="providerInvariantName">
    /// ADO.NET provider invariant name (e.g. <c>"Npgsql"</c>, <c>"MySql.Data.MySqlClient"</c>).
    /// Registered by <c>AddGranitPostgres()</c> or equivalent provider setup.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="schemas">Schema names to create.</param>
    /// <remarks>
    /// SQL Server does not support <c>CREATE SCHEMA IF NOT EXISTS</c>. For SQL Server,
    /// use the <see cref="EnsureSchemasAsync(DbContext, CancellationToken, string[])"/>
    /// overload with a DbContext, or handle schema creation via EF Core migrations.
    /// SQL Server also does not support <c>SchemaPerTenant</c> isolation
    /// (no session-level schema switching).
    /// </remarks>
    public static async Task EnsureSchemasAsync(
        string connectionString,
        string providerInvariantName = "Npgsql",
        CancellationToken cancellationToken = default,
        params string[] schemas)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        if (schemas.Length == 0)
        {
            return;
        }

        DbProviderFactory factory = DbProviderFactories.GetFactory(providerInvariantName);
        await using DbConnection connection = factory.CreateConnection()
            ?? throw new InvalidOperationException(
                $"Failed to create a database connection from the '{providerInvariantName}' provider factory.");
        connection.ConnectionString = connectionString;
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        foreach (string schema in schemas)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(schema);

            if (!SafeSchemaNameRegex().IsMatch(schema))
            {
                throw new ArgumentException(
                    $"Schema name '{schema}' contains unsafe characters. " +
                    "Only letters, digits, underscores, and hyphens are allowed.",
                    nameof(schemas));
            }

            await using DbCommand cmd = connection.CreateCommand();
            // Schema names cannot be parameterized in SQL DDL; input is validated
            // above against a strict allowlist to prevent injection.
#pragma warning disable S2077 // Formatting SQL queries: validated against SafeSchemaNameRegex allowlist above
            cmd.CommandText = $"CREATE SCHEMA IF NOT EXISTS \"{schema}\"";
#pragma warning restore S2077
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    [GeneratedRegex(@"^[a-zA-Z_][a-zA-Z0-9_\-]*$", RegexOptions.None, matchTimeoutMilliseconds: 100)]
    private static partial Regex SafeSchemaNameRegex();
}
