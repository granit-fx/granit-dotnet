using Granit.Dashboards.Domain;
using Granit.Dashboards.EntityFrameworkCore.Internal;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.Endpoints.Tests.Internal;

/// <summary>
/// SQLite-backed write-side tests — locks the load + mutate + persist round-trip
/// of <see cref="DashboardStateTransitionService"/>: idempotency, the
/// Draft → Published → Archived → Draft cycle, the invariants enforced by the
/// domain (cannot publish an archived dashboard, cannot restore a non-archived
/// one), and the not-found contract.
/// </summary>
public sealed class DashboardStateTransitionServiceTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync(TestContext.Current.CancellationToken);

        await using DashboardsDbContext seed = NewContext();
        await seed.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task PublishAsync_DraftDashboard_TransitionsToPublished()
    {
        Guid id = await SeedAsync(DashboardStatus.Draft);
        DashboardStateTransitionService service = new(NewContext());

        DashboardStateTransitionResult result = await service.PublishAsync(id, TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(DashboardStateTransitionOutcome.Updated);
        result.Dashboard.ShouldNotBeNull();
        result.Dashboard.Status.ShouldBe(DashboardStatus.Published);

        (await StatusOnDiskAsync(id)).ShouldBe(DashboardStatus.Published);
    }

    [Fact]
    public async Task PublishAsync_AlreadyPublished_IsIdempotent()
    {
        Guid id = await SeedAsync(DashboardStatus.Published);
        DashboardStateTransitionService service = new(NewContext());

        DashboardStateTransitionResult result = await service.PublishAsync(id, TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(DashboardStateTransitionOutcome.Updated);
        result.Dashboard!.Status.ShouldBe(DashboardStatus.Published);
    }

    [Fact]
    public async Task PublishAsync_ArchivedDashboard_ReturnsConflict()
    {
        Guid id = await SeedAsync(DashboardStatus.Archived);
        DashboardStateTransitionService service = new(NewContext());

        DashboardStateTransitionResult result = await service.PublishAsync(id, TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(DashboardStateTransitionOutcome.Conflict);
        result.ConflictReason.ShouldNotBeNullOrWhiteSpace();
        (await StatusOnDiskAsync(id)).ShouldBe(DashboardStatus.Archived);
    }

    [Fact]
    public async Task ArchiveAsync_FromDraft_TransitionsToArchived()
    {
        Guid id = await SeedAsync(DashboardStatus.Draft);
        DashboardStateTransitionService service = new(NewContext());

        DashboardStateTransitionResult result = await service.ArchiveAsync(id, TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(DashboardStateTransitionOutcome.Updated);
        result.Dashboard!.Status.ShouldBe(DashboardStatus.Archived);
        (await StatusOnDiskAsync(id)).ShouldBe(DashboardStatus.Archived);
    }

    [Fact]
    public async Task ArchiveAsync_FromPublished_TransitionsToArchived()
    {
        Guid id = await SeedAsync(DashboardStatus.Published);
        DashboardStateTransitionService service = new(NewContext());

        DashboardStateTransitionResult result = await service.ArchiveAsync(id, TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(DashboardStateTransitionOutcome.Updated);
        result.Dashboard!.Status.ShouldBe(DashboardStatus.Archived);
    }

    [Fact]
    public async Task ArchiveAsync_AlreadyArchived_IsIdempotent()
    {
        Guid id = await SeedAsync(DashboardStatus.Archived);
        DashboardStateTransitionService service = new(NewContext());

        DashboardStateTransitionResult result = await service.ArchiveAsync(id, TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(DashboardStateTransitionOutcome.Updated);
        result.Dashboard!.Status.ShouldBe(DashboardStatus.Archived);
    }

    [Fact]
    public async Task RestoreAsync_FromArchived_TransitionsToDraft()
    {
        Guid id = await SeedAsync(DashboardStatus.Archived);
        DashboardStateTransitionService service = new(NewContext());

        DashboardStateTransitionResult result = await service.RestoreAsync(id, TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(DashboardStateTransitionOutcome.Updated);
        result.Dashboard!.Status.ShouldBe(DashboardStatus.Draft);
        (await StatusOnDiskAsync(id)).ShouldBe(DashboardStatus.Draft);
    }

    [Fact]
    public async Task RestoreAsync_FromDraft_ReturnsConflict()
    {
        Guid id = await SeedAsync(DashboardStatus.Draft);
        DashboardStateTransitionService service = new(NewContext());

        DashboardStateTransitionResult result = await service.RestoreAsync(id, TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(DashboardStateTransitionOutcome.Conflict);
        result.ConflictReason.ShouldNotBeNullOrWhiteSpace();
        (await StatusOnDiskAsync(id)).ShouldBe(DashboardStatus.Draft);
    }

    [Fact]
    public async Task RestoreAsync_FromPublished_ReturnsConflict()
    {
        Guid id = await SeedAsync(DashboardStatus.Published);
        DashboardStateTransitionService service = new(NewContext());

        DashboardStateTransitionResult result = await service.RestoreAsync(id, TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(DashboardStateTransitionOutcome.Conflict);
    }

    [Fact]
    public async Task PublishAsync_UnknownId_ReturnsNotFound()
    {
        DashboardStateTransitionService service = new(NewContext());

        DashboardStateTransitionResult result = await service.PublishAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(DashboardStateTransitionOutcome.NotFound);
        result.Dashboard.ShouldBeNull();
    }

    [Fact]
    public async Task ArchiveAsync_UnknownId_ReturnsNotFound()
    {
        DashboardStateTransitionService service = new(NewContext());

        DashboardStateTransitionResult result = await service.ArchiveAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(DashboardStateTransitionOutcome.NotFound);
    }

    [Fact]
    public async Task RestoreAsync_UnknownId_ReturnsNotFound()
    {
        DashboardStateTransitionService service = new(NewContext());

        DashboardStateTransitionResult result = await service.RestoreAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(DashboardStateTransitionOutcome.NotFound);
    }

    [Fact]
    public async Task FullLifecycle_DraftPublishArchiveRestore_RoundTripsCorrectly()
    {
        Guid id = await SeedAsync(DashboardStatus.Draft);
        DashboardStateTransitionService service = new(NewContext());

        (await service.PublishAsync(id, TestContext.Current.CancellationToken)).Dashboard!.Status.ShouldBe(DashboardStatus.Published);
        (await service.ArchiveAsync(id, TestContext.Current.CancellationToken)).Dashboard!.Status.ShouldBe(DashboardStatus.Archived);
        (await service.RestoreAsync(id, TestContext.Current.CancellationToken)).Dashboard!.Status.ShouldBe(DashboardStatus.Draft);

        (await StatusOnDiskAsync(id)).ShouldBe(DashboardStatus.Draft);
    }

    private async Task<Guid> SeedAsync(DashboardStatus status)
    {
        var dashboard = Dashboard.Create(Guid.NewGuid(), "Sample", DashboardCategory.Finance);
        if (status == DashboardStatus.Published)
        {
            dashboard.Publish();
        }
        else if (status == DashboardStatus.Archived)
        {
            dashboard.Publish();
            dashboard.Archive();
        }
        dashboard.ClearDomainEvents();

        await using DashboardsDbContext ctx = NewContext();
        ctx.Dashboards.Add(dashboard);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return dashboard.Id;
    }

    private async Task<DashboardStatus> StatusOnDiskAsync(Guid id)
    {
        await using DashboardsDbContext ctx = NewContext();
        return await ctx.Dashboards
            .AsNoTracking()
            .Where(d => d.Id == id)
            .Select(d => d.Status)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    private DashboardsDbContext NewContext()
    {
        DbContextOptions<DashboardsDbContext> options = new DbContextOptionsBuilder<DashboardsDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new DashboardsDbContext(options);
    }
}
