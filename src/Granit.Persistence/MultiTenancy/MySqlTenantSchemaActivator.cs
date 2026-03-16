using System.Data.Common;
using System.Text.RegularExpressions;

namespace Granit.Persistence.MultiTenancy;

/// <summary>
/// MySQL / MariaDB implementation of <see cref="ITenantSchemaActivator"/> that executes
/// <c>USE `schema_name`</c> on the connection.
/// </summary>
/// <remarks>
/// <para>
/// In MySQL, a "schema" and a "database" are synonymous. This activator switches the
/// connection's current database to the tenant's schema using the <c>USE</c> statement.
/// </para>
/// <para>
/// Safety is ensured by two layers:
/// <list type="number">
///   <item><see cref="ValidateSchemaName"/>: strict regex allowlist — letters (case-insensitive),
///         digits, underscores, and dollar signs only (1–64 characters, MySQL identifier max).</item>
///   <item>Backtick quoting: the identifier is wrapped in <c>`…`</c> (MySQL delimited identifier),
///         making it safe even if the regex were ever relaxed.</item>
/// </list>
/// </para>
/// </remarks>
internal sealed partial class MySqlTenantSchemaActivator : ITenantSchemaActivator
{
    /// <summary>
    /// Matches valid MySQL identifiers: letters, digits, underscores, or dollar signs,
    /// starting with a letter, underscore, or dollar sign, 1–64 characters.
    /// </summary>
    [GeneratedRegex(@"^[a-zA-Z_$][a-zA-Z0-9_$]{0,63}$")]
    private static partial Regex SafeSchemaNameRegex();

    /// <inheritdoc/>
    public void ActivateSchema(DbConnection connection, string schemaName)
    {
        using DbCommand cmd = connection.CreateCommand();
        cmd.CommandText = BuildUseCommand(schemaName);
        cmd.ExecuteNonQuery();
    }

    /// <inheritdoc/>
    public async Task ActivateSchemaAsync(
        DbConnection connection,
        string schemaName,
        CancellationToken cancellationToken = default)
    {
        await using DbCommand cmd = connection.CreateCommand();
        cmd.CommandText = BuildUseCommand(schemaName);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Builds the <c>USE</c> command text with a validated and backtick-quoted MySQL identifier.
    /// </summary>
    private static string BuildUseCommand(string schema) =>
        $"USE `{ValidateSchemaName(schema)}`";

    /// <summary>
    /// Validates that <paramref name="schema"/> is a safe MySQL identifier before
    /// it is embedded in a <c>USE</c> command.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the schema name contains characters outside the allowed set.
    /// </exception>
    private static string ValidateSchemaName(string schema)
    {
        if (!SafeSchemaNameRegex().IsMatch(schema))
        {
            throw new InvalidOperationException(
                $"Schema name '{schema}' rejected: not a valid MySQL identifier. " +
                "Only letters, digits, underscores, and dollar signs are allowed (1\u201364 characters).");
        }

        return schema;
    }
}
