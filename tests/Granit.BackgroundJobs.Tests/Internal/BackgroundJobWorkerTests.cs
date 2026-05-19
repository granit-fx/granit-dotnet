using System.Threading.Channels;
using Granit.BackgroundJobs.Abstractions;
using Granit.BackgroundJobs.Domain;
using Granit.BackgroundJobs.Internal;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.BackgroundJobs.Tests.Internal;

public sealed class BackgroundJobWorkerTests
{
    private readonly Channel<BackgroundJobEnvelope> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IBackgroundJobStoreWriter _storeWriter = Substitute.For<IBackgroundJobStoreWriter>();
    private readonly IBackgroundJobStoreReader _storeReader = Substitute.For<IBackgroundJobStoreReader>();
    private readonly IBackgroundJobDispatcher _dispatcher = Substitute.For<IBackgroundJobDispatcher>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly FakeJobHandler _fakeHandler = new();
    private readonly DateTimeOffset _fixedTime = new(2026, 3, 15, 10, 0, 0, TimeSpan.Zero);

    public BackgroundJobWorkerTests()
    {
        _channel = Channel.CreateUnbounded<BackgroundJobEnvelope>();
        _clock.Now.Returns(_fixedTime);

        IServiceScope scope = Substitute.For<IServiceScope>();
        IServiceProvider sp = Substitute.For<IServiceProvider>();

        sp.GetService(typeof(IBackgroundJobStoreWriter)).Returns(_storeWriter);
        sp.GetService(typeof(IBackgroundJobStoreReader)).Returns(_storeReader);
        sp.GetService(typeof(IBackgroundJobDispatcher)).Returns(_dispatcher);
        sp.GetService(typeof(FakeJobHandler)).Returns(_fakeHandler);
        sp.GetService(typeof(RecurringFakeJobHandler)).Returns(new RecurringFakeJobHandler());
        sp.GetService(typeof(FailingJobHandler)).Returns(new FailingJobHandler());
        sp.GetService(typeof(NonRecurringFailingJobHandler)).Returns(new NonRecurringFailingJobHandler());

        scope.ServiceProvider.Returns(sp);
        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _scopeFactory.CreateAsyncScope().Returns(new AsyncServiceScope(scope));
    }

    private BackgroundJobWorker CreateWorker() =>
        new(_channel, _scopeFactory, _clock, NullLogger<BackgroundJobWorker>.Instance);

    /// <summary>
    /// Writes envelope to channel, starts the worker, and waits for processing.
    /// </summary>
    private async Task RunWorkerWithEnvelopeAsync(BackgroundJobEnvelope envelope)
    {
        await _channel.Writer.WriteAsync(envelope);
        _channel.Writer.Complete();

        BackgroundJobWorker worker = CreateWorker();
        await worker.StartAsync(CancellationToken.None);

        // Wait for the background service to finish reading the completed channel.
        await Task.Delay(200);
        await worker.StopAsync(CancellationToken.None);
    }

    // =========================================================================
    // Non-recurring job — no RecurringJobAttribute
    // =========================================================================

