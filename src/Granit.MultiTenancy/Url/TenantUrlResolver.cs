using System.Collections.Concurrent;
using Granit.MultiTenancy.Events;
using Granit.MultiTenancy.Options;
using Granit.MultiTenancy.Stores;
using Microsoft.Extensions.Options;

namespace Granit.MultiTenancy.Url;

/// <summary>
/// Resolves the base URL for the current tenant based on the configured
/// <see cref="TenantUrlStrategy"/>.
/// </summary>
/// <remarks>
/// <para>
/// Registered as <b>scoped</b> because it depends on <see cref="ICurrentTenant"/>
/// (async-local) and <see cref="ITenantReader"/> (scoped, EF Core).
/// </para>
/// <para>
/// Tenant URL data is cached in a static <see cref="ConcurrentDictionary{TKey,TValue}"/>
/// to avoid repeated database lookups. Cache entries expire after 5 minutes.
/// The cache is invalidated by <see cref="TenantUpdatedEvent"/> and
/// <see cref="TenantCustomDomainChangedEvent"/> handlers.
/// </para>
/// </remarks>
internal sealed class TenantUrlResolver(
    ICurrentTenant currentTenant,
    ITenantReader tenantReader,
    IOptions<MultiTenancyOptions> options) : ITenantUrlResolver
{
    // Per-replica in-memory cache: entries are NOT invalidated across replicas. When a tenant's
    // URL or custom domain changes, Evict() fires on the replica handling the request, while
    // other replicas serve stale data for up to CacheDuration. This is intentional — URL
    // resolution tolerates ~5 min eventual consistency and avoids a distributed cache dependency.
    private static readonly ConcurrentDictionary<Guid, (TenantUrlData Data, long ExpiresAtTicks)> Cache = new();
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private readonly MultiTenancyOptions _options = options.Value;

    /// <inheritdoc/>
    public async Task<string> ResolveBaseUrlAsync(CancellationToken cancellationToken = default)
    {
        if (_options.UrlStrategy == TenantUrlStrategy.Shared || !currentTenant.IsAvailable)
        {
            return GetFallbackUrl();
        }

        Guid tenantId = currentTenant.Id!.Value;
        TenantUrlData? urlData = await GetOrLoadUrlDataAsync(tenantId, cancellationToken).ConfigureAwait(false);

        if (urlData is null)
        {
            return GetFallbackUrl();
        }

        return _options.UrlStrategy switch
        {
            TenantUrlStrategy.Subdomain => BuildSubdomainUrl(urlData.Identifier),
            TenantUrlStrategy.CustomDomain => BuildCustomDomainUrl(urlData.CustomDomain),
            TenantUrlStrategy.Hybrid => BuildHybridUrl(urlData),
            _ => GetFallbackUrl(),
        };
    }

    /// <summary>
    /// Evicts the cached URL data for a tenant. Called by domain event handlers
    /// when tenant details or custom domain change.
    /// </summary>
    internal static void Evict(Guid tenantId) => Cache.TryRemove(tenantId, out _);

    private async Task<TenantUrlData?> GetOrLoadUrlDataAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (Cache.TryGetValue(tenantId, out (TenantUrlData Data, long ExpiresAtTicks) entry) && entry.ExpiresAtTicks > Environment.TickCount64)
        {
            return entry.Data;
        }

        TenantData? tenant = await tenantReader.FindByIdAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (tenant is null)
        {
            return null;
        }

        TenantUrlData data = new(tenant.Identifier, tenant.CustomDomain);
        long expiresAt = Environment.TickCount64 + (long)CacheDuration.TotalMilliseconds;
        Cache[tenantId] = (data, expiresAt);
        return data;
    }

    private string BuildSubdomainUrl(string identifier)
    {
        string? template = _options.DomainTemplate;
        if (string.IsNullOrEmpty(template))
        {
            return GetFallbackUrl();
        }

        string domain = template.Replace("{0}", identifier, StringComparison.Ordinal);
        return $"{_options.UrlScheme}://{domain}";
    }

    private string BuildCustomDomainUrl(string? customDomain) =>
        !string.IsNullOrEmpty(customDomain)
            ? $"{_options.UrlScheme}://{customDomain}"
            : GetFallbackUrl();

    private string BuildHybridUrl(TenantUrlData data) =>
        !string.IsNullOrEmpty(data.CustomDomain)
            ? $"{_options.UrlScheme}://{data.CustomDomain}"
            : BuildSubdomainUrl(data.Identifier);

    private string GetFallbackUrl() =>
        _options.FallbackBaseUrl?.TrimEnd('/') ?? string.Empty;
}
