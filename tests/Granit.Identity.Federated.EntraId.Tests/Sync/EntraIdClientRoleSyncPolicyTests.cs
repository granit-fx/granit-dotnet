using System.Net;
using Granit.Authorization;
using Granit.Identity.Federated.EntraId.Exceptions;
using Granit.Identity.Federated.EntraId.Options;
using Granit.Identity.Federated.EntraId.Sync;
using Granit.Identity.Federated.Sync;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.EntraId.Tests.Sync;

public sealed class EntraIdClientRoleSyncPolicyTests
{
    private static EntraIdClientRoleSyncPolicy Build(EntraIdClientRoleSyncOptions opts) =>
        new(Microsoft.Extensions.Options.Options.Create(opts));

    [Fact]
    public void Maps_options_onto_the_policy_surface()
    {
        EntraIdClientRoleSyncPolicy policy = Build(new EntraIdClientRoleSyncOptions
        {
            Enabled = true,
            TrackedAppIds = ["app-guid"],
            OrphanedRolePolicy = OrphanedRolePolicy.KeepAndLog,
        });

        policy.ProviderName.ShouldBe("Entra ID");
        policy.TrackedClientIds.ShouldBe(["app-guid"]);
        policy.OrphanedRolePolicy.ShouldBe(OrphanedRolePolicy.KeepAndLog);
    }

    [Fact]
    public void Classifies_provider_faults_and_rethrows_the_rest()
    {
        EntraIdClientRoleSyncPolicy policy = Build(new EntraIdClientRoleSyncOptions());

        policy.ClassifyFetchFault(new EntraIdClientNotFoundException("x"))
            .ShouldBe(ClientRoleFetchFault.ClientNotFound);
        policy.ClassifyFetchFault(new HttpRequestException("nope", null, HttpStatusCode.Forbidden))
            .ShouldBe(ClientRoleFetchFault.Forbidden);
        policy.ClassifyFetchFault(new HttpRequestException("down"))
            .ShouldBe(ClientRoleFetchFault.Transient);
        policy.ClassifyFetchFault(new InvalidOperationException()).ShouldBeNull();
    }
}
