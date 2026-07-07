using Granit.BackgroundJobs.Domain;
using Granit.BackgroundJobs.Internal;
using Granit.BackgroundJobs.Options;
using Granit.Guids;
using Shouldly;
using Xunit;

namespace Granit.BackgroundJobs.Tests;

public sealed class InMemoryBackgroundJobStoreTests
{
    private readonly InMemoryBackgroundJobStore _sut = new(
        new SimpleGuidGenerator(), Microsoft.Extensions.Options.Options.Create(new BackgroundJobsOptions()));
    private readonly DateTimeOffset _now = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

    private static RecurringJobRegistration MakeRegistration(
        string name = "test-job",
        string cron = "0 * * * *",
        string messageType = "Granit.BackgroundJobs.Tests.FakeMessage, Granit.BackgroundJobs.Tests") =>
        new(JobName: name, CronExpression: cron, MessageType: messageType);

    // =========================================================================
    // SeedJobsAsync
    // =========================================================================

    [Fact]
    public async Task SeedJobsAsync_NewJob_AddsItToStore()
    {
        // Arrange
        RecurringJobRegistration reg = MakeRegistration("daily-report", "0 8 * * *");
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        // Act
        await _sut.SeedJobsAsync([reg], cancellationToken);
        BackgroundJobDefinition? job = await _sut.FindAsync("daily-report", cancellationToken);

        // Assert
        job.ShouldNotBeNull();
        job!.JobName.ShouldBe("daily-report");
        job.CronExpression.ShouldBe("0 8 * * *");
        job.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task SeedJobsAsync_ExistingJob_PreservesIsEnabledAndUpdatesCron()
    {
        // Arrange — seed first, then pause it manually
        RecurringJobRegistration reg = MakeRegistration("daily-report", "0 8 * * *");
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await _sut.SeedJobsAsync([reg], cancellationToken);
        await _sut.SetEnabledAsync("daily-report", false, cancellationToken);

        // Act — re-seed with updated cron expression
        RecurringJobRegistration updated = MakeRegistration("daily-report", "0 9 * * *");
        await _sut.SeedJobsAsync([updated], cancellationToken);
        BackgroundJobDefinition? job = await _sut.FindAsync("daily-report", cancellationToken);

        // Assert — cron updated, pause state preserved
        job!.CronExpression.ShouldBe("0 9 * * *");
        job.IsEnabled.ShouldBeFalse();
    }

    // =========================================================================
    // FindAsync / GetAllJobsAsync / GetEnabledJobsAsync
    // =========================================================================

    [Fact]
    public async Task FindAsync_UnknownJob_ReturnsNull()
    {
        // Act
        BackgroundJobDefinition? result =
            await _sut.FindAsync("does-not-exist", TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetAllJobsAsync_MultipleJobs_ReturnsAll()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await _sut.SeedJobsAsync([MakeRegistration("job-a"), MakeRegistration("job-b")], cancellationToken);

        // Act
        IReadOnlyList<BackgroundJobDefinition> jobs = await _sut.GetAllJobsAsync(cancellationToken);

        // Assert
        jobs.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetEnabledJobsAsync_FiltersDisabledJobs()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await _sut.SeedJobsAsync([MakeRegistration("job-a"), MakeRegistration("job-b")], cancellationToken);
        await _sut.SetEnabledAsync("job-a", false, cancellationToken);

        // Act
        IReadOnlyList<BackgroundJobDefinition> enabled = await _sut.GetEnabledJobsAsync(cancellationToken);

        // Assert
        enabled.Count.ShouldBe(1);
        enabled.Single().JobName.ShouldBe("job-b");
    }

    // =========================================================================
    // Execution tracking
    // =========================================================================

    [Fact]
    public async Task RecordExecutionStartAsync_UpdatesLastExecutedAtAndClearsErrors()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await _sut.SeedJobsAsync([MakeRegistration("test-job")], cancellationToken);
        await _sut.RecordExecutionFailureAsync("test-job", "previous error", cancellationToken);

        // Act
        await _sut.RecordExecutionStartAsync("test-job", _now, cancellationToken);
        BackgroundJobDefinition? job = await _sut.FindAsync("test-job", cancellationToken);

        // Assert
        job!.LastExecutedAt.ShouldBe(_now);
        job.LastErrorMessage.ShouldBeNull();
        job.ConsecutiveFailureCount.ShouldBe(0);
        job.TriggeredBy.ShouldBeNull();
    }

    [Fact]
    public async Task RecordNextExecutionAsync_UpdatesNextExecutionAt()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await _sut.SeedJobsAsync([MakeRegistration("test-job")], cancellationToken);
        DateTimeOffset nextRun = _now.AddHours(1);

        // Act
        await _sut.RecordNextExecutionAsync("test-job", nextRun, cancellationToken);
        BackgroundJobDefinition? job = await _sut.FindAsync("test-job", cancellationToken);

        // Assert
        job!.NextExecutionAt.ShouldBe(nextRun);
    }

    [Fact]
    public async Task RecordExecutionFailureAsync_IncrementsConsecutiveFailureCount()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await _sut.SeedJobsAsync([MakeRegistration("test-job")], cancellationToken);

        // Act
        await _sut.RecordExecutionFailureAsync("test-job", "timeout", cancellationToken);
        await _sut.RecordExecutionFailureAsync("test-job", "timeout again", cancellationToken);
        BackgroundJobDefinition? job = await _sut.FindAsync("test-job", cancellationToken);

        // Assert
        job!.ConsecutiveFailureCount.ShouldBe(2);
        job.LastErrorMessage.ShouldBe("timeout again");
    }

    // =========================================================================
    // Pause / Resume / TriggeredBy
    // =========================================================================

    [Fact]
    public async Task SetEnabledAsync_PausesAndResumesJob()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await _sut.SeedJobsAsync([MakeRegistration("test-job")], cancellationToken);

        // Act — pause, assert immediately (store returns same object reference)
        await _sut.SetEnabledAsync("test-job", false, cancellationToken);
        BackgroundJobDefinition? paused = await _sut.FindAsync("test-job", cancellationToken);
        paused!.IsEnabled.ShouldBeFalse();

        // Act — resume, assert
        await _sut.SetEnabledAsync("test-job", true, cancellationToken);
        BackgroundJobDefinition? resumed = await _sut.FindAsync("test-job", cancellationToken);
        resumed!.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task SetTriggeredByAsync_SetsTriggeredBy()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await _sut.SeedJobsAsync([MakeRegistration("test-job")], cancellationToken);

        // Act
        await _sut.SetTriggeredByAsync("test-job", "user-123", cancellationToken);
        BackgroundJobDefinition? job = await _sut.FindAsync("test-job", cancellationToken);

        // Assert
        job!.TriggeredBy.ShouldBe("user-123");
    }

    [Fact]
    public async Task RecordExecutionStartAsync_UnknownJob_DoesNotThrow()
    {
        // Act — should silently ignore unknown job
        Func<Task> act = () => _sut.RecordExecutionStartAsync(
            "ghost-job", _now, TestContext.Current.CancellationToken);

        // Assert
        await Should.NotThrowAsync(act);
    }
}
