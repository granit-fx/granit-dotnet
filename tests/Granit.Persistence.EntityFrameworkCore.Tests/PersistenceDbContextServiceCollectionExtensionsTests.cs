using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests;

public sealed class PersistenceDbContextServiceCollectionExtensionsTests
{
    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options);

    [Fact]
    public void AddGranitDbContext_RegistersDbContextFactory_Scoped()
    {
        ServiceCollection services = new();
        AddRequiredDependencies(services);
        services.AddGranitPersistence();

        services.AddGranitDbContext<TestDbContext>(options =>
            options.UseInMemoryDatabase("test-lifetime"));

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IDbContextFactory<TestDbContext>));
        descriptor.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitDbContext_ReturnsServiceCollectionForChaining()
    {
        ServiceCollection services = new();

        IServiceCollection result = services.AddGranitDbContext<TestDbContext>(
            options => options.UseInMemoryDatabase("test-chain"));

        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddGranitDbContext_ThrowsOnNullServices()
    {
        Should.Throw<ArgumentNullException>(() =>
            PersistenceDbContextServiceCollectionExtensions.AddGranitDbContext<TestDbContext>(
                null!, _ => { }));
    }

    [Fact]
    public void AddGranitDbContext_ThrowsOnNullConfigure()
    {
        ServiceCollection services = new();

        Should.Throw<ArgumentNullException>(() =>
            services.AddGranitDbContext<TestDbContext>(null!));
    }

    [Fact]
    public void AddGranitDbContext_CanCreateDbContext()
    {
        ServiceCollection services = new();
        AddRequiredDependencies(services);
        services.AddGranitPersistence();
        services.AddGranitDbContext<TestDbContext>(options =>
            options.UseInMemoryDatabase("test-create"));

        using ServiceProvider sp = services.BuildServiceProvider();
        IDbContextFactory<TestDbContext> factory =
            sp.GetRequiredService<IDbContextFactory<TestDbContext>>();

        using TestDbContext ctx = factory.CreateDbContext();

        ctx.ShouldNotBeNull();
    }

    private static void AddRequiredDependencies(ServiceCollection services)
    {
        services.AddSingleton(NSubstitute.Substitute.For<Granit.Timing.IClock>());
        services.AddSingleton(NSubstitute.Substitute.For<Granit.Guids.IGuidGenerator>());
        services.AddSingleton(NSubstitute.Substitute.For<Granit.Users.ICurrentUserService>());
        services.AddSingleton(NSubstitute.Substitute.For<ICurrentTenant>());
        services.AddLogging();
        services.AddMetrics();
    }
}
