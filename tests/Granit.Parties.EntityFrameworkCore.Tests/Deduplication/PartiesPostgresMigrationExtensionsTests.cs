using Granit.Parties.EntityFrameworkCore.Deduplication;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Shouldly;
using Xunit;

namespace Granit.Parties.EntityFrameworkCore.Tests.Deduplication;

public sealed class PartiesPostgresMigrationExtensionsTests
{
    private const string NpgsqlProvider = "Npgsql.EntityFrameworkCore.PostgreSQL";
    private const string SqlServerProvider = "Microsoft.EntityFrameworkCore.SqlServer";

    [Fact]
    public void Add_emits_CREATE_EXTENSION_and_CREATE_INDEX_on_Npgsql()
    {
        MigrationBuilder builder = new(NpgsqlProvider);

        builder.AddPartyTrigramSimilarityIndexes(schema: "granit", tableName: "contacts_parties");

        builder.Operations.Count.ShouldBe(2);

        var extensionOp = (SqlOperation)builder.Operations[0];
        extensionOp.Sql.ShouldContain("CREATE EXTENSION IF NOT EXISTS pg_trgm");

        var indexOp = (SqlOperation)builder.Operations[1];
        indexOp.Sql.ShouldContain("CREATE INDEX IF NOT EXISTS");
        indexOp.Sql.ShouldContain("\"IX_contacts_parties_name_trgm\"");
        indexOp.Sql.ShouldContain("\"granit\".\"contacts_parties\"");
        indexOp.Sql.ShouldContain("USING GIST (lower(\"name\") gist_trgm_ops)");
    }

    [Fact]
    public void Add_is_a_noop_on_SqlServer()
    {
        MigrationBuilder builder = new(SqlServerProvider);

        builder.AddPartyTrigramSimilarityIndexes(schema: "dbo", tableName: "contacts_parties");

        builder.Operations.ShouldBeEmpty();
    }

    [Fact]
    public void Add_falls_back_to_GranitPartiesDbProperties_defaults_when_called_without_args()
    {
        MigrationBuilder builder = new(NpgsqlProvider);

        builder.AddPartyTrigramSimilarityIndexes();

        builder.Operations.Count.ShouldBe(2);
        var indexOp = (SqlOperation)builder.Operations[1];
        // Default table name follows the GranitPartiesDbProperties.DbTablePrefix convention.
        indexOp.Sql.ShouldContain("\"contacts_parties\"");
    }

    [Fact]
    public void Remove_emits_DROP_INDEX_only_on_Npgsql_and_does_NOT_drop_the_extension()
    {
        MigrationBuilder builder = new(NpgsqlProvider);

        builder.RemovePartyTrigramSimilarityIndexes(schema: "granit", tableName: "contacts_parties");

        builder.Operations.Count.ShouldBe(1);
        var dropOp = (SqlOperation)builder.Operations[0];
        dropOp.Sql.ShouldContain("DROP INDEX IF EXISTS");
        dropOp.Sql.ShouldContain("\"IX_contacts_parties_name_trgm\"");
        // Extension is NOT dropped — shared with other modules / tables, removing it
        // here would break unrelated indexes elsewhere in the database.
        dropOp.Sql.ShouldNotContain("DROP EXTENSION");
    }

    [Fact]
    public void Remove_is_a_noop_on_SqlServer()
    {
        MigrationBuilder builder = new(SqlServerProvider);

        builder.RemovePartyTrigramSimilarityIndexes(schema: "dbo", tableName: "contacts_parties");

        builder.Operations.ShouldBeEmpty();
    }
}
