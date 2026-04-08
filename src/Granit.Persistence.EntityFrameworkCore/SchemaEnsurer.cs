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
/// provider throws a fatal error. Call <see cref="EnsureSchemasAsync"/> at application
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
            // Schema names cannot be parameterized in SQL; input is validated
            // above against a strict allowlist to prevent injection.
            cmd.CommandText = $"CREATE SCHEMA IF NOT EXISTS \"{schema}\"";
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    [GeneratedRegex(@"^[a-zA-Z_][a-zA-Z0-9_\-]*$", RegexOptions.None, matchTimeoutMilliseconds: 100)]
    private static partial Regex SafeSchemaNameRegex();
}
