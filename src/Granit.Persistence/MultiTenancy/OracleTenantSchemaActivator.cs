using System.Data.Common;
using System.Text.RegularExpressions;

namespace Granit.Persistence.MultiTenancy;

/// <summary>
/// Oracle implementation of <see cref="ITenantSchemaActivator"/> that executes
/// <c>ALTER SESSION SET CURRENT_SCHEMA = "schema_name"</c> on the connection.
/// </summary>
/// <remarks>
/// <para>
/// This sets the current schema for the session, allowing unqualified table names
/// to resolve to the tenant's schema. The underlying user's privileges are unchanged —
/// the application account must have appropriate grants on each tenant schema.
/// </para>
/// <para>
/// Safety is ensured by two layers:
/// <list type="number">
///   <item><see cref="ValidateSchemaName"/>: strict regex allowlist — letters (case-insensitive),
///         digits, underscores, dollar signs, and hash marks only (1–128 characters,
///         Oracle identifier max).</item>
///   <item>Double-quoting: the identifier is wrapped in <c>"…"</c> per Oracle's delimited
///         identifier syntax, making it safe even if the regex were ever relaxed.</item>
/// </list>
/// </para>
/// </remarks>
internal sealed partial class OracleTenantSchemaActivator : ITenantSchemaActivator
{
    /// <summary>
    /// Matches valid Oracle identifiers: letters, digits, underscores, dollar signs,
    /// or hash marks, starting with a letter, 1–128 characters.
    /// </summary>
    [GeneratedRegex(@"^[a-zA-Z][a-zA-Z0-9_$#]{0,127}$")]
    private static partial Regex SafeSchemaNameRegex();

    /// <inheritdoc/>
    public void ActivateSchema(DbConnection connection, string schemaName)
    {
        using DbCommand cmd = connection.CreateCommand();
        cmd.CommandText = BuildAlterSessionCommand(schemaName);
        cmd.ExecuteNonQuery();
    }

    /// <inheritdoc/>
    public async Task ActivateSchemaAsync(
        DbConnection connection,
        string schemaName,
        CancellationToken cancellationToken = default)
    {
        await using DbCommand cmd = connection.CreateCommand();
        cmd.CommandText = BuildAlterSessionCommand(schemaName);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Builds the <c>ALTER SESSION SET CURRENT_SCHEMA</c> command text with a validated
    /// and double-quoted Oracle identifier.
    /// </summary>
    private static string BuildAlterSessionCommand(string schema) =>
        $"ALTER SESSION SET CURRENT_SCHEMA = \"{ValidateSchemaName(schema)}\"";

    /// <summary>
    /// Validates that <paramref name="schema"/> is a safe Oracle identifier before
    /// it is embedded in an <c>ALTER SESSION SET CURRENT_SCHEMA</c> command.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the schema name contains characters outside the allowed set.
    /// </exception>
    private static string ValidateSchemaName(string schema)
    {
        if (!SafeSchemaNameRegex().IsMatch(schema))
        {
            throw new InvalidOperationException(
                $"Schema name '{schema}' rejected: not a valid Oracle identifier. " +
                "Only letters, digits, underscores, dollar signs, and hash marks are allowed (1\u2013128 characters).");
        }

        return schema;
    }
}
