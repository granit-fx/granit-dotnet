using System.Data.Common;
using Granit.Persistence.MultiTenancy;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Persistence.Tests.MultiTenancy;

public sealed class OracleTenantSchemaActivatorTests
{
    private readonly OracleTenantSchemaActivator _activator = new();

    [Fact]
    public void ActivateSchema_ValidSchemaName_ExecutesAlterSessionCommand()
    {
        DbConnection connection = Substitute.For<DbConnection>();
        DbCommand command = Substitute.For<DbCommand>();
        connection.CreateCommand().Returns(command);

        _activator.ActivateSchema(connection, "TENANT_ABC");

        command.CommandText.ShouldBe("ALTER SESSION SET CURRENT_SCHEMA = \"TENANT_ABC\"");
        command.Received(1).ExecuteNonQuery();
    }

    [Fact]
    public async Task ActivateSchemaAsync_ValidSchemaName_ExecutesAlterSessionCommand()
    {
        DbConnection connection = Substitute.For<DbConnection>();
        DbCommand command = Substitute.For<DbCommand>();
        connection.CreateCommand().Returns(command);

        await _activator.ActivateSchemaAsync(connection, "TENANT_XYZ",
            TestContext.Current.CancellationToken);

        command.CommandText.ShouldBe("ALTER SESSION SET CURRENT_SCHEMA = \"TENANT_XYZ\"");
        await command.Received(1).ExecuteNonQueryAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("ValidSchema")]
    [InlineData("A")]
    [InlineData("Schema123")]
    [InlineData("schema_with_underscore")]
    [InlineData("schema$dollar")]
    [InlineData("schema#hash")]
    public void ActivateSchema_ValidIdentifiers_DoNotThrow(string schema)
    {
        DbConnection connection = Substitute.For<DbConnection>();
        DbCommand command = Substitute.For<DbCommand>();
        connection.CreateCommand().Returns(command);

        Should.NotThrow(() => _activator.ActivateSchema(connection, schema));
    }

    [Theory]
    [InlineData("")]
    [InlineData("schema with spaces")]
    [InlineData("schema;drop")]
    [InlineData("123start")]
    [InlineData("_underscore_start")]
    [InlineData("a-hyphen")]
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

        string tooLong = "A" + new string('B', 128); // 129 chars, Oracle max is 128

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
            () => _activator.ActivateSchema(connection, "bad schema!"));

        ex.Message.ShouldContain("bad schema!");
        ex.Message.ShouldContain("Oracle");
    }
}
