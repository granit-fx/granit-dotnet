using System.Diagnostics;
using Granit.BackgroundJobs.Abstractions;
using Granit.BackgroundJobs.Domain;
using Granit.BackgroundJobs.Internal;
using Granit.Exceptions;
using Granit.Timing;
using Granit.Users;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.BackgroundJobs.Tests;

public sealed class BackgroundJobManagerTests : IDisposable
{
    private readonly IBackgroundJobStoreReader _storeReader = Substitute.For<IBackgroundJobStoreReader>();
    private readonly IBackgroundJobStoreWriter _storeWriter = Substitute.For<IBackgroundJobStoreWriter>();
    private readonly IBackgroundJobDispatcher _dispatcher = Substitute.For<IBackgroundJobDispatcher>();
    private readonly IDeadLetterQueueInspector _dlqInspector = Substitute.For<IDeadLetterQueueInspector>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ICurrentUserService _user = Substitute.For<ICurrentUserService>();
    private readonly ILogger<BackgroundJobManager> _logger =
        Substitute.For<ILogger<BackgroundJobManager>>();
    private readonly ActivityListener _activityListener;

    public BackgroundJobManagerTests()
    {
        _activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Granit.BackgroundJobs",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(_activityListener);

        // Allow [LoggerMessage] generated code to execute both branches (IsEnabled check)
        _logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);

