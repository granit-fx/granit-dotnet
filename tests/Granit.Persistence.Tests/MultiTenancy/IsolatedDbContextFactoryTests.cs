// =============================================================================
// Tests - IsolatedDbContextFactory<TContext>
// =============================================================================
// Vérifie le dispatch vers la factory keyed correspondant à la stratégie résolue,
// ainsi que le comportement fail-fast quand aucune factory n'est enregistrée.
//
// IDbContextFactory<StubIsolatedDbContext> est implémenté par une fake concrète
// (Castle.DynamicProxy ne peut pas générer un proxy pour une méthode renvoyant
// un type internal depuis un assembly externe).
// =============================================================================

using Granit.MultiTenancy;
using Granit.Persistence.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Persistence.Tests.MultiTenancy;

internal sealed class StubIsolatedDbContext(DbContextOptions<StubIsolatedDbContext> options)
    : DbContext(options);

public sealed class IsolatedDbContextFactoryTests
{
    private static readonly Guid TenantA = Guid.NewGuid();

    // -----------------------------------------------------------------------
    // Fake factory — compteur d'appels, sans dépendance NSubstitute
    // -----------------------------------------------------------------------

    private sealed class CountingFactory : IDbContextFactory<StubIsolatedDbContext>
    {
        private int _callCount;
        public int CallCount => _callCount;

        public StubIsolatedDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<StubIsolatedDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        public Task<StubIsolatedDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);
            return Task.FromResult(CreateDbContext());
        }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static ICurrentTenant MakeTenant(Guid? id)
    {
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.Id.Returns(id);
        tenant.IsAvailable.Returns(id.HasValue);
        return tenant;
    }

    private static ITenantIsolationStrategyProvider MakeStrategy(TenantIsolationStrategy strategy)
    {
        ITenantIsolationStrategyProvider provider = Substitute.For<ITenantIsolationStrategyProvider>();
#pragma warning disable CA2012 // NSubstitute setup pattern
        provider.GetStrategyAsync(Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(strategy));
#pragma warning restore CA2012
        return provider;
    }

    private static IsolatedDbContextFactory<StubIsolatedDbContext> BuildFacade(
        ICurrentTenant tenant,
        ITenantIsolationStrategyProvider strategy,
        Action<IServiceCollection>? configure = null)
    {
        ServiceCollection services = new();
        configure?.Invoke(services);
        IServiceProvider sp = services.BuildServiceProvider();

        return new IsolatedDbContextFactory<StubIsolatedDbContext>(
            tenant,
            strategy,
            sp,
            NullLogger<IsolatedDbContextFactory<StubIsolatedDbContext>>.Instance);
    }

    // -----------------------------------------------------------------------
    // Dispatch vers SharedDatabase
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreateDbContextAsync_SharedDatabaseStrategy_CallsSharedFactory()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        CountingFactory factory = new();

        IsolatedDbContextFactory<StubIsolatedDbContext> facade = BuildFacade(
            MakeTenant(TenantA),
            MakeStrategy(TenantIsolationStrategy.SharedDatabase),
            svc => svc.AddKeyedScoped<IDbContextFactory<StubIsolatedDbContext>>(
                TenantIsolationStrategy.SharedDatabase, (_, _) => factory));

        await using StubIsolatedDbContext _ = await facade.CreateDbContextAsync(cancellationToken);

        factory.CallCount.ShouldBe(1);
    }

    // -----------------------------------------------------------------------
    // Dispatch vers DatabasePerTenant
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreateDbContextAsync_DatabasePerTenantStrategy_CallsPerDatabaseFactory()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        CountingFactory factory = new();

        IsolatedDbContextFactory<StubIsolatedDbContext> facade = BuildFacade(
            MakeTenant(TenantA),
            MakeStrategy(TenantIsolationStrategy.DatabasePerTenant),
            svc => svc.AddKeyedScoped<IDbContextFactory<StubIsolatedDbContext>>(
                TenantIsolationStrategy.DatabasePerTenant, (_, _) => factory));

        await using StubIsolatedDbContext _ = await facade.CreateDbContextAsync(cancellationToken);

        factory.CallCount.ShouldBe(1);
    }

    // -----------------------------------------------------------------------
    // Dispatch vers SchemaPerTenant
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreateDbContextAsync_SchemaPerTenantStrategy_CallsPerSchemaFactory()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        CountingFactory factory = new();

        IsolatedDbContextFactory<StubIsolatedDbContext> facade = BuildFacade(
            MakeTenant(TenantA),
            MakeStrategy(TenantIsolationStrategy.SchemaPerTenant),
            svc => svc.AddKeyedScoped<IDbContextFactory<StubIsolatedDbContext>>(
                TenantIsolationStrategy.SchemaPerTenant, (_, _) => factory));

        await using StubIsolatedDbContext _ = await facade.CreateDbContextAsync(cancellationToken);

        factory.CallCount.ShouldBe(1);
    }

    // -----------------------------------------------------------------------
    // Provider dynamique custom — dispatch conditionnel par tenant
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreateDbContextAsync_CustomDynamicProvider_DispatchesPerTenant()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        CountingFactory premiumFactory = new();
        CountingFactory standardFactory = new();

        var premiumTenantId = Guid.NewGuid();

        // Provider custom : tenant premium → DatabasePerTenant, sinon SharedDatabase.
        ITenantIsolationStrategyProvider dynamicProvider =
            Substitute.For<ITenantIsolationStrategyProvider>();
#pragma warning disable CA2012 // NSubstitute setup pattern
        dynamicProvider.GetStrategyAsync(premiumTenantId, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(TenantIsolationStrategy.DatabasePerTenant));
        dynamicProvider.GetStrategyAsync(
                Arg.Is<Guid?>(id => id != premiumTenantId), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(TenantIsolationStrategy.SharedDatabase));
#pragma warning restore CA2012

        IsolatedDbContextFactory<StubIsolatedDbContext> facade = BuildFacade(
            MakeTenant(premiumTenantId),
            dynamicProvider,
            svc =>
            {
                svc.AddKeyedScoped<IDbContextFactory<StubIsolatedDbContext>>(
                    TenantIsolationStrategy.DatabasePerTenant, (_, _) => premiumFactory);
                svc.AddKeyedScoped<IDbContextFactory<StubIsolatedDbContext>>(
                    TenantIsolationStrategy.SharedDatabase, (_, _) => standardFactory);
            });

        await using StubIsolatedDbContext _ = await facade.CreateDbContextAsync(cancellationToken);

        premiumFactory.CallCount.ShouldBe(1);
        standardFactory.CallCount.ShouldBe(0);
    }

    // -----------------------------------------------------------------------
    // Fail-fast — stratégie sans factory enregistrée
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreateDbContextAsync_MissingFactory_ThrowsInvalidOperationException()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        // Aucune factory keyed SchemaPerTenant enregistrée.
        IsolatedDbContextFactory<StubIsolatedDbContext> facade = BuildFacade(
            MakeTenant(TenantA),
            MakeStrategy(TenantIsolationStrategy.SchemaPerTenant));

        Func<Task> act = async () => await facade.CreateDbContextAsync(cancellationToken);

        (await Should.ThrowAsync<InvalidOperationException>(act)).Message.ShouldContain("SchemaPerTenant");
    }
}
