using Granit.Events;
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
        IIdentityClientRoleManager clientRoleManager =
            scope.ServiceProvider.GetRequiredService<IIdentityClientRoleManager>();

        object.ReferenceEquals(provider, clientRoleManager).ShouldBeTrue(
            "IIdentityProvider and IIdentityClientRoleManager must resolve to the same " +
            "scoped KeycloakIdentityProvider instance — see ADR-025 and the DI block " +
            "in AddGranitIdentityKeycloak.");
    }

    [Fact]
    public void IdentityProvider_And_SessionManager_ResolveToSameScopedInstance()
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
        IIdentitySessionManager sessionManager =
            scope.ServiceProvider.GetRequiredService<IIdentitySessionManager>();

        object.ReferenceEquals(provider, sessionManager).ShouldBeTrue(
            "IIdentityProvider and IIdentitySessionManager must resolve to the same scoped " +
            "KeycloakIdentityProvider instance so Granit.Identity.UserSessions surfaces Keycloak " +
            "sessions/devices through the canonical /sessions API — see the DI block in " +
            "AddGranitIdentityKeycloak (#2659).");
    }
}
