using Granit.MultiTenancy.Options;
using Granit.MultiTenancy.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Granit.MultiTenancy.Resolvers;

/// <summary>
/// Resolves the tenant from the request <c>Host</c> header by matching against
/// the <c>CustomDomain</c> field stored on each tenant entity.
/// </summary>
/// <remarks>
/// <para>Order = 25 (highest priority) — custom domain resolution takes precedence
/// over subdomain (<see cref="DomainTenantResolver"/>, Order = 50) because a
/// custom domain is an explicit configuration that must not be shadowed by
/// template-based extraction.</para>
/// <para>Only active when <see cref="TenantUrlStrategy"/> is
/// <see cref="TenantUrlStrategy.CustomDomain"/> or
/// <see cref="TenantUrlStrategy.Hybrid"/>.</para>
/// </remarks>
public sealed class CustomDomainTenantResolver(
    ITenantReader tenantReader,
    IOptions<MultiTenancyOptions> options) : ITenantResolver
{
    private readonly MultiTenancyOptions _options = options.Value;

    /// <inheritdoc/>
    public int Order => 25;

    /// <inheritdoc/>
    public async Task<TenantInfo?> ResolveAsync(
        HttpContext context,
        CancellationToken cancellationToken = default)
    {
        // Only active for strategies that use custom domains
        if (_options.UrlStrategy is not (TenantUrlStrategy.CustomDomain
            or TenantUrlStrategy.Hybrid))
        {
            return null;
        }

        string host = context.Request.Host.Host;

        // Skip if the host matches the DomainTemplate pattern (handled by DomainTenantResolver)
        if (MatchesDomainTemplate(host))
        {
            return null;
        }

        TenantData? tenant = await tenantReader
            .FindByCustomDomainAsync(host, cancellationToken)
            .ConfigureAwait(false);

        if (tenant is null || !tenant.IsActive)
        {
            return null;
        }

        return new TenantInfo(tenant.Id, tenant.Name, tenant.Identifier, tenant.Jurisdiction);
    }

    /// <summary>
    /// Checks whether the host matches the configured <see cref="MultiTenancyOptions.DomainTemplate"/>
    /// pattern to avoid collisions with <see cref="DomainTenantResolver"/>.
    /// </summary>
    private bool MatchesDomainTemplate(string host)
    {
        string? template = _options.DomainTemplate;
        if (string.IsNullOrEmpty(template))
        {
            return false;
        }

        // Extract the suffix from the template (everything after {0})
        int placeholderIndex = template.IndexOf("{0}", StringComparison.Ordinal);
        if (placeholderIndex < 0)
        {
            return false;
        }

        string suffix = template[(placeholderIndex + 3)..];
        return suffix.Length > 0 && host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
    }
}
