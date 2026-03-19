using System.Data.Common;
using Granit.BackgroundJobs.Domain;
using Granit.BackgroundJobs.EntityFrameworkCore.Internal;
using Granit.BackgroundJobs.Internal;
using Granit.Guids;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.BackgroundJobs.EntityFrameworkCore.Tests;

public sealed class EfBackgroundJobStoreTests : IDisposable
{
    // =========================================================================
    // Test infrastructure — SQLite in-memory (supports ExecuteUpdateAsync)
    // =========================================================================

    private readonly SqliteConnection _connection;

    public EfBackgroundJobStoreTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    private sealed class SqliteContextFactory(DbConnection connection) : IDbContextFactory<BackgroundJobsDbContext>
    {
        public BackgroundJobsDbContext CreateDbContext()
        {
            DbContextOptions<BackgroundJobsDbContext> options =
                new DbContextOptionsBuilder<BackgroundJobsDbContext>()
                    .UseSqlite(connection)
                    .Options;
            return new BackgroundJobsDbContext(options);
        }

        public Task<BackgroundJobsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }

    private EfBackgroundJobStore CreateStore()
    {
        var factory = new SqliteContextFactory(_connection);
        using BackgroundJobsDbContext ctx = factory.CreateDbContext();
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();
        return new EfBackgroundJobStore(factory, new SimpleGuidGenerator());
    }

    private static RecurringJobRegistration MakeRegistration(
        string jobName = "test-job",
        string cron = "0 * * * *",
        string messageType = "My.App.TestMessage, My.App") =>
        new(jobName, cron, messageType);

    private async Task<EfBackgroundJobStore> SeedAsync(
        string jobName = "test-job",
        CancellationToken cancellationToken = default)
    {
        EfBackgroundJobStore store = CreateStore();
        await store.SeedJobsAsync([MakeRegistration(jobName)], cancellationToken);
        return store;
    }

    // =========================================================================
    // FindAsync
    // =========================================================================

