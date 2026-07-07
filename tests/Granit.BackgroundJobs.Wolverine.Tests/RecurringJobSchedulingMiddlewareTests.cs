using System.Diagnostics.Metrics;
using Granit.BackgroundJobs.Diagnostics;
using Granit.BackgroundJobs.Domain;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Wolverine;
using Xunit;

namespace Granit.BackgroundJobs.Wolverine.Tests;

public sealed class RecurringJobSchedulingMiddlewareTests : IDisposable
{
    private readonly IBackgroundJobStoreReader _storeReader = Substitute.For<IBackgroundJobStoreReader>();
    private readonly IBackgroundJobStoreWriter _storeWriter = Substitute.For<IBackgroundJobStoreWriter>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ServiceProvider _serviceProvider;
    private readonly BackgroundJobsMetrics _metrics;
    private readonly ILogger<RecurringJobSchedulingMiddleware> _logger =
        Substitute.For<ILogger<RecurringJobSchedulingMiddleware>>();

    public RecurringJobSchedulingMiddlewareTests()
    {
        _serviceProvider = new ServiceCollection()
            .AddMetrics()
            .BuildServiceProvider();
        _metrics = new BackgroundJobsMetrics(_serviceProvider.GetRequiredService<IMeterFactory>());

        // Allow [LoggerMessage] generated code to execute both branches (IsEnabled check)
        _logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
    }

    public void Dispose() => _serviceProvider.Dispose();

    private RecurringJobSchedulingMiddleware MakeSut() =>
        new(_storeReader, _storeWriter, _clock, _metrics, _logger);

