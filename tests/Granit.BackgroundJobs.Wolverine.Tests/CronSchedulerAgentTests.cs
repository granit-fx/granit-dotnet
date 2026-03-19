using Granit.BackgroundJobs.Abstractions;
using Granit.BackgroundJobs.Domain;
using Granit.BackgroundJobs.Wolverine.Internal;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.BackgroundJobs.Wolverine.Tests;

public sealed class CronSchedulerAgentTests
{
    // =========================================================================
    // Test infrastructure
    // =========================================================================

    private readonly IBackgroundJobStoreReader _storeReader = Substitute.For<IBackgroundJobStoreReader>();
    private readonly IBackgroundJobStoreWriter _storeWriter = Substitute.For<IBackgroundJobStoreWriter>();
    private readonly IBackgroundJobDispatcher _dispatcher = Substitute.For<IBackgroundJobDispatcher>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IServiceScopeFactory _scopeFactory;

    public CronSchedulerAgentTests()
    {
        _clock.Now.Returns(new DateTimeOffset(2026, 2, 20, 8, 0, 0, TimeSpan.Zero));

        IServiceScope scope = Substitute.For<IServiceScope>();
        IServiceProvider sp = Substitute.For<IServiceProvider>();
        sp.GetService(typeof(IBackgroundJobDispatcher)).Returns(_dispatcher);
        scope.ServiceProvider.Returns(sp);
        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _scopeFactory.CreateScope().Returns(scope);
    }

    private CronSchedulerAgent CreateAgent() =>
        new(_storeReader, _storeWriter, _scopeFactory, _clock, NullLogger<CronSchedulerAgent>.Instance);

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
    private sealed class FakeJobMessage;

    // =========================================================================
    // Scenario: job with null NextExecutionAt -> schedules first occurrence
    // =========================================================================

    [Fact]
    public async Task StartAsync_JobWithNoNextExecution_SchedulesFirstOccurrence()
    {
        BackgroundJobDefinition job = MakeJob("daily-sync", cron: "0 9 * * *");
        _storeReader.GetEnabledJobsAsync(Arg.Any<CancellationToken>())
            .Returns([job]);

        await ((Microsoft.Extensions.Hosting.IHostedService)CreateAgent())
            .StartAsync(TestContext.Current.CancellationToken);

        await _dispatcher.Received(1).ScheduleAsync(
            Arg.Any<object>(),
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
    public async Task StartAsync_JobAlreadyScheduledInFuture_DoesNotReschedule()
    {
        DateTimeOffset future = _clock.Now.AddHours(2);
        BackgroundJobDefinition job = MakeJob("daily-sync", nextExecutionAt: future);
        _storeReader.GetEnabledJobsAsync(Arg.Any<CancellationToken>())
            .Returns([job]);

        await ((Microsoft.Extensions.Hosting.IHostedService)CreateAgent())
            .StartAsync(TestContext.Current.CancellationToken);

        await _dispatcher.DidNotReceive().ScheduleAsync(
            Arg.Any<object>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await _storeWriter.DidNotReceive().RecordNextExecutionAsync(
            Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Scenario: NextExecutionAt is in the past -> reschedules
    // =========================================================================

    [Fact]
    public async Task StartAsync_JobWithPastNextExecution_Reschedules()
    {
        DateTimeOffset past = _clock.Now.AddHours(-2);
        BackgroundJobDefinition job = MakeJob("daily-sync", cron: "0 9 * * *", nextExecutionAt: past);
        _storeReader.GetEnabledJobsAsync(Arg.Any<CancellationToken>())
            .Returns([job]);

        await ((Microsoft.Extensions.Hosting.IHostedService)CreateAgent())
            .StartAsync(TestContext.Current.CancellationToken);

        await _dispatcher.Received(1).ScheduleAsync(
            Arg.Any<object>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Scenario: paused job -> not initialized (filtered by GetEnabledJobsAsync)
    // =========================================================================

    [Fact]
    public async Task StartAsync_NoEnabledJobs_DoesNotScheduleAnything()
    {
        _storeReader.GetEnabledJobsAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<BackgroundJobDefinition>());

        await ((Microsoft.Extensions.Hosting.IHostedService)CreateAgent())
            .StartAsync(TestContext.Current.CancellationToken);

        await _dispatcher.DidNotReceive().ScheduleAsync(
            Arg.Any<object>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Scenario: idempotence -> two startAsync calls don't duplicate
    // =========================================================================

    [Fact]
    public async Task StartAsync_CalledTwice_SchedulesOnlyOnce()
    {
        BackgroundJobDefinition job = MakeJob("daily-sync");
        _storeReader.GetEnabledJobsAsync(Arg.Any<CancellationToken>())
            .Returns([job]);

        Microsoft.Extensions.Hosting.IHostedService agent = CreateAgent();

        await agent.StartAsync(TestContext.Current.CancellationToken);

        // Simulate that RecordNextExecutionAsync updated NextExecutionAt
        job.ScheduleNext(_clock.Now.AddHours(1));
        _storeReader.GetEnabledJobsAsync(Arg.Any<CancellationToken>())
            .Returns([job]);

        await agent.StartAsync(TestContext.Current.CancellationToken);

        await _dispatcher.Received(1).ScheduleAsync(
            Arg.Any<object>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Scenario: invalid cron -> skip without scheduling
    // =========================================================================

    [Fact]
    public async Task StartAsync_JobWithInvalidCron_SkipsWithoutScheduling()
    {
        BackgroundJobDefinition job = MakeJob("bad-cron", cron: "NOT_A_CRON");
        _storeReader.GetEnabledJobsAsync(Arg.Any<CancellationToken>()).Returns([job]);

        await ((Microsoft.Extensions.Hosting.IHostedService)CreateAgent())
            .StartAsync(TestContext.Current.CancellationToken);

        await _dispatcher.DidNotReceive().ScheduleAsync(
            Arg.Any<object>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await _storeWriter.DidNotReceive().RecordNextExecutionAsync(
            Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Scenario: 6-field cron (with seconds) -> schedules using IncludeSeconds parse
    // =========================================================================

    [Fact]
    public async Task StartAsync_JobWithSixFieldCron_SchedulesSuccessfully()
    {
        // "*/30 * * * * *" = every 30 seconds — 6-field cron parsed with IncludeSeconds
        BackgroundJobDefinition job = MakeJob("seconds-job", cron: "*/30 * * * * *");
        _storeReader.GetEnabledJobsAsync(Arg.Any<CancellationToken>()).Returns([job]);

        await ((Microsoft.Extensions.Hosting.IHostedService)CreateAgent())
            .StartAsync(TestContext.Current.CancellationToken);

        await _dispatcher.Received(1).ScheduleAsync(
            Arg.Any<object>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // StopAsync — no-op
    // =========================================================================

    [Fact]
    public async Task StopAsync_DoesNothing()
    {
        Func<Task> act = () =>
            ((Microsoft.Extensions.Hosting.IHostedService)CreateAgent())
                .StopAsync(TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }
}
