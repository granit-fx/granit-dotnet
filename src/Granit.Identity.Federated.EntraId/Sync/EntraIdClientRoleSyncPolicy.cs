using System.Net;
using Granit.Authorization;
using Granit.Identity.Federated.EntraId.Exceptions;
using Granit.Identity.Federated.EntraId.Options;
using Granit.Identity.Federated.Sync;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Federated.EntraId.Sync;

/// <summary>
/// Microsoft Entra ID's <see cref="IClientRoleSyncPolicy"/>: tracked app ids and orphan policy
/// come from <see cref="EntraIdClientRoleSyncOptions"/>; the shared <see cref="ClientRoleSyncEngine"/>
/// does the upsert and orphan handling. Replaces the former EntraIdClientRoleSyncService.
/// </summary>
internal sealed class EntraIdClientRoleSyncPolicy(IOptions<EntraIdClientRoleSyncOptions> options)
    : IClientRoleSyncPolicy
{
    public string ProviderName => "Entra ID";

    public bool Enabled => options.Value.Enabled;

    public IReadOnlyList<string> TrackedClientIds => options.Value.TrackedAppIds;

    public OrphanedRolePolicy OrphanedRolePolicy => options.Value.OrphanedRolePolicy;

    public string ForbiddenRemediation =>
        "The service principal needs Application permissions: Application.Read.All (for listing), "
        + "AppRoleAssignment.ReadWrite.All (for user assignments).";

    public ClientRoleFetchFault? ClassifyFetchFault(Exception exception) => exception switch
    {
        EntraIdClientNotFoundException => ClientRoleFetchFault.ClientNotFound,
        HttpRequestException { StatusCode: HttpStatusCode.Forbidden } => ClientRoleFetchFault.Forbidden,
        HttpRequestException or TaskCanceledException => ClientRoleFetchFault.Transient,
        _ => null,
    };
}
