using System.Security.Claims;
using System.Text.Json;
using Granit.Authentication.DPoP.Options;
using Granit.Authentication.DPoP.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Authentication.DPoP.Middleware;

/// <summary>
/// Middleware that validates DPoP proofs on authenticated requests.
/// Runs after authentication — extracts the <c>DPoP</c> header, validates the proof JWT,
/// and verifies the <c>cnf.jkt</c> token binding against the access token claims.
/// </summary>
internal sealed partial class DPoPValidationMiddleware(
    RequestDelegate next,
    IDPoPProofValidator proofValidator,
    IOptions<DPoPValidationOptions> options,
    ILogger<DPoPValidationMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        DPoPValidationOptions opts = options.Value;

        // Skip the OIDC authorization-server endpoints when co-located (monolith): the proof
        // presented at /connect/token is validated server-side, which records its jti for
        // replay protection. Validating the same proof here first would make the server handler
        // see a duplicate jti → "proof replay detected" → token exchange fails. See
        // DPoPValidationOptions.ExcludedPathPrefixes.
        if (opts.ExcludedPathPrefixes.Any(p =>
                context.Request.Path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase)))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        // Only process authenticated requests with DPoP header or when DPoP is required
        bool hasDPoPHeader = context.Request.Headers.ContainsKey("DPoP");
        bool hasDPoPScheme = context.Request.Headers.Authorization
            .ToString().StartsWith("DPoP ", StringComparison.OrdinalIgnoreCase);

        if (!hasDPoPHeader && !hasDPoPScheme)
        {
            await HandleMissingDPoPProofAsync(context, opts).ConfigureAwait(false);
            return;
        }

        // Detect middleware ordering issue: DPoP header is present but auth hasn't run yet
        if (opts.RequireDPoP && context.User.Identity is null)
        {
            LogMiddlewareOrderingError(logger);
        }

        // Extract DPoP proof JWT: dedicated DPoP header takes precedence over
        // the Authorization header with DPoP scheme (used at the token endpoint).
        string? proofJwt = hasDPoPHeader
            ? context.Request.Headers["DPoP"].ToString()
            : context.Request.Headers.Authorization.ToString()["DPoP ".Length..];

        if (string.IsNullOrWhiteSpace(proofJwt))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        // Build the request URI for htu validation
        // Include PathBase so htu matches behind a path-prefixed ingress (RFC 9449 §4.3): the
        // client builds htu from the externally-visible URL, which carries the prefix.
        string requestUri = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase}{context.Request.Path}";
        string httpMethod = context.Request.Method;

        // Access token the proof is presented with — bound via the ath claim (RFC 9449 §4.3).
        // When a dedicated DPoP proof header is present, the Authorization header carries the
        // access token (DPoP or Bearer scheme); extract it so ath is enforced at the resource.
        string? accessToken = ExtractAccessToken(context, hasDPoPHeader);

        // Validate the proof
        DPoPValidationResult result = await proofValidator.ValidateAsync(
            proofJwt, httpMethod, requestUri, accessToken, context.RequestAborted).ConfigureAwait(false);

        // Always return the server nonce for the next request (RFC 9449 §8)
        if (result.ServerNonce is not null)
        {
            context.Response.Headers["DPoP-Nonce"] = result.ServerNonce;
        }

        if (!result.IsValid)
        {
            LogProofInvalid(logger, result.Error!);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        // Verify cnf.jkt token binding (RFC 9449 §4.3)
        if (!VerifyTokenBinding(context, result.JwkThumbprint, opts))
        {
            return;
        }

        await next(context).ConfigureAwait(false);
    }

    /// <summary>
    /// Extracts the access token the DPoP proof is bound to. Only meaningful when a dedicated
    /// <c>DPoP</c> proof header is present, in which case the <c>Authorization</c> header carries
    /// the access token (<c>DPoP</c> or <c>Bearer</c> scheme). Returns null otherwise, so ath
    /// binding is skipped when no access token accompanies the proof.
    /// </summary>
    private static string? ExtractAccessToken(HttpContext context, bool hasDPoPHeader)
    {
        if (!hasDPoPHeader)
        {
            return null;
        }

        string authHeader = context.Request.Headers.Authorization.ToString();
        if (authHeader.StartsWith("DPoP ", StringComparison.OrdinalIgnoreCase))
        {
            return authHeader["DPoP ".Length..];
        }

        if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authHeader["Bearer ".Length..];
        }

        return null;
    }

    /// <summary>
    /// Handles requests without a DPoP proof header. If DPoP is required and the user
    /// is authenticated, returns 401 with a nonce hint. Otherwise passes through.
    /// </summary>
    private async Task HandleMissingDPoPProofAsync(HttpContext context, DPoPValidationOptions opts)
    {
        if (opts.RequireDPoP && context.User.Identity?.IsAuthenticated == true)
        {
            LogDPoPRequired(logger);

            // Return a fresh nonce for the client to use on retry (RFC 9449 §8)
            if (opts.RequireNonce)
            {
                string? nonce = await proofValidator.GenerateNonceAsync(context.RequestAborted)
                    .ConfigureAwait(false);
                if (nonce is not null)
                {
                    context.Response.Headers["DPoP-Nonce"] = nonce;
                }
            }

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate = "DPoP error=\"use_dpop_nonce\"";
            return;
        }

        await next(context).ConfigureAwait(false);
    }

    /// <summary>
    /// Verifies the cnf.jkt token binding between the access token and the DPoP proof.
    /// Returns <c>false</c> and sets 401 if verification fails.
    /// </summary>
    private bool VerifyTokenBinding(HttpContext context, string? jwkThumbprint, DPoPValidationOptions opts)
    {
        string? expectedThumbprint = ExtractCnfJkt(context.User);

        if (expectedThumbprint is not null
            && !string.Equals(expectedThumbprint, jwkThumbprint, StringComparison.Ordinal))
        {
            LogThumbprintMismatch(logger);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return false;
        }

        if (expectedThumbprint is null && opts.RequireTokenBinding)
        {
            LogMissingTokenBinding(logger);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Extracts the <c>cnf.jkt</c> value from the authenticated principal's claims.
    /// Keycloak and OpenIddict encode the confirmation claim as a JSON object:
    /// <c>{"jkt":"base64url-thumbprint"}</c>.
    /// </summary>
    private static string? ExtractCnfJkt(ClaimsPrincipal? principal)
    {
        Claim? cnfClaim = principal?.FindFirst("cnf");
        if (cnfClaim is null)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(cnfClaim.Value);
            return doc.RootElement.TryGetProperty("jkt", out JsonElement jkt)
                ? jkt.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "DPoP validation: proof required but not present")]
    private static partial void LogDPoPRequired(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "DPoP validation: proof invalid — {Error}")]
    private static partial void LogProofInvalid(ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "DPoP validation: JWK thumbprint mismatch (cnf.jkt binding failed)")]
    private static partial void LogThumbprintMismatch(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "DPoP validation: access token missing cnf.jkt claim (token binding required)")]
    private static partial void LogMissingTokenBinding(ILogger logger);

    [LoggerMessage(Level = LogLevel.Critical, Message = "DPoP validation: User.Identity is null — UseGranitDPoPValidation() must be placed after UseAuthentication() in the middleware pipeline")]
    private static partial void LogMiddlewareOrderingError(ILogger logger);
}
