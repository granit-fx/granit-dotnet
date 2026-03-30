using System.Data.Common;
using Granit.Persistence.EntityFrameworkCore.Postgres.Internal;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Postgres.Tests;

public sealed class NpgsqlTenantSchemaActivatorTests
{
    private readonly NpgsqlTenantSchemaActivator _activator = new();

    [Fact]
    public void ActivateSchema_ValidSchemaName_ExecutesSetSearchPath()
    {
        DbConnection connection = Substitute.For<DbConnection>();
        DbCommand command = Substitute.For<DbCommand>();
        connection.CreateCommand().Returns(command);

        _activator.ActivateSchema(connection, "tenant_abc");

        command.CommandText.ShouldBe("SET search_path TO \"tenant_abc\", public");
        command.Received(1).ExecuteNonQuery();
    }

    [Fact]
    public async Task ActivateSchemaAsync_ValidSchemaName_ExecutesSetSearchPath()
    {
        DbConnection connection = Substitute.For<DbConnection>();
        DbCommand command = Substitute.For<DbCommand>();
        connection.CreateCommand().Returns(command);

        await _activator.ActivateSchemaAsync(connection, "tenant_xyz",
            TestContext.Current.CancellationToken);

        command.CommandText.ShouldBe("SET search_path TO \"tenant_xyz\", public");
        await command.Received(1).ExecuteNonQueryAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("valid_schema")]
    [InlineData("a")]
    [InlineData("_underscore")]
    [InlineData("abc123")]
    [InlineData("tenant_3fa85f6457174562b3fc2c963f66afa6")]
    public void ActivateSchema_ValidIdentifiers_DoNotThrow(string schema)
    {
        DbConnection connection = Substitute.For<DbConnection>();
        DbCommand command = Substitute.For<DbCommand>();
        connection.CreateCommand().Returns(command);

        Should.NotThrow(() => _activator.ActivateSchema(connection, schema));
    }

    [Theory]
    [InlineData("")]
    [InlineData("UPPERCASE")]
    [InlineData("schema with spaces")]
    [InlineData("schema;drop")]
    [InlineData("123start")]
    [InlineData("a-hyphen")]
    [InlineData("Schema$dollar")]
    public void ActivateSchema_InvalidIdentifiers_ThrowsInvalidOperationException(string schema)
    {
        DbConnection connection = Substitute.For<DbConnection>();
        DbCommand command = Substitute.For<DbCommand>();
        connection.CreateCommand().Returns(command);

        Should.Throw<InvalidOperationException>(
            () => _activator.ActivateSchema(connection, schema));
    }

    [Fact]
    public void ActivateSchema_TooLongIdentifier_ThrowsInvalidOperationException()
    {
        DbConnection connection = Substitute.For<DbConnection>();
        DbCommand command = Substitute.For<DbCommand>();
        connection.CreateCommand().Returns(command);

        string tooLong = "a" + new string('b', 63); // 64 chars, PG max is 63

        Should.Throw<InvalidOperationException>(
            () => _activator.ActivateSchema(connection, tooLong));
    }

    [Fact]
    public void ActivateSchema_ErrorMessage_ContainsSchemaName()
    {
        DbConnection connection = Substitute.For<DbConnection>();
        DbCommand command = Substitute.For<DbCommand>();
        connection.CreateCommand().Returns(command);

        InvalidOperationException ex = Should.Throw<InvalidOperationException>(
            () => _activator.ActivateSchema(connection, "BAD SCHEMA"));

        ex.Message.ShouldContain("BAD SCHEMA");
        ex.Message.ShouldContain("PostgreSQL");
    }
}
