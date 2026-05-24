using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Migrations.Operations.Builders;

namespace Granit.Persistence.EntityFrameworkCore.Extensions;

/// <summary>
/// Helpers for writing EF Core migrations that follow Granit conventions.
/// </summary>
/// <remarks>
/// Designed for PostgreSQL — uses <c>varchar</c> and PG's <c>USING</c> clause syntax.
/// SQL Server / SQLite would need adapted flavours.
/// </remarks>
public static class MigrationBuilderExtensions
{
    /// <summary>
    /// Emits an <c>ALTER COLUMN</c> that converts an enum column from <c>integer</c>
    /// to <c>varchar(N)</c> with a <c>USING CASE</c> clause that maps each enum
    /// ordinal to its PascalCase name. Paired with the Granit enum persistence
    /// convention applied by <c>ApplyGranitConventions</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// EF Core does <b>not</b> generate the <c>USING CASE</c> clause automatically.
    /// Without it, PostgreSQL either rejects the migration (no implicit cast) or, if
    /// you use the naive <c>USING status::varchar</c>, silently corrupts data by
    /// writing <c>"0"</c>, <c>"1"</c>, … instead of the enum names. This helper
    /// generates the correct clause from the enum's reflected values.
    /// </para>
    /// <para>Use it inside a migration's <c>Up()</c> method:</para>
    /// <code>
    /// migrationBuilder.AlterEnumColumnIntToString&lt;ExportJobStatus&gt;(
    ///     table: "data_exchange_export_jobs",
    ///     column: "status");
    /// </code>
    /// <para>Pair with <see cref="AlterEnumColumnStringToInt{TEnum}"/> in <c>Down()</c>.</para>
    /// </remarks>
    /// <typeparam name="TEnum">The enum type backing the column. Must be int-based.</typeparam>
    /// <param name="migrationBuilder">The migration builder.</param>
    /// <param name="table">Unqualified table name.</param>
    /// <param name="column">Column name to alter.</param>
    /// <param name="schema">Optional schema name; <c>null</c> targets the default schema.</param>
    /// <param name="maxLength">
    /// Explicit <c>varchar</c> length. When <c>null</c> (default), uses
    /// <c>max(20, longestEnumValueName + 4)</c> — matches the convention auto-applied
    /// by <see cref="ModelBuilderExtensions.ApplyGranitConventions"/>.
    /// </param>
    public static OperationBuilder<SqlOperation> AlterEnumColumnIntToString<TEnum>(
        this MigrationBuilder migrationBuilder,
        string table,
        string column,
        string? schema = null,
        int? maxLength = null)
        where TEnum : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        ArgumentException.ThrowIfNullOrEmpty(table);
        ArgumentException.ThrowIfNullOrEmpty(column);

        int length = maxLength ?? Math.Max(20, Enum.GetNames<TEnum>().Max(name => name.Length) + 4);
        string qualifiedTable = QualifyTable(table, schema);
        string quotedColumn = Quote(column);

        StringBuilder caseClause = new();
        caseClause.Append("CASE ").Append(quotedColumn).Append(' ');
        foreach (TEnum value in Enum.GetValues<TEnum>())
        {
            long ordinal = Convert.ToInt64(value, CultureInfo.InvariantCulture);
            caseClause.Append("WHEN ").Append(ordinal.ToString(CultureInfo.InvariantCulture))
                .Append(" THEN '").Append(value.ToString()).Append("' ");
        }

        caseClause.Append("END");

        string sql = $"ALTER TABLE {qualifiedTable} ALTER COLUMN {quotedColumn} TYPE varchar({length}) USING {caseClause};";
        return migrationBuilder.Sql(sql);
    }

    /// <summary>
    /// Inverse of <see cref="AlterEnumColumnIntToString{TEnum}"/>. Converts a
    /// <c>varchar</c> enum column back to <c>integer</c> via a <c>USING CASE</c>
    /// clause mapping each PascalCase name to its ordinal. Use in a migration's
    /// <c>Down()</c> method to make the migration reversible.
    /// </summary>
    /// <typeparam name="TEnum">The enum type backing the column. Must be int-based.</typeparam>
    /// <param name="migrationBuilder">The migration builder.</param>
    /// <param name="table">Unqualified table name.</param>
    /// <param name="column">Column name to alter.</param>
    /// <param name="schema">Optional schema name; <c>null</c> targets the default schema.</param>
    public static OperationBuilder<SqlOperation> AlterEnumColumnStringToInt<TEnum>(
        this MigrationBuilder migrationBuilder,
        string table,
        string column,
        string? schema = null)
        where TEnum : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        ArgumentException.ThrowIfNullOrEmpty(table);
        ArgumentException.ThrowIfNullOrEmpty(column);

        string qualifiedTable = QualifyTable(table, schema);
        string quotedColumn = Quote(column);

        StringBuilder caseClause = new();
        caseClause.Append("CASE ").Append(quotedColumn).Append(' ');
        foreach (TEnum value in Enum.GetValues<TEnum>())
        {
            long ordinal = Convert.ToInt64(value, CultureInfo.InvariantCulture);
            caseClause.Append("WHEN '").Append(value.ToString())
                .Append("' THEN ").Append(ordinal.ToString(CultureInfo.InvariantCulture)).Append(' ');
        }

        caseClause.Append("END");

        string sql = $"ALTER TABLE {qualifiedTable} ALTER COLUMN {quotedColumn} TYPE integer USING {caseClause};";
        return migrationBuilder.Sql(sql);
    }

    // PostgreSQL identifier quoting (double quotes preserve case + reserved words).
    private static string Quote(string identifier) => $"\"{identifier}\"";

    private static string QualifyTable(string table, string? schema)
        => schema is null ? Quote(table) : $"{Quote(schema)}.{Quote(table)}";
}
