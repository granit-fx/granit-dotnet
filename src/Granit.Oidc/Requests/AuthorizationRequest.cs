using System.Text;

namespace Granit.Oidc.Requests;

/// <summary>
/// Represents an OAuth 2.0 authorization request that can be serialized to a redirect URL.
/// </summary>
public sealed record AuthorizationRequest
{
    /// <summary>
    /// The authorization endpoint URL.
    /// </summary>
    public required string AuthorizationEndpoint { get; init; }

    /// <summary>
    /// The client identifier.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// The redirect URI where the authorization response will be sent.
    /// </summary>
    public required string RedirectUri { get; init; }

    /// <summary>
    /// The response type. Defaults to <c>"code"</c> for the authorization code flow.
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
    /// Additional query parameters to include in the authorization URL.
    /// </summary>
    public Dictionary<string, string> AdditionalParameters { get; init; } = [];

    /// <summary>
    /// Builds the full authorization redirect URL with all query parameters.
    /// </summary>
    /// <returns>An absolute URL string suitable for redirecting the user agent.</returns>
    public string ToUrl()
    {
        var sb = new StringBuilder(AuthorizationEndpoint);
        sb.Append('?');

        AppendParameter(sb, OidcConstants.Parameters.ClientId, ClientId, isFirst: true);
        AppendParameter(sb, OidcConstants.Parameters.RedirectUri, RedirectUri);
        AppendParameter(sb, OidcConstants.Parameters.ResponseType, ResponseType);
        AppendParameter(sb, OidcConstants.Parameters.Scope, Scope);
        AppendParameter(sb, OidcConstants.Parameters.State, State);

        if (CodeChallenge is not null)
        {
            AppendParameter(sb, OidcConstants.Parameters.CodeChallenge, CodeChallenge);
        }

        if (CodeChallengeMethod is not null)
        {
            AppendParameter(sb, OidcConstants.Parameters.CodeChallengeMethod, CodeChallengeMethod);
        }

        if (Nonce is not null)
        {
            AppendParameter(sb, OidcConstants.Parameters.Nonce, Nonce);
        }

        foreach (KeyValuePair<string, string> kvp in AdditionalParameters)
        {
            AppendParameter(sb, kvp.Key, kvp.Value);
        }

        return sb.ToString();
    }

    private static void AppendParameter(StringBuilder sb, string key, string value, bool isFirst = false)
    {
        if (!isFirst)
        {
            sb.Append('&');
        }

        sb.Append(Uri.EscapeDataString(key));
        sb.Append('=');
        sb.Append(Uri.EscapeDataString(value));
    }
}
