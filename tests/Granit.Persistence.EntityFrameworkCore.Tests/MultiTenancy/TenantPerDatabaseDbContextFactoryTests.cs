// =============================================================================
// Tests - TenantPerDatabaseDbContextFactory<TContext>
// =============================================================================
// Verifies tenant routing, missing-tenant guard, provider call correctness,
// and async isolation between concurrent tenant contexts.
// No real database connection required: DbContextOptions are built but the
// connection is never opened (only happens on query execution, not construction).
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
    : DbContext(options);

public sealed class TenantPerDatabaseDbContextFactoryTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();

    private const string ConnA = "Host=host-a;Database=db_a;Username=usr;Password=pwd";
    private const string ConnB = "Host=host-b;Database=db_b;Username=usr;Password=pwd";

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
            Configure = static (opts, cs) => opts.UseInMemoryDatabase(cs),
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
                opts.UseInMemoryDatabase(cs);
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
}
