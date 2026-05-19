using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Granit.MultiTenancy.Internal;

/// <summary>
/// Builds RFC 7807 <c>application/problem+json</c> bodies for short-circuited 403
/// responses emitted by <c>TenantResolutionMiddleware</c> and writes them through
/// <see cref="IProblemDetailsService"/> for consistency with the rest of the API.
/// </summary>
/// <remarks>
/// ASP.NET Core does not auto-emit a ProblemDetails body for middleware-level
/// short-circuits, so we drive the emission explicitly. Going through
/// <see cref="IProblemDetailsService"/> (instead of a hand-rolled
/// <c>JsonSerializer.Serialize</c>) reuses the writers configured by the host
/// and shares any custom <c>CustomizeProblemDetails</c> hooks with
/// <c>Granit.Http.ExceptionHandling</c>.
/// </remarks>
internal static class ProblemDetailsWriter
{
    public static Task WriteHostImpersonationDeniedAsync(
        HttpContext context,
        IProblemDetailsService problemDetailsService,
        IStringLocalizer<MultiTenancyLocalizationResource> localizer,
        string denyReasonCode,
        Guid? tenantId)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(problemDetailsService);
        ArgumentNullException.ThrowIfNull(localizer);

        string detailKey = denyReasonCode switch
        {
            "HostImpersonation.NotConfigured" => "Problem:HostImpersonation.NotConfigured.Detail",
            "HostImpersonation.PermissionDenied" => "Problem:HostImpersonation.PermissionDenied.Detail",
            // Future codes fall back to a generic title-only payload — DenyReasonCode is
            // still emitted in the extensions so callers can disambiguate.
            _ => "Problem:HostImpersonation.PermissionDenied.Detail",
        };

        ProblemDetails problemDetails = new()
        {
            Type = "https://granit-fx.dev/errors/host-impersonation-denied",
            Title = localizer["Problem:HostImpersonation.Title"].Value,
            Status = StatusCodes.Status403Forbidden,
            Detail = localizer[detailKey].Value,
        };
        problemDetails.Extensions["denyReasonCode"] = denyReasonCode;
        if (tenantId.HasValue)
        {
            problemDetails.Extensions["tenantId"] = tenantId.Value;
        }

        return WriteAsync(context, problemDetailsService, problemDetails);
    }

    public static Task WriteTenantMismatchAsync(
        HttpContext context,
        IProblemDetailsService problemDetailsService,
        IStringLocalizer<MultiTenancyLocalizationResource> localizer,
        Guid resolvedTenantId,
        Guid jwtClaimTenantId)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(problemDetailsService);
        ArgumentNullException.ThrowIfNull(localizer);

        ProblemDetails problemDetails = new()
        {
            Type = "https://granit-fx.dev/errors/tenant-context-mismatch",
            Title = localizer["Problem:TenantMismatch.Title"].Value,
            Status = StatusCodes.Status403Forbidden,
            Detail = localizer["Problem:TenantMismatch.Detail"].Value,
        };
        problemDetails.Extensions["resolvedTenantId"] = resolvedTenantId;
        problemDetails.Extensions["claimTenantId"] = jwtClaimTenantId;

        return WriteAsync(context, problemDetailsService, problemDetails);
    }

    private static async Task WriteAsync(
        HttpContext context,
        IProblemDetailsService problemDetailsService,
        ProblemDetails problemDetails)
    {
        // IProblemDetailsService relies on the response's status code to drive
        // negotiation and writer selection — set it before calling.
        context.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status403Forbidden;

        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problemDetails,
        }).ConfigureAwait(false);
    }
}
