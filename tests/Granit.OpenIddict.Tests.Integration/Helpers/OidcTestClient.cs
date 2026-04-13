using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
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

    // ──── Token endpoint (private_key_jwt) ────

    public async Task<HttpResponseMessage> RawClientCredentialsWithAssertionAsync(
        string clientId, string privateKeyJwk, string? scope = null)
    {
        string assertion = BuildClientAssertionJwt(clientId, privateKeyJwk, DateTimeOffset.UtcNow.AddSeconds(60));

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_assertion"] = assertion,
            ["client_assertion_type"] = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
            ["scope"] = scope ?? "openid",
        });

        return await client.PostAsync("/connect/token", content);
    }

    /// <summary>
    /// Builds a client assertion JWT with a custom expiration, allowing tests to
    /// create expired assertions for negative test cases.
    /// </summary>
    internal static string BuildClientAssertionJwt(
        string clientId, string privateKeyJwk, DateTimeOffset expiration)
    {
        using var doc = JsonDocument.Parse(privateKeyJwk);
        JsonElement root = doc.RootElement;

        byte[] x = Base64UrlDecode(root.GetProperty("x").GetString()!);
        byte[] y = Base64UrlDecode(root.GetProperty("y").GetString()!);
        byte[] d = Base64UrlDecode(root.GetProperty("d").GetString()!);

        using var ecdsa = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = x, Y = y },
            D = d,
        });

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        string header = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["alg"] = "ES256",
        });

        string payload = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["iss"] = clientId,
            ["sub"] = clientId,
            ["aud"] = "http://localhost/connect/token",
            ["jti"] = Guid.NewGuid().ToString("N"),
            ["iat"] = now,
            ["exp"] = expiration.ToUnixTimeSeconds(),
        });

        string headerB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(header));
        string payloadB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(payload));
        string signingInput = $"{headerB64}.{payloadB64}";

        byte[] signature = ecdsa.SignData(
            Encoding.ASCII.GetBytes(signingInput),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        return $"{signingInput}.{Base64UrlEncode(signature)}";
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

    private static byte[] Base64UrlDecode(string base64Url)
    {
        string padded = base64Url
            .Replace('-', '+')
            .Replace('_', '/');

        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }

        return Convert.FromBase64String(padded);
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

        return await client.PostAsync("/account/register", content);
    }

    public async Task<HttpResponseMessage> GetProfileAsync(string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/account/profile");
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
