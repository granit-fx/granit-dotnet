using Granit.Events;
using Granit.Identity;
using Granit.Identity.Extensions;
using Granit.Identity.Federated.Keycloak.Extensions;
using Granit.Timing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Keycloak.Tests;

/// <summary>
/// Locks the invariant that <see cref="IIdentityProvider"/> and
/// <see cref="IIdentityClientRoleManager"/> resolve to the SAME scoped instance —
/// required so <c>KeycloakAdminTokenService</c> caching and any internal state stay
/// coherent across the two facets of the provider. Plain ServiceProvider test, no
/// architecture test dependency.
/// </summary>
public sealed class IdentityKeycloakServiceCollectionExtensionsClientRoleTests
{
    [Fact]
    public void IdentityProvider_And_ClientRoleManager_ResolveToSameScopedInstance()
    {
        ServiceCollection services = [];
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KeycloakAdmin:BaseUrl"] = "https://keycloak.test",
                ["KeycloakAdmin:Realm"] = "test",
                ["KeycloakAdmin:ClientId"] = "admin",
                ["KeycloakAdmin:ClientSecret"] = "secret",
            }).Build());

        services.AddLogging();
        services.AddSingleton(Substitute.For<IClock>());
        services.AddSingleton(Substitute.For<IDistributedEventBus>());
        services.AddGranitIdentity();
        services.AddGranitIdentityKeycloak();

        using ServiceProvider sp = services.BuildServiceProvider();
        using IServiceScope scope = sp.CreateScope();

        IIdentityProvider provider = scope.ServiceProvider.GetRequiredService<IIdentityProvider>();
        IIdentityClientRoleManager clientRoleManager =
            scope.ServiceProvider.GetRequiredService<IIdentityClientRoleManager>();

        object.ReferenceEquals(provider, clientRoleManager).ShouldBeTrue(
            "IIdentityProvider and IIdentityClientRoleManager must resolve to the same " +
            "scoped KeycloakIdentityProvider instance — see ADR-025 and the DI block " +
            "in AddGranitIdentityKeycloak.");
    }
}
