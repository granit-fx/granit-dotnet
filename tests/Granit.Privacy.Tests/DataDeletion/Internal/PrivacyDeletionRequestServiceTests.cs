using System.Diagnostics.Metrics;
using Granit.Events;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Privacy.DataDeletion;
using Granit.Privacy.DataDeletion.Events;
using Granit.Privacy.DataDeletion.Internal;
using Granit.Privacy.Diagnostics;
using Granit.Privacy.Options;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Tests.DataDeletion.Internal;

public sealed class PrivacyDeletionRequestServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 4, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid RequestId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = new("22222222-2222-2222-2222-222222222222");

    private readonly ServiceProvider _sp;
    private readonly PrivacyMetrics _metrics;
    private readonly IDistributedEventBus _eventBus = Substitute.For<IDistributedEventBus>();
    private readonly IDeletionRequestTrackerReader _reader = Substitute.For<IDeletionRequestTrackerReader>();
    private readonly IDeletionRequestTrackerWriter _writer = Substitute.For<IDeletionRequestTrackerWriter>();
    private readonly IGuidGenerator _guids = Substitute.For<IGuidGenerator>();
    private readonly TimeProvider _time = Substitute.For<TimeProvider>();

    public PrivacyDeletionRequestServiceTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        _metrics = new PrivacyMetrics(_sp.GetRequiredService<IMeterFactory>());
        _guids.Create().Returns(RequestId);
        _time.GetUtcNow().Returns(Now);
        _reader.GetByUserAsync(UserId, Arg.Any<CancellationToken>()).Returns([]);
    }

    public void Dispose() => _sp.Dispose();

    private PrivacyDeletionRequestService CreateSut(int graceDays = 30) =>
        new(_eventBus, _metrics, _time, NullTenantContext.Instance,
            Microsoft.Extensions.Options.Options.Create(new GranitPrivacyOptions { DefaultGracePeriodDays = graceDays }),
            _guids, _reader, _writer);

    [Fact]
    public async Task RequestDeletionAsync_Deferred_SchedulesAndPublishesDeferredEto()
    {
        PrivacyDeletionRequestService sut = CreateSut(graceDays: 30);

        RequestDeletionOutcome outcome = await sut.RequestDeletionAsync(
            new RequestDeletionCommand(UserId, "user@example.com", Defer: true, "no longer needed", "EU_GDPR"),
            TestContext.Current.CancellationToken);

        outcome.Result.ShouldBe(RequestDeletionResult.Deferred);
        outcome.RequestId.ShouldBe(RequestId);
        outcome.ScheduledDeletionAt.ShouldBe(Now.AddDays(30));
        await _eventBus.Received(1).PublishAsync(Arg.Any<DeletionDeferredEto>(), Arg.Any<CancellationToken>());
        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<DeletionExecutedEto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestDeletionAsync_Immediate_RecordsAndPublishesRequestedThenExecuted()
    {
        PrivacyDeletionRequestService sut = CreateSut();

        RequestDeletionOutcome outcome = await sut.RequestDeletionAsync(
            new RequestDeletionCommand(UserId, "user@example.com", Defer: false, "erase now", "EU_GDPR"),
            TestContext.Current.CancellationToken);

        outcome.Result.ShouldBe(RequestDeletionResult.ExecutedImmediately);
        outcome.RequestId.ShouldBe(RequestId);
        outcome.ScheduledDeletionAt.ShouldBeNull();
        await _writer.Received(1).RecordImmediateDeletionAsync(RequestId, UserId, "erase now", Now, Arg.Any<CancellationToken>());
        await _eventBus.Received(1).PublishAsync(Arg.Any<PersonalDataDeletionRequestedEto>(), Arg.Any<CancellationToken>());
        await _eventBus.Received(1).PublishAsync(Arg.Any<DeletionExecutedEto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestDeletionAsync_WithExistingDeferredRequest_ReturnsDuplicateAndPublishesNothing()
    {
        _reader.GetByUserAsync(UserId, Arg.Any<CancellationToken>()).Returns([
            new DeletionRequestStatus(Guid.NewGuid(), UserId, DeletionRequestState.Deferred, "prior", Now, Now.AddDays(30), null, null)
        ]);
        PrivacyDeletionRequestService sut = CreateSut();

        RequestDeletionOutcome outcome = await sut.RequestDeletionAsync(
            new RequestDeletionCommand(UserId, "user@example.com", Defer: true, "again", "EU_GDPR"),
            TestContext.Current.CancellationToken);

        outcome.Result.ShouldBe(RequestDeletionResult.DuplicatePending);
        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<DeletionDeferredEto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelDeletionAsync_UnknownRequest_ReturnsNotFound()
    {
        _reader.GetStatusAsync(RequestId, Arg.Any<CancellationToken>()).Returns((DeletionRequestStatus?)null);
        PrivacyDeletionRequestService sut = CreateSut();

        CancelDeletionOutcome outcome = await sut.CancelDeletionAsync(RequestId, UserId, TestContext.Current.CancellationToken);

        outcome.Result.ShouldBe(CancelDeletionResult.NotFound);
        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<DeletionCancelledEto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelDeletionAsync_OtherUsersRequest_ReturnsNotFound()
    {
        _reader.GetStatusAsync(RequestId, Arg.Any<CancellationToken>()).Returns(
            new DeletionRequestStatus(RequestId, Guid.NewGuid(), DeletionRequestState.Deferred, "r", Now, Now.AddDays(30), null, null));
        PrivacyDeletionRequestService sut = CreateSut();

        CancelDeletionOutcome outcome = await sut.CancelDeletionAsync(RequestId, UserId, TestContext.Current.CancellationToken);

        outcome.Result.ShouldBe(CancelDeletionResult.NotFound);
    }

    [Fact]
    public async Task CancelDeletionAsync_AlreadyExecuted_ReturnsNotCancellableWithState()
    {
        _reader.GetStatusAsync(RequestId, Arg.Any<CancellationToken>()).Returns(
            new DeletionRequestStatus(RequestId, UserId, DeletionRequestState.Executed, "r", Now, Now.AddDays(30), null, Now));
        PrivacyDeletionRequestService sut = CreateSut();

        CancelDeletionOutcome outcome = await sut.CancelDeletionAsync(RequestId, UserId, TestContext.Current.CancellationToken);

        outcome.Result.ShouldBe(CancelDeletionResult.NotCancellable);
        outcome.CurrentState.ShouldBe(DeletionRequestState.Executed);
    }

    [Fact]
    public async Task CancelDeletionAsync_DeferredAndOwned_CancelsAndPublishesEto()
    {
        _reader.GetStatusAsync(RequestId, Arg.Any<CancellationToken>()).Returns(
            new DeletionRequestStatus(RequestId, UserId, DeletionRequestState.Deferred, "r", Now, Now.AddDays(30), null, null));
        PrivacyDeletionRequestService sut = CreateSut();

        CancelDeletionOutcome outcome = await sut.CancelDeletionAsync(RequestId, UserId, TestContext.Current.CancellationToken);

        outcome.Result.ShouldBe(CancelDeletionResult.Cancelled);
        await _eventBus.Received(1).PublishAsync(Arg.Any<DeletionCancelledEto>(), Arg.Any<CancellationToken>());
    }
}
