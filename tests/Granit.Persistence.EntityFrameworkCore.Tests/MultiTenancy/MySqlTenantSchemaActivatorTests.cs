using System.Data.Common;
using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests.MultiTenancy;

public sealed class MySqlTenantSchemaActivatorTests
{
    private readonly MySqlTenantSchemaActivator _activator = new();

    [Fact]
    public void ActivateSchema_ValidSchemaName_ExecutesUseCommand()
    {
        DbConnection connection = Substitute.For<DbConnection>();
        DbCommand command = Substitute.For<DbCommand>();
        connection.CreateCommand().Returns(command);

        _activator.ActivateSchema(connection, "tenant_abc");

        command.CommandText.ShouldBe("USE `tenant_abc`");
        command.Received(1).ExecuteNonQuery();
    }

    [Fact]
    public async Task ActivateSchemaAsync_ValidSchemaName_ExecutesUseCommand()
    {
        DbConnection connection = Substitute.For<DbConnection>();
        DbCommand command = Substitute.For<DbCommand>();
        connection.CreateCommand().Returns(command);

        await _activator.ActivateSchemaAsync(connection, "tenant_xyz",
            TestContext.Current.CancellationToken);

        command.CommandText.ShouldBe("USE `tenant_xyz`");
        await command.Received(1).ExecuteNonQueryAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("valid_schema")]
    [InlineData("a")]
    [InlineData("_underscore")]
    [InlineData("$dollar")]
    [InlineData("Schema123")]
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

        string tooLong = "a" + new string('b', 64); // 65 chars, MySQL max is 64

        Should.Throw<InvalidOperationException>(
            () => _activator.ActivateSchema(connection, tooLong));
    }
}
