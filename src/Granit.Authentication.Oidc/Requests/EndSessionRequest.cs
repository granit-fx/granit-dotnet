using System.Text;

namespace Granit.Authentication.Oidc.Requests;

/// <summary>
/// Represents an OIDC RP-Initiated Logout request (OpenID Connect RP-Initiated Logout 1.0).
/// </summary>
public sealed record EndSessionRequest
{
    /// <summary>
    /// The end session (logout) endpoint URL.
    /// </summary>
    public required string EndSessionEndpoint { get; init; }

    /// <summary>
    /// The ID token previously issued to the client, used as a hint about the user's session.
    /// </summary>
    public string? IdTokenHint { get; init; }

    /// <summary>
    /// The URI to redirect to after logout.
    /// </summary>
    public string? PostLogoutRedirectUri { get; init; }

    /// <summary>
    /// The client identifier. Recommended when <see cref="IdTokenHint"/> is not provided.
    /// </summary>
    public string? ClientId { get; init; }

    /// <summary>
    /// The state parameter for correlating the logout response.
    /// </summary>
    public string? State { get; init; }

    /// <summary>
    /// Builds the full end session redirect URL with all query parameters.
    /// </summary>
    /// <returns>An absolute URL string suitable for redirecting the user agent.</returns>
    public string ToUrl()
    {
        var sb = new StringBuilder(EndSessionEndpoint);
        bool isFirst = true;

        sb.Append('?');

        if (IdTokenHint is not null)
        {
            AppendParameter(sb, OidcConstants.Parameters.IdTokenHint, IdTokenHint, isFirst);
            isFirst = false;
        }

        if (PostLogoutRedirectUri is not null)
        {
            AppendParameter(sb, OidcConstants.Parameters.PostLogoutRedirectUri, PostLogoutRedirectUri, isFirst);
            isFirst = false;
        }

        if (ClientId is not null)
        {
            AppendParameter(sb, OidcConstants.Parameters.ClientId, ClientId, isFirst);
            isFirst = false;
        }

        if (State is not null)
        {
            AppendParameter(sb, OidcConstants.Parameters.State, State, isFirst);
        }

        return sb.ToString();
    }

    private static void AppendParameter(StringBuilder sb, string key, string value, bool isFirst)
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
