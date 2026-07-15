using System.Net;
using Granit.Authorization;
using Granit.Identity.Federated.Keycloak.Exceptions;
using Granit.Identity.Federated.Keycloak.Options;
using Granit.Identity.Federated.Sync;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Federated.Keycloak.Sync;

/// <summary>
/// Keycloak's <see cref="IClientRoleSyncPolicy"/>: the tracked clients and orphan policy come from
/// <see cref="KeycloakClientRoleSyncOptions"/>; the shared <see cref="ClientRoleSyncEngine"/> does
/// the upsert and orphan handling. Replaces the former KeycloakClientRoleSyncService.
/// </summary>
internal sealed class KeycloakClientRoleSyncPolicy(IOptions<KeycloakClientRoleSyncOptions> options)
    : IClientRoleSyncPolicy
{
    public string ProviderName => "Keycloak";

    public bool Enabled => options.Value.Enabled;

    public IReadOnlyList<string> TrackedClientIds => options.Value.TrackedClientIds;

    public OrphanedRolePolicy OrphanedRolePolicy => options.Value.OrphanedRolePolicy;

    public string ForbiddenRemediation =>
        "The admin service account needs realm-management roles: view-clients, query-clients, view-realm.";

    public ClientRoleFetchFault? ClassifyFetchFault(Exception exception) => exception switch
    {
        KeycloakClientNotFoundException => ClientRoleFetchFault.ClientNotFound,
        HttpRequestException { StatusCode: HttpStatusCode.Forbidden } => ClientRoleFetchFault.Forbidden,
        HttpRequestException or TaskCanceledException => ClientRoleFetchFault.Transient,
        _ => null,
    };
}
