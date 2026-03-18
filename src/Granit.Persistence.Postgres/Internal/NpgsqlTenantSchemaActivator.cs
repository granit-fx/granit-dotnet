using System.Data.Common;
using System.Text.RegularExpressions;
using Granit.Persistence.MultiTenancy;

namespace Granit.Persistence.Postgres.Internal;

/// <summary>
/// PostgreSQL implementation of <see cref="ITenantSchemaActivator"/> that executes
/// <c>SET search_path TO "{schema}", public</c> on the connection.
/// </summary>
/// <remarks>
/// <para>
/// Registered by <c>AddGranitPostgres()</c>. Call that method before
/// <c>AddTenantPerSchemaDbContext</c> to ensure this implementation is used.
/// </para>
/// <para>
/// Safety is ensured by two layers:
/// <list type="number">
///   <item><see cref="ValidateSchemaName"/>: strict regex allowlist — lower-case letters,
///         digits, and underscores only (1–63 characters, PostgreSQL NAMEDATALEN - 1).</item>
///   <item>Double-quoting: the identifier is wrapped in <c>"…"</c> per SQL standard,
///         making it a delimited identifier even if the regex were ever relaxed.</item>
/// </list>
/// </para>
/// </remarks>
internal sealed partial class NpgsqlTenantSchemaActivator : ITenantSchemaActivator
{
    /// <summary>
    /// Matches valid PostgreSQL unquoted identifiers: lower-case letters, digits, underscores,
    /// starting with a letter or underscore, 1–63 characters (NAMEDATALEN - 1).
    /// </summary>
    [GeneratedRegex(@"^[a-z_][a-z0-9_]{0,62}$")]
    private static partial Regex SafeSchemaNameRegex();

    /// <inheritdoc/>
    public void ActivateSchema(DbConnection connection, string schemaName)
    {
        using DbCommand cmd = connection.CreateCommand();
        cmd.CommandText = BuildSetSearchPathCommand(schemaName);
        cmd.ExecuteNonQuery();
    }

    /// <inheritdoc/>
    public async Task ActivateSchemaAsync(
        DbConnection connection,
        string schemaName,
        CancellationToken cancellationToken = default)
    {
        await using DbCommand cmd = connection.CreateCommand();
        cmd.CommandText = BuildSetSearchPathCommand(schemaName);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Builds the <c>SET search_path</c> command text with a validated and double-quoted
    /// PostgreSQL identifier.
    /// </summary>
    private static string BuildSetSearchPathCommand(string schema) =>
        $"SET search_path TO \"{ValidateSchemaName(schema)}\", public";

    /// <summary>
    /// Validates that <paramref name="schema"/> is a safe PostgreSQL identifier before
    /// it is embedded verbatim in a <c>SET search_path</c> command.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the schema name contains characters outside the allowed set.
    /// </exception>
    private static string ValidateSchemaName(string schema)
    {
        if (!SafeSchemaNameRegex().IsMatch(schema))
        {
            throw new InvalidOperationException(
                $"Schema name '{schema}' rejected: not a valid PostgreSQL identifier. " +
                "Only lower-case letters, digits, and underscores are allowed (1\u201363 characters).");
        }

        return schema;
    }
}
