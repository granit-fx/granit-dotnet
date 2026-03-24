using System.Text.Json;

namespace Granit.Authentication.Oidc.Discovery;

/// <summary>
/// Represents an immutable OpenID Connect discovery document (RFC 8414).
/// Contains the provider's metadata including endpoint URLs, supported scopes, and capabilities.
/// </summary>
public sealed record OidcDiscoveryDocument
{
    /// <summary>
    /// The issuer identifier for the OpenID Provider.
    /// </summary>
    public required string Issuer { get; init; }

    /// <summary>
    /// The URL of the authorization endpoint.
    /// </summary>
    public required string AuthorizationEndpoint { get; init; }

    /// <summary>
    /// The URL of the token endpoint.
    /// </summary>
    public required string TokenEndpoint { get; init; }

    /// <summary>
    /// The URL of the token revocation endpoint (RFC 7009).
    /// </summary>
    public string? RevocationEndpoint { get; init; }

    /// <summary>
    /// The URL of the end session (logout) endpoint.
    /// </summary>
    public string? EndSessionEndpoint { get; init; }

    /// <summary>
    /// The URL of the UserInfo endpoint.
    /// </summary>
    public string? UserInfoEndpoint { get; init; }

    /// <summary>
    /// The URL of the JSON Web Key Set document.
    /// </summary>
    public string? JwksUri { get; init; }

    /// <summary>
    /// The URL of the Pushed Authorization Request endpoint (RFC 9126).
    /// </summary>
    public string? PushedAuthorizationRequestEndpoint { get; init; }

    /// <summary>
    /// The list of OAuth 2.0 scopes supported by the provider.
    /// </summary>
    public IReadOnlyList<string> ScopesSupported { get; init; } = [];

    /// <summary>
    /// The list of OAuth 2.0 grant types supported by the provider.
    /// </summary>
    public IReadOnlyList<string> GrantTypesSupported { get; init; } = [];

    /// <summary>
    /// The list of OAuth 2.0 response types supported by the provider.
    /// </summary>
    public IReadOnlyList<string> ResponseTypesSupported { get; init; } = [];

    /// <summary>
    /// The list of DPoP signing algorithms supported by the provider (RFC 9449).
    /// </summary>
    public IReadOnlyList<string> DPoPSigningAlgValuesSupported { get; init; } = [];

    /// <summary>
    /// Parses an <see cref="OidcDiscoveryDocument"/> from a JSON element.
    /// </summary>
    /// <param name="json">The root JSON element of the discovery document.</param>
    /// <returns>A fully populated <see cref="OidcDiscoveryDocument"/>.</returns>
    internal static OidcDiscoveryDocument FromJson(JsonElement json) =>
        new()
        {
            Issuer = json.GetProperty(OidcConstants.Discovery.Issuer).GetString()!,
            AuthorizationEndpoint = json.GetProperty(OidcConstants.Discovery.AuthorizationEndpoint).GetString()!,
            TokenEndpoint = json.GetProperty(OidcConstants.Discovery.TokenEndpoint).GetString()!,
            RevocationEndpoint = GetOptionalString(json, OidcConstants.Discovery.RevocationEndpoint),
            EndSessionEndpoint = GetOptionalString(json, OidcConstants.Discovery.EndSessionEndpoint),
            UserInfoEndpoint = GetOptionalString(json, "userinfo_endpoint"),
            JwksUri = GetOptionalString(json, OidcConstants.Discovery.JwksUri),
            PushedAuthorizationRequestEndpoint = GetOptionalString(json, OidcConstants.Discovery.PushedAuthorizationRequestEndpoint),
            ScopesSupported = GetStringArray(json, OidcConstants.Discovery.ScopesSupported),
            GrantTypesSupported = GetStringArray(json, OidcConstants.Discovery.GrantTypesSupported),
            ResponseTypesSupported = GetStringArray(json, OidcConstants.Discovery.ResponseTypesSupported),
            DPoPSigningAlgValuesSupported = GetStringArray(json, OidcConstants.Discovery.DPoPSigningAlgValuesSupported),
        };

    private static string? GetOptionalString(JsonElement json, string propertyName) =>
        json.TryGetProperty(propertyName, out JsonElement element)
            ? element.GetString()
            : null;

    private static List<string> GetStringArray(JsonElement json, string propertyName)
    {
        if (!json.TryGetProperty(propertyName, out JsonElement element) ||
            element.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var items = new List<string>(element.GetArrayLength());

        foreach (JsonElement item in element.EnumerateArray())
        {
            string? value = item.GetString();
            if (value is not null)
            {
                items.Add(value);
            }
        }

        return items;
    }
}
