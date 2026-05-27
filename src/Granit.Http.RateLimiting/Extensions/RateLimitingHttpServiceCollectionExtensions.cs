using Granit.Http.ExceptionHandling;
using Granit.Http.RateLimiting.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Http.RateLimiting.Extensions;

/// <summary>
/// Extension methods for registering the ASP.NET Core binding of Granit rate limiting.
/// </summary>
public static class RateLimitingHttpServiceCollectionExtensions
{
    /// <summary>
    /// Registers the HTTP-specific rate limiting services: the RFC 7807 mapping of
    /// <see cref="Granit.RateLimiting.Exceptions.RateLimitExceededException"/> to HTTP 429.
    /// </summary>
    /// <remarks>
    /// Call <c>AddGranitRateLimiting()</c> (from <c>Granit.RateLimiting</c>) for the core counter
    /// store and <see cref="Granit.RateLimiting.TenantPartitionedRateLimiter"/>. The Granit module
    /// system wires both automatically via <see cref="GranitHttpRateLimitingModule"/>.
    /// </remarks>
    public static IServiceCollection AddGranitHttpRateLimiting(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IExceptionStatusCodeMapper, RateLimitExceptionStatusCodeMapper>();

        return services;
    }
}
