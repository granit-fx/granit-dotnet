using System.Diagnostics.Metrics;
using Granit.Privacy.DataDeletion;
using Granit.Privacy.DataDeletion.Events;
using Granit.Privacy.Diagnostics;
using Granit.Privacy.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Wolverine;
using Xunit;

namespace Granit.Privacy.Tests.DataDeletion;

public sealed class GdprDeletionSagaTests : IDisposable
{
    private readonly ServiceProvider _sp;
    private readonly PrivacyMetrics _metrics;
    private readonly IDeletionRequestTrackerWriter _tracker;
    private readonly TimeProvider _timeProvider;

    public GdprDeletionSagaTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        _metrics = new PrivacyMetrics(_sp.GetRequiredService<IMeterFactory>());
        _tracker = Substitute.For<IDeletionRequestTrackerWriter>();
        _timeProvider = Substitute.For<TimeProvider>();
        _timeProvider.GetUtcNow().Returns(DateTimeOffset.UtcNow);
    }

    public void Dispose() => _sp.Dispose();

    private static IOptions<GranitPrivacyOptions> DefaultOptions() =>
        Microsoft.Extensions.Options.Options.Create(new GranitPrivacyOptions());

    private static DeletionDeferredEto CreateDeferredEvent(
        Guid? requestId = null,
        Guid? userId = null,
        int graceDays = 30)
    {
        Guid id = requestId ?? Guid.NewGuid();
        Guid uid = userId ?? Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        return new DeletionDeferredEto(
            id, uid, "user@example.com", now, "Account closure",
            now.AddDays(graceDays));
    }

    // ── StartAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task StartAsync_InitializesState_FromEvent()
    {
        GdprDeletionSaga saga = new();
        IMessageContext context = Substitute.For<IMessageContext>();
        DeletionDeferredEto evt = CreateDeferredEvent();

        await saga.StartAsync(evt, DefaultOptions(), context, _tracker, _metrics);

        saga.Id.ShouldBe(evt.RequestId);
        saga.UserId.ShouldBe(evt.UserId);
        saga.Reason.ShouldBe("Account closure");
        saga.ScheduledDeletionAt.ShouldBe(evt.ScheduledDeletionAt);
        saga.ReminderSent.ShouldBeFalse();
    }

    [Fact]
    public async Task StartAsync_RecordsDeferredRequest_InTracker()
    {
        GdprDeletionSaga saga = new();
        IMessageContext context = Substitute.For<IMessageContext>();
        DeletionDeferredEto evt = CreateDeferredEvent();

        await saga.StartAsync(evt, DefaultOptions(), context, _tracker, _metrics);

        await _tracker.Received(1).RecordDeferredAsync(
            evt.RequestId, evt.UserId, evt.Reason, evt.RequestedAt, evt.ScheduledDeletionAt,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_SchedulesReminderAndDeadline()
    {
        GdprDeletionSaga saga = new();
        IMessageContext context = Substitute.For<IMessageContext>();
        DeletionDeferredEto evt = CreateDeferredEvent(graceDays: 30);

        await saga.StartAsync(evt, DefaultOptions(), context, _tracker, _metrics);

        // Two ScheduleAsync calls: reminder + deadline
        await context.Received(2).PublishAsync(
            Arg.Any<object>(),
            Arg.Any<DeliveryOptions>());
    }

    [Fact]
    public async Task StartAsync_SkipsReminder_WhenGracePeriodShorterThanReminderDays()
    {
        GdprDeletionSaga saga = new();
        IMessageContext context = Substitute.For<IMessageContext>();
        DeletionDeferredEto evt = CreateDeferredEvent(graceDays: 2); // < 3 days reminder

        await saga.StartAsync(evt, DefaultOptions(), context, _tracker, _metrics);

        // Only deadline, no reminder
        await context.Received(1).PublishAsync(
            Arg.Any<object>(),
            Arg.Any<DeliveryOptions>());
    }

    // ── Reminder ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReminderDueEvent_PublishesReminderEto()
    {
        GdprDeletionSaga saga = new();
        IMessageContext context = Substitute.For<IMessageContext>();
        DeletionDeferredEto startEvt = CreateDeferredEvent();
        await saga.StartAsync(startEvt, DefaultOptions(), context, _tracker, _metrics);

        DeletionReminderDueEto result = saga.Handle(
            new DeletionReminderDueEvent(startEvt.RequestId), _metrics);

        result.ShouldNotBeNull();
        result.RequestId.ShouldBe(startEvt.RequestId);
        result.UserId.ShouldBe(startEvt.UserId);
        result.ScheduledDeletionAt.ShouldBe(startEvt.ScheduledDeletionAt);
    }

    [Fact]
    public async Task Handle_ReminderDueEvent_SetsReminderSent()
    {
        GdprDeletionSaga saga = new();
        IMessageContext context = Substitute.For<IMessageContext>();
        await saga.StartAsync(CreateDeferredEvent(), DefaultOptions(), context, _tracker, _metrics);

        saga.Handle(new DeletionReminderDueEvent(saga.Id), _metrics);

        saga.ReminderSent.ShouldBeTrue();
    }

    // ── Deadline ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_DeadlineReached_PublishesDeletionAndExecutedEvents()
    {
        GdprDeletionSaga saga = new();
        IMessageContext context = Substitute.For<IMessageContext>();
        DeletionDeferredEto startEvt = CreateDeferredEvent();
        await saga.StartAsync(startEvt, DefaultOptions(), context, _tracker, _metrics);

        object[] results = await saga.HandleAsync(
            new DeletionDeadlineReachedEvent(startEvt.RequestId),
            _tracker, _metrics, _timeProvider);

        results.Length.ShouldBe(2);
        results[0].ShouldBeOfType<PersonalDataDeletionRequestedEto>();
        results[1].ShouldBeOfType<DeletionExecutedEto>();
    }

    [Fact]
    public async Task HandleAsync_DeadlineReached_MarksTrackerExecuted()
    {
        GdprDeletionSaga saga = new();
        IMessageContext context = Substitute.For<IMessageContext>();
        await saga.StartAsync(CreateDeferredEvent(), DefaultOptions(), context, _tracker, _metrics);

        await saga.HandleAsync(
            new DeletionDeadlineReachedEvent(saga.Id),
            _tracker, _metrics, _timeProvider);

        await _tracker.Received(1).MarkExecutedAsync(
            saga.Id, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    // ── Cancellation ─────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_Cancelled_MarksTrackerCancelled()
    {
        GdprDeletionSaga saga = new();
        IMessageContext context = Substitute.For<IMessageContext>();
        await saga.StartAsync(CreateDeferredEvent(), DefaultOptions(), context, _tracker, _metrics);

        DateTimeOffset cancelledAt = DateTimeOffset.UtcNow;
        await saga.HandleAsync(
            new DeletionCancelledEto(saga.Id, saga.UserId, cancelledAt),
            _tracker, _metrics);

        await _tracker.Received(1).MarkCancelledAsync(
            saga.Id, cancelledAt, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_Cancelled_DoesNotPublishDeletionEvent()
    {
        GdprDeletionSaga saga = new();
        IMessageContext context = Substitute.For<IMessageContext>();
        DeletionDeferredEto startEvt = CreateDeferredEvent();
        await saga.StartAsync(startEvt, DefaultOptions(), context, _tracker, _metrics);

        await saga.HandleAsync(
            new DeletionCancelledEto(saga.Id, saga.UserId, DateTimeOffset.UtcNow),
            _tracker, _metrics);

        await _tracker.DidNotReceive().MarkExecutedAsync(
            Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }
}
