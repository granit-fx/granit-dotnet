using Granit.Guids;
using Granit.Identity.Extensions;
using Granit.Identity.Federated.EntityFrameworkCore.Extensions;
using Granit.Identity.Federated.EntityFrameworkCore.Internal;
using Granit.Identity.Federated.Internal;
using Granit.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.EntityFrameworkCore.Tests;

public sealed class IdentityEfCoreDiRegistrationTests
{
    [Fact]
    public void AddGranitIdentityFederatedEntityFrameworkCore_ReplacesNullImplementations()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        // Simulate what AddGranitIdentity() does on the service collection.
        builder.Services.AddGranitIdentity();

        // Dependencies expected by CachedUserLookupService.
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(Substitute.For<ICurrentTenant>());
        builder.Services.AddSingleton(Substitute.For<IUserLookupHasher>());
        builder.Services.AddSingleton(Substitute.For<IGuidGenerator>());
        builder.Services.AddSingleton(Substitute.For<IUserDirectoryWriter>());

        // EfCoreUserCacheStore depends on the soft-dep ITenantsAccessor primitive (base
        // Granit). The default NullTenantsAccessor is registered by AddGranit<T>(), but
        // this test bypasses the module loader — stub it explicitly.
        ITenantsAccessor accessor = Substitute.For<ITenantsAccessor>();
        accessor.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<(Guid, string)>>([]));
        builder.Services.AddSingleton(accessor);

        // Phase B options-based registration — Shared mode default.
        builder.AddGranitIdentityFederatedEntityFrameworkCore(opts =>
        {
            opts.Configure = b => b.UseInMemoryDatabase(Guid.NewGuid().ToString());
        });

        ServiceProvider provider = builder.Services.BuildServiceProvider();

        IUserLookupService lookupService = provider.GetRequiredService<IUserLookupService>();
        lookupService.ShouldBeOfType<CachedUserLookupService>();

        IUserCacheStats stats = provider.GetRequiredService<IUserCacheStats>();
        stats.ShouldBeOfType<EfCoreUserCacheStats>();

        IUserCacheStore store = provider.GetRequiredService<IUserCacheStore>();
        store.ShouldBeOfType<EfCoreUserCacheStore>();

        IDbContextFactory<IdentityFederatedHostDbContext> factory =
            provider.GetRequiredService<IDbContextFactory<IdentityFederatedHostDbContext>>();
        factory.ShouldNotBeNull();
    }
}