    [Fact]
    public async Task FindAsync_UnknownJob_ReturnsNull()
    {
        EfBackgroundJobStore store = CreateStore();

        BackgroundJobDefinition? result = await store.FindAsync(
            "non-existent", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task FindAsync_KnownJob_ReturnsDefinition()
    {
        EfBackgroundJobStore store = await SeedAsync(cancellationToken: TestContext.Current.CancellationToken);

        BackgroundJobDefinition? result = await store.FindAsync(
            "test-job", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result!.JobName.ShouldBe("test-job");
    }

    // =========================================================================
    // SeedJobsAsync
    // =========================================================================

    [Fact]
    public async Task SeedJobsAsync_NewJob_IsInsertedWithDefaultsEnabled()
    {
        EfBackgroundJobStore store = CreateStore();

        await store.SeedJobsAsync([MakeRegistration()], TestContext.Current.CancellationToken);

        BackgroundJobDefinition? job = await store.FindAsync(
            "test-job", TestContext.Current.CancellationToken);
        job.ShouldNotBeNull();
        job!.IsEnabled.ShouldBeTrue();
        job.ConsecutiveFailureCount.ShouldBe(0);
    }

    [Fact]
    public async Task SeedJobsAsync_ExistingJob_PreservesAdminStateAndUpdatesCron()
    {
        EfBackgroundJobStore store = await SeedAsync(cancellationToken: TestContext.Current.CancellationToken);
        await store.SetEnabledAsync("test-job", false, TestContext.Current.CancellationToken);

        await store.SeedJobsAsync(
            [MakeRegistration(cron: "0 8 * * *")], TestContext.Current.CancellationToken);

        BackgroundJobDefinition? job = await store.FindAsync(
            "test-job", TestContext.Current.CancellationToken);
        job!.IsEnabled.ShouldBeFalse("administrative state must be preserved");
        job.CronExpression.ShouldBe("0 8 * * *", "cron expression must be updated");
    }

    [Fact]
    public async Task SeedJobsAsync_CalledTwice_IsIdempotent()
    {
        EfBackgroundJobStore store = CreateStore();

        await store.SeedJobsAsync([MakeRegistration()], TestContext.Current.CancellationToken);
        await store.SeedJobsAsync([MakeRegistration()], TestContext.Current.CancellationToken);

        IReadOnlyList<BackgroundJobDefinition> all = await store.GetAllJobsAsync(
            TestContext.Current.CancellationToken);
        all.Count.ShouldBe(1);
    }

    // =========================================================================
    // SetEnabledAsync
    // =========================================================================

    [Fact]
    public async Task SetEnabledAsync_ToFalse_PersistsValue()
    {
        EfBackgroundJobStore store = await SeedAsync(cancellationToken: TestContext.Current.CancellationToken);

        await store.SetEnabledAsync("test-job", false, TestContext.Current.CancellationToken);

        BackgroundJobDefinition? job = await store.FindAsync(
            "test-job", TestContext.Current.CancellationToken);
        job!.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public async Task SetEnabledAsync_UnknownJob_DoesNotThrow()
    {
        EfBackgroundJobStore store = CreateStore();

        Func<Task> act = () => store.SetEnabledAsync(
            "ghost", false, TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    // =========================================================================
    // RecordExecutionStartAsync
    // =========================================================================

    [Fact]
    public async Task RecordExecutionStartAsync_ResetsCountersAndSetsTimestamp()
    {
        EfBackgroundJobStore store = await SeedAsync(cancellationToken: TestContext.Current.CancellationToken);
        await store.RecordExecutionFailureAsync(
            "test-job", "previous error", TestContext.Current.CancellationToken);

        DateTimeOffset startedAt = new(2026, 2, 20, 8, 0, 0, TimeSpan.Zero);
        await store.RecordExecutionStartAsync(
            "test-job", startedAt, TestContext.Current.CancellationToken);

        BackgroundJobDefinition? job = await store.FindAsync(
            "test-job", TestContext.Current.CancellationToken);
        job!.LastExecutedAt.ShouldBe(startedAt);
        job.ConsecutiveFailureCount.ShouldBe(0);
        job.LastErrorMessage.ShouldBeNull();
        job.TriggeredBy.ShouldBeNull();
    }

    // =========================================================================
    // RecordNextExecutionAsync
    // =========================================================================

    [Fact]
    public async Task RecordNextExecutionAsync_PersistsNextOccurrence()
    {
        EfBackgroundJobStore store = await SeedAsync(cancellationToken: TestContext.Current.CancellationToken);

        DateTimeOffset next = new(2026, 2, 21, 8, 0, 0, TimeSpan.Zero);
        await store.RecordNextExecutionAsync(
            "test-job", next, TestContext.Current.CancellationToken);

        BackgroundJobDefinition? job = await store.FindAsync(
            "test-job", TestContext.Current.CancellationToken);
        job!.NextExecutionAt.ShouldBe(next);
    }

    // =========================================================================
    // RecordExecutionFailureAsync
    // =========================================================================

    [Fact]
    public async Task RecordExecutionFailureAsync_IncrementsCounterAndStoresMessage()
    {
        EfBackgroundJobStore store = await SeedAsync(cancellationToken: TestContext.Current.CancellationToken);
        await store.RecordExecutionFailureAsync(
            "test-job", "first error", TestContext.Current.CancellationToken);

        await store.RecordExecutionFailureAsync(
            "test-job", "timeout", TestContext.Current.CancellationToken);

        BackgroundJobDefinition? job = await store.FindAsync(
            "test-job", TestContext.Current.CancellationToken);
        job!.ConsecutiveFailureCount.ShouldBe(2);
        job.LastErrorMessage.ShouldBe("timeout");
    }

    [Fact]
    public async Task RecordExecutionFailureAsync_UnknownJob_DoesNotThrow()
    {
        EfBackgroundJobStore store = CreateStore();

        Func<Task> act = () => store.RecordExecutionFailureAsync(
            "ghost", "err", TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    // =========================================================================
    // GetEnabledJobsAsync / GetAllJobsAsync
    // =========================================================================

    [Fact]
    public async Task GetEnabledJobsAsync_ReturnsOnlyEnabledJobs()
    {
        EfBackgroundJobStore store = CreateStore();
        await store.SeedJobsAsync(
            [MakeRegistration("job-a"), MakeRegistration("job-b")],
            TestContext.Current.CancellationToken);
        await store.SetEnabledAsync("job-b", false, TestContext.Current.CancellationToken);

        IReadOnlyList<BackgroundJobDefinition> enabled = await store.GetEnabledJobsAsync(
            TestContext.Current.CancellationToken);

        enabled.Count.ShouldBe(1);
        enabled[0].JobName.ShouldBe("job-a");
    }

    [Fact]
    public async Task GetAllJobsAsync_ReturnsAllJobsRegardlessOfEnabled()
    {
        EfBackgroundJobStore store = CreateStore();
        await store.SeedJobsAsync(
            [MakeRegistration("job-a"), MakeRegistration("job-b")],
            TestContext.Current.CancellationToken);
        await store.SetEnabledAsync("job-b", false, TestContext.Current.CancellationToken);

        IReadOnlyList<BackgroundJobDefinition> all = await store.GetAllJobsAsync(
            TestContext.Current.CancellationToken);

        all.Count.ShouldBe(2);
    }

    // =========================================================================
    // SetTriggeredByAsync
    // =========================================================================

    [Fact]
    public async Task SetTriggeredByAsync_PersistsOperatorId()
    {
        EfBackgroundJobStore store = await SeedAsync(cancellationToken: TestContext.Current.CancellationToken);

        await store.SetTriggeredByAsync(
            "test-job", "operator-42", TestContext.Current.CancellationToken);

        BackgroundJobDefinition? job = await store.FindAsync(
            "test-job", TestContext.Current.CancellationToken);
        job!.TriggeredBy.ShouldBe("operator-42");
    }

    [Fact]
    public async Task SetTriggeredByAsync_Null_ClearsField()
    {
        EfBackgroundJobStore store = await SeedAsync(cancellationToken: TestContext.Current.CancellationToken);
        await store.SetTriggeredByAsync(
            "test-job", "operator-42", TestContext.Current.CancellationToken);

        await store.SetTriggeredByAsync(
            "test-job", null, TestContext.Current.CancellationToken);

        BackgroundJobDefinition? job = await store.FindAsync(
            "test-job", TestContext.Current.CancellationToken);
        job!.TriggeredBy.ShouldBeNull();
    }
}
