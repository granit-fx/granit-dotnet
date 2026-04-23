namespace Granit.Identity.Federated.Exceptions;

/// <summary>
/// Thrown by federated identity providers (Keycloak, Entra ID, Cognito, Google Cloud) when
/// the upstream API rejects the configured service-account credentials with a 401 or 403.
/// </summary>
/// <remarks>
/// <para>
/// The federated providers previously swallowed every upstream failure — including
/// credential expiry and missing IAM permissions — and returned an empty result set to
/// callers. Operators saw "no users" instead of "your service account is unauthorized",
/// masking real outages and quietly degrading the admin UI.
/// </para>
/// <para>
/// Providers now distinguish 401/403 from transient 5xx and re-throw this exception so
/// the HTTP layer can surface a 503 (or 401 if appropriate) rather than 200/empty.
/// </para>
/// </remarks>
public sealed class IdentityProviderUnauthorizedException : Exception
{
    public IdentityProviderUnauthorizedException(string providerName, string operation, Exception? innerException = null)
        : base($"Identity provider '{providerName}' rejected operation '{operation}': service-account credentials are missing or unauthorized.", innerException)
    {
        ProviderName = providerName;
        Operation = operation;
    }

    /// <summary>The provider that returned the 401/403 (e.g. <c>"keycloak"</c>).</summary>
    public string ProviderName { get; }

    /// <summary>The operation that failed (e.g. <c>"get_user"</c>).</summary>
    public string Operation { get; }
}
