using Granit.BackgroundJobs.Abstractions;
using Granit.BackgroundJobs.Domain;
using Granit.BackgroundJobs.Internal;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.BackgroundJobs.Tests;

public sealed class ChannelCronSchedulerServiceTests
{
    // =========================================================================
    // Test infrastructure
    // =========================================================================

    private readonly IBackgroundJobStoreReader _storeReader = Substitute.For<IBackgroundJobStoreReader>();
    private readonly IBackgroundJobStoreWriter _storeWriter = Substitute.For<IBackgroundJobStoreWriter>();
    private readonly IBackgroundJobDispatcher _dispatcher = Substitute.For<IBackgroundJobDispatcher>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IServiceScopeFactory _scopeFactory;

    public ChannelCronSchedulerServiceTests()
    {
        _clock.Now.Returns(new DateTimeOffset(2026, 2, 20, 8, 0, 0, TimeSpan.Zero));

        IServiceScope scope = Substitute.For<IServiceScope>();
        IServiceProvider sp = Substitute.For<IServiceProvider>();
        sp.GetService(typeof(IBackgroundJobStoreReader)).Returns(_storeReader);
        sp.GetService(typeof(IBackgroundJobStoreWriter)).Returns(_storeWriter);
        sp.GetService(typeof(IBackgroundJobDispatcher)).Returns(_dispatcher);
        scope.ServiceProvider.Returns(sp);
        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _scopeFactory.CreateAsyncScope().Returns(new AsyncServiceScope(scope));
    }

    private ChannelCronSchedulerService CreateService() =>
        new(_scopeFactory, _clock, NullLogger<ChannelCronSchedulerService>.Instance);

    /// <summary>
    /// Starts the <see cref="BackgroundService"/> and waits for <c>ExecuteAsync</c> to complete.
    /// The service has a 2-second startup delay, so we wait long enough for it to finish.
    /// </summary>
    private static async Task RunServiceAsync(ChannelCronSchedulerService service, CancellationToken cancellationToken)
    {
        await service.StartAsync(cancellationToken);

        // ExecuteAsync has a 2-second Task.Delay before processing jobs.
        // Wait for it to complete naturally before calling StopAsync.
        await Task.Delay(TimeSpan.FromSeconds(4), cancellationToken);

        await service.StopAsync(cancellationToken);
    }

    private static BackgroundJobDefinition MakeJob(
        string jobName,
        string cron = "0 9 * * *",
        bool isEnabled = true,
        DateTimeOffset? nextExecutionAt = null)
    {
        var job = BackgroundJobDefinition.Create(
            Guid.NewGuid(), jobName, cron, typeof(FakeJobMessage).AssemblyQualifiedName!);

        if (!isEnabled)
        {
            job.Pause();
            job.ClearDomainEvents();
        }

        if (nextExecutionAt.HasValue)
        {
            job.ScheduleNext(nextExecutionAt);
        }

        return job;
    }

    // Minimal job message class for testing
    private sealed class FakeJobMessage : IBackgroundJob;

    // =========================================================================
    // Scenario: job with null NextExecutionAt -> schedules first occurrence
    // =========================================================================

