using Granit.MultiTenancy;
using Granit.Privacy.DataExport;
using Granit.Privacy.EntityFrameworkCore.DataExport.Internal;
using Granit.Privacy.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Privacy.EntityFrameworkCore.Tests;

public sealed class EfExportRequestTrackerTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly TestFactory _factory;
    private readonly FakeTimeProvider _time;
    private readonly EfExportRequestTracker<PrivacyDbContext> _sut;

    public EfExportRequestTrackerTests()
    {
        DbContextOptions<PrivacyDbContext> options = new DbContextOptionsBuilder<PrivacyDbContext>()
            .UseInMemoryDatabase($"privacy-export-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _factory = new TestFactory(options);
        _time = new FakeTimeProvider(Now);
        _sut = new EfExportRequestTracker<PrivacyDbContext>(_factory, _factory.Tenant, _time);
    }

    public async ValueTask DisposeAsync()
    {
        await using PrivacyDbContext db = await _factory.CreateDbContextAsync();
        await db.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task RecordRequestAsync_ThenGetStatusAsync_RoundTrips()
    {
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await _sut.RecordRequestAsync(requestId, userId, Now, TestContext.Current.CancellationToken);

        ExportRequestStatus? status = await _sut.GetStatusAsync(requestId, TestContext.Current.CancellationToken);

        status.ShouldNotBeNull();
        status.UserId.ShouldBe(userId);
        status.State.ShouldBe(ExportRequestState.Pending);
        status.RequestedAt.ShouldBe(Now);
    }

    [Fact]
    public async Task GetStatusAsync_Missing_ReturnsNull()
    {
        ExportRequestStatus? status = await _sut.GetStatusAsync(
            Guid.NewGuid(), TestContext.Current.CancellationToken);
        status.ShouldBeNull();
    }

    [Fact]
    public async Task GetByUserAsync_FiltersAndOrders()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        await _sut.RecordRequestAsync(Guid.NewGuid(), userA, Now.AddDays(-1), TestContext.Current.CancellationToken);
        await _sut.RecordRequestAsync(Guid.NewGuid(), userA, Now, TestContext.Current.CancellationToken);
        await _sut.RecordRequestAsync(Guid.NewGuid(), userB, Now, TestContext.Current.CancellationToken);

        IReadOnlyList<ExportRequestStatus> result = await _sut.GetByUserAsync(userA, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
        result[0].RequestedAt.ShouldBe(Now);
        result[1].RequestedAt.ShouldBe(Now.AddDays(-1));
    }

    [Fact]
    public async Task MarkCompletedAsync_SetsState_BlobReference_AndCompletedAt()
    {
        var requestId = Guid.NewGuid();
        await _sut.RecordRequestAsync(requestId, Guid.NewGuid(), Now, TestContext.Current.CancellationToken);

        _time.Set(Now.AddMinutes(5));
        await _sut.MarkCompletedAsync(
            requestId,
            ExportRequestState.Completed,
            archiveBlobReferenceId: "blob://archive-123",
            missingProviders: ["mailer"],
            TestContext.Current.CancellationToken);

        ExportRequestStatus? status = await _sut.GetStatusAsync(requestId, TestContext.Current.CancellationToken);
        status.ShouldNotBeNull();
        status.State.ShouldBe(ExportRequestState.Completed);
        status.CompletedAt.ShouldBe(Now.AddMinutes(5));
        status.ArchiveBlobReferenceId!.Value.ShouldBe("blob://archive-123");
        status.MissingProviders.ShouldNotBeNull();
        status.MissingProviders.ShouldContain("mailer");
    }

    [Fact]
    public async Task MarkCompletedAsync_NullMissingProviders_StoresEmptyList()
    {
        var requestId = Guid.NewGuid();
        await _sut.RecordRequestAsync(requestId, Guid.NewGuid(), Now, TestContext.Current.CancellationToken);

        await _sut.MarkCompletedAsync(
            requestId, ExportRequestState.Completed, "ref", missingProviders: null,
            TestContext.Current.CancellationToken);

        ExportRequestStatus? status = await _sut.GetStatusAsync(requestId, TestContext.Current.CancellationToken);
        status.ShouldNotBeNull();
        status.MissingProviders.ShouldNotBeNull();
        status.MissingProviders.Count.ShouldBe(0);
    }

    [Fact]
    public async Task MarkCompletedAsync_UnknownId_NoOp() =>
        await Should.NotThrowAsync(() => _sut.MarkCompletedAsync(
            Guid.NewGuid(), ExportRequestState.Completed, null, null,
            TestContext.Current.CancellationToken));

    private sealed class TestFactory(DbContextOptions<PrivacyDbContext> options)
        : IDbContextFactory<PrivacyDbContext>
    {
        public ICurrentTenant Tenant { get; } = Substitute.For<ICurrentTenant>();

        public PrivacyDbContext CreateDbContext() => new(options, Tenant);

        public Task<PrivacyDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new PrivacyDbContext(options, Tenant));
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Set(DateTimeOffset value) => _now = value;
    }
}
