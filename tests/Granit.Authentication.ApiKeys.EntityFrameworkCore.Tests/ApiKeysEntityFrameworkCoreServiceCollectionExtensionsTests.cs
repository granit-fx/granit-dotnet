using Granit.Authentication.ApiKeys.EntityFrameworkCore.Extensions;
using Granit.Authentication.ApiKeys.EntityFrameworkCore.Internal;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Interceptors;
using Granit.Timing;
using Granit.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Authentication.ApiKeys.EntityFrameworkCore.Tests;

public sealed class ApiKeysEntityFrameworkCoreServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitApiKeysEntityFrameworkCore_RegistersRequiredServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Substitute.For<ICurrentTenant>());

        services.AddGranitApiKeysEntityFrameworkCore(
            options => options.UseSqlite("DataSource=:memory:"));

        ServiceProvider provider = services.BuildServiceProvider();

        provider.GetService<IDbContextFactory<AuthenticationApiKeysDbContext>>().ShouldNotBeNull();
        provider.GetService<IApiKeyStore>().ShouldNotBeNull();
        provider.GetService<IApiKeyAdminStore>().ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitApiKeysEntityFrameworkCore_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        Should.Throw<ArgumentNullException>(
            () => services.AddGranitApiKeysEntityFrameworkCore(_ => { }));
    }

    [Fact]
    public void AddGranitApiKeysEntityFrameworkCore_NullConfigureAction_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(
            () => services.AddGranitApiKeysEntityFrameworkCore(null!));
    }

    [Fact]
    public void AddGranitApiKeysEntityFrameworkCore_DoesNotDuplicateOnSecondCall()
    {
        var services = new ServiceCollection();

        services.AddGranitApiKeysEntityFrameworkCore(
            options => options.UseSqlite("DataSource=:memory:"));
        services.AddGranitApiKeysEntityFrameworkCore(
            options => options.UseSqlite("DataSource=:memory:"));

        // TryAddScoped should prevent duplicate store registrations
        services.Count(s => s.ServiceType == typeof(IApiKeyStore)).ShouldBe(1);
        services.Count(s => s.ServiceType == typeof(IApiKeyAdminStore)).ShouldBe(1);
    }

    [Fact]
    public void AddGranitApiKeysEntityFrameworkCore_WithInterceptors_ResolvesFactory()
    {
        var services = new ServiceCollection();

        // Register real interceptors with substituted dependencies
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        services.AddSingleton(new AuditedEntityInterceptor(
            Substitute.For<ICurrentUserService>(),
            Substitute.For<IClock>(),
            Substitute.For<IGuidGenerator>(),
            currentTenant));
        services.AddSingleton(new SoftDeleteInterceptor(
            Substitute.For<ICurrentUserService>(),
            Substitute.For<IClock>()));

        services.AddGranitApiKeysEntityFrameworkCore(
            options => options.UseSqlite("DataSource=:memory:"));

        ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IDbContextFactory<AuthenticationApiKeysDbContext> factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AuthenticationApiKeysDbContext>>();
        factory.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitApiKeysEntityFrameworkCore_WithoutInterceptors_ResolvesFactory()
    {
        var services = new ServiceCollection();

        // No interceptors registered — the null path in the extension method
        services.AddGranitApiKeysEntityFrameworkCore(
            options => options.UseSqlite("DataSource=:memory:"));

        ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IDbContextFactory<AuthenticationApiKeysDbContext> factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AuthenticationApiKeysDbContext>>();
        factory.ShouldNotBeNull();
    }
}
