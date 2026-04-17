using Granit.MultiTenancy.Options;
using Granit.MultiTenancy.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Granit.MultiTenancy.Resolvers;

/// <summary>
/// Resolves the tenant from the request <c>Host</c> header using a configurable
/// domain template (e.g. <c>{0}.monsaas.com</c>). The <c>{0}</c> placeholder is
/// extracted as the tenant identifier and resolved via <see cref="ITenantReader"/>.
/// </summary>
/// <remarks>
/// <para>Order = 50 (highest priority) — subdomain resolution takes precedence over
/// header and JWT claim in SaaS deployments.</para>
/// <para>Disabled when <see cref="MultiTenancyOptions.DomainTemplate"/> is <c>null</c>
/// or empty.</para>
/// </remarks>
public sealed class DomainTenantResolver(
    ITenantReader tenantReader,
    IOptions<MultiTenancyOptions> options) : ITenantResolver
{
    private readonly MultiTenancyOptions _options = options.Value;

    /// <inheritdoc/>
    public int Order => 50;

    /// <inheritdoc/>
    public async Task<TenantInfo?> ResolveAsync(
        HttpContext context,
        CancellationToken cancellationToken = default)
    {
        string? template = _options.DomainTemplate;
        if (string.IsNullOrEmpty(template))
        {
            return null;
        }

        string host = context.Request.Host.Host;
        string? identifier = ExtractIdentifier(host, template);
        if (identifier is null)
        {
            return null;
        }

        TenantData? tenant = await tenantReader
            .FindByIdentifierAsync(identifier, cancellationToken)
            .ConfigureAwait(false);

        if (tenant is null || !tenant.Activated)
        {
            return null;
        }

        return new TenantInfo(tenant.Id, tenant.Name, tenant.Identifier, tenant.Jurisdiction);
    }

    /// <summary>
    /// Extracts the tenant identifier from a host string using the configured template.
    /// </summary>
    /// <param name="host">The request host (e.g. <c>acme.example.com</c>).</param>
    /// <param name="template">The domain template with <c>{0}</c> placeholder (e.g. <c>{0}.example.com</c>).</param>
    /// <returns>The extracted identifier, or <c>null</c> if the host doesn't match.</returns>
    internal static string? ExtractIdentifier(string host, string template)
    {
        int placeholderIndex = template.IndexOf("{0}", StringComparison.Ordinal);
        if (placeholderIndex < 0)
        {
            return null;
        }

        string prefix = template[..placeholderIndex];
        string suffix = template[(placeholderIndex + 3)..];

        if (suffix.Length > 0 && !host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (prefix.Length > 0 && !host.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        int identifierLength = host.Length - prefix.Length - suffix.Length;
        if (identifierLength <= 0)
        {
            return null;
        }

        return host[prefix.Length..(prefix.Length + identifierLength)];
    }
}
