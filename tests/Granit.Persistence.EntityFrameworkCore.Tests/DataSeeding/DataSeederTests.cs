// =============================================================================
// Tests — DataSeeder
// =============================================================================
// Vérifie que l'orchestrateur de seeding :
//   - Exécute les IHostDataSeedContributor dans SeedHostAsync
//   - Exécute les ITenantDataSeedContributor par tenant dans SeedTenantsAsync
//   - Gère le backward compat avec IDataSeedContributor (legacy)
//   - Continue l'exécution si un contributeur échoue (résilience)
//   - Propage OperationCanceledException sans l'attraper
//   - Active le contexte tenant via ICurrentTenant.Change()
// =============================================================================

using Granit.MultiTenancy;
using Granit.Persistence.DataSeeding;
using Granit.Persistence.EntityFrameworkCore.DataSeeding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests.DataSeeding;

public sealed class DataSeederTests
{
    private readonly ILogger<DataSeeder> _logger = NullLogger<DataSeeder>.Instance;

    // =========================================================================
    // SeedAsync — backward compat (calls both SeedHostAsync + SeedTenantsAsync)
    // =========================================================================

    [Fact]
    public async Task SeedAsync_NoContributors_CompletesWithoutError()
    {
        // Arrange
        ServiceCollection services = new();
        await using ServiceProvider sp = services.BuildServiceProvider();
        DataSeeder seeder = new(sp.GetRequiredService<IServiceScopeFactory>(), _logger);
        DataSeedContext context = new();

        // Act
        Func<Task> act = () => seeder.SeedAsync(context, TestContext.Current.CancellationToken);

        // Assert
        await Should.NotThrowAsync(act);
    }

#pragma warning disable CS0618 // Obsolete IDataSeedContributor: testing backward compat
    [Fact]
    public async Task SeedAsync_SingleLegacyContributor_CallsSeedAsyncTwice()
    {
        // Arrange — legacy contributor runs in both host pass (IsHostOnly=true) and tenant pass
        IDataSeedContributor contributor = Substitute.For<IDataSeedContributor>();
        ServiceCollection services = new();
        services.AddTransient(_ => contributor);
        await using ServiceProvider sp = services.BuildServiceProvider();
        DataSeeder seeder = new(sp.GetRequiredService<IServiceScopeFactory>(), _logger);
        DataSeedContext context = new();

        // Act
        await seeder.SeedAsync(context, TestContext.Current.CancellationToken);

        // Assert — called twice: once with IsHostOnly=true, once with IsHostOnly=false
        await contributor.Received(2).SeedAsync(Arg.Any<DataSeedContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedAsync_CallsBothHostAndTenantPhases()
    {
        // Arrange
        IHostDataSeedContributor hostContributor = Substitute.For<IHostDataSeedContributor>();
        ITenantDataSeedContributor tenantContributor = Substitute.For<ITenantDataSeedContributor>();
        ServiceCollection services = new();
        services.AddTransient(_ => hostContributor);
        services.AddTransient(_ => tenantContributor);
        await using ServiceProvider sp = services.BuildServiceProvider();
        DataSeeder seeder = new(sp.GetRequiredService<IServiceScopeFactory>(), _logger);
        DataSeedContext context = new();

        // Act
        await seeder.SeedAsync(context, TestContext.Current.CancellationToken);

        // Assert
        await hostContributor.Received(1).SeedAsync(Arg.Any<DataSeedContext>(), Arg.Any<CancellationToken>());
        await tenantContributor.Received(1).SeedAsync(Arg.Any<DataSeedContext>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // SeedHostAsync
    // =========================================================================

    [Fact]
    public async Task SeedHostAsync_ExecutesHostContributors()
    {
        // Arrange
        IHostDataSeedContributor contributor1 = Substitute.For<IHostDataSeedContributor>();
        IHostDataSeedContributor contributor2 = Substitute.For<IHostDataSeedContributor>();
        ServiceCollection services = new();
        services.AddTransient(_ => contributor1);
        services.AddTransient(_ => contributor2);
        await using ServiceProvider sp = services.BuildServiceProvider();
        DataSeeder seeder = new(sp.GetRequiredService<IServiceScopeFactory>(), _logger);

        // Act
        await seeder.SeedHostAsync(new DataSeedContext(), TestContext.Current.CancellationToken);

        // Assert
        await contributor1.Received(1).SeedAsync(Arg.Any<DataSeedContext>(), Arg.Any<CancellationToken>());
        await contributor2.Received(1).SeedAsync(Arg.Any<DataSeedContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedHostAsync_ExecutesLegacyContributorsWithIsHostOnly()
    {
        // Arrange
        DataSeedContext? capturedContext = null;
        IDataSeedContributor legacy = Substitute.For<IDataSeedContributor>();
        legacy
            .SeedAsync(Arg.Any<DataSeedContext>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedContext = callInfo.Arg<DataSeedContext>();
                return Task.CompletedTask;
            });

        ServiceCollection services = new();
        services.AddTransient(_ => legacy);
        await using ServiceProvider sp = services.BuildServiceProvider();
        DataSeeder seeder = new(sp.GetRequiredService<IServiceScopeFactory>(), _logger);

        // Act
        await seeder.SeedHostAsync(new DataSeedContext(), TestContext.Current.CancellationToken);

        // Assert
        capturedContext.ShouldNotBeNull();
        capturedContext!.IsHostOnly.ShouldBeTrue();
    }
#pragma warning restore CS0618

    [Fact]
    public async Task SeedHostAsync_ContributorThrows_ContinuesWithRemaining()
    {
        // Arrange
        IHostDataSeedContributor failing = Substitute.For<IHostDataSeedContributor>();
        failing
            .SeedAsync(Arg.Any<DataSeedContext>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Seed failed"));

        IHostDataSeedContributor success = Substitute.For<IHostDataSeedContributor>();

        ServiceCollection services = new();
        services.AddTransient(_ => failing);
        services.AddTransient(_ => success);
        await using ServiceProvider sp = services.BuildServiceProvider();
        DataSeeder seeder = new(sp.GetRequiredService<IServiceScopeFactory>(), _logger);

        // Act
        await seeder.SeedHostAsync(new DataSeedContext(), TestContext.Current.CancellationToken);

        // Assert
        await success.Received(1).SeedAsync(Arg.Any<DataSeedContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedHostAsync_OperationCanceledException_Propagates()
    {
        // Arrange
        IHostDataSeedContributor contributor = Substitute.For<IHostDataSeedContributor>();
        contributor
            .SeedAsync(Arg.Any<DataSeedContext>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        ServiceCollection services = new();
        services.AddTransient(_ => contributor);
        await using ServiceProvider sp = services.BuildServiceProvider();
        DataSeeder seeder = new(sp.GetRequiredService<IServiceScopeFactory>(), _logger);

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(
            () => seeder.SeedHostAsync(new DataSeedContext(), TestContext.Current.CancellationToken));
    }

    // =========================================================================
    // SeedTenantsAsync
    // =========================================================================

#pragma warning disable CS0618 // Obsolete IDataSeedContributor: testing backward compat
    [Fact]
    public async Task SeedTenantsAsync_ExecutesLegacyContributorsWithoutIsHostOnly()
    {
        // Arrange
        DataSeedContext? capturedContext = null;
        IDataSeedContributor legacy = Substitute.For<IDataSeedContributor>();
        legacy
            .SeedAsync(Arg.Any<DataSeedContext>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedContext = callInfo.Arg<DataSeedContext>();
                return Task.CompletedTask;
            });

        ServiceCollection services = new();
        services.AddTransient(_ => legacy);
        await using ServiceProvider sp = services.BuildServiceProvider();
        DataSeeder seeder = new(sp.GetRequiredService<IServiceScopeFactory>(), _logger);

        // Act
        await seeder.SeedTenantsAsync(new DataSeedContext(), TestContext.Current.CancellationToken);

        // Assert
        capturedContext.ShouldNotBeNull();
        capturedContext!.IsHostOnly.ShouldBeFalse();
    }
#pragma warning restore CS0618

    [Fact]
    public async Task SeedTenantsAsync_NoTenantProvider_ExecutesTenantContributorsOnce()
    {
        // Arrange — no IDataSeedTenantProvider registered (single-tenant)
        ITenantDataSeedContributor contributor = Substitute.For<ITenantDataSeedContributor>();
        ServiceCollection services = new();
        services.AddTransient(_ => contributor);
        await using ServiceProvider sp = services.BuildServiceProvider();
        DataSeeder seeder = new(sp.GetRequiredService<IServiceScopeFactory>(), _logger);

        // Act
        await seeder.SeedTenantsAsync(new DataSeedContext(), TestContext.Current.CancellationToken);

        // Assert — called once without tenant context
        await contributor.Received(1).SeedAsync(Arg.Any<DataSeedContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedTenantsAsync_WithTenantProvider_ExecutesPerTenant()
    {
        // Arrange
        var tenant1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var tenant2 = Guid.Parse("22222222-2222-2222-2222-222222222222");

        ITenantDataSeedContributor contributor = Substitute.For<ITenantDataSeedContributor>();
        IDataSeedTenantProvider tenantProvider = Substitute.For<IDataSeedTenantProvider>();
        tenantProvider.GetTenantIdsAsync(Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(tenant1, tenant2));

        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>())
            .Returns(Substitute.For<IDisposable>());

        ServiceCollection services = new();
        services.AddTransient(_ => contributor);
        services.AddSingleton(tenantProvider);
        services.AddScoped(_ => currentTenant);
        await using ServiceProvider sp = services.BuildServiceProvider();
        DataSeeder seeder = new(sp.GetRequiredService<IServiceScopeFactory>(), _logger);

        // Act
        await seeder.SeedTenantsAsync(new DataSeedContext(), TestContext.Current.CancellationToken);

        // Assert — called once per tenant
        await contributor.Received(2).SeedAsync(Arg.Any<DataSeedContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedTenantsAsync_ActivatesTenantContext_PerTenant()
    {
        // Arrange
        var tenant1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var tenant2 = Guid.Parse("22222222-2222-2222-2222-222222222222");

        List<Guid?> capturedTenantIds = [];
        ITenantDataSeedContributor contributor = Substitute.For<ITenantDataSeedContributor>();
        contributor
            .SeedAsync(Arg.Any<DataSeedContext>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedTenantIds.Add(callInfo.Arg<DataSeedContext>().TenantId);
                return Task.CompletedTask;
            });

        IDataSeedTenantProvider tenantProvider = Substitute.For<IDataSeedTenantProvider>();
        tenantProvider.GetTenantIdsAsync(Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(tenant1, tenant2));

        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>())
            .Returns(Substitute.For<IDisposable>());

        ServiceCollection services = new();
        services.AddTransient(_ => contributor);
        services.AddSingleton(tenantProvider);
        services.AddScoped(_ => currentTenant);
        await using ServiceProvider sp = services.BuildServiceProvider();
        DataSeeder seeder = new(sp.GetRequiredService<IServiceScopeFactory>(), _logger);

        // Act
        await seeder.SeedTenantsAsync(new DataSeedContext(), TestContext.Current.CancellationToken);

        // Assert — context had correct TenantId for each call
        capturedTenantIds.ShouldBe([tenant1, tenant2]);
    }

    [Fact]
    public async Task SeedTenantsAsync_ContributorThrows_ContinuesWithRemaining()
    {
        // Arrange
        ITenantDataSeedContributor failing = Substitute.For<ITenantDataSeedContributor>();
        failing
            .SeedAsync(Arg.Any<DataSeedContext>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Seed failed"));

        ITenantDataSeedContributor success = Substitute.For<ITenantDataSeedContributor>();

        ServiceCollection services = new();
        services.AddTransient(_ => failing);
        services.AddTransient(_ => success);
        await using ServiceProvider sp = services.BuildServiceProvider();
        DataSeeder seeder = new(sp.GetRequiredService<IServiceScopeFactory>(), _logger);

        // Act
        await seeder.SeedTenantsAsync(new DataSeedContext(), TestContext.Current.CancellationToken);

        // Assert
        await success.Received(1).SeedAsync(Arg.Any<DataSeedContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedTenantsAsync_OperationCanceledException_Propagates()
    {
        // Arrange
        ITenantDataSeedContributor contributor = Substitute.For<ITenantDataSeedContributor>();
        contributor
            .SeedAsync(Arg.Any<DataSeedContext>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        ServiceCollection services = new();
        services.AddTransient(_ => contributor);
        await using ServiceProvider sp = services.BuildServiceProvider();
        DataSeeder seeder = new(sp.GetRequiredService<IServiceScopeFactory>(), _logger);

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(
            () => seeder.SeedTenantsAsync(new DataSeedContext(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SeedTenantsAsync_PassesPropertiesToTenantContext()
    {
        // Arrange
        var tenantId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        DataSeedContext? capturedContext = null;

        ITenantDataSeedContributor contributor = Substitute.For<ITenantDataSeedContributor>();
        contributor
            .SeedAsync(Arg.Any<DataSeedContext>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedContext = callInfo.Arg<DataSeedContext>();
                return Task.CompletedTask;
            });

        IDataSeedTenantProvider tenantProvider = Substitute.For<IDataSeedTenantProvider>();
        tenantProvider.GetTenantIdsAsync(Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(tenantId));

        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>())
            .Returns(Substitute.For<IDisposable>());

        ServiceCollection services = new();
        services.AddTransient(_ => contributor);
        services.AddSingleton(tenantProvider);
        services.AddScoped(_ => currentTenant);
        await using ServiceProvider sp = services.BuildServiceProvider();
        DataSeeder seeder = new(sp.GetRequiredService<IServiceScopeFactory>(), _logger);

        DataSeedContext context = new();
        context["AdminEmail"] = "admin@test.com";

        // Act
        await seeder.SeedTenantsAsync(context, TestContext.Current.CancellationToken);

        // Assert
        capturedContext.ShouldNotBeNull();
        capturedContext!.TenantId.ShouldBe(tenantId);
        capturedContext["AdminEmail"].ShouldBe("admin@test.com");
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static async IAsyncEnumerable<Guid> ToAsyncEnumerable(params Guid[] ids)
    {
        foreach (Guid id in ids)
        {
            yield return id;
        }

        await Task.CompletedTask;
    }
}
