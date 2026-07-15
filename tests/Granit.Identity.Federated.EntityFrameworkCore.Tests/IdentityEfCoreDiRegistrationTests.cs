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

        // New options-based registration — no more generic on the consuming DbContext.
        builder.AddGranitIdentityFederatedEntityFrameworkCore(opts => opts.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        ServiceProvider provider = builder.Services.BuildServiceProvider();

        IUserLookupService lookupService = provider.GetRequiredService<IUserLookupService>();
        lookupService.ShouldBeOfType<CachedUserLookupService>();

        IUserCacheStats stats = provider.GetRequiredService<IUserCacheStats>();
        stats.ShouldBeOfType<UserCacheStats>();

        IUserCacheStore store = provider.GetRequiredService<IUserCacheStore>();
        store.ShouldBeOfType<EfCoreUserCacheStore>();

        IDbContextFactory<IdentityFederatedDbContext> factory =
            provider.GetRequiredService<IDbContextFactory<IdentityFederatedDbContext>>();
        factory.ShouldNotBeNull();
    }
}
