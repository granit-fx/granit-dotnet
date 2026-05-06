using System.Security.Claims;
using Granit.MultiTenancy.Diagnostics;
using Granit.MultiTenancy.Options;
using Granit.MultiTenancy.Pipeline;
using Granit.MultiTenancy.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.MultiTenancy.Middleware;

/// <summary>
/// Middleware for per-request HTTP tenant resolution.
/// Uses <see cref="TenantResolverPipeline"/> and activates <see cref="ICurrentTenant"/>.
/// </summary>
/// <remarks>
/// When <see cref="MultiTenancyOptions.ValidateTenantExistence"/> is enabled, the resolved
/// tenant ID is verified against <see cref="ITenantReader"/> before activating the context.
/// This prevents phantom tenants (arbitrary GUIDs) from creating orphaned data.
/// </remarks>
public sealed partial class TenantResolutionMiddleware(
    ICurrentTenant currentTenant,
    TenantResolverPipeline pipeline,
    ITenantReader tenantReader,
    IUserTenantMembershipReader membershipReader,
    MultiTenancyMetrics metrics,
    IOptions<MultiTenancyOptions> options,
    ILogger<TenantResolutionMiddleware> logger) : IMiddleware
{
    private readonly ICurrentTenant _currentTenant = currentTenant;
    private readonly TenantResolverPipeline _pipeline = pipeline;
    private readonly ITenantReader _tenantReader = tenantReader;
    private readonly IUserTenantMembershipReader _membershipReader = membershipReader;
    private readonly MultiTenancyMetrics _metrics = metrics;
    private readonly MultiTenancyOptions _options = options.Value;
    private readonly ILogger _logger = logger;

    /// <inheritdoc/>
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (!_options.IsEnabled)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        TenantResolutionResult result = await _pipeline.ResolveAsync(context, context.RequestAborted).ConfigureAwait(false);

        if (result.Tenant is not null)
        {
            if (_options.HeaderTrustMode == TenantHeaderTrustMode.CrossValidate
                && context.User.Identity?.IsAuthenticated == true)
            {
                string? jwtClaim = context.User.FindFirstValue(_options.TenantIdClaimType);
                if (!string.IsNullOrEmpty(jwtClaim)
                    && Guid.TryParse(jwtClaim, out Guid jwtTenantId)
                    && jwtTenantId != result.Tenant.Id)
                {
                    _metrics.RecordResolutionFailed();
                    LogTenantMismatch(result.Tenant.Id!.Value, result.ResolverType, jwtTenantId);
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }
            }

            // Validate that the resolved tenant actually exists in the store.
            // Prevents phantom tenants (arbitrary GUIDs) from creating orphaned data.
            if (_options.ValidateTenantExistence
                && result.Tenant.Id.HasValue
                && !await _tenantReader.ExistsAsync(result.Tenant.Id.Value, context.RequestAborted).ConfigureAwait(false))
            {
                _metrics.RecordResolutionFailed();
                LogPhantomTenant(result.Tenant.Id.Value, result.ResolverType);
                // Bare 403 without response body: intentional information-minimal
                // rejection. Don't reveal to an attacker why the tenant was rejected.
                // Server-side observability is provided by LogPhantomTenant.
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            // Server-side membership check. Without it, an authenticated user whose
            // identity provider lets them set their own `tenant_id` claim could pivot
            // to any tenant. The check is opt-in via RequireMembershipCheck and only
            // runs when the request is actually authenticated — anonymous flows
            // (login, public webhooks) are unaffected.
            if (_options.RequireMembershipCheck
                && result.Tenant.Id.HasValue
                && context.User.Identity?.IsAuthenticated == true)
            {
                string? userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? context.User.FindFirstValue("sub");
                if (string.IsNullOrEmpty(userId)
                    || !await _membershipReader.IsMemberAsync(userId, result.Tenant.Id.Value, context.RequestAborted).ConfigureAwait(false))
                {
                    _metrics.RecordMembershipRejected();
                    LogMembershipRejected(userId ?? "(no sub claim)", result.Tenant.Id.Value, result.ResolverType);
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }
            }

            _metrics.RecordResolutionSucceeded(result.Tenant.Id.ToString()!, result.ResolverType);

            using IDisposable _ = _currentTenant.Change(result.Tenant.Id, result.Tenant.Name);
            await next(context).ConfigureAwait(false);
        }
        else
        {
            _metrics.RecordResolutionFailed();
            await next(context).ConfigureAwait(false);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Tenant mismatch: resolved tenant {ResolvedTenantId} via {ResolverType} but JWT claim contains {JwtTenantId}. Request rejected.")]
    private partial void LogTenantMismatch(Guid resolvedTenantId, string resolverType, Guid jwtTenantId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Phantom tenant rejected: resolved tenant {TenantId} via {ResolverType} does not exist in the tenant store.")]
    private partial void LogPhantomTenant(Guid tenantId, string resolverType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Membership check rejected user {UserId} for tenant {TenantId} (resolver: {ResolverType}). Possible tenant-claim spoofing attempt.")]
    private partial void LogMembershipRejected(string userId, Guid tenantId, string resolverType);
}
