using System.Security.Claims;
using Granit.MultiTenancy.Authorization;

namespace Granit.MultiTenancy.Internal;

/// <summary>
/// Default no-op <see cref="IHostImpersonationAuditWriter"/>. The audit metric
/// <c>granit.multi_tenancy.host_impersonation</c> still fires regardless;
/// only the persistent trail is omitted until <c>Granit.MultiTenancy.Auditing</c>
/// is wired.
/// </summary>
internal sealed class NullHostImpersonationAuditWriter : IHostImpersonationAuditWriter
{
    public ValueTask WriteAsync(
        ClaimsPrincipal principal,
        Guid targetTenantId,
        HostImpersonationDecision decision,
        string resolverType,
        string? ipAddress,
        string? userAgent,
        string? correlationId,
        CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;
}
