using Granit.Events;
using Granit.Identity.Extensions;
using Granit.Identity.Federated.Keycloak.Extensions;
using Granit.Identity.Federated.Keycloak.Internal;
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
                ["Identity:Federated:Keycloak:BaseUrl"] = "https://keycloak.test",
                ["Identity:Federated:Keycloak:Realm"] = "test",
                ["Identity:Federated:Keycloak:ClientId"] = "admin",
                ["Identity:Federated:Keycloak:ClientSecret"] = "secret",
            }).Build());

        services.AddLogging();
        services.AddSingleton(Substitute.For<IClock>());
        services.AddSingleton(Substitute.For<IDistributedEventBus>());
        services.AddSingleton(TimeProvider.System);
        services.AddGranitIdentity();
        services.AddGranitIdentityKeycloak();

        using ServiceProvider sp = services.BuildServiceProvider();
        using IServiceScope scope = sp.CreateScope();

        IIdentityProvider provider = scope.ServiceProvider.GetRequiredService<IIdentityProvider>();
        KeycloakIdentityProvider concrete = scope.ServiceProvider.GetRequiredService<KeycloakIdentityProvider>();
        IIdentityClientRoleManager clientRoleManager =
            scope.ServiceProvider.GetRequiredService<IIdentityClientRoleManager>();

        // The client-role facet (which IIdentityProvider does not compose) resolves the concrete
        // provider, while IIdentityProvider is the graceful-degradation decorator wrapping that
        // same scoped instance — see ADR-025 and the DI block in AddGranitIdentityKeycloak.
        clientRoleManager.ShouldBeSameAs(concrete);
        provider.ShouldNotBeSameAs(concrete);
    }

    [Fact]
    public void IdentityProvider_And_SessionProviders_ResolveToSameScopedInstance()
    {
        ServiceCollection services = [];
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Identity:Federated:Keycloak:BaseUrl"] = "https://keycloak.test",
                ["Identity:Federated:Keycloak:Realm"] = "test",
                ["Identity:Federated:Keycloak:ClientId"] = "admin",
                ["Identity:Federated:Keycloak:ClientSecret"] = "secret",
            }).Build());

        services.AddLogging();
        services.AddSingleton(Substitute.For<IClock>());
        services.AddSingleton(Substitute.For<IDistributedEventBus>());
        services.AddSingleton(TimeProvider.System);
        services.AddGranitIdentity();
        services.AddGranitIdentityKeycloak();

        using ServiceProvider sp = services.BuildServiceProvider();
        using IServiceScope scope = sp.CreateScope();

        KeycloakIdentityProvider concrete = scope.ServiceProvider.GetRequiredService<KeycloakIdentityProvider>();
        IUserSessionProvider sessionProvider =
            scope.ServiceProvider.GetRequiredService<IUserSessionProvider>();
        IUserDeviceProvider deviceProvider =
            scope.ServiceProvider.GetRequiredService<IUserDeviceProvider>();

        // The session/device facets (which IIdentityProvider does not compose) resolve the concrete
        // KeycloakIdentityProvider so the canonical /sessions and /devices APIs surface Keycloak data
        // — see the DI block in AddGranitIdentityKeycloak (#2659).
        sessionProvider.ShouldBeSameAs(concrete);
        deviceProvider.ShouldBeSameAs(concrete);
    }
}
