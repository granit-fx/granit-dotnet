using Granit.BackgroundJobs.Domain;
using Granit.BackgroundJobs.EntityFrameworkCore.Internal;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using Xunit;

namespace Granit.BackgroundJobs.EntityFrameworkCore.Tests;

public sealed class BackgroundJobsDbContextTests
{
    private static BackgroundJobsDbContext CreateInMemory()
    {
        DbContextOptions<BackgroundJobsDbContext> options =
            new DbContextOptionsBuilder<BackgroundJobsDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

        return new BackgroundJobsDbContext(options, GranitDesignTime.CurrentTenant);
    }

    // =========================================================================
    // Schema creation
    // =========================================================================

    [Fact]
    public async Task EnsureCreatedAsync_WithInMemoryProvider_DoesNotThrow()
    {
        await using BackgroundJobsDbContext ctx = CreateInMemory();

        Func<Task> act = () => ctx.Database.EnsureCreatedAsync(
            TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    // =========================================================================
    // Entity configuration — model metadata
    // =========================================================================

    [Fact]
    public void Model_TableName_IsSchedulingBackgroundJobs()
    {
        using BackgroundJobsDbContext ctx = CreateInMemory();

        string? tableName = ctx.Model
            .FindEntityType(typeof(BackgroundJobDefinition))!
            .GetTableName();

        tableName.ShouldBe("background_jobs_background_jobs");
    }

    [Fact]
    public void Model_JobNameIndex_IsUnique()
    {
        using BackgroundJobsDbContext ctx = CreateInMemory();

        IEntityType entityType = ctx.Model.FindEntityType(typeof(BackgroundJobDefinition))!;

        IIndex? uniqueIndex = entityType.GetIndexes()
            .FirstOrDefault(i =>
                i.IsUnique &&
                i.Properties.Any(p => p.Name == nameof(BackgroundJobDefinition.JobName)));

        uniqueIndex.ShouldNotBeNull("a unique index on JobName must be configured");
    }

    [Fact]
    public void Model_JobName_HasMaxLength200()
    {
        using BackgroundJobsDbContext ctx = CreateInMemory();

        IProperty? property = ctx.Model
            .FindEntityType(typeof(BackgroundJobDefinition))!
            .FindProperty(nameof(BackgroundJobDefinition.JobName));

        property!.GetMaxLength().ShouldBe(200);
        property.IsNullable.ShouldBeFalse();
    }

    [Fact]
    public void Model_MessageType_HasMaxLength500()
    {
        using BackgroundJobsDbContext ctx = CreateInMemory();

        IProperty? property = ctx.Model
            .FindEntityType(typeof(BackgroundJobDefinition))!
            .FindProperty(nameof(BackgroundJobDefinition.MessageType));

        property!.GetMaxLength().ShouldBe(500);
        property.IsNullable.ShouldBeFalse();
    }

    [Fact]
    public void Model_CronExpression_HasMaxLength100()
    {
        using BackgroundJobsDbContext ctx = CreateInMemory();

        IProperty? property = ctx.Model
            .FindEntityType(typeof(BackgroundJobDefinition))!
            .FindProperty(nameof(BackgroundJobDefinition.CronExpression));

        property!.GetMaxLength().ShouldBe(100);
        property.IsNullable.ShouldBeFalse();
    }

    [Fact]
    public void Model_LastErrorMessage_HasMaxLength2000()
    {
        using BackgroundJobsDbContext ctx = CreateInMemory();

        IProperty? property = ctx.Model
            .FindEntityType(typeof(BackgroundJobDefinition))!
            .FindProperty(nameof(BackgroundJobDefinition.LastErrorMessage));

        property!.GetMaxLength().ShouldBe(2000);
        property.IsNullable.ShouldBeTrue();
    }

    [Fact]
    public void Model_TriggeredBy_HasMaxLength450()
    {
        using BackgroundJobsDbContext ctx = CreateInMemory();

        IProperty? property = ctx.Model
            .FindEntityType(typeof(BackgroundJobDefinition))!
            .FindProperty(nameof(BackgroundJobDefinition.TriggeredBy));

        property!.GetMaxLength().ShouldBe(450);
        property.IsNullable.ShouldBeTrue();
    }

    // =========================================================================
    // CRUD roundtrip
    // =========================================================================

    [Fact]
    public async Task SaveAndReload_AllFields_MatchOriginal()
    {
        await using BackgroundJobsDbContext ctx = CreateInMemory();

        var job = BackgroundJobDefinition.Create(
            Guid.NewGuid(), "daily-report", "0 8 * * *", "My.App.DailyReportMessage, My.App");
        job.RecordExecutionStart(new DateTimeOffset(2026, 1, 15, 8, 0, 0, TimeSpan.Zero));
        job.ScheduleNext(new DateTimeOffset(2026, 1, 16, 8, 0, 0, TimeSpan.Zero));
        job.RecordFailure("Timeout after 30s", 3);
        job.RecordFailure("Timeout after 30s", 3);
        job.SetTriggeredBy("admin-user");

        ctx.Jobs.Add(job);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        ctx.ChangeTracker.Clear();

        BackgroundJobDefinition? loaded = await ctx.Jobs
            .FindAsync([job.Id], TestContext.Current.CancellationToken);

        loaded.ShouldNotBeNull();
        loaded!.JobName.ShouldBe("daily-report");
        loaded.MessageType.ShouldBe("My.App.DailyReportMessage, My.App");
        loaded.CronExpression.ShouldBe("0 8 * * *");
        loaded.IsEnabled.ShouldBeTrue();
        loaded.LastExecutedAt.ShouldBe(job.LastExecutedAt);
        loaded.NextExecutionAt.ShouldBe(job.NextExecutionAt);
        loaded.ConsecutiveFailureCount.ShouldBe(2);
        loaded.LastErrorMessage.ShouldBe("Timeout after 30s");
        loaded.TriggeredBy.ShouldBe("admin-user");
    }
}
