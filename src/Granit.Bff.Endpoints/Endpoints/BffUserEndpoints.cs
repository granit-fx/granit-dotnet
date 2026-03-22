using System.Diagnostics;
using System.Text.Json;
using Granit.Bff.Diagnostics;
using Granit.Bff.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Bff.Endpoints.Endpoints;

/// <summary>
/// BFF user endpoint. Returns filtered claims from the ID token for the SPA.
/// Registered per-frontend under <c>/{pathPrefix}/bff/user</c>.
/// </summary>
internal static class BffUserEndpoints
{
    internal static RouteGroupBuilder MapUserEndpoints(this RouteGroupBuilder group, BffFrontendOptions frontend)
    {
        group.MapGet("/user", (HttpContext httpContext,
                [FromServices] IBffTokenStore tokenStore,
                CancellationToken cancellationToken) =>
                HandleGetUserAsync(httpContext, frontend, tokenStore, cancellationToken))
            .WithName($"BffGetUser_{frontend.Name}")
            .WithSummary("Returns the current user's claims for the SPA.")
            .WithDescription(
                "Reads the session cookie, loads the ID token from the token store, decodes "
                + "the JWT payload, and returns a filtered set of claims: sub, name, email, roles, "
                + "tenantId, and sessionExpiresAt. Returns { authenticated: false } if no valid "
                + "session exists. Tokens are never exposed to the browser.")
            .Produces<BffUserResponse>()
            .Produces<BffUnauthenticatedResponse>();

        return group;
    }

#pragma warning disable GRAPI003 // Private handler — not a direct endpoint delegate; services are resolved via lambda
    private static async Task<Ok<object>> HandleGetUserAsync(
        HttpContext httpContext,
        BffFrontendOptions frontend,
        [FromServices] IBffTokenStore tokenStore,
        CancellationToken cancellationToken)
    {
        Activity? activity = BffActivitySource.Source.StartActivity(BffActivitySource.User);
        using IDisposable? activityScope = activity;

        string? sessionId = httpContext.Request.Cookies[frontend.SessionCookieName];

        if (string.IsNullOrEmpty(sessionId))
        {
            return TypedResults.Ok<object>(new BffUnauthenticatedResponse());
        }

#pragma warning disable GRSEC003 // Reading tokens — server-side only, never returned
        BffTokenSet? tokens = await tokenStore.GetAsync(frontend.Name, sessionId, cancellationToken)
            .ConfigureAwait(false);
#pragma warning restore GRSEC003

        if (tokens is null)
        {
            return TypedResults.Ok<object>(new BffUnauthenticatedResponse());
        }

        Dictionary<string, string>? claims = DecodeIdTokenClaims(tokens.IdToken);
        if (claims is null)
        {
            return TypedResults.Ok<object>(new BffUnauthenticatedResponse());
        }

        BffUserResponse response = new(
            Authenticated: true,
            Sub: claims.GetValueOrDefault("sub"),
            Name: claims.GetValueOrDefault("name"),
            Email: claims.GetValueOrDefault("email"),
            Roles: ExtractStringArray(claims, "roles"),
            TenantId: claims.GetValueOrDefault("tenant_id"),
            SessionExpiresAt: tokens.ExpiresAt);

        return TypedResults.Ok<object>(response);
    }
#pragma warning restore GRAPI003

    /// <summary>
    /// Decodes the payload of a JWT without validation (the token was already validated
    /// server-side during the token exchange). Extracts claims as a dictionary.
    /// </summary>
    private static Dictionary<string, string>? DecodeIdTokenClaims(string? idToken)
    {
        if (string.IsNullOrEmpty(idToken))
        {
            return null;
        }

        string[] parts = idToken.Split('.');
        if (parts.Length < 2)
        {
            return null;
        }

        try
        {
            // Add padding if needed for base64url decoding
            string payload = parts[1];
            payload = payload.Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }

            byte[] bytes = Convert.FromBase64String(payload);
            using var doc = JsonDocument.Parse(bytes);

            Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
            foreach (JsonProperty prop in doc.RootElement.EnumerateObject())
            {
                result[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                    ? prop.Value.GetString()!
                    : prop.Value.GetRawText();
            }

            return result;
        }
        catch (FormatException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string[] ExtractStringArray(Dictionary<string, string> claims, string key)
    {
        if (!claims.TryGetValue(key, out string? value))
        {
            // Try "role" (singular) as a fallback — common in many OIDC providers
            if (!claims.TryGetValue("role", out value))
            {
                return [];
            }
        }

        try
        {
            // Try parsing as JSON array
            using var doc = JsonDocument.Parse(value);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                return doc.RootElement
                    .EnumerateArray()
                    .Select(e => e.GetString()!)
                    .Where(s => s is not null)
                    .ToArray();
            }

            // Single value — wrap in array
            return [doc.RootElement.GetString()!];
        }
        catch (JsonException)
        {
            // Plain string value
            return string.IsNullOrEmpty(value) ? [] : [value];
        }
    }
}

/// <summary>User claims response for authenticated sessions.</summary>
internal sealed record BffUserResponse(
    bool Authenticated,
    string? Sub,
    string? Name,
    string? Email,
    string[] Roles,
    string? TenantId,
    DateTimeOffset SessionExpiresAt);

/// <summary>Response for unauthenticated sessions.</summary>
internal sealed record BffUnauthenticatedResponse(bool Authenticated = false);
