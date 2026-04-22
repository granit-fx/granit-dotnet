namespace Granit.Identity.Federated.Keycloak.Exceptions;

/// <summary>
/// Thrown by <c>KeycloakIdentityProvider</c> client-role methods when the OIDC <c>client_id</c>
/// passed by the caller does not resolve to a registered Keycloak client in the configured realm.
/// </summary>
/// <remarks>
/// The sync pipeline (<c>KeycloakClientRoleSyncContributor</c>) catches this and logs a
/// Warning before skipping the client — a missing tracked client is not fatal (realm
/// configuration drift, typo in <c>TrackedClientIds</c>).
/// </remarks>
internal sealed class KeycloakClientNotFoundException : Exception
{
    public KeycloakClientNotFoundException(string clientId)
        : base($"Keycloak client '{clientId}' not found in the configured realm.")
    {
        ClientId = clientId;
    }

    public string ClientId { get; }
}
