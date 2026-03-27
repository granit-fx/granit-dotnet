using System.Security.Claims;
using Granit.MultiTenancy.Diagnostics;
using Granit.MultiTenancy.Options;
using Granit.MultiTenancy.Pipeline;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.MultiTenancy.Middleware;

/// <summary>
/// Middleware for per-request HTTP tenant resolution.
/// Uses <see cref="TenantResolverPipeline"/> and activates <see cref="ICurrentTenant"/>.
/// </summary>
public sealed partial class TenantResolutionMiddleware(
    ICurrentTenant currentTenant,
    TenantResolverPipeline pipeline,
    MultiTenancyMetrics metrics,
    IOptions<MultiTenancyOptions> options,
    ILogger<TenantResolutionMiddleware> logger) : IMiddleware
{
    private readonly ICurrentTenant _currentTenant = currentTenant;
    private readonly TenantResolverPipeline _pipeline = pipeline;
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
}
