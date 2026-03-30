using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests;

public sealed class DbContextOptionsBuilderExtensionsTests
{
    [Fact]
    public void UseGranitInterceptors_AddsAllRegisteredInterceptors()
    {
        // Arrange
        ServiceCollection services = new();
        AddRequiredDependencies(services);
        services.AddGranitPersistence();

        using ServiceProvider sp = services.BuildServiceProvider();
        using IServiceScope scope = sp.CreateScope();

        DbContextOptionsBuilder builder = new();

        // Act
        builder.UseGranitInterceptors(scope.ServiceProvider);

        // Assert — all 5 interceptors should be present
        DbContextOptions options = builder.Options;
        IEnumerable<IInterceptor> interceptors = options.Extensions
            .OfType<Microsoft.EntityFrameworkCore.Infrastructure.CoreOptionsExtension>()
            .SelectMany(e => e.Interceptors ?? []);

        interceptors.ShouldContain(i => i is AuditedEntityInterceptor);
        interceptors.ShouldContain(i => i is VersioningInterceptor);
        interceptors.ShouldContain(i => i is ConcurrencyStampInterceptor);
        interceptors.ShouldContain(i => i is DomainEventDispatcherInterceptor);
        interceptors.ShouldContain(i => i is SoftDeleteInterceptor);
    }

    [Fact]
    public void UseGranitInterceptors_SkipsUnregisteredInterceptors()
    {
        // Arrange — empty service provider with no interceptors registered
        ServiceCollection services = new();
        using ServiceProvider sp = services.BuildServiceProvider();

        DbContextOptionsBuilder builder = new();

        // Act — should not throw
        builder.UseGranitInterceptors(sp);

        // Assert — no interceptors added
        DbContextOptions options = builder.Options;
        IEnumerable<IInterceptor> interceptors = options.Extensions
            .OfType<Microsoft.EntityFrameworkCore.Infrastructure.CoreOptionsExtension>()
            .SelectMany(e => e.Interceptors ?? []);

        interceptors.ShouldBeEmpty();
    }

    [Fact]
    public void UseGranitInterceptors_ThrowsOnNullOptions()
    {
        using ServiceProvider sp = new ServiceCollection().BuildServiceProvider();
        Should.Throw<ArgumentNullException>(() =>
            DbContextOptionsBuilderExtensions.UseGranitInterceptors(null!, sp));
    }

    [Fact]
    public void UseGranitInterceptors_ThrowsOnNullServiceProvider()
    {
        DbContextOptionsBuilder builder = new();
        Should.Throw<ArgumentNullException>(() =>
            builder.UseGranitInterceptors(null!));
    }

    [Fact]
    public void UseGranitInterceptors_ReturnsBuilderForChaining()
    {
        // Arrange
        using ServiceProvider sp = new ServiceCollection().BuildServiceProvider();
        DbContextOptionsBuilder builder = new();

        // Act
        DbContextOptionsBuilder result = builder.UseGranitInterceptors(sp);

        // Assert
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void AddGranitDbContext_RegistersDbContextFactory()
    {
        // Arrange
        ServiceCollection services = new();
        AddRequiredDependencies(services);
        services.AddGranitPersistence();

        // Act
        services.AddGranitDbContext<TestDbContext>(options =>
            options.UseInMemoryDatabase("test-db"));

        using ServiceProvider sp = services.BuildServiceProvider();

        // Assert — factory should be resolvable
        IDbContextFactory<TestDbContext> factory = sp.GetRequiredService<IDbContextFactory<TestDbContext>>();
        factory.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitDbContext_WiresGranitInterceptors()
    {
        // Arrange
        ServiceCollection services = new();
        AddRequiredDependencies(services);
        services.AddGranitPersistence();
        services.AddGranitDbContext<TestDbContext>(options =>
            options.UseInMemoryDatabase("test-interceptors"));

        using ServiceProvider sp = services.BuildServiceProvider();

        // Act
        using TestDbContext context = sp.GetRequiredService<IDbContextFactory<TestDbContext>>()
            .CreateDbContext();

        // Assert — interceptors should be wired (verify via options extensions)
        IEnumerable<IInterceptor> interceptors = context.GetService<DbContextOptions>()!.Extensions
            .OfType<Microsoft.EntityFrameworkCore.Infrastructure.CoreOptionsExtension>()
            .SelectMany(e => e.Interceptors ?? []);

        interceptors.ShouldContain(i => i is AuditedEntityInterceptor);
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

    private static void AddRequiredDependencies(ServiceCollection services)
    {
        services.AddSingleton(NSubstitute.Substitute.For<Granit.Timing.IClock>());
        services.AddSingleton(NSubstitute.Substitute.For<Granit.Guids.IGuidGenerator>());
        services.AddSingleton(NSubstitute.Substitute.For<Granit.Users.ICurrentUserService>());
        services.AddSingleton(NSubstitute.Substitute.For<ICurrentTenant>());
        services.AddLogging();
        services.AddMetrics();
    }

    /// <summary>Minimal test DbContext for AddGranitDbContext tests.</summary>
    private sealed class TestDbContext(DbContextOptions<TestDbContext> options)
        : DbContext(options);
}
