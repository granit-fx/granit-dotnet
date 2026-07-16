namespace Granit.Identity.Federated.Exceptions;

/// <summary>
/// Classifies why a federated identity provider operation failed, so callers (and the
/// graceful-degradation decorator) can react to the failure mode instead of treating every
/// fault as an opaque empty result.
/// </summary>
public enum IdentityProviderFailureCategory
{
    /// <summary>The upstream API rejected the service-account credentials (401/403).</summary>
    Unauthorized,

    /// <summary>The targeted resource (user, client, group) does not exist upstream (404).</summary>
    NotFound,

    /// <summary>The upstream API rate-limited the request (429 / TooManyRequests).</summary>
    Throttled,

    /// <summary>A transient upstream fault (5xx, timeout, connection reset) — retry may succeed.</summary>
    Transient,
}

/// <summary>
/// Base type for the classified failures a federated identity provider raises. The concrete
/// subtype and <see cref="Category"/> let the HTTP layer, health checks, and the
/// graceful-degradation decorator distinguish "the user is gone" from "our IAM is broken"
/// from "we are being throttled" — the distinction the providers previously collapsed into a
/// silent empty result.
/// </summary>
public abstract class IdentityProviderException : Exception
{
    private protected IdentityProviderException(
        IdentityProviderFailureCategory category,
        string providerName,
        string operation,
        string message,
        Exception? innerException)
        : base(message, innerException)
    {
        Category = category;
        ProviderName = providerName;
        Operation = operation;
    }

    /// <summary>The classified failure mode.</summary>
    public IdentityProviderFailureCategory Category { get; }

    /// <summary>The provider that raised the failure (e.g. <c>"keycloak"</c>).</summary>
    public string ProviderName { get; }

    /// <summary>The operation that failed (e.g. <c>"get_users"</c>).</summary>
    public string Operation { get; }
}
