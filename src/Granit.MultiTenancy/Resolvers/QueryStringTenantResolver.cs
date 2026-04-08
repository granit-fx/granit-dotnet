using Granit.MultiTenancy.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Granit.MultiTenancy.Resolvers;

/// <summary>
/// Resolves the tenant from a query string parameter (e.g. <c>?__tenant={guid}</c>).
/// Intended for development and debugging — lowest priority (order = 300).
/// </summary>
/// <remarks>
/// Disabled by default (<see cref="MultiTenancyOptions.QueryStringParamName"/> is <c>null</c>).
/// Enable in <c>appsettings.Development.json</c> by setting to <c>"__tenant"</c>.
/// </remarks>
public sealed class QueryStringTenantResolver(
    IOptions<MultiTenancyOptions> options) : ITenantResolver
{
    private readonly MultiTenancyOptions _options = options.Value;

    /// <inheritdoc/>
    public int Order => 300;

    /// <inheritdoc/>
    public Task<TenantInfo?> ResolveAsync(
        HttpContext context,
        CancellationToken cancellationToken = default)
    {
        string? paramName = _options.QueryStringParamName;
        if (string.IsNullOrEmpty(paramName))
        {
            return Task.FromResult<TenantInfo?>(null);
        }

        if (!context.Request.Query.TryGetValue(paramName, out StringValues values))
        {
            return Task.FromResult<TenantInfo?>(null);
        }

        string? value = values.FirstOrDefault();
        if (string.IsNullOrEmpty(value) || !Guid.TryParse(value, out Guid tenantId))
        {
            return Task.FromResult<TenantInfo?>(null);
        }

        return Task.FromResult<TenantInfo?>(new TenantInfo(tenantId));
    }
}
