using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

#pragma warning disable GRSEC003 // Record contains token properties — these are protocol-level OAuth 2.0 response fields

namespace Granit.Authentication.Oidc.Responses;

/// <summary>
/// Represents an OAuth 2.0 token endpoint response (RFC 6749 §5.1).
/// </summary>
public sealed record TokenResponse
{
    /// <summary>
    /// The access token issued by the authorization server.
    /// </summary>
    public string? AccessToken { get; init; }

    /// <summary>
    /// The refresh token, which can be used to obtain new access tokens.
    /// </summary>
    public string? RefreshToken { get; init; }

    /// <summary>
    /// The ID token (OpenID Connect).
    /// </summary>
    public string? IdToken { get; init; }

    /// <summary>
    /// The lifetime in seconds of the access token.
    /// </summary>
    public int ExpiresIn { get; init; }

    /// <summary>
    /// The type of the token issued (e.g., <c>"Bearer"</c>, <c>"DPoP"</c>).
    /// </summary>
    public string? TokenType { get; init; }

    /// <summary>
    /// The scope of the access token.
    /// </summary>
    public string? Scope { get; init; }

    /// <summary>
    /// The DPoP nonce returned by the server for subsequent requests (RFC 9449 §8).
    /// </summary>
    public string? DPoPNonce { get; init; }

    /// <summary>
    /// The error details if the token request failed.
    /// </summary>
    public OidcError? Error { get; init; }

    /// <summary>
    /// Indicates whether the token response represents a successful token issuance.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Error))]
    [MemberNotNullWhen(true, nameof(AccessToken))]
    public bool IsSuccess => Error is null && AccessToken is not null;

    /// <summary>
    /// Parses a token response from a JSON element.
    /// </summary>
    /// <param name="json">The root JSON element of the token endpoint response.</param>
    /// <param name="dpopNonce">An optional DPoP nonce extracted from the response header.</param>
    /// <returns>A <see cref="TokenResponse"/> representing the parsed response.</returns>
    public static TokenResponse FromJson(JsonElement json, string? dpopNonce = null)
    {
        if (json.TryGetProperty("error", out JsonElement errorElement))
        {
            string error = errorElement.GetString() ?? "unknown_error";
            string? errorDescription = json.TryGetProperty("error_description", out JsonElement descElement)
                ? descElement.GetString()
                : null;

            return new TokenResponse
            {
                Error = new OidcError(error, errorDescription),
                DPoPNonce = dpopNonce,
            };
        }

        return new TokenResponse
        {
            AccessToken = json.TryGetProperty("access_token", out JsonElement atElement)
                ? atElement.GetString()
                : null,
            RefreshToken = json.TryGetProperty("refresh_token", out JsonElement rtElement)
                ? rtElement.GetString()
                : null,
            IdToken = json.TryGetProperty("id_token", out JsonElement idElement)
                ? idElement.GetString()
                : null,
            ExpiresIn = json.TryGetProperty("expires_in", out JsonElement exElement)
                ? exElement.GetInt32()
                : 0,
            TokenType = json.TryGetProperty("token_type", out JsonElement ttElement)
                ? ttElement.GetString()
                : null,
            Scope = json.TryGetProperty("scope", out JsonElement scopeElement)
                ? scopeElement.GetString()
                : null,
            DPoPNonce = dpopNonce,
        };
    }

    /// <summary>
    /// Creates a <see cref="TokenResponse"/> representing an error.
    /// </summary>
    /// <param name="error">The error code.</param>
    /// <param name="description">Optional error description.</param>
    /// <param name="dpopNonce">An optional DPoP nonce from the error response header.</param>
    /// <returns>A <see cref="TokenResponse"/> with the <see cref="Error"/> property set.</returns>
    public static TokenResponse FromError(string error, string? description = null, string? dpopNonce = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(error);

        return new TokenResponse
        {
            Error = new OidcError(error, description),
            DPoPNonce = dpopNonce,
        };
    }
}

#pragma warning restore GRSEC003