    [Fact]
    public async Task ExecuteAsync_JobWithNoNextExecution_SchedulesFirstOccurrence()
    {
        BackgroundJobDefinition job = MakeJob("daily-sync", cron: "0 9 * * *");
        _storeReader.GetEnabledJobsAsync(Arg.Any<CancellationToken>())
            .Returns([job]);

        await RunServiceAsync(CreateService(), TestContext.Current.CancellationToken);

        await _dispatcher.Received(1).ScheduleAsync(
            Arg.Any<FakeJobMessage>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
        await _storeWriter.Received(1).RecordNextExecutionAsync(
            "daily-sync",
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Scenario: NextExecutionAt is in the future -> no duplicate
    // =========================================================================

    [Fact]
    public async Task ExecuteAsync_JobAlreadyScheduledInFuture_DoesNotReschedule()
    {
        DateTimeOffset future = _clock.Now.AddHours(2);
        BackgroundJobDefinition job = MakeJob("daily-sync", nextExecutionAt: future);
        _storeReader.GetEnabledJobsAsync(Arg.Any<CancellationToken>())
            .Returns([job]);

        await RunServiceAsync(CreateService(), TestContext.Current.CancellationToken);

        await _dispatcher.DidNotReceive().ScheduleAsync(
            Arg.Any<object>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await _storeWriter.DidNotReceive().RecordNextExecutionAsync(
            Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Scenario: NextExecutionAt is in the past -> reschedules
    // =========================================================================

    [Fact]
    public async Task ExecuteAsync_JobWithPastNextExecution_Reschedules()
    {
        DateTimeOffset past = _clock.Now.AddHours(-2);
        BackgroundJobDefinition job = MakeJob("daily-sync", cron: "0 9 * * *", nextExecutionAt: past);
        _storeReader.GetEnabledJobsAsync(Arg.Any<CancellationToken>())
            .Returns([job]);

        await RunServiceAsync(CreateService(), TestContext.Current.CancellationToken);

        await _dispatcher.Received(1).ScheduleAsync(
            Arg.Any<FakeJobMessage>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Scenario: paused job -> not initialized (filtered by GetEnabledJobsAsync)
    // =========================================================================

    [Fact]
    public async Task ExecuteAsync_NoEnabledJobs_DoesNotScheduleAnything()
    {
        _storeReader.GetEnabledJobsAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<BackgroundJobDefinition>());

        await RunServiceAsync(CreateService(), TestContext.Current.CancellationToken);

        await _dispatcher.DidNotReceive().ScheduleAsync(
            Arg.Any<object>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Scenario: invalid cron -> skip without scheduling
    // =========================================================================

    [Fact]
    public async Task ExecuteAsync_JobWithInvalidCron_SkipsWithoutScheduling()
    {
        BackgroundJobDefinition job = MakeJob("bad-cron", cron: "NOT_A_CRON");
        _storeReader.GetEnabledJobsAsync(Arg.Any<CancellationToken>()).Returns([job]);

        await RunServiceAsync(CreateService(), TestContext.Current.CancellationToken);

        await _dispatcher.DidNotReceive().ScheduleAsync(
            Arg.Any<object>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await _storeWriter.DidNotReceive().RecordNextExecutionAsync(
            Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Scenario: 6-field cron (with seconds) -> schedules using IncludeSeconds parse
    // =========================================================================

    [Fact]
    public async Task ExecuteAsync_JobWithSixFieldCron_SchedulesSuccessfully()
    {
        // "*/30 * * * * *" = every 30 seconds — 6-field cron parsed with IncludeSeconds
        BackgroundJobDefinition job = MakeJob("seconds-job", cron: "*/30 * * * * *");
        _storeReader.GetEnabledJobsAsync(Arg.Any<CancellationToken>()).Returns([job]);

        await RunServiceAsync(CreateService(), TestContext.Current.CancellationToken);

        await _dispatcher.Received(1).ScheduleAsync(
            Arg.Any<FakeJobMessage>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // CronSchedulerHelper.CreateMessage — unknown type throws
    // =========================================================================

    [Fact]
    public void CreateMessage_UnknownType_ThrowsInvalidOperationException()
    {
        Action act = () =>
            CronSchedulerHelper.CreateMessage("Unknown.Type, UnknownAssembly", "test-job");

        Should.Throw<InvalidOperationException>(act).Message.ShouldContain("Cannot resolve message type");
    }

    // =========================================================================
    // StopAsync — graceful
    // =========================================================================

    [Fact]
    public async Task StopAsync_DoesNotThrow()
    {
        Func<Task> act = () =>
            ((IHostedService)CreateService())
                .StopAsync(TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }
}
