using System.Security.Claims;

namespace Granit.MultiTenancy.Authorization;

/// <summary>
/// Gates whether an authenticated Host user (a principal carrying no <c>tenant_id</c>
/// claim) may impersonate a specific tenant via a non-JWT resolver (header, query, domain).
/// </summary>
/// <remarks>
/// <para>
/// Granit ships a secure-by-default implementation (<c>DenyAllHostImpersonationGate</c>)
/// that refuses every call. Applications that want permission-based gating reference
/// <c>Granit.MultiTenancy.Authorization</c> and call
/// <c>AddGranitHostImpersonationWithPermissions()</c> — this replaces the default with
/// a gate that delegates to <c>IPermissionChecker</c>.
/// </para>
/// <para>
/// The dependency direction is one-way: <c>Granit.MultiTenancy</c> (infrastructure)
/// must not reference <c>Granit.Authorization</c> (application domain). The gate is the
/// abstraction that preserves that boundary.
/// </para>
/// </remarks>
public interface IHostImpersonationGate
{
    /// <summary>
    /// Determines whether the supplied principal may impersonate <paramref name="targetTenantId"/>.
    /// </summary>
    /// <param name="principal">The authenticated principal. Must be a Host user — callers verify
    /// the absence of a <c>tenant_id</c> claim before invoking the gate.</param>
    /// <param name="targetTenantId">The tenant identifier the principal is attempting to impersonate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<HostImpersonationDecision> CanImpersonateAsync(
        ClaimsPrincipal principal,
        Guid targetTenantId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Outcome of a <see cref="IHostImpersonationGate"/> check.
/// </summary>
/// <param name="Allowed"><c>true</c> if impersonation is permitted; <c>false</c> otherwise.</param>
/// <param name="DenyReasonCode">
/// Stable, non-localized reason code when <see cref="Allowed"/> is <c>false</c>.
/// Localization, if any, happens at the ProblemDetails layer. <c>null</c> when allowed.
/// </param>
public sealed record HostImpersonationDecision(bool Allowed, string? DenyReasonCode)
{
    /// <summary>Reason code emitted when no gate is wired and the deny-all default refuses.</summary>
    public const string NotConfigured = "HostImpersonation.NotConfigured";

    /// <summary>Reason code emitted when the principal lacks the required permission.</summary>
    public const string PermissionDenied = "HostImpersonation.PermissionDenied";

    /// <summary>Shared allow singleton — no allocation per call.</summary>
    public static readonly HostImpersonationDecision Allow = new(true, null);
}
