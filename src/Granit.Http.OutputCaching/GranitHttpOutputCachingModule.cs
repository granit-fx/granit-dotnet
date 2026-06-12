using Granit.Http.OutputCaching.Extensions;
using Granit.Modularity;

namespace Granit.Http.OutputCaching;

/// <summary>
/// Granit module for HTTP response output caching with privacy-safe, tenant-aware defaults.
/// </summary>
/// <remarks>
/// Registers the ASP.NET Core output caching middleware with:
/// <list type="bullet">
///   <item>Authenticated and credentialed responses excluded by default (private-response isolation)</item>
///   <item>Automatic tenant ID cache key isolation (when <c>Granit.MultiTenancy</c> is active)</item>
///   <item>Tag-based eviction per module and per tenant via <see cref="Eviction.IOutputCacheEvictionService"/></item>
/// </list>
/// <para>
/// The in-memory store is used by default. For distributed caching across pods,
/// install <c>Granit.Http.OutputCaching.StackExchangeRedis</c>.
/// </para>
/// <para>
/// <b>Middleware ordering:</b> call <c>UseGranitOutputCaching()</c> after <c>UseCors()</c>
/// and after <c>UseRouting()</c>.
/// </para>
/// </remarks>
public sealed class GranitHttpOutputCachingModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitOutputCaching();
}
