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
/// the HTTP layer can surface a 503 (or 401 if appropriate) rather than 200/empty. The
/// graceful-degradation decorator re-throws this category rather than degrading it, so an
/// IAM outage is never hidden behind an empty result.
/// </para>
/// </remarks>
public sealed class IdentityProviderUnauthorizedException : IdentityProviderException
{
    public IdentityProviderUnauthorizedException(string providerName, string operation, Exception? innerException = null)
        : base(
            IdentityProviderFailureCategory.Unauthorized,
            providerName,
            operation,
            $"Identity provider '{providerName}' rejected operation '{operation}': service-account credentials are missing or unauthorized.",
            innerException)
    {
    }
}
