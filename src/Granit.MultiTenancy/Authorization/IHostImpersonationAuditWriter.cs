using System.Security.Claims;

namespace Granit.MultiTenancy.Authorization;

/// <summary>
/// Sink for host-impersonation audit events. Called once per gate decision —
/// both allowed and denied — by <c>TenantResolutionMiddleware</c>.
/// </summary>
/// <remarks>
/// <para>
/// Default implementation (<see cref="Internal.NullHostImpersonationAuditWriter"/>) is
/// a no-op. Applications that need a persistent audit trail (ISO 27001 A.12.4)
/// reference <c>Granit.MultiTenancy.Auditing</c> and call
/// <c>AddGranitHostImpersonationAuditing()</c>, which replaces the default with an
/// implementation that writes to <c>IAuditingWriter</c>.
/// </para>
/// <para>
/// Same dependency-direction rule as <see cref="IHostImpersonationGate"/>:
/// <c>Granit.MultiTenancy</c> defines the contract; the glue package brings in
/// the audit-store dependency.
/// </para>
/// </remarks>
public interface IHostImpersonationAuditWriter
{
    /// <summary>
    /// Records a host-impersonation attempt. Implementations must not throw —
    /// audit failure should be logged but never propagate to the request pipeline.
    /// </summary>
    /// <param name="principal">The host operator attempting to impersonate.</param>
    /// <param name="targetTenantId">Tenant the operator is targeting.</param>
    /// <param name="decision">Gate decision (allowed/denied + reason code).</param>
    /// <param name="resolverType">Resolver that produced the tenant (header, query, domain).</param>
    /// <param name="ipAddress">Source IP, if known.</param>
    /// <param name="userAgent">User-Agent header, if known.</param>
    /// <param name="correlationId">Distributed-tracing correlation id, if known.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask WriteAsync(
        ClaimsPrincipal principal,
        Guid targetTenantId,
        HostImpersonationDecision decision,
        string resolverType,
        string? ipAddress,
        string? userAgent,
        string? correlationId,
        CancellationToken cancellationToken = default);
}
