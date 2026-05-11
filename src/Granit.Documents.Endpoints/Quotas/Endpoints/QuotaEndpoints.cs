using Granit.Documents;
using Granit.Documents.Domain;
using Granit.Documents.Endpoints.Quotas.Dtos;
using Granit.Documents.Permissions;
using Granit.MultiTenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Documents.Endpoints.Quotas.Endpoints;

/// <summary>
/// HTTP endpoint exposing the current tenant's storage quota usage (F7.3).
/// </summary>
internal static class QuotaEndpoints
{
    private const string TagName = "Documents - Quotas";

    public static RouteGroupBuilder MapQuotaEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        RouteGroupBuilder quotas = group.MapGroup("/quota").WithTags(TagName);

        quotas.MapGet("/", GetQuotaAsync)
            .WithName("GetTenantStorageQuota")
            .WithSummary("Returns the current tenant's storage quota usage.")
            .WithDescription(
                "Returns the active `LimitBytes` / `UsageBytes` pair for the calling tenant "
                + "plus a precomputed `percentUsed` ratio. The quota row is lazy-created on "
                + "first call (mirrors the F2.2 tenant-root bootstrap), so a brand-new "
                + "tenant always sees `UsageBytes = 0` instead of a 404. Returns 401 when "
                + "the request carries no tenant context.")
            .RequireAuthorization(p => p.RequireClaim("permission", DocumentsPermissions.Quotas.Read))
            .Produces<TenantStorageQuotaResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return quotas;
    }

    private static async Task<Results<Ok<TenantStorageQuotaResponse>, ProblemHttpResult>> GetQuotaAsync(
        [FromServices] ICurrentTenant currentTenant,
        [FromServices] ITenantQuotaService quotas,
        CancellationToken cancellationToken)
    {
        if (!currentTenant.IsAvailable || currentTenant.Id is not { } tenantId)
        {
            return TypedResults.Problem(
                "Tenant context is required to read the storage quota.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        TenantStorageQuota quota = await quotas
            .EnsureTenantQuotaAsync(tenantId, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(new TenantStorageQuotaResponse(
            quota.LimitBytes,
            quota.UsageBytes,
            ComputePercentUsed(quota.UsageBytes, quota.LimitBytes),
            quota.UpdatedAt));
    }

    /// <summary>
    /// Convenience ratio for the UI — clamps at 100 so callers don't render &gt; 100% when
    /// bookkeeping drift overshoots the limit before the F9.3 recompute reconciles it.
    /// Internal so endpoint-level tests can pin the rounding / clamp behaviour without
    /// going through HTTP.
    /// </summary>
    internal static double ComputePercentUsed(long usageBytes, long limitBytes) =>
        limitBytes <= 0
            ? 0d
            : Math.Round(Math.Min(100d, (double)usageBytes / limitBytes * 100d), 2);
}
