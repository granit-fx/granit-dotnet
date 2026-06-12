namespace Granit.Identity;

/// <summary>
/// The session layer a <see cref="UserSessionCreatedEto"/> originated from. Lets consumers reason about the
/// session's authentication topology without coupling to a specific provider package.
/// </summary>
public enum UserSessionSource
{
    /// <summary>Unknown or unspecified source.</summary>
    Unknown = 0,

    /// <summary>A browser↔BFF gateway session (cookie-backed).</summary>
    Bff,

    /// <summary>A session issued by the local OpenIddict authorization server.</summary>
    OpenIddict,

    /// <summary>A session issued by a federated Keycloak realm.</summary>
    Keycloak,
}
