namespace Granit.MultiTenancy.Stores;

/// <summary>
/// Verifies that a user is a member of a given tenant.
/// </summary>
/// <remarks>
/// <para>
/// SECURITY: the JWT <c>tenant_id</c> claim is trusted today by
/// <see cref="Resolvers.JwtClaimTenantResolver"/> to identify the active tenant.
/// If the upstream identity provider is misconfigured to allow the user to set
/// their own claim value, that user can pivot to any tenant they choose. This
/// reader provides server-side verification: the claim is honored only when the
/// user is recorded as a member of that tenant.
/// </para>
/// <para>
/// Concrete implementations are shipped by the identity / membership module
/// (e.g. <c>Granit.Identity.Local.EntityFrameworkCore</c>). When no real
/// implementation is registered, <see cref="NullUserTenantMembershipReader"/>
/// is the permissive fallback (returns <see langword="true"/>) so that
/// downstream code paths do not fail in single-tenant or test deployments.
/// </para>
/// <para>
/// Enforcement is gated by
/// <see cref="Options.MultiTenancyOptions.RequireMembershipCheck"/>; when that
/// flag is enabled, <see cref="Middleware.TenantResolutionMiddleware"/> calls
/// <see cref="IsMemberAsync"/> after resolution and rejects the request with
/// 403 if it returns <see langword="false"/>.
/// </para>
/// </remarks>
public interface IUserTenantMembershipReader
{
    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="userId"/> is a member of the
    /// tenant identified by <paramref name="tenantId"/>.
    /// </summary>
    /// <param name="userId">The user identifier (typically the <c>sub</c> JWT claim).</param>
    /// <param name="tenantId">The tenant identifier resolved from the request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> IsMemberAsync(string userId, Guid tenantId, CancellationToken cancellationToken = default);
}