    [Fact]
    public async Task ProcessAsync_NonRecurringJob_InvokesHandler()
    {
        var envelope = new BackgroundJobEnvelope(new FakeJob());

        await RunWorkerWithEnvelopeAsync(envelope);

        _fakeHandler.HandleCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task ProcessAsync_NonRecurringJob_DoesNotCallStoreWriter()
    {
        var envelope = new BackgroundJobEnvelope(new FakeJob());

        await RunWorkerWithEnvelopeAsync(envelope);

        await _storeWriter.DidNotReceive().RecordExecutionStartAsync(
            Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Recurring job — with RecurringJobAttribute
    // =========================================================================

    [Fact]
    public async Task ProcessAsync_RecurringJob_RecordsExecutionStart()
    {
        var envelope = new BackgroundJobEnvelope(new RecurringFakeJob());

        await RunWorkerWithEnvelopeAsync(envelope);

        await _storeWriter.Received(1).RecordExecutionStartAsync(
            "test-recurring-job", _fixedTime, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_RecurringJobWithTriggeredByHeader_SetsTriggeredBy()
    {
        var headers = new Dictionary<string, string>
        {
            [BackgroundJobHeaders.TriggeredBy] = "admin-user-42",
        };
        var envelope = new BackgroundJobEnvelope(new RecurringFakeJob(), headers);

        await RunWorkerWithEnvelopeAsync(envelope);

        await _storeWriter.Received(1).SetTriggeredByAsync(
            "test-recurring-job", "admin-user-42", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_RecurringJobWithNoTriggeredByHeader_DoesNotSetTriggeredBy()
    {
        var envelope = new BackgroundJobEnvelope(new RecurringFakeJob());

        await RunWorkerWithEnvelopeAsync(envelope);

        await _storeWriter.DidNotReceive().SetTriggeredByAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_RecurringJobWithEmptyTriggeredBy_DoesNotSetTriggeredBy()
    {
        var headers = new Dictionary<string, string>
        {
            [BackgroundJobHeaders.TriggeredBy] = "",
        };
        var envelope = new BackgroundJobEnvelope(new RecurringFakeJob(), headers);

        await RunWorkerWithEnvelopeAsync(envelope);

        await _storeWriter.DidNotReceive().SetTriggeredByAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Rescheduling on success
    // =========================================================================

    [Fact]
    public async Task ProcessAsync_RecurringJobSuccess_ReschedulesWhenEnabled()
    {
        var jobDef = BackgroundJobDefinition.Create(
            Guid.NewGuid(), "test-recurring-job", "0 * * * *", typeof(RecurringFakeJob).AssemblyQualifiedName!);

        _storeReader.FindAsync("test-recurring-job", Arg.Any<CancellationToken>())
            .Returns(jobDef);

        var envelope = new BackgroundJobEnvelope(new RecurringFakeJob());

        await RunWorkerWithEnvelopeAsync(envelope);

        await _dispatcher.Received(1).ScheduleAsync(
            Arg.Any<RecurringFakeJob>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await _storeWriter.Received(1).RecordNextExecutionAsync(
            "test-recurring-job", Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_RecurringJobSuccess_DisabledJob_DoesNotReschedule()
    {
        var jobDef = BackgroundJobDefinition.Create(
            Guid.NewGuid(), "test-recurring-job", "0 * * * *", typeof(RecurringFakeJob).AssemblyQualifiedName!);
        jobDef.Pause();

        _storeReader.FindAsync("test-recurring-job", Arg.Any<CancellationToken>())
            .Returns(jobDef);

        var envelope = new BackgroundJobEnvelope(new RecurringFakeJob());

        await RunWorkerWithEnvelopeAsync(envelope);

        await _dispatcher.DidNotReceive().ScheduleAsync(
            Arg.Any<object>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_RecurringJobSuccess_JobNotFound_DoesNotReschedule()
    {
        _storeReader.FindAsync("test-recurring-job", Arg.Any<CancellationToken>())
            .Returns((BackgroundJobDefinition?)null);

        var envelope = new BackgroundJobEnvelope(new RecurringFakeJob());

        await RunWorkerWithEnvelopeAsync(envelope);

        await _dispatcher.DidNotReceive().ScheduleAsync(
            Arg.Any<object>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Handler failure
    // =========================================================================

    [Fact]
    public async Task ProcessAsync_RecurringJobHandlerFails_RecordsFailure()
    {
        var envelope = new BackgroundJobEnvelope(new FailingJob());

        await RunWorkerWithEnvelopeAsync(envelope);

        await _storeWriter.Received(1).RecordExecutionFailureAsync(
            "test-failing-job", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_RecurringJobHandlerFails_DoesNotReschedule()
    {
        var envelope = new BackgroundJobEnvelope(new FailingJob());

        await RunWorkerWithEnvelopeAsync(envelope);

        await _dispatcher.DidNotReceive().ScheduleAsync(
            Arg.Any<object>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_NonRecurringJobHandlerFails_DoesNotRecordFailure()
    {
        // FakeJob has no [RecurringJob] attribute, so failure should not call RecordExecutionFailureAsync.
        // We need a handler that fails for FakeJob — reuse FailingJob which does have the attribute.
        // Instead, test that a non-recurring failure is caught at the outer level (no store call).
        var envelope = new BackgroundJobEnvelope(new NonRecurringFailingJob());

        await RunWorkerWithEnvelopeAsync(envelope);

        await _storeWriter.DidNotReceive().RecordExecutionFailureAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Cron expression parsing
    // =========================================================================

    [Fact]
    public async Task ProcessAsync_SixFieldCron_ReschedulesSuccessfully()
    {
        var jobDef = BackgroundJobDefinition.Create(
            Guid.NewGuid(), "test-recurring-job", "*/30 * * * * *", typeof(RecurringFakeJob).AssemblyQualifiedName!);

        _storeReader.FindAsync("test-recurring-job", Arg.Any<CancellationToken>())
            .Returns(jobDef);

        var envelope = new BackgroundJobEnvelope(new RecurringFakeJob());

        await RunWorkerWithEnvelopeAsync(envelope);

        await _dispatcher.Received(1).ScheduleAsync(
            Arg.Any<RecurringFakeJob>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_InvalidCron_DoesNotReschedule()
    {
        var jobDef = BackgroundJobDefinition.Create(
            Guid.NewGuid(), "test-recurring-job", "NOT_A_CRON", typeof(RecurringFakeJob).AssemblyQualifiedName!);

        _storeReader.FindAsync("test-recurring-job", Arg.Any<CancellationToken>())
            .Returns(jobDef);

        var envelope = new BackgroundJobEnvelope(new RecurringFakeJob());

        await RunWorkerWithEnvelopeAsync(envelope);

        await _dispatcher.DidNotReceive().ScheduleAsync(
            Arg.Any<object>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Handler resolution — missing handler
    // =========================================================================

    [Fact]
    public async Task ProcessAsync_NoHandlerInAssembly_LogsError()
    {
        // OrphanJob has no matching OrphanJobHandler in its assembly.
        var envelope = new BackgroundJobEnvelope(new OrphanJob());

        // Should not throw — the outer catch logs the error.
        await Should.NotThrowAsync(() => RunWorkerWithEnvelopeAsync(envelope));
    }

    // =========================================================================
    // Channel completion — graceful shutdown
    // =========================================================================

    [Fact]
    public async Task ExecuteAsync_ChannelCompleted_ExitsGracefully()
    {
        _channel.Writer.Complete();

        BackgroundJobWorker worker = CreateWorker();
        await Should.NotThrowAsync(async () =>
        {
            await worker.StartAsync(TestContext.Current.CancellationToken);
            await Task.Delay(100, TestContext.Current.CancellationToken);
            await worker.StopAsync(TestContext.Current.CancellationToken);
        });
    }

    // =========================================================================
    // Test doubles
    // =========================================================================

    public sealed class FakeJob : IBackgroundJob;

    [RecurringJob("0 * * * *", "test-recurring-job")]
    public sealed class RecurringFakeJob : IBackgroundJob;

    [RecurringJob("0 * * * *", "test-failing-job")]
    public sealed class FailingJob : IBackgroundJob;

    /// <summary>
    /// A non-recurring job whose handler fails. No <see cref="RecurringJobAttribute"/>.
    /// </summary>
    public sealed class NonRecurringFailingJob : IBackgroundJob;

    /// <summary>
    /// A job with no corresponding handler in its assembly.
    /// </summary>
    public sealed class OrphanJob : IBackgroundJob;

    public sealed class FakeJobHandler
    {
        public int HandleCallCount { get; private set; }

        public Task HandleAsync(FakeJob job, CancellationToken cancellationToken)
        {
            HandleCallCount++;
            return Task.CompletedTask;
        }
    }

    public sealed class RecurringFakeJobHandler
    {
        public static Task HandleAsync(RecurringFakeJob job, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    public sealed class FailingJobHandler
    {
        public static Task HandleAsync(FailingJob job, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Simulated handler failure");
    }

    public sealed class NonRecurringFailingJobHandler
    {
        public static Task HandleAsync(NonRecurringFailingJob job, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Non-recurring failure");
    }
}
