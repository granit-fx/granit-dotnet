using Granit.Identity.Extensions;
using Granit.Identity.Federated.EntityFrameworkCore.Extensions;
using Granit.Identity.Federated.EntityFrameworkCore.Internal;
using Granit.Identity.Federated.Internal;
using Granit.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.EntityFrameworkCore.Tests;

public sealed class IdentityEfCoreDiRegistrationTests
{
    [Fact]
    public void AddGranitIdentityEntityFrameworkCore_ReplacesNullImplementations()
    {
        var services = new ServiceCollection();

        // Simulate what AddGranitIdentity() does
        services.AddGranitIdentity();

        // Add dependencies required by CachedUserLookupService
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(NSubstitute.Substitute.For<ICurrentTenant>());
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        // Register EF Core context
        services.AddDbContext<TestDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        // Register EF Core identity services
        services.AddGranitIdentityEntityFrameworkCore<TestDbContext>();

        ServiceProvider provider = services.BuildServiceProvider();

        // IUserLookupService should be CachedUserLookupService, not NullUserLookupService
        IUserLookupService lookupService = provider.GetRequiredService<IUserLookupService>();
        lookupService.ShouldBeOfType<CachedUserLookupService>();

        // IUserCacheStats should be EfCoreUserCacheStats, not NullUserCacheStats
        IUserCacheStats stats = provider.GetRequiredService<IUserCacheStats>();
        stats.ShouldBeOfType<EfCoreUserCacheStats>();

        // IUserCacheStore should be registered
        IUserCacheStore store = provider.GetRequiredService<IUserCacheStore>();
        store.ShouldBeOfType<EfCoreUserCacheStore<TestDbContext>>();
    }
}
