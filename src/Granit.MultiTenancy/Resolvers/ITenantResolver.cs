using Microsoft.AspNetCore.Http;

namespace Granit.MultiTenancy.Resolvers;

/// <summary>
/// Tenant resolver for an HTTP context.
/// Resolvers are chained in ascending order of <see cref="Order"/>.
/// </summary>
public interface ITenantResolver
{
    /// <summary>
    /// Resolution priority. Lower value = resolved first.
    /// HeaderTenantResolver = 100, JwtClaimTenantResolver = 200.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// When <c>true</c>, this resolver's output is treated as authoritative —
    /// it derives the tenant from a server-validated artefact (e.g. a signed JWT
    /// claim) and is not subject to host-impersonation gating. Default <c>false</c>
    /// — header, query, and domain resolvers must pass through
    /// <c>IHostImpersonationGate</c> when the principal is a Host user.
    /// </summary>
    bool IsAuthoritative => false;

    /// <summary>
    /// Attempts to resolve the tenant from the HTTP context.
    /// </summary>
    /// <param name="context">HTTP context of the request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved <see cref="TenantInfo"/>, or <c>null</c> if it cannot be determined.</returns>
    Task<TenantInfo?> ResolveAsync(HttpContext context, CancellationToken cancellationToken = default);
}
