using Granit.Events;
using Granit.Privacy.BackgroundJobs.Jobs;
using Granit.Privacy.DataDeletion;
using Granit.Privacy.DataDeletion.Events;
using Granit.Privacy.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Privacy.BackgroundJobs.Tests.Jobs;

public sealed class DeletionDeadlineEnforcerHandlerTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 3, 23, 2, 0, 0, TimeSpan.Zero);

    private readonly IDeletionRequestTrackerReader _trackerReader = Substitute.For<IDeletionRequestTrackerReader>();
    private readonly IDeletionRequestTrackerWriter _trackerWriter = Substitute.For<IDeletionRequestTrackerWriter>();
    private readonly IDistributedEventBus _eventBus = Substitute.For<IDistributedEventBus>();
    private readonly FakeTimeProvider _timeProvider = new(Now);
    private readonly ServiceProvider _sp;
    private readonly PrivacyMetrics _metrics;

    public DeletionDeadlineEnforcerHandlerTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        _metrics = new PrivacyMetrics(_sp.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>());
    }

    public void Dispose() => _sp.Dispose();

    [Fact]
    public async Task HandleAsync_when_no_expired_requests_should_return_immediately()
    {
        _trackerReader.GetExpiredDeferredAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await DeletionDeadlineEnforcerHandler.HandleAsync(
            new DeletionDeadlineEnforcerJob(),
            _trackerReader,
            _trackerWriter,
            _eventBus,
            _timeProvider,
            _metrics,
            NullLogger<DeletionDeadlineEnforcerJob>.Instance,
            TestContext.Current.CancellationToken);

        await _trackerWriter.DidNotReceive()
            .MarkExecutedAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_should_enforce_expired_requests()
    {
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        DateTimeOffset requested = Now.AddDays(-30);
        DateTimeOffset scheduled = Now.AddDays(-1);

        _trackerReader.GetExpiredDeferredAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([new DeletionRequestStatus(requestId, userId, DeletionRequestState.Deferred, "test", requested, scheduled, null, null)]);

        await DeletionDeadlineEnforcerHandler.HandleAsync(
            new DeletionDeadlineEnforcerJob(),
            _trackerReader,
            _trackerWriter,
            _eventBus,
            _timeProvider,
            _metrics,
            NullLogger<DeletionDeadlineEnforcerJob>.Instance,
            TestContext.Current.CancellationToken);

        await _trackerWriter.Received(1)
            .MarkExecutedAsync(requestId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await _eventBus.Received(1)
            .PublishAsync(Arg.Any<PersonalDataDeletionRequestedEto>(), Arg.Any<CancellationToken>());
        await _eventBus.Received(1)
            .PublishAsync(Arg.Any<DeletionExecutedEto>(), Arg.Any<CancellationToken>());
    }
}
