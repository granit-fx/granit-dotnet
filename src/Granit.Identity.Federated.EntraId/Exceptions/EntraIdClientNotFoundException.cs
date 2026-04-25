namespace Granit.Identity.Federated.EntraId.Exceptions;

/// <summary>
/// Thrown by <c>EntraIdIdentityProvider</c> client-role methods when the OIDC
/// <c>appId</c> passed by the caller does not resolve to any Service Principal in the
/// configured tenant.
/// </summary>
/// <remarks>
/// Sibling of <c>KeycloakClientNotFoundException</c> in the Keycloak provider — the sync
/// pipeline catches it and logs a Warning before skipping the client.
/// </remarks>
public sealed class EntraIdClientNotFoundException : Exception
{
    public EntraIdClientNotFoundException(string appId)
        : base($"No Service Principal found with appId '{appId}' in the configured Entra tenant.")
    {
        AppId = appId;
    }

    public string AppId { get; }
}
