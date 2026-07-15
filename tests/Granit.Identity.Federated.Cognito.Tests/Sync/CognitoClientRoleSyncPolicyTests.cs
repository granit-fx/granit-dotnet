using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Granit.Authorization;
using Granit.Identity.Federated.Cognito.Options;
using Granit.Identity.Federated.Cognito.Sync;
using Granit.Identity.Federated.Sync;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Cognito.Tests.Sync;

public sealed class CognitoClientRoleSyncPolicyTests
{
    private static CognitoClientRoleSyncPolicy Build(CognitoClientRoleSyncOptions opts) =>
        new(Microsoft.Extensions.Options.Options.Create(opts));

    [Fact]
    public void Maps_options_onto_the_policy_surface()
    {
        CognitoClientRoleSyncPolicy policy = Build(new CognitoClientRoleSyncOptions
        {
            Enabled = true,
            TrackedAppClientIds = ["client-1"],
            OrphanedRolePolicy = OrphanedRolePolicy.HardDelete,
        });

        policy.ProviderName.ShouldBe("Cognito");
        policy.TrackedClientIds.ShouldBe(["client-1"]);
        policy.OrphanedRolePolicy.ShouldBe(OrphanedRolePolicy.HardDelete);
    }

    [Fact]
    public void Classifies_provider_faults_and_rethrows_the_rest()
    {
        CognitoClientRoleSyncPolicy policy = Build(new CognitoClientRoleSyncOptions());

        policy.ClassifyFetchFault(new NotAuthorizedException("denied"))
            .ShouldBe(ClientRoleFetchFault.Forbidden);
        policy.ClassifyFetchFault(new AmazonCognitoIdentityProviderException("boom"))
            .ShouldBe(ClientRoleFetchFault.Transient);
        policy.ClassifyFetchFault(new InvalidOperationException()).ShouldBeNull();
    }
}
