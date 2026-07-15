using System.Net;
using Granit.Authorization;
using Granit.Identity.Federated.Keycloak.Exceptions;
using Granit.Identity.Federated.Keycloak.Options;
using Granit.Identity.Federated.Keycloak.Sync;
using Granit.Identity.Federated.Sync;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Keycloak.Tests.Sync;

public sealed class KeycloakClientRoleSyncPolicyTests
{
    private static KeycloakClientRoleSyncPolicy Build(KeycloakClientRoleSyncOptions opts) =>
        new(Microsoft.Extensions.Options.Options.Create(opts));

    [Fact]
    public void Maps_options_onto_the_policy_surface()
    {
        KeycloakClientRoleSyncPolicy policy = Build(new KeycloakClientRoleSyncOptions
        {
            Enabled = true,
            TrackedClientIds = ["app-a", "app-b"],
            OrphanedRolePolicy = OrphanedRolePolicy.SoftDelete,
        });

        policy.ProviderName.ShouldBe("Keycloak");
        policy.Enabled.ShouldBeTrue();
        policy.TrackedClientIds.ShouldBe(["app-a", "app-b"]);
        policy.OrphanedRolePolicy.ShouldBe(OrphanedRolePolicy.SoftDelete);
    }

    [Fact]
    public void Classifies_provider_faults_and_rethrows_the_rest()
    {
        KeycloakClientRoleSyncPolicy policy = Build(new KeycloakClientRoleSyncOptions());

        policy.ClassifyFetchFault(new KeycloakClientNotFoundException("x"))
            .ShouldBe(ClientRoleFetchFault.ClientNotFound);
        policy.ClassifyFetchFault(new HttpRequestException("nope", null, HttpStatusCode.Forbidden))
            .ShouldBe(ClientRoleFetchFault.Forbidden);
        policy.ClassifyFetchFault(new HttpRequestException("down"))
            .ShouldBe(ClientRoleFetchFault.Transient);
        policy.ClassifyFetchFault(new TaskCanceledException())
            .ShouldBe(ClientRoleFetchFault.Transient);
        policy.ClassifyFetchFault(new InvalidOperationException()).ShouldBeNull();
    }
}