    private static BackgroundJobDefinition MakeJob(
        string name = "fake-daily-report",
        string cron = "0 8 * * *",
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
    // BeforeAsync
    // =========================================================================

    [Fact]
    public async Task BeforeAsync_DecoratedMessage_RecordsExecutionStart()
    {
        // Arrange
        DateTimeOffset now = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
        _clock.Now.Returns(now);
        Envelope envelope = new(new FakeDailyReportMessage());
        RecurringJobSchedulingMiddleware sut = MakeSut();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        // Act
        await sut.BeforeAsync(envelope, cancellationToken);

        // Assert
        await _storeWriter.Received(1).RecordExecutionStartAsync("fake-daily-report", now, cancellationToken);
    }

    [Fact]
    public async Task BeforeAsync_WithTriggeredByHeader_CallsSetTriggeredBy()
    {
        // Arrange
        _clock.Now.Returns(DateTimeOffset.UtcNow);
        Envelope envelope = new(new FakeDailyReportMessage());
        envelope.Headers[BackgroundJobHeaders.TriggeredBy] = "admin-user";
        RecurringJobSchedulingMiddleware sut = MakeSut();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        // Act
        await sut.BeforeAsync(envelope, cancellationToken);

        // Assert
        await _storeWriter.Received(1).SetTriggeredByAsync("fake-daily-report", "admin-user", cancellationToken);
    }

    [Fact]
    public async Task BeforeAsync_WithoutTriggeredByHeader_DoesNotCallSetTriggeredBy()
    {
        // Arrange — no X-Triggered-By header (normal scheduled execution)
        _clock.Now.Returns(DateTimeOffset.UtcNow);
        Envelope envelope = new(new FakeDailyReportMessage());
        RecurringJobSchedulingMiddleware sut = MakeSut();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        // Act
        await sut.BeforeAsync(envelope, cancellationToken);

        // Assert — header absent → SetTriggeredByAsync must not be called
        await _storeWriter.DidNotReceive().SetTriggeredByAsync(
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BeforeAsync_UndecoratedMessage_SkipsRecording()
    {
        // Arrange
        Envelope envelope = new(new UndecoratedMessage());
        RecurringJobSchedulingMiddleware sut = MakeSut();

        // Act
        await sut.BeforeAsync(envelope, TestContext.Current.CancellationToken);

        // Assert — store must not be called at all
        await _storeWriter.DidNotReceive()
            .RecordExecutionStartAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // AfterAsync
    // =========================================================================

    [Fact]
    public async Task AfterAsync_EnabledJob_SchedulesNextOccurrence()
    {
        // Arrange
        DateTimeOffset now = new(2026, 1, 15, 7, 0, 0, TimeSpan.Zero); // before 08:00
        _clock.Now.Returns(now);
        BackgroundJobDefinition job = MakeJob("fake-daily-report", "0 8 * * *");
        _storeReader.FindAsync("fake-daily-report", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<BackgroundJobDefinition?>(job));

        Envelope envelope = new(new FakeDailyReportMessage());
        IMessageContext context = Substitute.For<IMessageContext>();
        RecurringJobSchedulingMiddleware sut = MakeSut();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        // Act
        await sut.AfterAsync(envelope, context, cancellationToken);

        // Assert — next occurrence is today at 08:00 UTC
        DateTimeOffset expectedNext = new(2026, 1, 15, 8, 0, 0, TimeSpan.Zero);
        await _storeWriter.Received(1).RecordNextExecutionAsync(
            "fake-daily-report", expectedNext, cancellationToken);
    }

    [Fact]
    public async Task AfterAsync_PausedJob_SkipsRescheduling()
    {
        // Arrange
        _clock.Now.Returns(DateTimeOffset.UtcNow);
        BackgroundJobDefinition job = MakeJob(enabled: false);
        _storeReader.FindAsync("fake-daily-report", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<BackgroundJobDefinition?>(job));

        Envelope envelope = new(new FakeDailyReportMessage());
        IMessageContext context = Substitute.For<IMessageContext>();
        RecurringJobSchedulingMiddleware sut = MakeSut();

        // Act
        await sut.AfterAsync(envelope, context, TestContext.Current.CancellationToken);

        // Assert — paused job must not be rescheduled
        await _storeWriter.DidNotReceive()
            .RecordNextExecutionAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AfterAsync_UndecoratedMessage_SkipsAllProcessing()
    {
        // Arrange
        Envelope envelope = new(new UndecoratedMessage());
        IMessageContext context = Substitute.For<IMessageContext>();
        RecurringJobSchedulingMiddleware sut = MakeSut();

        // Act
        await sut.AfterAsync(envelope, context, TestContext.Current.CancellationToken);

        // Assert
        await _storeReader.DidNotReceive()
            .FindAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AfterAsync_NoCronNextOccurrence_LogsWarningAndSkips()
    {
        // Arrange — Feb 31 never exists, GetNextOccurrence returns null
        _clock.Now.Returns(new DateTimeOffset(2026, 2, 28, 8, 0, 0, TimeSpan.Zero));
        BackgroundJobDefinition job = MakeJob("fake-daily-report", "0 8 31 2 *");
        _storeReader.FindAsync("fake-daily-report", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<BackgroundJobDefinition?>(job));

        Envelope envelope = new(new FakeDailyReportMessage());
        IMessageContext context = Substitute.For<IMessageContext>();
        RecurringJobSchedulingMiddleware sut = MakeSut();

        // Act
        Func<Task> act = () =>
            sut.AfterAsync(envelope, context, TestContext.Current.CancellationToken);

        // Assert — must not throw, no next execution recorded
        await Should.NotThrowAsync(act);
        await _storeWriter.DidNotReceive()
            .RecordNextExecutionAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AfterAsync_JobNotFoundInStore_SkipsRescheduling()
    {
        // Arrange — store returns null (job was removed between BeforeAsync and AfterAsync)
        _clock.Now.Returns(DateTimeOffset.UtcNow);
        _storeReader.FindAsync("fake-daily-report", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<BackgroundJobDefinition?>(null));

        Envelope envelope = new(new FakeDailyReportMessage());
        IMessageContext context = Substitute.For<IMessageContext>();
        RecurringJobSchedulingMiddleware sut = MakeSut();

        // Act
        await sut.AfterAsync(envelope, context, TestContext.Current.CancellationToken);

        // Assert
        await _storeWriter.DidNotReceive()
            .RecordNextExecutionAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AfterAsync_ManualTrigger_DoesNotAdvanceTheChain()
    {
        // Arrange — manual trigger: the regular occurrence stays armed; rescheduling here
        // would permanently double the recurrence.
        _clock.Now.Returns(new DateTimeOffset(2026, 1, 15, 7, 0, 0, TimeSpan.Zero));
        BackgroundJobDefinition job = MakeJob("fake-daily-report", "0 8 * * *");
        _storeReader.FindAsync("fake-daily-report", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<BackgroundJobDefinition?>(job));

        Envelope envelope = new(new FakeDailyReportMessage());
        envelope.Headers[BackgroundJobHeaders.ManualTrigger] = "true";
        IMessageContext context = Substitute.For<IMessageContext>();
        RecurringJobSchedulingMiddleware sut = MakeSut();

        // Act
        await sut.AfterAsync(envelope, context, TestContext.Current.CancellationToken);

        // Assert
        await _storeWriter.DidNotReceive()
            .RecordNextExecutionAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AfterAsync_OccurrenceAlreadyArmed_DoesNotScheduleTwice()
    {
        // Arrange — the failure path already armed 08:00 before a retry succeeded.
        _clock.Now.Returns(new DateTimeOffset(2026, 1, 15, 7, 0, 0, TimeSpan.Zero));
        BackgroundJobDefinition job = MakeJob("fake-daily-report", "0 8 * * *");
        job.ScheduleNext(new DateTimeOffset(2026, 1, 15, 8, 0, 0, TimeSpan.Zero));
        _storeReader.FindAsync("fake-daily-report", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<BackgroundJobDefinition?>(job));

        Envelope envelope = new(new FakeDailyReportMessage());
        IMessageContext context = Substitute.For<IMessageContext>();
        RecurringJobSchedulingMiddleware sut = MakeSut();

        // Act
        await sut.AfterAsync(envelope, context, TestContext.Current.CancellationToken);

        // Assert
        await _storeWriter.DidNotReceive()
            .RecordNextExecutionAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AfterAsync_LocalClockBehindScheduledTime_ComputesNextFromScheduledTime()
    {
        // Arrange — cluster clock skew: this node's clock (07:59:58) lags behind the
        // occurrence being executed (08:00). The next occurrence must be tomorrow 08:00,
        // not today's 08:00 (which would then be skipped as "already armed" — dead chain).
        _clock.Now.Returns(new DateTimeOffset(2026, 1, 15, 7, 59, 58, TimeSpan.Zero));
        BackgroundJobDefinition job = MakeJob("fake-daily-report", "0 8 * * *");
        job.ScheduleNext(new DateTimeOffset(2026, 1, 15, 8, 0, 0, TimeSpan.Zero));
        _storeReader.FindAsync("fake-daily-report", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<BackgroundJobDefinition?>(job));

        Envelope envelope = new(new FakeDailyReportMessage())
        {
            ScheduledTime = new DateTimeOffset(2026, 1, 15, 8, 0, 0, TimeSpan.Zero),
        };
        IMessageContext context = Substitute.For<IMessageContext>();
        RecurringJobSchedulingMiddleware sut = MakeSut();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        // Act
        await sut.AfterAsync(envelope, context, cancellationToken);

        // Assert
        DateTimeOffset expectedNext = new(2026, 1, 16, 8, 0, 0, TimeSpan.Zero);
        await _storeWriter.Received(1).RecordNextExecutionAsync(
            "fake-daily-report", expectedNext, cancellationToken);
    }

    // =========================================================================
    // OnExceptionAsync — failure accounting + chain continuation
    // =========================================================================

    [Fact]
    public async Task OnExceptionAsync_DecoratedMessage_RecordsFailure()
    {
        // Arrange
        _clock.Now.Returns(new DateTimeOffset(2026, 1, 15, 7, 0, 0, TimeSpan.Zero));
        BackgroundJobDefinition job = MakeJob("fake-daily-report", "0 8 * * *");
        _storeReader.FindAsync("fake-daily-report", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<BackgroundJobDefinition?>(job));

        Envelope envelope = new(new FakeDailyReportMessage());
        IMessageBus bus = Substitute.For<IMessageBus>();
        RecurringJobSchedulingMiddleware sut = MakeSut();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        // Act
        await sut.OnExceptionAsync(
            new InvalidOperationException("boom"), envelope, bus, cancellationToken);

        // Assert
        await _storeWriter.Received(1).RecordExecutionFailureAsync(
            "fake-daily-report", "boom", cancellationToken);
    }

    [Fact]
    public async Task OnExceptionAsync_EnabledJob_ReArmsNextOccurrenceViaBus()
    {
        // Arrange — a failing run must not stop the recurring chain. The next message is
        // scheduled via IMessageBus because the envelope transaction is rolling back.
        _clock.Now.Returns(new DateTimeOffset(2026, 1, 15, 7, 0, 0, TimeSpan.Zero));
        BackgroundJobDefinition job = MakeJob("fake-daily-report", "0 8 * * *");
        _storeReader.FindAsync("fake-daily-report", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<BackgroundJobDefinition?>(job));

        Envelope envelope = new(new FakeDailyReportMessage());
        IMessageBus bus = Substitute.For<IMessageBus>();
        RecurringJobSchedulingMiddleware sut = MakeSut();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        // Act
        await sut.OnExceptionAsync(
            new InvalidOperationException("boom"), envelope, bus, cancellationToken);

        // Assert — ScheduleAsync is an extension that calls PublishAsync with DeliveryOptions.ScheduledTime
        DateTimeOffset expectedNext = new(2026, 1, 15, 8, 0, 0, TimeSpan.Zero);
        await bus.Received(1).PublishAsync(
            Arg.Any<object>(), Arg.Is<DeliveryOptions>(o => o.ScheduledTime == expectedNext));
        await _storeWriter.Received(1).RecordNextExecutionAsync(
            "fake-daily-report", expectedNext, cancellationToken);
    }

    [Fact]
    public async Task OnExceptionAsync_ThenRetrySucceeds_AfterAsyncDoesNotDoubleSchedule()
    {
        // Arrange — attempt 1 fails and arms 08:00; the retry succeeds and AfterAsync
        // must see the already-armed occurrence and skip.
        _clock.Now.Returns(new DateTimeOffset(2026, 1, 15, 7, 0, 0, TimeSpan.Zero));
        BackgroundJobDefinition job = MakeJob("fake-daily-report", "0 8 * * *");
        _storeReader.FindAsync("fake-daily-report", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<BackgroundJobDefinition?>(job));
        _storeWriter
            .When(w => w.RecordNextExecutionAsync(
                "fake-daily-report", Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()))
            .Do(call => job.ScheduleNext(call.Arg<DateTimeOffset>()));

        Envelope envelope = new(new FakeDailyReportMessage());
        IMessageBus bus = Substitute.For<IMessageBus>();
        IMessageContext context = Substitute.For<IMessageContext>();
        RecurringJobSchedulingMiddleware sut = MakeSut();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        // Act
        await sut.OnExceptionAsync(
            new InvalidOperationException("boom"), envelope, bus, cancellationToken);
        await sut.AfterAsync(envelope, context, cancellationToken);

        // Assert — one arm total: the failure path scheduled it, the success path skipped.
        await bus.Received(1).PublishAsync(Arg.Any<object>(), Arg.Any<DeliveryOptions?>());
        await context.DidNotReceive().PublishAsync(Arg.Any<object>(), Arg.Any<DeliveryOptions?>());
    }

    [Fact]
    public async Task OnExceptionAsync_ManualTrigger_RecordsFailureButDoesNotReArm()
    {
        // Arrange
        _clock.Now.Returns(new DateTimeOffset(2026, 1, 15, 7, 0, 0, TimeSpan.Zero));
        BackgroundJobDefinition job = MakeJob("fake-daily-report", "0 8 * * *");
        _storeReader.FindAsync("fake-daily-report", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<BackgroundJobDefinition?>(job));

        Envelope envelope = new(new FakeDailyReportMessage());
        envelope.Headers[BackgroundJobHeaders.ManualTrigger] = "true";
        IMessageBus bus = Substitute.For<IMessageBus>();
        RecurringJobSchedulingMiddleware sut = MakeSut();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        // Act
        await sut.OnExceptionAsync(
            new InvalidOperationException("boom"), envelope, bus, cancellationToken);

        // Assert
        await _storeWriter.Received(1).RecordExecutionFailureAsync(
            "fake-daily-report", "boom", cancellationToken);
        await bus.DidNotReceive().PublishAsync(Arg.Any<object>(), Arg.Any<DeliveryOptions?>());
    }

    [Fact]
    public async Task OnExceptionAsync_UndecoratedMessage_SkipsAllProcessing()
    {
        // Arrange
        Envelope envelope = new(new UndecoratedMessage());
        IMessageBus bus = Substitute.For<IMessageBus>();
        RecurringJobSchedulingMiddleware sut = MakeSut();

        // Act
        await sut.OnExceptionAsync(
            new InvalidOperationException("boom"), envelope, bus,
            TestContext.Current.CancellationToken);

        // Assert
        await _storeWriter.DidNotReceive().RecordExecutionFailureAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AfterAsync_SixFieldCron_ParsesAndSchedules()
    {
        // Arrange — 6-field cron (with seconds): every minute at second 0
        DateTimeOffset now = new(2026, 1, 15, 10, 0, 30, TimeSpan.Zero);
        _clock.Now.Returns(now);
        BackgroundJobDefinition job = MakeJob("fake-daily-report", "0 * * * * *"); // every minute, second 0
        _storeReader.FindAsync("fake-daily-report", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<BackgroundJobDefinition?>(job));

        Envelope envelope = new(new FakeDailyReportMessage());
        IMessageContext context = Substitute.For<IMessageContext>();
        RecurringJobSchedulingMiddleware sut = MakeSut();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        // Act
        await sut.AfterAsync(envelope, context, cancellationToken);

        // Assert — next occurrence: next minute at second 0
        DateTimeOffset expectedNext = new(2026, 1, 15, 10, 1, 0, TimeSpan.Zero);
        await _storeWriter.Received(1)
            .RecordNextExecutionAsync("fake-daily-report", expectedNext, cancellationToken);
    }
}

// Test fixtures — shared with RecurringJobDiscoveryTests in Granit.BackgroundJobs.Tests

[RecurringJob("0 8 * * *", "fake-daily-report")]
public sealed class FakeDailyReportMessage : IBackgroundJob;

/// <summary>A message without [RecurringJob] — middleware must skip it.</summary>
public sealed class UndecoratedMessage;
