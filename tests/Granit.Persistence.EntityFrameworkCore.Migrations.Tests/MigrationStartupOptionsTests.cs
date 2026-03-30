using Granit.Persistence.EntityFrameworkCore.Migrations.Options;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Migrations.Tests;

public sealed class MigrationStartupOptionsTests
{
    [Fact]
    public void SectionName_HasExpectedValue() => MigrationStartupOptions.SectionName.ShouldBe("GranitMigrations");

    [Fact]
    public void Default_DefaultBatchSize_Is500()
    {
        MigrationStartupOptions options = new();

        options.DefaultBatchSize.ShouldBe(500);
    }

    [Fact]
    public void Default_BatchExecutionTimeout_Is5Minutes()
    {
        MigrationStartupOptions options = new();

        options.BatchExecutionTimeout.ShouldBe(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void DefaultBatchSize_SetAndGet_ReturnsAssignedValue()
    {
        MigrationStartupOptions options = new() { DefaultBatchSize = 1000 };

        options.DefaultBatchSize.ShouldBe(1000);
    }

    [Fact]
    public void BatchExecutionTimeout_SetAndGet_ReturnsAssignedValue()
    {
        MigrationStartupOptions options = new()
        {
            BatchExecutionTimeout = TimeSpan.FromMinutes(10),
        };

        options.BatchExecutionTimeout.ShouldBe(TimeSpan.FromMinutes(10));
    }
}
