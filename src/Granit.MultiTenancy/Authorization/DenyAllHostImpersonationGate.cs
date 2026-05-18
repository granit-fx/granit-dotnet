using System.Security.Claims;

namespace Granit.MultiTenancy.Authorization;

/// <summary>
/// Default <see cref="IHostImpersonationGate"/> implementation: refuses every impersonation
/// attempt with the <see cref="HostImpersonationDecision.NotConfigured"/> reason code.
/// </summary>
/// <remarks>
/// Registered by <c>AddGranitMultiTenancy()</c> as the secure-by-default behaviour.
/// Apps that want permission-based gating add a project ref to
/// <c>Granit.MultiTenancy.Authorization</c> and call
/// <c>AddGranitHostImpersonationWithPermissions()</c>, which replaces this implementation
/// with one that delegates to <c>IPermissionChecker</c>.
/// </remarks>
internal sealed class DenyAllHostImpersonationGate : IHostImpersonationGate
{
    private static readonly HostImpersonationDecision Denied =
        new(false, HostImpersonationDecision.NotConfigured);

    public ValueTask<HostImpersonationDecision> CanImpersonateAsync(
        ClaimsPrincipal principal,
        Guid targetTenantId,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Denied);
}
