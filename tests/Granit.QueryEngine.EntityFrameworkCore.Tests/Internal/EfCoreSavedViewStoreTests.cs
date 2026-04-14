using Granit.MultiTenancy;
using Granit.QueryEngine.EntityFrameworkCore.Internal;
using Granit.QueryEngine.SavedViews;
using Granit.QueryEngine.SavedViews.Domain;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.EntityFrameworkCore.Tests.Internal;

public sealed class EfCoreSavedViewStoreTests : IAsyncLifetime
{
    private TestQueryEngineDbContextFactory _factory = null!;
    private EfCoreSavedViewStore _store = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new TestQueryEngineDbContextFactory();
        _store = new EfCoreSavedViewStore(_factory, Substitute.For<ICurrentTenant>());
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _factory.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task CreateAsync_persists_saved_view()
    {
        SavedView view = CreateView("Test View");

        await _store.CreateAsync(view, TestContext.Current.CancellationToken);

        SavedView? retrieved = await _store.GetAsync(view.Id, TestContext.Current.CancellationToken);
        retrieved.ShouldNotBeNull();
        retrieved.Name.ShouldBe("Test View");
        retrieved.EntityType.ShouldBe("Products");
    }

    [Fact]
    public async Task GetAsync_returns_null_for_unknown_id()
    {
        SavedView? result = await _store.GetAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetListAsync_returns_own_and_shared_views()
    {
        SavedView ownView = CreateView("My View", userId: "user-1");
        SavedView sharedView = CreateView("Shared View", userId: "user-2", isShared: true);
        SavedView otherView = CreateView("Other View", userId: "user-2");

        await _store.CreateAsync(ownView, TestContext.Current.CancellationToken);
        await _store.CreateAsync(sharedView, TestContext.Current.CancellationToken);
        await _store.CreateAsync(otherView, TestContext.Current.CancellationToken);

        IReadOnlyList<SavedView> result = await _store.GetListAsync(
            "Products", "user-1", null, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
        result.ShouldContain(v => v.Name == "My View");
        result.ShouldContain(v => v.Name == "Shared View");
    }

    [Fact]
    public async Task GetListAsync_filters_by_tenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        SavedView viewA = CreateView("View A", tenantId: tenantA);
        SavedView viewB = CreateView("View B", tenantId: tenantB);

        await _store.CreateAsync(viewA, TestContext.Current.CancellationToken);
        await _store.CreateAsync(viewB, TestContext.Current.CancellationToken);

        IReadOnlyList<SavedView> result = await _store.GetListAsync(
            "Products", "user-1", tenantA, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("View A");
    }

    [Fact]
    public async Task GetListAsync_returns_ordered_by_name()
    {
        SavedView viewC = CreateView("Charlie");
        SavedView viewA = CreateView("Alpha");
        SavedView viewB = CreateView("Bravo");

        await _store.CreateAsync(viewC, TestContext.Current.CancellationToken);
        await _store.CreateAsync(viewA, TestContext.Current.CancellationToken);
        await _store.CreateAsync(viewB, TestContext.Current.CancellationToken);

        IReadOnlyList<SavedView> result = await _store.GetListAsync(
            "Products", "user-1", null, TestContext.Current.CancellationToken);

        result[0].Name.ShouldBe("Alpha");
        result[1].Name.ShouldBe("Bravo");
        result[2].Name.ShouldBe("Charlie");
    }

    [Fact]
    public async Task UpdateAsync_modifies_existing_view()
    {
        SavedView view = CreateView("Original");
        await _store.CreateAsync(view, TestContext.Current.CancellationToken);

        view.Name = "Updated";
        view.FilterJson = "{\"status\":\"active\"}";
        await _store.UpdateAsync(view, TestContext.Current.CancellationToken);

        SavedView? retrieved = await _store.GetAsync(view.Id, TestContext.Current.CancellationToken);
        retrieved.ShouldNotBeNull();
        retrieved.Name.ShouldBe("Updated");
        retrieved.FilterJson.ShouldBe("{\"status\":\"active\"}");
    }

    [Fact]
    public async Task DeleteAsync_removes_view()
    {
        SavedView view = CreateView("To Delete");
        await _store.CreateAsync(view, TestContext.Current.CancellationToken);

        await _store.DeleteAsync(view.Id, TestContext.Current.CancellationToken);

        SavedView? retrieved = await _store.GetAsync(view.Id, TestContext.Current.CancellationToken);
        retrieved.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteAsync_does_nothing_for_unknown_id() =>
        await Should.NotThrowAsync(() => _store.DeleteAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));

    [Fact]
    public async Task SetDefaultAsync_sets_default_and_unsets_previous()
    {
        SavedView view1 = CreateView("View 1", isDefault: true);
        SavedView view2 = CreateView("View 2");

        await _store.CreateAsync(view1, TestContext.Current.CancellationToken);
        await _store.CreateAsync(view2, TestContext.Current.CancellationToken);

        await _store.SetDefaultAsync(view2.Id, "user-1", "Products", TestContext.Current.CancellationToken);

        SavedView? updated1 = await _store.GetAsync(view1.Id, TestContext.Current.CancellationToken);
        SavedView? updated2 = await _store.GetAsync(view2.Id, TestContext.Current.CancellationToken);

        updated1!.IsDefault.ShouldBeFalse();
        updated2!.IsDefault.ShouldBeTrue();
    }

    [Fact]
    public async Task SetDefaultAsync_ignores_unknown_id() =>
        await Should.NotThrowAsync(() => _store.SetDefaultAsync(Guid.NewGuid(), "user-1", "Products", TestContext.Current.CancellationToken));

    private static SavedView CreateView(
        string name,
        string userId = "user-1",
        string entityType = "Products",
        bool isShared = false,
        bool isDefault = false,
        Guid? tenantId = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            EntityType = entityType,
            Name = name,
            UserId = userId,
            IsShared = isShared,
            IsDefault = isDefault,
            TenantId = tenantId,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = userId,
        };

    /// <summary>
    /// In-memory factory for <see cref="QueryEngineDbContext"/> used by tests.
    /// </summary>
    private sealed class TestQueryEngineDbContextFactory : IDbContextFactory<QueryEngineDbContext>, IDisposable
    {
        private readonly DbContextOptions<QueryEngineDbContext> _options =
            new DbContextOptionsBuilder<QueryEngineDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

        public QueryEngineDbContext CreateDbContext() => new(_options);

        public Task<QueryEngineDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new QueryEngineDbContext(_options));

        public void Dispose()
        {
            // InMemory database is cleaned up automatically
        }
    }
}
