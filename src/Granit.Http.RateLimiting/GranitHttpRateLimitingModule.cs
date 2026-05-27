using Granit.Http.ExceptionHandling;
using Granit.Http.RateLimiting.AspNetCore;
using Granit.Http.RateLimiting.Extensions;
using Granit.Modularity;
using Granit.RateLimiting;

namespace Granit.Http.RateLimiting;

/// <summary>
/// Granit module for the ASP.NET Core binding of rate limiting. Depends on the framework-pure
/// <see cref="GranitRateLimitingModule"/> (counter store, quota provider,
/// <see cref="TenantPartitionedRateLimiter"/>) and adds the HTTP-specific 429 mapping.
/// </summary>
/// <remarks>
/// Apply the per-endpoint filter with <c>.RequireGranitRateLimiting("policy")</c>
/// (see <see cref="RateLimitEndpointExtensions"/>).
/// </remarks>
[DependsOn(
    typeof(GranitRateLimitingModule),
    typeof(GranitHttpExceptionHandlingModule))]
public sealed class GranitHttpRateLimitingModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitHttpRateLimiting();
}
