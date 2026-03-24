using Granit.MultiTenancy;
using Granit.Persistence.Interceptors;
using Granit.Persistence.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Persistence.Tests.MultiTenancy;

internal sealed class StubSharedDbContext(DbContextOptions<StubSharedDbContext> options)
    : DbContext(options);

public sealed class SharedDatabaseDbContextFactoryTests
{
    [Fact]
    public void CreateDbContext_ReturnsNonNullContext()
    {
        ServiceCollection services = new();
        using ServiceProvider sp = services.BuildServiceProvider();

        SharedDatabaseDbContextOptions<StubSharedDbContext> opts = new()
        {
            Configure = static builder => builder.UseInMemoryDatabase("shared-test"),
        };

        SharedDatabaseDbContextFactory<StubSharedDbContext> factory = new(sp, opts);

        using StubSharedDbContext ctx = factory.CreateDbContext();

        ctx.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateDbContextAsync_ReturnsNonNullContext()
    {
        ServiceCollection services = new();
        using ServiceProvider sp = services.BuildServiceProvider();

        SharedDatabaseDbContextOptions<StubSharedDbContext> opts = new()
        {
            Configure = static builder => builder.UseInMemoryDatabase("shared-test-async"),
        };

        SharedDatabaseDbContextFactory<StubSharedDbContext> factory = new(sp, opts);

        await using StubSharedDbContext ctx = await factory.CreateDbContextAsync(
            TestContext.Current.CancellationToken);

        ctx.ShouldNotBeNull();
    }

    [Fact]
    public void CreateDbContext_WithAuditInterceptorRegistered_ReturnsNonNullContext()
    {
        ServiceCollection services = new();
        services.AddSingleton(Substitute.For<Granit.Timing.IClock>());
        services.AddSingleton(Substitute.For<Granit.Guids.IGuidGenerator>());
        services.AddSingleton(Substitute.For<Granit.Users.ICurrentUserService>());
        services.AddSingleton(Substitute.For<ICurrentTenant>());
        services.AddScoped<AuditedEntityInterceptor>();
        using ServiceProvider sp = services.BuildServiceProvider();
        using IServiceScope scope = sp.CreateScope();

        SharedDatabaseDbContextOptions<StubSharedDbContext> opts = new()
        {
            Configure = static builder => builder.UseInMemoryDatabase("shared-audit"),
        };

        SharedDatabaseDbContextFactory<StubSharedDbContext> factory =
            new(scope.ServiceProvider, opts);

        using StubSharedDbContext ctx = factory.CreateDbContext();

        // Context should be created successfully with AuditedEntityInterceptor wired
        ctx.ShouldNotBeNull();
    }

    [Fact]
    public void CreateDbContext_WithoutAuditInterceptor_DoesNotThrow()
    {
        ServiceCollection services = new();
        using ServiceProvider sp = services.BuildServiceProvider();

        SharedDatabaseDbContextOptions<StubSharedDbContext> opts = new()
        {
            Configure = static builder => builder.UseInMemoryDatabase("shared-no-audit"),
        };

        SharedDatabaseDbContextFactory<StubSharedDbContext> factory = new(sp, opts);

        using StubSharedDbContext ctx = factory.CreateDbContext();

        ctx.ShouldNotBeNull();
    }
}
