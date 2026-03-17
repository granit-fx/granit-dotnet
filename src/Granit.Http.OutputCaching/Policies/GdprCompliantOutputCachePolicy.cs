using Microsoft.AspNetCore.OutputCaching;

namespace Granit.Http.OutputCaching.Policies;

/// <summary>
/// Output cache policy that enforces GDPR-safe defaults:
/// <list type="bullet">
///   <item>Authenticated requests are excluded from caching (no personal data leaks)</item>
///   <item>Responses with <c>Set-Cookie</c> headers are never stored</item>
/// </list>
/// </summary>
/// <remarks>
/// Registered as a singleton base policy. Resolves authentication state from
/// <see cref="Microsoft.AspNetCore.Http.HttpContext.User"/> per request.
/// </remarks>
internal sealed class GdprCompliantOutputCachePolicy : IOutputCachePolicy
{
    /// <inheritdoc/>
    public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken cancellation)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated == true)
        {
            context.EnableOutputCaching = false;
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellation) =>
        ValueTask.CompletedTask;

    /// <inheritdoc/>
    public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken cancellation)
    {
        if (context.HttpContext.Response.Headers.ContainsKey("Set-Cookie"))
        {
            context.AllowCacheStorage = false;
        }

        return ValueTask.CompletedTask;
    }
}
