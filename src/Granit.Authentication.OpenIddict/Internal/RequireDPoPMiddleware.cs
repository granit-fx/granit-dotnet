using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Granit.Authentication.OpenIddict.Internal;

/// <summary>
/// Middleware that enforces DPoP proof-of-possession on all authenticated requests.
/// When enabled, requests using <c>Authorization: Bearer</c> without a <c>DPoP</c>
/// proof header are rejected with 401 Unauthorized (FAPI 2.0 §5.3.4).
/// </summary>
/// <remarks>
/// Must be registered <strong>after</strong> authentication middleware. Only active
/// when <see cref="Options.GranitOpenIddictValidationOptions.RequireDPoP"/> is <c>true</c>.
/// OpenIddict handles the actual DPoP proof validation — this middleware only
/// rejects requests that bypass DPoP entirely by using the Bearer scheme.
/// </remarks>
internal sealed class RequireDPoPMiddleware(RequestDelegate next)
{
    private const string DPoPScheme = "DPoP";
    private const string BearerScheme = "Bearer";

    /// <summary>
    /// Rejects authenticated requests that use Bearer scheme without a DPoP proof.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("Authorization", out StringValues authHeader))
        {
            string? authValue = authHeader.ToString();

            // Reject Bearer tokens — DPoP is required
            if (authValue.StartsWith(BearerScheme, StringComparison.OrdinalIgnoreCase)
                && !context.Request.Headers.ContainsKey(DPoPScheme))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.Headers.WWWAuthenticate = "DPoP";
                return;
            }
        }

        await next(context).ConfigureAwait(false);
    }
}
