namespace Granit.MultiTenancy;

/// <summary>
/// Resolves the base URL for the current tenant context.
/// </summary>
/// <remarks>
/// <para>
/// Used by email templates (<c>{{ app.base_url }}</c>), notification channels
/// (unsubscribe links), and identity handlers (password reset, email confirmation)
/// to generate tenant-aware URLs.
/// </para>
/// <para>
/// The resolution strategy is controlled by configuration:
/// <list type="bullet">
///   <item><b>Shared</b> — static fallback URL (default, backward-compatible)</item>
///   <item><b>Subdomain</b> — derived from domain template + tenant identifier</item>
///   <item><b>CustomDomain</b> — per-tenant custom domain from database</item>
///   <item><b>Hybrid</b> — custom domain override, subdomain fallback</item>
/// </list>
/// </para>
/// <para>
/// Consumers should resolve this interface as a soft dependency via
/// <c>IServiceProvider.GetService&lt;ITenantUrlResolver&gt;()</c> or Wolverine
/// optional parameter injection and fall back to static configuration when unavailable
/// (apps without multi-tenancy).
/// </para>
/// <para>
/// The default implementation is <c>NullTenantUrlResolver</c> (returns empty string).
/// When <c>Granit.MultiTenancy</c> is registered, it is replaced by
/// <c>TenantUrlResolver</c> which performs strategy-based resolution.
/// </para>
/// </remarks>
public interface ITenantUrlResolver
{
    /// <summary>
    /// Resolves the base URL for the current tenant.
    /// Returns the static fallback URL when no tenant context is active or
    /// the strategy is <b>Shared</b>.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The base URL without trailing slash (e.g., <c>"https://acme.example.com"</c>).
    /// Returns an empty string when no URL can be resolved.
    /// </returns>
    Task<string> ResolveBaseUrlAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the base URL for an explicit tenant, ignoring the ambient
    /// <see cref="ICurrentTenant"/> context.
    /// </summary>
    /// <remarks>
    /// Background handlers that act on behalf of a specific account (e.g. password
    /// reset, email confirmation) must resolve the URL from the account's own tenant
    /// — carried on the integration event — not from the ambient context, which is
    /// whatever tenant the originating HTTP request happened to resolve. A host
    /// account (<paramref name="tenantId"/> is <see langword="null"/>) requesting a
    /// reset from a tenant domain would otherwise receive a tenant-scoped link.
    /// </remarks>
    /// <param name="tenantId">
    /// The tenant to resolve the URL for, or <see langword="null"/> for a host/global
    /// account. <see langword="null"/> (and the <b>Shared</b> strategy) yields the
    /// static fallback URL.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The base URL without trailing slash, or an empty string when none can be resolved.
    /// </returns>
    Task<string> ResolveBaseUrlAsync(Guid? tenantId, CancellationToken cancellationToken = default);
}
