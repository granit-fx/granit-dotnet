using System.Diagnostics.Metrics;
using Granit.Privacy.DataDeletion;
using Granit.Privacy.DataDeletion.Events;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Internal;
using Granit.Privacy.Diagnostics;
using Granit.Privacy.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Wolverine;
using Xunit;

namespace Granit.Privacy.Tests.DataDeletion;

public sealed class PersonalDataDeletionSagaTests : IDisposable
{
    private readonly ServiceProvider _sp;
    private readonly PrivacyMetrics _metrics;
    private readonly IDeletionRequestTrackerWriter _tracker;
    private readonly TimeProvider _timeProvider;

    public PersonalDataDeletionSagaTests()
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

    private static DataProviderRegistry Registry(params string[] providers)
    {
        DataProviderRegistry registry = new();
        foreach (string name in providers)
        {
            registry.Register(name);
        }

        return registry;
    }

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
            now.AddDays(graceDays), "EU_GDPR");
    }

    private async Task<PersonalDataDeletionSaga> StartedSagaAsync(DeletionDeferredEto startEvt)
    {
        PersonalDataDeletionSaga saga = new();
        IMessageContext context = Substitute.For<IMessageContext>();
        await saga.Start(startEvt, DefaultOptions(), context, _tracker, _metrics);
        return saga;
    }

    private Task<object[]> ReachDeadlineAsync(
        PersonalDataDeletionSaga saga, IDataProviderRegistry registry) =>
        saga.Handle(
            new DeletionDeadlineReachedEvent(saga.Id),
            _tracker,
            registry,
            DefaultOptions(),
            Substitute.For<IMessageContext>(),
            _metrics,
            _timeProvider);

    // ── StartAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task StartAsync_InitializesState_FromEvent()
    {
        DeletionDeferredEto evt = CreateDeferredEvent();

        PersonalDataDeletionSaga saga = await StartedSagaAsync(evt);

        saga.Id.ShouldBe(evt.RequestId);
        saga.UserId.ShouldBe(evt.UserId);
        saga.Reason.ShouldBe("Account closure");
        saga.ScheduledDeletionAt.ShouldBe(evt.ScheduledDeletionAt);
        saga.ReminderSent.ShouldBeFalse();
    }

    [Fact]
    public async Task StartAsync_RecordsDeferredRequest_InTracker()
    {
        DeletionDeferredEto evt = CreateDeferredEvent();

        await StartedSagaAsync(evt);

        await _tracker.Received(1).RecordDeferredAsync(
            evt.RequestId, evt.UserId, evt.Reason, evt.RequestedAt, evt.ScheduledDeletionAt,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_SchedulesReminderAndDeadline()
    {
        PersonalDataDeletionSaga saga = new();
        IMessageContext context = Substitute.For<IMessageContext>();
        DeletionDeferredEto evt = CreateDeferredEvent(graceDays: 30);

        await saga.Start(evt, DefaultOptions(), context, _tracker, _metrics);

        // Two ScheduleAsync calls: reminder + deadline
        await context.Received(2).PublishAsync(
            Arg.Any<object>(),
            Arg.Any<DeliveryOptions>());
    }

    [Fact]
    public async Task StartAsync_SkipsReminder_WhenGracePeriodShorterThanReminderDays()
    {
        PersonalDataDeletionSaga saga = new();
        IMessageContext context = Substitute.For<IMessageContext>();
        DeletionDeferredEto evt = CreateDeferredEvent(graceDays: 2); // < 3 days reminder

        await saga.Start(evt, DefaultOptions(), context, _tracker, _metrics);

        // Only deadline, no reminder
        await context.Received(1).PublishAsync(
            Arg.Any<object>(),
            Arg.Any<DeliveryOptions>());
    }

    // ── Reminder ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReminderDueEvent_PublishesReminderEto()
    {
        DeletionDeferredEto startEvt = CreateDeferredEvent();
        PersonalDataDeletionSaga saga = await StartedSagaAsync(startEvt);

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
        PersonalDataDeletionSaga saga = await StartedSagaAsync(CreateDeferredEvent());

        saga.Handle(new DeletionReminderDueEvent(saga.Id), _metrics);

        saga.ReminderSent.ShouldBeTrue();
    }

    // ── Deadline / fan-out ─────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_DeadlineReached_PublishesDeletionAndExecutedEvents()
    {
        PersonalDataDeletionSaga saga = await StartedSagaAsync(CreateDeferredEvent());

        object[] results = await ReachDeadlineAsync(saga, Registry("identity", "indexing"));

        results.Length.ShouldBe(2);
        results[0].ShouldBeOfType<PersonalDataDeletionRequestedEto>();
        results[1].ShouldBeOfType<DeletionExecutedEto>();
    }

    [Fact]
    public async Task HandleAsync_DeadlineReached_WithProviders_MarksExecuting_NotExecuted()
    {
        PersonalDataDeletionSaga saga = await StartedSagaAsync(CreateDeferredEvent());

        await ReachDeadlineAsync(saga, Registry("identity", "indexing"));

        // Fan-out started but no provider acknowledged yet: the tracker must NOT be told the
        // deletion is Executed — that is the whole point of the fan-in (GDPR Art. 17 provability).
        await _tracker.DidNotReceive().MarkExecutedAsync(
            Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await _tracker.Received(1).MarkExecutingAsync(saga.Id, Arg.Any<CancellationToken>());
        saga.PendingProviders.ShouldBe(["identity", "indexing"], ignoreOrder: true);
        saga.ExpectedProviderCount.ShouldBe(2);
    }

    [Fact]
    public async Task HandleAsync_DeadlineReached_SchedulesAcknowledgementTimeout()
    {
        PersonalDataDeletionSaga saga = await StartedSagaAsync(CreateDeferredEvent());
        IMessageContext context = Substitute.For<IMessageContext>();

        await saga.Handle(
            new DeletionDeadlineReachedEvent(saga.Id),
            _tracker, Registry("identity"), DefaultOptions(), context, _metrics, _timeProvider);

        // ScheduleAsync is an extension method calling PublishAsync with DeliveryOptions;
        // NSubstitute cannot intercept the extension, so we verify the underlying call.
        await context.Received(1).PublishAsync(
            Arg.Any<DeletionAcknowledgementTimedOutEvent>(),
            Arg.Any<DeliveryOptions>());
    }

    [Fact]
    public async Task HandleAsync_DeadlineReached_NoProviders_MarksExecutedImmediately()
    {
        PersonalDataDeletionSaga saga = await StartedSagaAsync(CreateDeferredEvent());

        await ReachDeadlineAsync(saga, Registry());

        // Zero-provider fast path mirrors the export saga: nothing to fan out to, so the
        // request is provably complete the moment the deadline is reached.
        await _tracker.Received(1).MarkExecutedAsync(
            saga.Id, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await _tracker.DidNotReceive().MarkExecutingAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // ── Fan-in: acknowledgements ───────────────────────────────────────────────

    [Fact]
    public async Task Handle_AllProvidersAcknowledge_MarksExecuted()
    {
        PersonalDataDeletionSaga saga = await StartedSagaAsync(CreateDeferredEvent());
        await ReachDeadlineAsync(saga, Registry("identity", "indexing"));

        await AcknowledgeAsync(saga, "identity");
        // First ack does NOT complete: one provider still outstanding.
        await _tracker.DidNotReceive().MarkExecutedAsync(
            Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());

        await AcknowledgeAsync(saga, "indexing");

        // Last ack drains PendingProviders → Executed.
        saga.PendingProviders.ShouldBeEmpty();
        await _tracker.Received(1).MarkExecutedAsync(
            saga.Id, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await _tracker.DidNotReceive().MarkPartiallyExecutedAsync(
            Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DuplicateAcknowledge_IsIdempotent()
    {
        PersonalDataDeletionSaga saga = await StartedSagaAsync(CreateDeferredEvent());
        await ReachDeadlineAsync(saga, Registry("identity", "indexing"));

        // Wolverine at-least-once: "identity" acknowledges twice. The duplicate must not
        // spuriously drain "indexing" or complete the saga early.
        await AcknowledgeAsync(saga, "identity");
        await AcknowledgeAsync(saga, "identity");

        saga.PendingProviders.ShouldBe(["indexing"]);
        await _tracker.DidNotReceive().MarkExecutedAsync(
            Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());

        // The genuinely-outstanding provider then acknowledges once → Executed.
        await AcknowledgeAsync(saga, "indexing");
        await _tracker.Received(1).MarkExecutedAsync(
            saga.Id, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownProviderAcknowledge_DoesNotComplete()
    {
        PersonalDataDeletionSaga saga = await StartedSagaAsync(CreateDeferredEvent());
        await ReachDeadlineAsync(saga, Registry("identity"));

        // An event from a provider that was not part of the fan-out set must not drain a slot.
        await AcknowledgeAsync(saga, "ghost");

        saga.PendingProviders.ShouldBe(["identity"]);
        await _tracker.DidNotReceive().MarkExecutedAsync(
            Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    // ── Fan-in: timeout ────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_AcknowledgementTimeout_OneProviderNeverAcks_MarksPartiallyExecuted()
    {
        PersonalDataDeletionSaga saga = await StartedSagaAsync(CreateDeferredEvent());
        await ReachDeadlineAsync(saga, Registry("identity", "indexing"));

        // Only "identity" acknowledged; "indexing" is stuck.
        await AcknowledgeAsync(saga, "identity");

        await saga.Handle(
            new DeletionAcknowledgementTimedOutEvent(saga.Id),
            _tracker, _metrics, NullLogger<PersonalDataDeletionSaga>.Instance, _timeProvider);

        await _tracker.Received(1).MarkPartiallyExecutedAsync(
            saga.Id,
            Arg.Any<DateTimeOffset>(),
            Arg.Is<IReadOnlyList<string>>(m => m.Count == 1 && m[0] == "indexing"),
            Arg.Any<CancellationToken>());

        // Crucially: it must NOT be reported Executed — the deletion is not provably complete.
        await _tracker.DidNotReceive().MarkExecutedAsync(
            Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    // ── Cancellation ─────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_Cancelled_MarksTrackerCancelled()
    {
        PersonalDataDeletionSaga saga = await StartedSagaAsync(CreateDeferredEvent());

        DateTimeOffset cancelledAt = DateTimeOffset.UtcNow;
        await saga.Handle(
            new DeletionCancelledEto(saga.Id, saga.UserId, cancelledAt),
            _tracker, _metrics);

        await _tracker.Received(1).MarkCancelledAsync(
            saga.Id, cancelledAt, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_Cancelled_DoesNotPublishDeletionEvent()
    {
        PersonalDataDeletionSaga saga = await StartedSagaAsync(CreateDeferredEvent());

        await saga.Handle(
            new DeletionCancelledEto(saga.Id, saga.UserId, DateTimeOffset.UtcNow),
            _tracker, _metrics);

        await _tracker.DidNotReceive().MarkExecutedAsync(
            Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    private Task AcknowledgeAsync(PersonalDataDeletionSaga saga, string providerName) =>
        saga.Handle(
            new PersonalDataDeletedEto(saga.Id, providerName, DeletionAction.PhysicalDelete, 1, null, saga.TenantId),
            _tracker,
            _metrics,
            _timeProvider);
}
