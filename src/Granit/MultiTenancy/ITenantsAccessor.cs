namespace Granit.MultiTenancy;

/// <summary>
/// Enumerates every tenant known to the application. Soft-dep primitive analogous to
/// <see cref="ICurrentTenant"/>: lives in base <c>Granit</c> so modules that need
/// cross-tenant iteration (for host-admin aggregation under
/// <c>DualScopeStorageMode.Segregated</c>, periodic cleanup jobs, etc.) can inject it
/// without taking a hard dependency on the <c>Granit.MultiTenancy</c> package.
/// </summary>
/// <remarks>
/// <para>
/// Default registration is <see cref="NullTenantsAccessor"/>, which returns an empty
/// list — graceful fallback for single-tenant deployments that do not load
/// <c>Granit.MultiTenancy</c>. The full <c>Granit.MultiTenancy</c> registration replaces
/// the default with an adapter over <c>ITenantReader</c>.
/// </para>
/// <para>
/// The returned shape is intentionally a minimal value tuple <c>(Guid Id, string Name)</c>
/// to avoid leaking the <c>TenantData</c> aggregate (which lives in
/// <c>Granit.MultiTenancy</c>). Consumers that need the full aggregate inject
/// <c>ITenantReader</c> directly and accept the package dependency.
/// </para>
/// </remarks>
public interface ITenantsAccessor
{
    /// <summary>
    /// Returns the identifier and display name of every tenant in the system.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An empty list when no tenants exist or when the default
    /// <see cref="NullTenantsAccessor"/> is in effect.</returns>
    Task<IReadOnlyList<(Guid Id, string Name)>> GetAllAsync(CancellationToken cancellationToken = default);
}
