using Granit.Parties.EntityFrameworkCore.Deduplication;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Shouldly;
using Xunit;

namespace Granit.Parties.EntityFrameworkCore.Tests.Deduplication;

public sealed class PartiesPostgresMigrationExtensionsTests
{
    // Use the configured prefix (default "parties_") so tests stay aligned with whatever
    // GranitPartiesDbProperties.DbTablePrefix the host app sets, instead of pinning the
    // pre-rename literal.
    private static readonly string PartiesTable = $"{GranitPartiesDbProperties.DbTablePrefix}parties";
    private static readonly string PartiesIndex = $"IX_{PartiesTable}_name_trgm";

    [Fact]
    public void Add_emits_CREATE_EXTENSION_and_CREATE_INDEX_on_Npgsql()
    {
        MigrationBuilder builder = new(GranitDbProviders.Postgres);

        builder.AddPartyTrigramSimilarityIndexes(schema: "granit", tableName: PartiesTable);

        builder.Operations.Count.ShouldBe(2);

        var extensionOp = (SqlOperation)builder.Operations[0];
        extensionOp.Sql.ShouldContain("CREATE EXTENSION IF NOT EXISTS pg_trgm");

        var indexOp = (SqlOperation)builder.Operations[1];
        indexOp.Sql.ShouldContain("CREATE INDEX IF NOT EXISTS");
        indexOp.Sql.ShouldContain($"\"{PartiesIndex}\"");
        indexOp.Sql.ShouldContain($"\"granit\".\"{PartiesTable}\"");
        indexOp.Sql.ShouldContain("USING GIST (lower(\"name\") gist_trgm_ops)");
    }

    [Fact]
    public void Add_is_a_noop_on_SqlServer()
    {
        MigrationBuilder builder = new(GranitDbProviders.SqlServer);

        builder.AddPartyTrigramSimilarityIndexes(schema: "dbo", tableName: PartiesTable);

        builder.Operations.ShouldBeEmpty();
    }

    [Fact]
    public void Add_falls_back_to_GranitPartiesDbProperties_defaults_when_called_without_args()
    {
        MigrationBuilder builder = new(GranitDbProviders.Postgres);

        builder.AddPartyTrigramSimilarityIndexes();

        builder.Operations.Count.ShouldBe(2);
        var indexOp = (SqlOperation)builder.Operations[1];
        // Default table name follows the GranitPartiesDbProperties.DbTablePrefix convention.
        indexOp.Sql.ShouldContain($"\"{PartiesTable}\"");
    }

    [Fact]
    public void Remove_emits_DROP_INDEX_only_on_Npgsql_and_does_NOT_drop_the_extension()
    {
        MigrationBuilder builder = new(GranitDbProviders.Postgres);

        builder.RemovePartyTrigramSimilarityIndexes(schema: "granit", tableName: PartiesTable);

        builder.Operations.Count.ShouldBe(1);
        var dropOp = (SqlOperation)builder.Operations[0];
        dropOp.Sql.ShouldContain("DROP INDEX IF EXISTS");
        dropOp.Sql.ShouldContain($"\"{PartiesIndex}\"");
        // Extension is NOT dropped — shared with other modules / tables, removing it
        // here would break unrelated indexes elsewhere in the database.
        dropOp.Sql.ShouldNotContain("DROP EXTENSION");
    }

    [Fact]
    public void Remove_is_a_noop_on_SqlServer()
    {
        MigrationBuilder builder = new(GranitDbProviders.SqlServer);

        builder.RemovePartyTrigramSimilarityIndexes(schema: "dbo", tableName: PartiesTable);

        builder.Operations.ShouldBeEmpty();
    }
}