        // Default: no dead letters (graceful baseline)
        _dlqInspector.GetCountsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyDictionary<string, long>>(
                new Dictionary<string, long>()));
    }

    public void Dispose() => _activityListener.Dispose();

    private BackgroundJobManager MakeSut() =>
        new(_storeReader, _storeWriter, _dispatcher, _dlqInspector, _clock, _user, _logger);

    private static BackgroundJobDefinition MakeJob(
        string name = "test-job",
        string cron = "0 * * * *",
        bool enabled = true)
    {
        var job = BackgroundJobDefinition.Create(
            Guid.NewGuid(), name, cron, typeof(FakeDailyReportMessage).AssemblyQualifiedName!);

        if (!enabled)
        {
            job.Pause();
            job.ClearDomainEvents();
        }

        return job;
    }

    // =========================================================================
    // GetAllAsync
    // =========================================================================

    [Fact]
    public async Task GetAllAsync_ReturnsStatusForEachJob()
    {
        // Arrange
        BackgroundJobDefinition job = MakeJob("daily-report", "0 8 * * *");
        _storeReader.GetAllJobsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<BackgroundJobDefinition>>([job]));

        BackgroundJobManager sut = MakeSut();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        // Act
        IReadOnlyList<BackgroundJobStatus> result = await sut.GetAllAsync(cancellationToken);

        // Assert
        result.Count.ShouldBe(1);
        result[0].JobName.ShouldBe("daily-report");
        result[0].CronExpression.ShouldBe("0 8 * * *");
        result[0].IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task GetAllAsync_WithDlqEntries_PopulatesDeadLetterCount()
    {
        // Arrange
        string messageTypeShortName =
            typeof(FakeDailyReportMessage).AssemblyQualifiedName!.Split(',')[0].Trim();

        BackgroundJobDefinition job = MakeJob("daily-report", "0 8 * * *");
        job.RecordFailure("timeout");
        job.RecordFailure("timeout");
        job.RecordFailure("timeout");
        _storeReader.GetAllJobsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<BackgroundJobDefinition>>([job]));

        _dlqInspector.GetCountsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyDictionary<string, long>>(
                new Dictionary<string, long> { [messageTypeShortName] = 5 }));

        BackgroundJobManager sut = MakeSut();

        // Act
        IReadOnlyList<BackgroundJobStatus> result =
            await sut.GetAllAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Count.ShouldBe(1);
        result[0].ConsecutiveFailures.ShouldBe(3);
        result[0].LastError.ShouldBe("timeout");
        result[0].DeadLetterCount.ShouldBe(5);
    }

    // =========================================================================
    // FindAsync
    // =========================================================================

    [Fact]
    public async Task FindAsync_KnownJob_ReturnsStatus()
    {
        // Arrange
        BackgroundJobDefinition job = MakeJob("daily-report");
        _storeReader.FindAsync("daily-report", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<BackgroundJobDefinition?>(job));

        BackgroundJobManager sut = MakeSut();

        // Act
        BackgroundJobStatus? status =
            await sut.FindAsync("daily-report", TestContext.Current.CancellationToken);

        // Assert
        status.ShouldNotBeNull();
        status!.JobName.ShouldBe("daily-report");
    }

    [Fact]
    public async Task FindAsync_UnknownJob_ReturnsNull()
    {
        // Arrange
        _storeReader.FindAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<BackgroundJobDefinition?>(null));

        BackgroundJobManager sut = MakeSut();

        // Act
        BackgroundJobStatus? status =
            await sut.FindAsync("ghost", TestContext.Current.CancellationToken);

        // Assert
        status.ShouldBeNull();
    }

    // =========================================================================
    // PauseAsync
    // =========================================================================

    [Fact]
    public async Task PauseAsync_KnownJob_SetsEnabledFalse()
    {
        // Arrange
        BackgroundJobDefinition job = MakeJob("daily-report");
        _storeReader.FindAsync("daily-report", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<BackgroundJobDefinition?>(job));

        BackgroundJobManager sut = MakeSut();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        // Act
        await sut.PauseAsync("daily-report", cancellationToken);

        // Assert
        await _storeWriter.Received(1).SetEnabledAsync("daily-report", false, cancellationToken);
    }

    [Fact]
    public async Task PauseAsync_UnknownJob_ThrowsEntityNotFoundException()
    {
        // Arrange
        _storeReader.FindAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<BackgroundJobDefinition?>(null));

        BackgroundJobManager sut = MakeSut();

        // Act
        Func<Task> act = () => sut.PauseAsync("ghost", TestContext.Current.CancellationToken);

        // Assert
        await Should.ThrowAsync<EntityNotFoundException>(act);
    }

    // =========================================================================
    // ResumeAsync
    // =========================================================================

    [Fact]
    public async Task ResumeAsync_KnownJob_SetsEnabledTrueAndSchedules()
    {
        // Arrange
        BackgroundJobDefinition job = MakeJob("daily-report", "0 8 * * *");
        _storeReader.FindAsync("daily-report", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<BackgroundJobDefinition?>(job));
        _clock.Now.Returns(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        BackgroundJobManager sut = MakeSut();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        // Act
        await sut.ResumeAsync("daily-report", cancellationToken);

        // Assert
        await _storeWriter.Received(1).SetEnabledAsync("daily-report", true, cancellationToken);
        await _storeWriter.Received(1).RecordNextExecutionAsync(
            "daily-report", Arg.Any<DateTimeOffset>(), cancellationToken);
    }

    [Fact]
    public async Task ResumeAsync_UnknownJob_ThrowsEntityNotFoundException()
    {
        // Arrange
        _storeReader.FindAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<BackgroundJobDefinition?>(null));

        BackgroundJobManager sut = MakeSut();

        // Act
        Func<Task> act = () => sut.ResumeAsync("ghost", TestContext.Current.CancellationToken);

        // Assert
        await Should.ThrowAsync<EntityNotFoundException>(act);
    }

    [Fact]
    public async Task ResumeAsync_InvalidCron_LogsWarningAndSkipsScheduling()
    {
        // Arrange — cron expression that produces no next occurrence (unreachable)
        BackgroundJobDefinition job = MakeJob("daily-report", "0 8 31 2 *"); // Feb 31 never exists
        _storeReader.FindAsync("daily-report", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<BackgroundJobDefinition?>(job));
        _clock.Now.Returns(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        BackgroundJobManager sut = MakeSut();

        // Act — should not throw, just log warning
        Func<Task> act = () =>
            sut.ResumeAsync("daily-report", TestContext.Current.CancellationToken);

        // Assert
        await Should.NotThrowAsync(act);
        await _storeWriter.DidNotReceive()
            .RecordNextExecutionAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // TriggerNowAsync
    // =========================================================================

    [Fact]
    public async Task TriggerNowAsync_AuthenticatedUser_PublishesWithTriggeredByHeader()
    {
        // Arrange
        BackgroundJobDefinition job = MakeJob("daily-report");
        _storeReader.FindAsync("daily-report", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<BackgroundJobDefinition?>(job));
        _user.IsAuthenticated.Returns(true);
        _user.UserId.Returns("user-abc");

        BackgroundJobManager sut = MakeSut();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        // Act
        await sut.TriggerNowAsync("daily-report", cancellationToken);

        // Assert
        await _dispatcher.Received(1).PublishAsync(
            Arg.Is<FakeDailyReportMessage>(m => m != null),
            Arg.Is<IDictionary<string, string>>(h =>
                h.ContainsKey(BackgroundJobHeaders.TriggeredBy)
                && h[BackgroundJobHeaders.TriggeredBy] == "user-abc"),
            cancellationToken);
    }

    [Fact]
    public async Task TriggerNowAsync_AnonymousUser_PublishesWithoutTriggeredByHeader()
    {
        // Arrange
        BackgroundJobDefinition job = MakeJob("daily-report");
        _storeReader.FindAsync("daily-report", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<BackgroundJobDefinition?>(job));
        _user.IsAuthenticated.Returns(false);

        BackgroundJobManager sut = MakeSut();

        // Act
        await sut.TriggerNowAsync("daily-report", TestContext.Current.CancellationToken);

        // Assert — headers should be null for anonymous user
        await _dispatcher.Received(1).PublishAsync(
            Arg.Any<FakeDailyReportMessage>(),
            null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TriggerNowAsync_UnknownJob_ThrowsEntityNotFoundException()
    {
        // Arrange
        _storeReader.FindAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<BackgroundJobDefinition?>(null));

        BackgroundJobManager sut = MakeSut();

        // Act
        Func<Task> act = () =>
            sut.TriggerNowAsync("ghost", TestContext.Current.CancellationToken);

        // Assert
        await Should.ThrowAsync<EntityNotFoundException>(act);
    }
}
