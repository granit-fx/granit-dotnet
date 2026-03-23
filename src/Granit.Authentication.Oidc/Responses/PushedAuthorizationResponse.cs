using System.Text.Json;

namespace Granit.Authentication.Oidc.Responses;

/// <summary>
/// Represents a Pushed Authorization Request response (RFC 9126 §2.2).
/// </summary>
public sealed record PushedAuthorizationResponse
{
    /// <summary>
    /// The request URI that the client uses in the subsequent authorization request.
    /// </summary>
    public string? RequestUri { get; init; }

    /// <summary>
    /// The lifetime in seconds of the request URI.
    /// </summary>
    public int? ExpiresIn { get; init; }

    /// <summary>
    /// The error details if the PAR request failed.
    /// </summary>
    public OidcError? Error { get; init; }

    /// <summary>
    /// Indicates whether the PAR response represents a successful request registration.
    /// </summary>
    public bool IsSuccess => Error is null && RequestUri is not null;

    /// <summary>
    /// Parses a PAR response from a JSON element.
    /// </summary>
    /// <param name="json">The root JSON element of the PAR endpoint response.</param>
    /// <returns>A <see cref="PushedAuthorizationResponse"/> representing the parsed response.</returns>
    public static PushedAuthorizationResponse FromJson(JsonElement json)
    {
        if (json.TryGetProperty("error", out JsonElement errorElement))
        {
            string error = errorElement.GetString() ?? "unknown_error";
            string? errorDescription = json.TryGetProperty("error_description", out JsonElement descElement)
                ? descElement.GetString()
                : null;

            return new PushedAuthorizationResponse
            {
                Error = new OidcError(error, errorDescription),
            };
        }

        return new PushedAuthorizationResponse
        {
            RequestUri = json.TryGetProperty("request_uri", out JsonElement uriElement)
                ? uriElement.GetString()
                : null,
            ExpiresIn = json.TryGetProperty("expires_in", out JsonElement exElement)
                ? exElement.GetInt32()
                : null,
        };
    }

    /// <summary>
    /// Creates a <see cref="PushedAuthorizationResponse"/> representing an error.
    /// </summary>
    /// <param name="error">The error code.</param>
    /// <param name="description">Optional error description.</param>
    /// <returns>A <see cref="PushedAuthorizationResponse"/> with the <see cref="Error"/> property set.</returns>
    public static PushedAuthorizationResponse FromError(string error, string? description = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(error);

        return new PushedAuthorizationResponse
        {
            Error = new OidcError(error, description),
        };
    }
}
