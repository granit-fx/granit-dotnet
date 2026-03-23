namespace Granit.Authentication.Oidc.Discovery;

/// <summary>
/// Thrown when the OIDC discovery document cannot be retrieved or parsed from the specified authority.
/// </summary>
public sealed class OidcDiscoveryException(string authority, string message, Exception? innerException = null)
    : InvalidOperationException(
        $"Failed to retrieve OIDC discovery document from '{authority}/.well-known/openid-configuration': {message}",
        innerException);
