using Granit.Events;
using Granit.Guids.Extensions;
using Granit.Identity.Extensions;
using Granit.Identity.Federated.EntraId.Extensions;
using Granit.Timing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.EntraId.Tests;

/// <summary>
/// Locks the invariant that <see cref="IIdentityProvider"/> and
/// <see cref="IIdentityClientRoleManager"/> resolve to the SAME scoped instance —
/// required so <c>EntraIdAdminTokenService</c> caching and any internal state stay
/// coherent across the two facets of the provider. Plain ServiceProvider test, no
/// architecture test dependency.
/// </summary>
public sealed class IdentityEntraIdServiceCollectionExtensionsClientRoleTests
{
    [Fact]
    public void IdentityProvider_And_ClientRoleManager_ResolveToSameScopedInstance()
    {
        ServiceCollection services = [];
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Identity:Federated:EntraId:TenantId"] = "test-tenant-id",
                ["Identity:Federated:EntraId:ClientId"] = "admin-service",
                ["Identity:Federated:EntraId:ClientSecret"] = "secret",
                ["Identity:Federated:EntraId:ServicePrincipalObjectId"] = "sp-object-id",
            }).Build());

        services.AddLogging();
        services.AddSingleton(Substitute.For<IClock>());
        services.AddSingleton(Substitute.For<IDistributedEventBus>());
        services.AddGranitGuids();
        services.AddGranitIdentity();
        services.AddGranitIdentityEntraId();

        using ServiceProvider sp = services.BuildServiceProvider();
        using IServiceScope scope = sp.CreateScope();

        IIdentityProvider provider = scope.ServiceProvider.GetRequiredService<IIdentityProvider>();
        IIdentityClientRoleManager clientRoleManager =
            scope.ServiceProvider.GetRequiredService<IIdentityClientRoleManager>();

        object.ReferenceEquals(provider, clientRoleManager).ShouldBeTrue(
            "IIdentityProvider and IIdentityClientRoleManager must resolve to the same " +
            "scoped EntraIdIdentityProvider instance — see ADR-026 and the DI block " +
            "in AddGranitIdentityEntraId.");
    }
}
