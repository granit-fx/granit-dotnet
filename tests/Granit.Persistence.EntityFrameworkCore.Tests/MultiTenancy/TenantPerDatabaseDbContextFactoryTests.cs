// =============================================================================
// Tests - TenantPerDatabaseDbContextFactory<TContext>
// =============================================================================
// Verifies tenant routing, missing-tenant guard, provider call correctness,
// async isolation between concurrent tenant contexts, and PHYSICAL data
// isolation between per-tenant databases (SQLite named in-memory databases —
// the InMemory provider proved nothing about isolation).
// =============================================================================

using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests.MultiTenancy;

// ---------------------------------------------------------------------------
// Minimal DbContext stub — constructor (DbContextOptions<T>) required
// by TenantPerDatabaseDbContextFactory<T>'s Activator.CreateInstance path.
// ---------------------------------------------------------------------------
internal sealed class StubPerTenantDbContext(DbContextOptions<StubPerTenantDbContext> options)
    : DbContext(options)
{
    public DbSet<StubTenantRow> Rows => Set<StubTenantRow>();
}

internal sealed class StubTenantRow
{
    public Guid Id { get; set; }
    public string Label { get; set; } = string.Empty;
}

public sealed class TenantPerDatabaseDbContextFactoryTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();

    private const string ConnA = "Data Source=file:tenant_a?mode=memory&cache=shared";
    private const string ConnB = "Data Source=file:tenant_b?mode=memory&cache=shared";

    // -----------------------------------------------------------------------
    // Helper — builds a factory with fully controlled dependencies.
    // -----------------------------------------------------------------------

    private static (TenantPerDatabaseDbContextFactory<StubPerTenantDbContext> factory,
                    ITenantConnectionStringProvider provider)
        BuildFactoryWithProvider(Guid? tenantId, string connectionString)
    {
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Id.Returns(tenantId);

        ITenantConnectionStringProvider provider =
            Substitute.For<ITenantConnectionStringProvider>();
        provider
            .GetConnectionStringAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(connectionString));

        ServiceCollection services = new();
        IServiceProvider sp = services.BuildServiceProvider();

        TenantPerDatabaseDbContextOptions<StubPerTenantDbContext> options = new()
        {
            Configure = static (opts, cs) => opts.UseSqlite(cs),
        };

        return (new TenantPerDatabaseDbContextFactory<StubPerTenantDbContext>(
            currentTenant, provider, sp, options), provider);
    }

    private static TenantPerDatabaseDbContextFactory<StubPerTenantDbContext> BuildFactory(
        Guid? tenantId, string connectionString) =>
        BuildFactoryWithProvider(tenantId, connectionString).factory;

    // -----------------------------------------------------------------------
    // CreateDbContextAsync — happy path
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreateDbContextAsync_WhenTenantActive_ReturnsDbContext()
    {
        TenantPerDatabaseDbContextFactory<StubPerTenantDbContext> factory =
            BuildFactory(TenantA, ConnA);

        await using StubPerTenantDbContext ctx =
            await factory.CreateDbContextAsync(TestContext.Current.CancellationToken);

        ctx.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateDbContextAsync_WhenTenantActive_CallsProviderWithCorrectTenantId()
    {
        (TenantPerDatabaseDbContextFactory<StubPerTenantDbContext> factory,
         ITenantConnectionStringProvider provider) =
            BuildFactoryWithProvider(TenantA, ConnA);

        await using StubPerTenantDbContext ctx =
            await factory.CreateDbContextAsync(TestContext.Current.CancellationToken);

        _ = provider.Received(1).GetConnectionStringAsync(TenantA, Arg.Any<CancellationToken>());
        _ = ctx;
    }

    [Fact]
    public async Task CreateDbContextAsync_TenantB_CallsProviderWithTenantBId()
    {
        (TenantPerDatabaseDbContextFactory<StubPerTenantDbContext> factory,
         ITenantConnectionStringProvider provider) =
            BuildFactoryWithProvider(TenantB, ConnB);

        await using StubPerTenantDbContext ctx =
            await factory.CreateDbContextAsync(TestContext.Current.CancellationToken);

        _ = provider.Received(1).GetConnectionStringAsync(TenantB, Arg.Any<CancellationToken>());
        _ = ctx;
    }

    // -----------------------------------------------------------------------
    // CreateDbContextAsync — missing tenant guard (ISO 27001: no silent fallback)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreateDbContextAsync_WhenNoTenantActive_ThrowsInvalidOperationException()
    {
        TenantPerDatabaseDbContextFactory<StubPerTenantDbContext> factory =
            BuildFactory(tenantId: null, connectionString: ConnA);

        Func<Task> act = async () =>
            await factory.CreateDbContextAsync(TestContext.Current.CancellationToken);

        (await Should.ThrowAsync<InvalidOperationException>(act)).Message.ShouldContain("No active tenant context");
    }

    [Fact]
    public async Task CreateDbContextAsync_WhenNoTenantActive_DoesNotCallProvider()
    {
        (TenantPerDatabaseDbContextFactory<StubPerTenantDbContext> factory,
         ITenantConnectionStringProvider provider) =
            BuildFactoryWithProvider(tenantId: null, connectionString: ConnA);

        try
        {
            await factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        }
        catch (InvalidOperationException) { }

        _ = provider.DidNotReceiveWithAnyArgs()
            .GetConnectionStringAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // CreateDbContext — synchronous overload
    // -----------------------------------------------------------------------

    [Fact]
    public void CreateDbContext_WhenTenantActive_ReturnsDbContext()
    {
        TenantPerDatabaseDbContextFactory<StubPerTenantDbContext> factory =
            BuildFactory(TenantA, ConnA);

        using StubPerTenantDbContext ctx = factory.CreateDbContext();

        ctx.ShouldNotBeNull();
    }

    [Fact]
    public void CreateDbContext_WhenNoTenantActive_ThrowsInvalidOperationException()
    {
        TenantPerDatabaseDbContextFactory<StubPerTenantDbContext> factory =
            BuildFactory(tenantId: null, connectionString: ConnA);

        Action act = () => factory.CreateDbContext();

        Should.Throw<InvalidOperationException>(act).Message.ShouldContain("No active tenant context");
    }

    // -----------------------------------------------------------------------
    // Async isolation — two concurrent tenants must not share connection strings
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreateDbContextAsync_ConcurrentTenants_IsolateConnectionStrings()
    {
        List<string> capturedConnections = [];

        ICurrentTenant tenantA = Substitute.For<ICurrentTenant>();
        tenantA.Id.Returns(TenantA);

        ICurrentTenant tenantB = Substitute.For<ICurrentTenant>();
        tenantB.Id.Returns(TenantB);

        ITenantConnectionStringProvider provider =
            Substitute.For<ITenantConnectionStringProvider>();
        provider.GetConnectionStringAsync(TenantA, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ConnA));
        provider.GetConnectionStringAsync(TenantB, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ConnB));

        ServiceCollection services = new();
        IServiceProvider sp = services.BuildServiceProvider();

        TenantPerDatabaseDbContextOptions<StubPerTenantDbContext> options = new()
        {
            Configure = (opts, cs) =>
            {
                capturedConnections.Add(cs);
                opts.UseSqlite(cs);
            },
        };

        TenantPerDatabaseDbContextFactory<StubPerTenantDbContext> factoryA =
            new(tenantA, provider, sp, options);
        TenantPerDatabaseDbContextFactory<StubPerTenantDbContext> factoryB =
            new(tenantB, provider, sp, options);

        await using StubPerTenantDbContext ctxA =
            await factoryA.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await using StubPerTenantDbContext ctxB =
            await factoryB.CreateDbContextAsync(TestContext.Current.CancellationToken);

        capturedConnections.ShouldContain(ConnA);
        capturedConnections.ShouldContain(ConnB);
        capturedConnections.Distinct().Count().ShouldBe(capturedConnections.Count);
    }

    // -----------------------------------------------------------------------
    // Physical isolation — tenant A's rows never appear in tenant B's database
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreateDbContextAsync_TenantData_IsPhysicallyIsolatedPerDatabase()
    {
        // Keep-alive connections pin the two named in-memory databases for the
        // duration of the test (cache=shared makes them addressable by name).
        await using Microsoft.Data.Sqlite.SqliteConnection keepAliveA = new(ConnA);
        await using Microsoft.Data.Sqlite.SqliteConnection keepAliveB = new(ConnB);
        await keepAliveA.OpenAsync(TestContext.Current.CancellationToken);
        await keepAliveB.OpenAsync(TestContext.Current.CancellationToken);

        string label = $"iso-{Guid.NewGuid():N}";

        TenantPerDatabaseDbContextFactory<StubPerTenantDbContext> factoryA =
            BuildFactory(TenantA, ConnA);
        await using (StubPerTenantDbContext ctxA =
            await factoryA.CreateDbContextAsync(TestContext.Current.CancellationToken))
        {
            await ctxA.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            ctxA.Rows.Add(new StubTenantRow { Id = Guid.NewGuid(), Label = label });
            await ctxA.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        TenantPerDatabaseDbContextFactory<StubPerTenantDbContext> factoryB =
            BuildFactory(TenantB, ConnB);
        await using (StubPerTenantDbContext ctxB =
            await factoryB.CreateDbContextAsync(TestContext.Current.CancellationToken))
        {
            await ctxB.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            (await ctxB.Rows.CountAsync(r => r.Label == label, TestContext.Current.CancellationToken))
                .ShouldBe(0, "tenant B's database must not contain tenant A's rows");
        }

        // Sanity: the row does exist in tenant A's database.
        TenantPerDatabaseDbContextFactory<StubPerTenantDbContext> factoryA2 =
            BuildFactory(TenantA, ConnA);
        await using StubPerTenantDbContext ctxA2 =
            await factoryA2.CreateDbContextAsync(TestContext.Current.CancellationToken);
        (await ctxA2.Rows.CountAsync(r => r.Label == label, TestContext.Current.CancellationToken))
            .ShouldBe(1);
    }
}
