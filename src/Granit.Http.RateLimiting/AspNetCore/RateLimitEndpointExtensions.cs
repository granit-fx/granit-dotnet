using Granit.RateLimiting;
using Granit.RateLimiting.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

namespace Granit.Http.RateLimiting.AspNetCore;

/// <summary>
/// Extension methods for applying rate limiting to ASP.NET Core endpoints.
/// </summary>
public static class RateLimitEndpointExtensions
{
    /// <summary>
    /// Applies the specified rate limiting policy to the endpoint.
    /// </summary>
    /// <remarks>
    /// IP-partitioned policies read <see cref="Microsoft.AspNetCore.Http.ConnectionInfo.RemoteIpAddress"/>.
    /// Behind a reverse proxy / ingress, register the <c>ForwardedHeaders</c> middleware
    /// (<c>X-Forwarded-For</c>) upstream — otherwise every client collapses into the
    /// proxy's IP bucket and the limit is effectively shared by all traffic.
    /// </remarks>
    /// <param name="builder">The endpoint convention builder.</param>
    /// <param name="policyName">Name of the rate limiting policy defined in <c>RateLimiting:Policies</c>.</param>
    public static TBuilder RequireGranitRateLimiting<TBuilder>(this TBuilder builder, string policyName)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(policyName);

        return builder.AddEndpointFilter(async (context, next) =>
        {
            TenantPartitionedRateLimiter limiter = context.HttpContext.RequestServices
                .GetRequiredService<TenantPartitionedRateLimiter>();

            string? clientIp = context.HttpContext.Connection.RemoteIpAddress?.ToString();

            RateLimitResult? result = await limiter.CheckAsync(policyName, clientIp, context.HttpContext.RequestAborted)
                .ConfigureAwait(false);

            if (result is { IsAllowed: false })
            {
                context.HttpContext.Response.Headers[HeaderNames.RetryAfter] = ((int)Math.Ceiling(result.RetryAfter.TotalSeconds)).ToString();

                return TypedResults.Problem(
                    detail: "Too many requests. Please retry later.",
                    statusCode: StatusCodes.Status429TooManyRequests,
                    title: "Too Many Requests",
                    extensions: new Dictionary<string, object?>
                    {
                        ["limit"] = result.Limit,
                        ["remaining"] = result.Remaining,
                        ["retryAfter"] = (int)Math.Ceiling(result.RetryAfter.TotalSeconds),
                    });
            }

            if (result is not null)
            {
                context.HttpContext.Response.OnStarting(() =>
                {
                    context.HttpContext.Response.Headers["X-RateLimit-Limit"] = result.Limit.ToString();
                    context.HttpContext.Response.Headers["X-RateLimit-Remaining"] = result.Remaining.ToString();
                    return Task.CompletedTask;
                });
            }

            return await next(context).ConfigureAwait(false);
        });
    }
}
