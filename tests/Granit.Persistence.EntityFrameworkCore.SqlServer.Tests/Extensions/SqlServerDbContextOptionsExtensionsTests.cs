using Granit.Persistence.EntityFrameworkCore.SqlServer.Extensions;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.SqlServer.Tests.Extensions;

public sealed class SqlServerDbContextOptionsExtensionsTests
{
    private const string ConnectionString =
        "Server=localhost;Database=test;User Id=test;Password=test;TrustServerCertificate=true";

    [Fact]
    public void UseGranitSqlServer_ReturnsOptionsBuilder()
    {
        var optionsBuilder = new DbContextOptionsBuilder();

        DbContextOptionsBuilder result = optionsBuilder.UseGranitSqlServer(ConnectionString);

        result.ShouldBeSameAs(optionsBuilder);
    }

    [Fact]
    public void UseGranitSqlServer_Generic_ReturnsGenericOptionsBuilder()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();

        DbContextOptionsBuilder<TestDbContext> result = optionsBuilder.UseGranitSqlServer(ConnectionString);

        result.ShouldBeSameAs(optionsBuilder);
    }

    [Fact]
    public void UseGranitSqlServer_WithCustomAction_DoesNotThrow() =>
        Should.NotThrow(() => new DbContextOptionsBuilder().UseGranitSqlServer(
            ConnectionString,
            b => b.CommandTimeout(60)));

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options);
}
