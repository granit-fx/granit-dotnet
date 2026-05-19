using System.Security.Claims;
using Granit.MultiTenancy.Authorization;
using Granit.MultiTenancy.Diagnostics;
using Granit.MultiTenancy.Internal;
using Granit.MultiTenancy.Options;
using Granit.MultiTenancy.Pipeline;
using Granit.MultiTenancy.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
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
    IHostImpersonationGate hostImpersonationGate,
    IHostImpersonationAuditWriter hostImpersonationAuditWriter,
    MultiTenancyMetrics metrics,
    IStringLocalizer<MultiTenancyLocalizationResource> localizer,
    IOptions<MultiTenancyOptions> options,
    ILogger<TenantResolutionMiddleware> logger) : IMiddleware
{
    private readonly ICurrentTenant _currentTenant = currentTenant;
    private readonly TenantResolverPipeline _pipeline = pipeline;
    private readonly ITenantReader _tenantReader = tenantReader;
    private readonly IHostImpersonationGate _hostImpersonationGate = hostImpersonationGate;
    private readonly IHostImpersonationAuditWriter _hostImpersonationAuditWriter = hostImpersonationAuditWriter;
    private readonly MultiTenancyMetrics _metrics = metrics;
    private readonly IStringLocalizer<MultiTenancyLocalizationResource> _localizer = localizer;
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
            if (context.User.Identity?.IsAuthenticated == true)
            {
                string? jwtClaim = context.User.FindFirstValue(_options.TenantIdClaimType);

                if (!string.IsNullOrEmpty(jwtClaim))
                {
                    // Tenant user: in CrossValidate mode the resolved tenant must match
                    // the JWT claim. In Unrestricted mode the header is trusted by
                    // configuration and the cross-check is skipped.
                    if (_options.HeaderTrustMode == TenantHeaderTrustMode.CrossValidate
                        && Guid.TryParse(jwtClaim, out Guid jwtTenantId)
                        && result.Tenant.Id is Guid resolvedTenantId
                        && jwtTenantId != resolvedTenantId)
                    {
                        _metrics.RecordResolutionFailed();
                        LogTenantMismatch(resolvedTenantId, result.ResolverType, jwtTenantId);
                        await ProblemDetailsWriter
                            .WriteTenantMismatchAsync(context, _localizer, resolvedTenantId, jwtTenantId)
                            .ConfigureAwait(false);
                        return;
                    }
                }
                else if (!result.IsAuthoritative && result.Tenant.Id.HasValue)
                {
                    // Host user (no tenant_id claim) attempting tenant impersonation via a
                    // non-JWT resolver (header, query, domain). The gate runs regardless of
                    // HeaderTrustMode: header trust and impersonation authorization are
                    // orthogonal — opting into Unrestricted trusts the proxy to scrub the
                    // header for Tenant users, not to waive permission checks for Host
                    // operators. Secure-by-default refuses unless wired up by
                    // Granit.MultiTenancy.Authorization.
                    HostImpersonationDecision decision = await _hostImpersonationGate
                        .CanImpersonateAsync(context.User, result.Tenant.Id.Value, context.RequestAborted)
                        .ConfigureAwait(false);

                    // Audit BEFORE short-circuiting so the trail captures attempts, not
                    // just successes. Audit-write failures must not propagate — wrap.
                    await SafeAuditAsync(context, result, decision).ConfigureAwait(false);

                    if (!decision.Allowed)
                    {
                        string reason = decision.DenyReasonCode ?? "unspecified";
                        _metrics.RecordHostImpersonationDenied(result.Tenant.Id.Value.ToString(), reason);
                        LogHostImpersonationDenied(result.Tenant.Id.Value, result.ResolverType, reason);
                        await ProblemDetailsWriter
                            .WriteHostImpersonationDeniedAsync(context, _localizer, reason, result.Tenant.Id)
                            .ConfigureAwait(false);
                        return;
                    }

                    _metrics.RecordHostImpersonationAllowed(result.Tenant.Id.Value.ToString());
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Host impersonation denied: principal attempting to impersonate {TenantId} via {ResolverType} — deny reason: {DenyReason}.")]
    private partial void LogHostImpersonationDenied(Guid tenantId, string resolverType, string denyReason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Host impersonation audit-write failed for tenant {TenantId}. Audit trail may be incomplete.")]
    private partial void LogAuditWriteFailed(Exception exception, Guid tenantId);

    private async ValueTask SafeAuditAsync(
        HttpContext context,
        TenantResolutionResult result,
        HostImpersonationDecision decision)
    {
        if (result.Tenant?.Id is not Guid tenantId)
        {
            return;
        }

        try
        {
            await _hostImpersonationAuditWriter.WriteAsync(
                context.User,
                tenantId,
                decision,
                result.ResolverType,
                context.Connection.RemoteIpAddress?.ToString(),
                context.Request.Headers.UserAgent.ToString(),
                context.TraceIdentifier,
                context.RequestAborted).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Audit failure must NEVER break the request — log + swallow.
            LogAuditWriteFailed(ex, tenantId);
        }
    }
}
