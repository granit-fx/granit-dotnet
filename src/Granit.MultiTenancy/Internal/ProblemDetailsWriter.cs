using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;

namespace Granit.MultiTenancy.Internal;

/// <summary>
/// Writes an RFC 7807 <c>application/problem+json</c> body to the response
/// for short-circuited 403 responses emitted by <c>TenantResolutionMiddleware</c>.
/// </summary>
/// <remarks>
/// The middleware sets the status code and returns; ASP.NET Core does not
/// auto-emit a ProblemDetails body for middleware-level short-circuits, so we
/// write it explicitly here. The body shape follows RFC 7807 with two extension
/// members: <c>denyReasonCode</c> (stable string from <c>HostImpersonationDecision</c>)
/// and <c>tenantId</c> (target, when known).
/// </remarks>
internal static class ProblemDetailsWriter
{
    private const string ProblemContentType = "application/problem+json";

    public static Task WriteHostImpersonationDeniedAsync(
        HttpContext context,
        IStringLocalizer<MultiTenancyLocalizationResource> localizer,
        string denyReasonCode,
        Guid? tenantId)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(localizer);

        string detailKey = denyReasonCode switch
        {
            "HostImpersonation.NotConfigured" => "Problem:HostImpersonation.NotConfigured.Detail",
            "HostImpersonation.PermissionDenied" => "Problem:HostImpersonation.PermissionDenied.Detail",
            // Future codes fall back to a generic title-only payload — DenyReasonCode is
            // still emitted in the extensions so callers can disambiguate.
            _ => "Problem:HostImpersonation.PermissionDenied.Detail",
        };

        Dictionary<string, object?> payload = new(StringComparer.Ordinal)
        {
            ["type"] = "https://granit-fx.dev/errors/host-impersonation-denied",
            ["title"] = localizer["Problem:HostImpersonation.Title"].Value,
            ["status"] = StatusCodes.Status403Forbidden,
            ["detail"] = localizer[detailKey].Value,
            ["denyReasonCode"] = denyReasonCode,
        };
        if (tenantId.HasValue)
        {
            payload["tenantId"] = tenantId.Value;
        }

        return WriteAsync(context, payload);
    }

    public static Task WriteTenantMismatchAsync(
        HttpContext context,
        IStringLocalizer<MultiTenancyLocalizationResource> localizer,
        Guid resolvedTenantId,
        Guid jwtClaimTenantId)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(localizer);

        Dictionary<string, object?> payload = new(StringComparer.Ordinal)
        {
            ["type"] = "https://granit-fx.dev/errors/tenant-context-mismatch",
            ["title"] = localizer["Problem:TenantMismatch.Title"].Value,
            ["status"] = StatusCodes.Status403Forbidden,
            ["detail"] = localizer["Problem:TenantMismatch.Detail"].Value,
            ["resolvedTenantId"] = resolvedTenantId,
            ["claimTenantId"] = jwtClaimTenantId,
        };

        return WriteAsync(context, payload);
    }

    private static Task WriteAsync(HttpContext context, Dictionary<string, object?> payload)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = ProblemContentType;
        return context.Response.WriteAsync(
            JsonSerializer.Serialize(payload),
            context.RequestAborted);
    }
}
