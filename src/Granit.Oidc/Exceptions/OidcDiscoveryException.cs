namespace Granit.Oidc.Exceptions;

/// <summary>
/// Thrown when the OIDC discovery document cannot be retrieved or parsed from the specified authority.
/// </summary>
public sealed class OidcDiscoveryException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OidcDiscoveryException"/> class.
    /// </summary>
    /// <param name="authority">The authority whose discovery document could not be resolved.</param>
    /// <param name="message">A description of the failure.</param>
    /// <param name="innerException">The underlying exception, if any.</param>
    public OidcDiscoveryException(string authority, string message, Exception? innerException = null)
        : base(
            $"Failed to retrieve OIDC discovery document from '{authority}/.well-known/openid-configuration': {message}",
            innerException) =>
        Authority = authority;

    /// <summary>The authority whose discovery document could not be resolved.</summary>
    public string Authority { get; }
}
