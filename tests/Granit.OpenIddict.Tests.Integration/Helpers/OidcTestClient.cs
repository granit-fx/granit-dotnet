using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Granit.OpenIddict.Tests.Integration.Helpers;

/// <summary>
/// Wraps <see cref="HttpClient"/> with helpers for OIDC token operations.
/// </summary>
public sealed class OidcTestClient(HttpClient client)
{
    // ──── Token endpoint ────

    public async Task<JsonDocument> ClientCredentialsAsync(
        string clientId, string clientSecret, string? scope = null)
    {
        HttpResponseMessage response = await RawClientCredentialsAsync(clientId, clientSecret, scope);
        response.EnsureSuccessStatusCode();
        string json = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(json);
    }

    public async Task<HttpResponseMessage> RawClientCredentialsAsync(
        string clientId, string clientSecret, string? scope = null)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["scope"] = scope ?? "openid",
        });

        return await client.PostAsync("/connect/token", content);
    }

    // ──── Introspection endpoint ────

    public async Task<JsonDocument> IntrospectAsync(
        string token, string clientId, string clientSecret)
    {
        HttpResponseMessage response = await RawIntrospectAsync(token, clientId, clientSecret);
        string json = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(json);
    }

    public async Task<HttpResponseMessage> RawIntrospectAsync(
        string token, string clientId, string clientSecret)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["token"] = token,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
        });

        return await client.PostAsync("/connect/introspect", content);
    }

    // ──── Revocation endpoint ────

    public async Task<HttpResponseMessage> RevokeAsync(
        string token, string clientId, string clientSecret)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["token"] = token,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
        });

        return await client.PostAsync("/connect/revoke", content);
    }

    // ──── Account endpoints ────

    public async Task<HttpResponseMessage> RegisterAsync(
        string email, string password, string? firstName = null, string? lastName = null)
    {
        var payload = new
        {
            email,
            password,
            firstName,
            lastName,
        };

        using var content = new StringContent(
            JsonSerializer.Serialize(payload),
            System.Text.Encoding.UTF8,
            "application/json");

        return await client.PostAsync("/api/account/register", content);
    }

    public async Task<HttpResponseMessage> GetProfileAsync(string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/account/profile");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await client.SendAsync(request);
    }

    // ──── Pushed Authorization Requests ────

    public async Task<HttpResponseMessage> RawPushedAuthorizationRequestAsync(
        string clientId, string clientSecret, string redirectUri, string? scope = null)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["response_type"] = "code",
            ["redirect_uri"] = redirectUri,
            ["scope"] = scope ?? "openid",
            ["code_challenge"] = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM",
            ["code_challenge_method"] = "S256",
        });

        return await client.PostAsync("/connect/par", content);
    }

    // ──── Discovery ────

    public async Task<JsonDocument> GetDiscoveryDocumentAsync()
    {
        HttpResponseMessage response = await client.GetAsync("/.well-known/openid-configuration");
        response.EnsureSuccessStatusCode();
        string json = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(json);
    }

    public Task<HttpResponseMessage> RawGetDiscoveryAsync() =>
        client.GetAsync("/.well-known/openid-configuration");
}
