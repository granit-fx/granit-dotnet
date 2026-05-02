using Granit.Documents;
using Granit.Documents.Domain;
using Granit.Documents.EntityFrameworkCore.Internal;
using Granit.Guids;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Documents.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// PostgreSQL-backed concurrency tests for <see cref="DocumentBootstrapService"/>.
/// Verifies that the partial unique index <c>ux_documents_folders_one_root_per_tenant</c>
/// keeps exactly one root row per tenant under realistic parallel access.
/// </summary>
public sealed class DocumentBootstrapServicePostgresTests :
    IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private DbContextOptions<DocumentsDbContext> _options = null!;
    private TestDbContextFactory _factory = null!;
    private static readonly Guid OwnerId = Guid.NewGuid();

    public DocumentBootstrapServicePostgresTests(PostgresFixture postgres) => _postgres = postgres;

    public async ValueTask InitializeAsync()
    {
        _options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseNpgsql(_postgres.ConnectionString)
            .Options;

        await using DocumentsDbContext init = new(_options);
        await init.Database.EnsureCreatedAsync();
        await init.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE documents_folders RESTART IDENTITY CASCADE;");

        _factory = new TestDbContextFactory(_options);
    }

    public ValueTask DisposeAsync() => default;

    [Fact]
    public async Task EnsureTenantRootAsync_FirstCall_CreatesRootOnPostgres()
    {
        var sut = new DocumentBootstrapService(_factory, new SimpleGuidGenerator());
        var tenant = Guid.NewGuid();

        Guid rootId = await sut.EnsureTenantRootAsync(tenant, OwnerId, TestContext.Current.CancellationToken);

        await using DocumentsDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        Folder root = await db.Folders.SingleAsync(f => f.Id == rootId, TestContext.Current.CancellationToken);
        root.IsTenantRoot.ShouldBeTrue();
        root.Path.ShouldBe("/");
    }

    [Fact]
    public async Task EnsureTenantRootAsync_HighConcurrency_StillProducesSingleRoot()
    {
        var tenant = Guid.NewGuid();
        const int parallelism = 32;

        DocumentBootstrapService[] services = Enumerable
            .Range(0, parallelism)
            .Select(_ => new DocumentBootstrapService(_factory, new SimpleGuidGenerator()))
            .ToArray();

        Task<Guid>[] tasks = services
            .Select(s => Task.Run(() => s.EnsureTenantRootAsync(tenant, OwnerId, TestContext.Current.CancellationToken)))
            .ToArray();

        Guid[] results = await Task.WhenAll(tasks);

        results.Distinct().ShouldHaveSingleItem();

        await using DocumentsDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        (await db.Folders.CountAsync(f => f.TenantId == tenant, TestContext.Current.CancellationToken))
            .ShouldBe(1);
    }

    [Fact]
    public async Task EnsureTenantRootAsync_PartialUniqueIndex_AllowsManyTenantsAndOneRootEach()
    {
        var sut = new DocumentBootstrapService(_factory, new SimpleGuidGenerator());
        Guid[] tenants = Enumerable.Range(0, 25).Select(_ => Guid.NewGuid()).ToArray();

        Guid[] roots = await Task.WhenAll(
            tenants.Select(t => sut.EnsureTenantRootAsync(t, OwnerId, TestContext.Current.CancellationToken)));

        roots.Distinct().Count().ShouldBe(tenants.Length);

        await using DocumentsDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        (await db.Folders.CountAsync(f => f.IsTenantRoot, TestContext.Current.CancellationToken))
            .ShouldBe(tenants.Length);
    }

    /// <summary>Lightweight DbContext factory wrapping fixed options for the tests.</summary>
    private sealed class TestDbContextFactory(DbContextOptions<DocumentsDbContext> options)
        : IDbContextFactory<DocumentsDbContext>
    {
        public DocumentsDbContext CreateDbContext() => new(options);

        public Task<DocumentsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<DocumentsDbContext>(new(options));
    }

    private sealed class SimpleGuidGenerator : IGuidGenerator
    {
        public Guid Create() => Guid.NewGuid();
    }
}
