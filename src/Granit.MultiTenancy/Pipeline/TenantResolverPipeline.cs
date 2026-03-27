using Granit.MultiTenancy.Resolvers;
using Microsoft.AspNetCore.Http;

namespace Granit.MultiTenancy.Pipeline;

/// <summary>
/// Result of a tenant resolution attempt, including which resolver matched.
/// </summary>
/// <param name="Tenant">Resolved tenant info, or <c>null</c> if no resolver matched.</param>
/// <param name="ResolverType">Simple type name of the resolver that matched, or <c>"none"</c>.</param>
public sealed record TenantResolutionResult(TenantInfo? Tenant, string ResolverType);

/// <summary>
/// Pipeline for resolving the current tenant.
/// Executes resolvers in ascending order of <see cref="ITenantResolver.Order"/>.
/// </summary>
public sealed class TenantResolverPipeline(IEnumerable<ITenantResolver> resolvers)
{
    private readonly IReadOnlyList<ITenantResolver> _resolvers = [.. resolvers.OrderBy(r => r.Order)];

    /// <summary>
    /// Resolves the current tenant from the HTTP context.
    /// Returns the result of the first resolver that produces a non-null response,
    /// along with the resolver type name for diagnostics.
    /// </summary>
    public async Task<TenantResolutionResult> ResolveAsync(
        HttpContext context,
        CancellationToken cancellationToken = default)
    {
        foreach (ITenantResolver resolver in _resolvers)
        {
            TenantInfo? tenant = await resolver.ResolveAsync(context, cancellationToken).ConfigureAwait(false);
            if (tenant is not null)
            {
                return new TenantResolutionResult(tenant, resolver.GetType().Name);
            }
        }

        return new TenantResolutionResult(null, "none");
    }
}
