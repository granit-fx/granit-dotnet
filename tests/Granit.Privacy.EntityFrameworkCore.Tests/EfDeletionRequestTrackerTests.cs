using Granit.MultiTenancy;
using Granit.Privacy.DataDeletion;
using Granit.Privacy.EntityFrameworkCore.DataDeletion.Internal;
using Granit.Privacy.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Privacy.EntityFrameworkCore.Tests;

public sealed class EfDeletionRequestTrackerTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly TestFactory _factory;
    private readonly EfDeletionRequestTracker<PrivacyDbContext> _sut;

    public EfDeletionRequestTrackerTests()
    {
        DbContextOptions<PrivacyDbContext> options = new DbContextOptionsBuilder<PrivacyDbContext>()
            .UseInMemoryDatabase($"privacy-deletion-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _factory = new TestFactory(options);
        _sut = new EfDeletionRequestTracker<PrivacyDbContext>(_factory, _factory.Tenant);
    }

    public async ValueTask DisposeAsync()
    {
        await using PrivacyDbContext db = await _factory.CreateDbContextAsync();
        await db.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task RecordDeferredAsync_ThenGetStatusAsync_RoundTrips()
    {
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await _sut.RecordDeferredAsync(
            requestId, userId, "test reason",
            requestedAt: Now,
            scheduledDeletionAt: Now.AddDays(30),
            TestContext.Current.CancellationToken);

        DeletionRequestStatus? status = await _sut.GetStatusAsync(requestId, TestContext.Current.CancellationToken);

        status.ShouldNotBeNull();
        status.UserId.ShouldBe(userId);
        status.State.ShouldBe(DeletionRequestState.Deferred);
        status.Reason.ShouldBe("test reason");
        status.ScheduledDeletionAt.ShouldBe(Now.AddDays(30));
    }

    [Fact]
    public async Task GetStatusAsync_Missing_ReturnsNull()
    {
        DeletionRequestStatus? status = await _sut.GetStatusAsync(
            Guid.NewGuid(), TestContext.Current.CancellationToken);

        status.ShouldBeNull();
    }

    [Fact]
    public async Task GetByUserAsync_FiltersByUser_OrdersByRequestedAtDesc()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        await _sut.RecordDeferredAsync(Guid.NewGuid(), userA, "first", Now.AddDays(-2), Now.AddDays(28), TestContext.Current.CancellationToken);
        await _sut.RecordDeferredAsync(Guid.NewGuid(), userA, "second", Now, Now.AddDays(30), TestContext.Current.CancellationToken);
        await _sut.RecordDeferredAsync(Guid.NewGuid(), userB, "other", Now, Now.AddDays(30), TestContext.Current.CancellationToken);

        IReadOnlyList<DeletionRequestStatus> result = await _sut.GetByUserAsync(userA, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
        result[0].Reason.ShouldBe("second");
        result[1].Reason.ShouldBe("first");
    }

    [Fact]
    public async Task GetExpiredDeferredAsync_ReturnsOnlyDueDeferred()
    {
        var dueId = Guid.NewGuid();
        var notDueId = Guid.NewGuid();
        await _sut.RecordDeferredAsync(dueId, Guid.NewGuid(), "due", Now.AddDays(-30), Now.AddDays(-1), TestContext.Current.CancellationToken);
        await _sut.RecordDeferredAsync(notDueId, Guid.NewGuid(), "not due", Now, Now.AddDays(30), TestContext.Current.CancellationToken);

        IReadOnlyList<DeletionRequestStatus> result = await _sut.GetExpiredDeferredAsync(Now, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].RequestId.ShouldBe(dueId);
    }

    [Fact]
    public async Task MarkExecutedAsync_TransitionsState_AndSetsExecutedAt()
    {
        var id = Guid.NewGuid();
        await _sut.RecordDeferredAsync(id, Guid.NewGuid(), "reason", Now, Now.AddDays(30), TestContext.Current.CancellationToken);

        await _sut.MarkExecutedAsync(id, Now.AddDays(31), TestContext.Current.CancellationToken);

        DeletionRequestStatus? status = await _sut.GetStatusAsync(id, TestContext.Current.CancellationToken);
        status.ShouldNotBeNull();
        status.State.ShouldBe(DeletionRequestState.Executed);
        status.ExecutedAt.ShouldBe(Now.AddDays(31));
    }

    [Fact]
    public async Task MarkCancelledAsync_TransitionsState_AndSetsCancelledAt()
    {
        var id = Guid.NewGuid();
        await _sut.RecordDeferredAsync(id, Guid.NewGuid(), "reason", Now, Now.AddDays(30), TestContext.Current.CancellationToken);

        await _sut.MarkCancelledAsync(id, Now.AddDays(7), TestContext.Current.CancellationToken);

        DeletionRequestStatus? status = await _sut.GetStatusAsync(id, TestContext.Current.CancellationToken);
        status.ShouldNotBeNull();
        status.State.ShouldBe(DeletionRequestState.Cancelled);
        status.CancelledAt.ShouldBe(Now.AddDays(7));
    }

    [Fact]
    public async Task MarkExecutedAsync_UnknownId_NoOp() =>
        await Should.NotThrowAsync(() => _sut.MarkExecutedAsync(
            Guid.NewGuid(), Now, TestContext.Current.CancellationToken));

    [Fact]
    public async Task MarkCancelledAsync_UnknownId_NoOp() =>
        await Should.NotThrowAsync(() => _sut.MarkCancelledAsync(
            Guid.NewGuid(), Now, TestContext.Current.CancellationToken));

    private sealed class TestFactory(DbContextOptions<PrivacyDbContext> options)
        : IDbContextFactory<PrivacyDbContext>
    {
        public ICurrentTenant Tenant { get; } = Substitute.For<ICurrentTenant>();

        public PrivacyDbContext CreateDbContext() => new(options, Tenant);

        public Task<PrivacyDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new PrivacyDbContext(options, Tenant));
    }
}
