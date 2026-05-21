using Granit.Events;
using Granit.Identity.Extensions;
using Granit.Identity.Federated.Cognito.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Cognito.Tests;

/// <summary>
/// Locks the invariant that <see cref="IIdentityProvider"/> and
/// <see cref="IIdentityClientRoleManager"/> resolve to the SAME scoped instance —
/// required so internal provider state (option snapshots, internal client handles)
/// stays coherent across the two facets. Same pattern as Keycloak/Entra. See ADR-027.
/// </summary>
public sealed class IdentityCognitoServiceCollectionExtensionsClientRoleTests
{
    [Fact]
    public void IdentityProvider_And_ClientRoleManager_ResolveToSameScopedInstance()
    {
        ServiceCollection services = [];
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Identity:Federated:Cognito:Region"] = "eu-west-1",
                ["Identity:Federated:Cognito:UserPoolId"] = "eu-west-1_TEST",
            }).Build());

        services.AddLogging();
        services.AddSingleton(Substitute.For<IDistributedEventBus>());
        services.AddGranitIdentity();
        services.AddGranitIdentityCognito();

        using ServiceProvider sp = services.BuildServiceProvider();
        using IServiceScope scope = sp.CreateScope();

        IIdentityProvider provider = scope.ServiceProvider.GetRequiredService<IIdentityProvider>();
        IIdentityClientRoleManager clientRoleManager =
            scope.ServiceProvider.GetRequiredService<IIdentityClientRoleManager>();

        object.ReferenceEquals(provider, clientRoleManager).ShouldBeTrue(
            "IIdentityProvider and IIdentityClientRoleManager must resolve to the same " +
            "scoped CognitoIdentityProvider instance — see ADR-027 and the DI block " +
            "in AddGranitIdentityCognito.");
    }
}
