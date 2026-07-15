using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Granit.Authorization;
using Granit.Identity.Federated.Cognito.Options;
using Granit.Identity.Federated.Sync;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Federated.Cognito.Sync;

/// <summary>
/// AWS Cognito's <see cref="IClientRoleSyncPolicy"/>: tracked app clients and orphan policy come
/// from <see cref="CognitoClientRoleSyncOptions"/>; the shared <see cref="ClientRoleSyncEngine"/>
/// does the upsert and orphan handling. Replaces the former CognitoClientRoleSyncService.
/// </summary>
internal sealed class CognitoClientRoleSyncPolicy(IOptions<CognitoClientRoleSyncOptions> options)
    : IClientRoleSyncPolicy
{
    public string ProviderName => "Cognito";

    public bool Enabled => options.Value.Enabled;

    public IReadOnlyList<string> TrackedClientIds => options.Value.TrackedAppClientIds;

    public OrphanedRolePolicy OrphanedRolePolicy => options.Value.OrphanedRolePolicy;

    public string ForbiddenRemediation =>
        "The admin IAM principal needs: cognito-idp:ListGroups, cognito-idp:AdminListGroupsForUser, "
        + "cognito-idp:ListUserPoolClients on the User Pool.";

    // Cognito has no "client not found" signal — a missing app client id simply yields no groups.
    public ClientRoleFetchFault? ClassifyFetchFault(Exception exception) => exception switch
    {
        NotAuthorizedException => ClientRoleFetchFault.Forbidden,
        AmazonCognitoIdentityProviderException => ClientRoleFetchFault.Transient,
        _ => null,
    };
}
