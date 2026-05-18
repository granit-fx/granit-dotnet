using System.Security.Claims;
using Granit.MultiTenancy.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Granit.MultiTenancy.Resolvers;

/// <summary>
/// Resolves the tenant from the JWT claim (order = 200, resolved after the header).
/// </summary>
public sealed class JwtClaimTenantResolver(IOptions<MultiTenancyOptions> options) : ITenantResolver
{
    private readonly MultiTenancyOptions _options = options.Value;

    /// <inheritdoc/>
    public int Order => 200;

    /// <inheritdoc/>
    public bool IsAuthoritative => true;

    /// <inheritdoc/>
    public Task<TenantInfo?> ResolveAsync(HttpContext context, CancellationToken cancellationToken = default)
    {
        string? claim = context.User.FindFirstValue(_options.TenantIdClaimType);
        if (string.IsNullOrEmpty(claim))
        {
            return Task.FromResult<TenantInfo?>(null);
        }

        if (!Guid.TryParse(claim, out Guid tenantId))
        {
            return Task.FromResult<TenantInfo?>(null);
        }

        return Task.FromResult<TenantInfo?>(new TenantInfo(tenantId));
    }
}
