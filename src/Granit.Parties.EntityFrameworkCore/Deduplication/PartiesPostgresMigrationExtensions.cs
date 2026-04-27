using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Granit.Parties.EntityFrameworkCore.Deduplication;

/// <summary>
/// PostgreSQL-specific migration helpers for Tier-2 trigram-similarity duplicate detection.
/// The framework cannot ship migrations directly (CLAUDE.md: migrations live in the consuming
/// app), so this static class provides composable raw-SQL helpers the app's migration calls
/// from <c>Up()</c> and <c>Down()</c>.
/// </summary>
/// <remarks>
/// <para>
/// PostgreSQL only — the helper inspects <see cref="MigrationBuilder.ActiveProvider"/> and
/// no-ops when the provider is not <c>Npgsql.EntityFrameworkCore.PostgreSQL</c>. The fallback
/// for SQL Server / SQLite / InMemory is a slower <c>LIKE</c>-based matching at query time
/// implemented in <c>Granit.Parties.Deduplication</c> (story #1299).
/// </para>
/// <para>
/// Consuming app migration usage:
/// <code>
/// public partial class AddPartyTrigramIndexes : Migration
/// {
///     protected override void Up(MigrationBuilder migrationBuilder) =&gt;
///         migrationBuilder.AddPartyTrigramSimilarityIndexes();
///
///     protected override void Down(MigrationBuilder migrationBuilder) =&gt;
///         migrationBuilder.RemovePartyTrigramSimilarityIndexes();
/// }
/// </code>
/// Schema and table-name defaults follow the Granit convention
/// (<c>contacts_parties</c> / <see cref="GranitPartiesDbProperties.DbSchema"/>); pass the
/// parameters explicitly when the app overrides the defaults.
/// </para>
/// <para>
/// Index choice: <b>GIST + gist_trgm_ops</b> rather than GIN. GIN is ~3× faster on pure
/// <c>%</c> / <c>similarity() &gt; threshold</c> reads but lacks support for the <c>&lt;-&gt;</c>
/// distance operator and <c>ORDER BY similarity(...)</c> ranking. Tier-3 fuzzy scoring will
/// want ranked candidates, so GIST is the more flexible default. Adjust the helper if a
/// specific deployment is read-only enough to prefer GIN.
/// </para>
/// </remarks>
public static class PartiesPostgresMigrationExtensions
{
    /// <summary>
    /// Installs the <c>pg_trgm</c> extension + a GIST trigram index on <c>lower(name)</c>
    /// of the parties table. No-op on non-PostgreSQL providers.
    /// </summary>
    /// <param name="migrationBuilder">The migration builder.</param>
    /// <param name="schema">Schema of the parties table. Defaults to
    /// <see cref="GranitPartiesDbProperties.DbSchema"/>; pass explicitly if the consuming app
    /// overrides it after framework-default resolution.</param>
    /// <param name="tableName">Parties table name. Defaults to <c>contacts_parties</c>.</param>
    /// <returns>The migration builder for chaining.</returns>
    public static MigrationBuilder AddPartyTrigramSimilarityIndexes(
        this MigrationBuilder migrationBuilder,
        string? schema = null,
        string? tableName = null)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        if (!IsPostgres(migrationBuilder))
        {
            return migrationBuilder;
        }

        string resolvedSchema = schema ?? GranitPartiesDbProperties.DbSchema ?? "public";
        string resolvedTable = tableName ?? GranitPartiesDbProperties.DbTablePrefix + "parties";
        string indexName = $"IX_{resolvedTable}_name_trgm";

        // pg_trgm provides similarity() and the % operator. CREATE EXTENSION is idempotent.
        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

        // GIST + gist_trgm_ops on lower(name) — case-insensitive trigram blocking.
        migrationBuilder.Sql(
            $"CREATE INDEX IF NOT EXISTS \"{indexName}\" "
            + $"ON \"{resolvedSchema}\".\"{resolvedTable}\" "
            + "USING GIST (lower(\"name\") gist_trgm_ops);");

        return migrationBuilder;
    }

    /// <summary>
    /// Drops the GIST trigram index installed by
    /// <see cref="AddPartyTrigramSimilarityIndexes"/>. Does <em>not</em> drop the
    /// <c>pg_trgm</c> extension itself — extensions are typically shared across
    /// modules / tables and removing them on a Parties-specific rollback would break
    /// unrelated indexes elsewhere. No-op on non-PostgreSQL providers.
    /// </summary>
    public static MigrationBuilder RemovePartyTrigramSimilarityIndexes(
        this MigrationBuilder migrationBuilder,
        string? schema = null,
        string? tableName = null)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        if (!IsPostgres(migrationBuilder))
        {
            return migrationBuilder;
        }

        string resolvedSchema = schema ?? GranitPartiesDbProperties.DbSchema ?? "public";
        string resolvedTable = tableName ?? GranitPartiesDbProperties.DbTablePrefix + "parties";
        string indexName = $"IX_{resolvedTable}_name_trgm";

        migrationBuilder.Sql($"DROP INDEX IF EXISTS \"{resolvedSchema}\".\"{indexName}\";");

        return migrationBuilder;
    }

    private static bool IsPostgres(MigrationBuilder migrationBuilder) =>
        string.Equals(
            migrationBuilder.ActiveProvider,
            GranitDbProviders.Postgres,
            StringComparison.Ordinal);
}
