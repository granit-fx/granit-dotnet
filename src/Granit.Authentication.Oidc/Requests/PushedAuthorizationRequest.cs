namespace Granit.Authentication.Oidc.Requests;

/// <summary>
/// Represents a Pushed Authorization Request (RFC 9126).
/// </summary>
public sealed record PushedAuthorizationRequest
{
    /// <summary>
    /// The client identifier.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// The redirect URI where the authorization response will be sent.
    /// </summary>
    public required string RedirectUri { get; init; }

    /// <summary>
    /// The response type. Typically <c>"code"</c> for the authorization code flow.
    /// </summary>
    public required string ResponseType { get; init; }

    /// <summary>
    /// The requested scope.
    /// </summary>
    public required string Scope { get; init; }

    /// <summary>
    /// The state parameter for CSRF protection.
    /// </summary>
    public required string State { get; init; }

    /// <summary>
    /// The PKCE code challenge (RFC 7636).
    /// </summary>
    public string? CodeChallenge { get; init; }

    /// <summary>
    /// The PKCE code challenge method (e.g., <c>"S256"</c>).
    /// </summary>
    public string? CodeChallengeMethod { get; init; }

    /// <summary>
    /// The nonce parameter for replay protection.
    /// </summary>
    public string? Nonce { get; init; }

    /// <summary>
    /// Additional parameters to include in the PAR request.
    /// </summary>
    public Dictionary<string, string> AdditionalParameters { get; init; } = [];
}
