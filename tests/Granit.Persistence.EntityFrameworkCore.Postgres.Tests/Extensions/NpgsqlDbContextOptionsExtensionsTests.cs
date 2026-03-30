using Granit.Persistence.EntityFrameworkCore.Postgres.Extensions;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Postgres.Tests.Extensions;

public sealed class NpgsqlDbContextOptionsExtensionsTests
{
    [Fact]
    public void UseGranitNpgsql_ReturnsOptionsBuilder()
    {
        var optionsBuilder = new DbContextOptionsBuilder();

        DbContextOptionsBuilder result = optionsBuilder.UseGranitNpgsql(
            "Host=localhost;Database=test;Username=test;Password=test");

        result.ShouldBeSameAs(optionsBuilder);
    }

    [Fact]
    public void UseGranitNpgsql_Generic_ReturnsGenericOptionsBuilder()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();

        DbContextOptionsBuilder<TestDbContext> result = optionsBuilder.UseGranitNpgsql(
            "Host=localhost;Database=test;Username=test;Password=test");

        result.ShouldBeSameAs(optionsBuilder);
    }

    [Fact]
    public void UseGranitNpgsql_WithCustomAction_DoesNotThrow()
    {
        var optionsBuilder = new DbContextOptionsBuilder();

        Should.NotThrow(() => optionsBuilder.UseGranitNpgsql(
            "Host=localhost;Database=test;Username=test;Password=test",
            b => b.CommandTimeout(60)));
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options);
}
