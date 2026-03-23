using System.Security.Claims;
using System.Text.Json;
using Granit.Authentication.DPoP.Options;
using Granit.Authentication.DPoP.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

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

        // Only process authenticated requests with DPoP header or when DPoP is required
        bool hasDPoPHeader = context.Request.Headers.ContainsKey("DPoP");
        bool hasDPoPScheme = context.Request.Headers.Authorization
            .ToString().StartsWith("DPoP ", StringComparison.OrdinalIgnoreCase);

        if (!hasDPoPHeader && !hasDPoPScheme)
        {
            if (opts.RequireDPoP && context.User.Identity?.IsAuthenticated == true)
            {
                LogDPoPRequired(logger);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.Headers["WWW-Authenticate"] = "DPoP error=\"use_dpop_nonce\"";
                return;
            }

            await next(context).ConfigureAwait(false);
            return;
        }

        // Extract DPoP proof JWT
        string? proofJwt = context.Request.Headers["DPoP"].ToString();
        if (string.IsNullOrEmpty(proofJwt))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        // Build the request URI for htu validation
        string requestUri = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}";
        string httpMethod = context.Request.Method;

        // Validate the proof
        DPoPValidationResult result = await proofValidator.ValidateAsync(
            proofJwt, httpMethod, requestUri, context.RequestAborted).ConfigureAwait(false);

        if (!result.IsValid)
        {
            LogProofInvalid(logger, result.Error!);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        // Verify cnf.jkt token binding — the access token must contain a cnf claim
        // with a jkt (JWK thumbprint) matching the proof's public key
        string? expectedThumbprint = ExtractCnfJkt(context.User);
        if (expectedThumbprint is not null
            && !string.Equals(expectedThumbprint, result.JwkThumbprint, StringComparison.Ordinal))
        {
            LogThumbprintMismatch(logger);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await next(context).ConfigureAwait(false);
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
}
