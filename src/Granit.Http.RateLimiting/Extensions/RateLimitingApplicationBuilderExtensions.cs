using Granit.Http.RateLimiting.AspNetCore;
using Microsoft.AspNetCore.Builder;

namespace Granit.Http.RateLimiting.Extensions;

/// <summary>
/// Extension methods for adding Granit rate limiting to the ASP.NET Core pipeline.
/// </summary>
/// <remarks>
/// Rate limiting is applied per-endpoint via <see cref="RateLimitEndpointExtensions.RequireGranitRateLimiting{TBuilder}"/>,
/// not as a global middleware. This extension is provided for future global policy support.
/// </remarks>
public static class RateLimitingApplicationBuilderExtensions
{
    /// <summary>
    /// Placeholder for future global rate limiting middleware.
    /// Currently, rate limiting is applied per-endpoint via <c>.RequireGranitRateLimiting("policy")</c>.
    /// </summary>
    public static IApplicationBuilder UseGranitRateLimiting(this IApplicationBuilder app) => app;
}
