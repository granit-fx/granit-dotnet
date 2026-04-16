using Granit.AI.EntityFrameworkCore.Entities;
using Granit.AI.EntityFrameworkCore.Internal;
using Granit.AI.Workspaces;
using Granit.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;

namespace Granit.AI.EntityFrameworkCore.Tests;

public sealed class EfAIWorkspaceStoreTests : IAsyncDisposable
{
    private readonly TestDbContextFactory _factory;
    private readonly EfAIWorkspaceStore _store;

    public EfAIWorkspaceStoreTests()
    {
        DbContextOptions<AIDbContext> options = new DbContextOptionsBuilder<AIDbContext>()
            .UseInMemoryDatabase($"ai-test-{Guid.NewGuid()}")
            .Options;

        _factory = new TestDbContextFactory(options);
        _store = new EfAIWorkspaceStore(_factory, Substitute.For<ICurrentTenant>());
    }

    public async ValueTask DisposeAsync()
    {
        await using AIDbContext context = await _factory.CreateDbContextAsync();
        await context.Database.EnsureDeletedAsync();
    }

    private static AIWorkspace CreateWorkspace(string name = "test-ws") => new()
    {
        Name = name,
        Provider = "OpenAI",
        Model = "gpt-4o",
        IsActive = true,
    };

    [Fact]
    public async Task CreateAsync_ThenFindAsync_ReturnsWorkspace()
    {
        await _store.CreateAsync(CreateWorkspace(), TestContext.Current.CancellationToken);

        AIWorkspace? result = await _store.FindAsync("test-ws", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Name.ShouldBe("test-ws");
        result.Provider.ShouldBe("OpenAI");
        result.Model.ShouldBe("gpt-4o");
        result.Kind.ShouldBe(AIWorkspaceKind.Dynamic);
    }

    [Fact]
    public async Task FindAsync_NotFound_ReturnsNull()
    {
        AIWorkspace? result = await _store.FindAsync("missing", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task FindAsync_ReturnsInactiveWorkspace()
    {
        await _store.CreateAsync(CreateWorkspace() with { IsActive = false }, TestContext.Current.CancellationToken);

        AIWorkspace? result = await _store.FindAsync("test-ws", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsActiveAndInactiveWorkspaces()
    {
        await _store.CreateAsync(CreateWorkspace("active"), TestContext.Current.CancellationToken);
        await _store.CreateAsync(CreateWorkspace("inactive") with { IsActive = false }, TestContext.Current.CancellationToken);

        IReadOnlyList<AIWorkspace> results = await _store.GetAllAsync(TestContext.Current.CancellationToken);

        results.Count.ShouldBe(2);
        results.Select(r => r.Name).ShouldBe(["active", "inactive"]);
    }

    [Fact]
    public async Task UpdateAsync_ModifiesExistingWorkspace()
    {
        await _store.CreateAsync(CreateWorkspace(), TestContext.Current.CancellationToken);

        AIWorkspace updated = CreateWorkspace() with { Model = "gpt-4o-mini" };
        await _store.UpdateAsync(updated, TestContext.Current.CancellationToken);

        AIWorkspace? result = await _store.FindAsync("test-ws", TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.Model.ShouldBe("gpt-4o-mini");
    }

    [Fact]
    public async Task DeleteAsync_RemovesWorkspace()
    {
        await _store.CreateAsync(CreateWorkspace(), TestContext.Current.CancellationToken);

        await _store.DeleteAsync("test-ws", TestContext.Current.CancellationToken);

        AIWorkspace? result = await _store.FindAsync("test-ws", TestContext.Current.CancellationToken);
        result.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteAsync_NonExistent_IsNoOp() =>
        await Should.NotThrowAsync(() => _store.DeleteAsync("missing", TestContext.Current.CancellationToken));

    /// <summary>
    /// Simple factory wrapping InMemory options for testing.
    /// </summary>
    private sealed class TestDbContextFactory(DbContextOptions<AIDbContext> options) : IDbContextFactory<AIDbContext>
    {
        public AIDbContext CreateDbContext() => new(options);

        public Task<AIDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AIDbContext(options));
    }
}
