using Granit.Persistence.EntityFrameworkCore.Migrations.Options;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Migrations.Tests;

public sealed class MigrationStartupOptionsTests
{
    [Fact]
    public void SectionName_HasExpectedValue() => MigrationStartupOptions.SectionName.ShouldBe("Persistence:Migrations");

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

}
