using Granit.MultiTenancy;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Http.OutputCaching.Policies;

/// <summary>
/// Output cache policy that isolates cached responses by tenant ID.
/// </summary>
/// <remarks>
/// <para>
/// Resolves <see cref="ICurrentTenant"/> from <c>HttpContext.RequestServices</c> per request
/// (not constructor-injected — policies are singletons, tenant context is scoped).
/// </para>
/// <para>
/// When <see cref="ICurrentTenant.IsAvailable"/> is <c>true</c>:
/// <list type="bullet">
///   <item>Adds <c>__tenant</c> to <c>VaryByValues</c> for cache key isolation</item>
///   <item>Tags the response with <c>tenant:{id}</c> for per-tenant bulk eviction</item>
/// </list>
/// </para>
/// <para>
/// Soft dependency: works with or without <c>Granit.MultiTenancy</c>.
/// When no tenant context is available (single-tenant apps), this policy is a no-op.
/// </para>
/// </remarks>
internal sealed class TenantAwareOutputCachePolicy : IOutputCachePolicy
{
    internal const string TenantVaryByKey = "__tenant";
    internal const string TenantTagPrefix = "tenant:";

    /// <inheritdoc/>
    public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken cancellation)
    {
        ICurrentTenant? tenant = context.HttpContext.RequestServices.GetService<ICurrentTenant>();

        if (tenant is { IsAvailable: true, Id: { } tenantId })
        {
            string tenantIdString = tenantId.ToString();
            context.CacheVaryByRules.VaryByValues[TenantVaryByKey] = tenantIdString;
            context.Tags.Add($"{TenantTagPrefix}{tenantIdString}");
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellation) =>
        ValueTask.CompletedTask;

    /// <inheritdoc/>
    public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken cancellation) =>
        ValueTask.CompletedTask;
}
