using System.Security.Claims;
using Granit.Authorization;

namespace Granit.MultiTenancy.Authorization;

/// <summary>
/// <see cref="IHostImpersonationGate"/> implementation that delegates to
/// <see cref="IPermissionChecker"/>: a Host user is permitted to impersonate a
/// tenant iff the principal has been granted <c>MultiTenancy.Host.Impersonate</c>.
/// </summary>
public sealed class PermissionBasedHostImpersonationGate(IPermissionChecker permissionChecker)
    : IHostImpersonationGate
{
    private static readonly HostImpersonationDecision Denied =
        new(false, HostImpersonationDecision.PermissionDenied);

    public async ValueTask<HostImpersonationDecision> CanImpersonateAsync(
        ClaimsPrincipal principal,
        Guid targetTenantId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);

        bool granted = await permissionChecker
            .IsGrantedAsync(MultiTenancyAuthorizationPermissions.Host.Impersonate, cancellationToken)
            .ConfigureAwait(false);

        return granted ? HostImpersonationDecision.Allow : Denied;
    }
}
