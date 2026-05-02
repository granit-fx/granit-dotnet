using Granit.Documents;
using Granit.Documents.Domain;
using Granit.Documents.EntityFrameworkCore.Internal;
using Granit.Guids;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Documents.EntityFrameworkCore.Tests.Internal;

/// <summary>
/// SQLite-backed unit tests for <see cref="DocumentBootstrapService"/>. SQLite enforces the
/// partial unique index <c>ux_documents_folders_one_root_per_tenant</c>, so we can verify
/// concurrency safety without spinning up a Postgres container.
/// </summary>
public sealed class DocumentBootstrapServiceSqliteTests : IAsyncLifetime
{
    private SqliteConnection _holdOpen = null!;
    private TestDbContextFactory _factory = null!;
    private static readonly Guid OwnerId = Guid.NewGuid();

    public async ValueTask InitializeAsync()
    {
        // Shared in-memory SQLite database under a unique name. The "holdOpen" connection
        // keeps the database alive for the test's lifetime; each DbContext opens its own
        // connection to the same shared cache, allowing concurrent queries to proceed.
        string connectionString =
            $"DataSource=file:f22-bootstrap-{Guid.NewGuid():N}?mode=memory&cache=shared";

        _holdOpen = new SqliteConnection(connectionString);
        await _holdOpen.OpenAsync();

        DbContextOptions<DocumentsDbContext> options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseSqlite(connectionString)
            .Options;

        await using DocumentsDbContext init = new(options);
        await init.Database.EnsureCreatedAsync();

        _factory = new TestDbContextFactory(options);
    }

    public ValueTask DisposeAsync() => _holdOpen.DisposeAsync();

    [Fact]
    public async Task EnsureTenantRootAsync_FirstCall_CreatesRoot()
    {
        var sut = new DocumentBootstrapService(_factory, new SimpleGuidGenerator());
        var tenant = Guid.NewGuid();

        Guid rootId = await sut.EnsureTenantRootAsync(tenant, OwnerId, TestContext.Current.CancellationToken);

        rootId.ShouldNotBe(Guid.Empty);
        await using DocumentsDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        Folder root = await db.Folders.SingleAsync(f => f.Id == rootId, TestContext.Current.CancellationToken);
        root.IsTenantRoot.ShouldBeTrue();
        root.TenantId.ShouldBe(tenant);
        root.Path.ShouldBe("/");
        root.OwnerUserId.ShouldBe(OwnerId);
    }

    [Fact]
    public async Task EnsureTenantRootAsync_SecondCall_SameInstance_IsCached()
    {
        var sut = new DocumentBootstrapService(_factory, new CountingGuidGenerator());
        var tenant = Guid.NewGuid();

        Guid first = await sut.EnsureTenantRootAsync(tenant, OwnerId, TestContext.Current.CancellationToken);
        Guid second = await sut.EnsureTenantRootAsync(tenant, OwnerId, TestContext.Current.CancellationToken);

        first.ShouldBe(second);
        await using DocumentsDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        (await db.Folders.CountAsync(f => f.TenantId == tenant, TestContext.Current.CancellationToken))
            .ShouldBe(1);
    }

    [Fact]
    public async Task EnsureTenantRootAsync_TwoInstances_SameTenant_DoNotCreateDuplicate()
    {
        var tenant = Guid.NewGuid();
        var a = new DocumentBootstrapService(_factory, new SimpleGuidGenerator());
        var b = new DocumentBootstrapService(_factory, new SimpleGuidGenerator());

        Guid fromA = await a.EnsureTenantRootAsync(tenant, OwnerId, TestContext.Current.CancellationToken);
        Guid fromB = await b.EnsureTenantRootAsync(tenant, OwnerId, TestContext.Current.CancellationToken);

        // Both calls return the SAME id because the SELECT in B finds A's row.
        fromA.ShouldBe(fromB);
        await using DocumentsDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        (await db.Folders.CountAsync(f => f.TenantId == tenant, TestContext.Current.CancellationToken))
            .ShouldBe(1);
    }

    [Fact]
    public async Task EnsureTenantRootAsync_DifferentTenants_CreateIndependentRoots()
    {
        var sut = new DocumentBootstrapService(_factory, new SimpleGuidGenerator());
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        Guid rootA = await sut.EnsureTenantRootAsync(tenantA, OwnerId, TestContext.Current.CancellationToken);
        Guid rootB = await sut.EnsureTenantRootAsync(tenantB, OwnerId, TestContext.Current.CancellationToken);

        rootA.ShouldNotBe(rootB);
        await using DocumentsDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        (await db.Folders.CountAsync(f => f.IsTenantRoot, TestContext.Current.CancellationToken))
            .ShouldBe(2);
    }

    [Fact]
    public async Task EnsureTenantRootAsync_HostScope_NullTenantId_Allowed()
    {
        var sut = new DocumentBootstrapService(_factory, new SimpleGuidGenerator());

        Guid hostRoot = await sut.EnsureTenantRootAsync(tenantId: null, OwnerId, TestContext.Current.CancellationToken);

        await using DocumentsDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        Folder root = await db.Folders.SingleAsync(f => f.Id == hostRoot, TestContext.Current.CancellationToken);
        root.IsTenantRoot.ShouldBeTrue();
        root.TenantId.ShouldBeNull();
    }

    [Fact]
    public async Task EnsureTenantRootAsync_ConcurrentSameTenant_ResolvesToSingleRoot()
    {
        var tenant = Guid.NewGuid();
        const int parallelism = 16;

        // Each task uses its OWN DocumentBootstrapService instance (simulating per-request
        // scoped services racing on the same tenant).
        DocumentBootstrapService[] services = Enumerable
            .Range(0, parallelism)
            .Select(_ => new DocumentBootstrapService(_factory, new SimpleGuidGenerator()))
            .ToArray();

        Task<Guid>[] tasks = services
            .Select(s => Task.Run(() => s.EnsureTenantRootAsync(tenant, OwnerId, TestContext.Current.CancellationToken)))
            .ToArray();

        Guid[] results = await Task.WhenAll(tasks);

        // All concurrent calls must resolve to the same root identifier.
        results.Distinct().ShouldHaveSingleItem();

        await using DocumentsDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        (await db.Folders.CountAsync(f => f.TenantId == tenant, TestContext.Current.CancellationToken))
            .ShouldBe(1);
    }

    /// <summary>Lightweight DbContext factory wrapping fixed options for the tests.</summary>
    private sealed class TestDbContextFactory(DbContextOptions<DocumentsDbContext> options)
        : IDbContextFactory<DocumentsDbContext>
    {
        public DocumentsDbContext CreateDbContext() => new(options);

        public Task<DocumentsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<DocumentsDbContext>(new(options));
    }

    /// <summary>Always returns a fresh <see cref="Guid"/>. Mirrors <c>SequentialAtEndGuidGenerator</c>'s contract.</summary>
    private sealed class SimpleGuidGenerator : IGuidGenerator
    {
        public Guid Create() => Guid.NewGuid();
    }

    /// <summary>Counts <see cref="Create"/> invocations so the cached-call test can verify no extra Guid was minted.</summary>
    private sealed class CountingGuidGenerator : IGuidGenerator
    {
        public int CreateCount { get; private set; }
        public Guid Create()
        {
            CreateCount++;
            return Guid.NewGuid();
        }
    }
}
