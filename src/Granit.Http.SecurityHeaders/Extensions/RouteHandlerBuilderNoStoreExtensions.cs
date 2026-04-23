using Granit.Http.SecurityHeaders.Filters;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.Http.SecurityHeaders.Extensions;

/// <summary>
/// Convenience extensions for marking a route as <c>Cache-Control: no-store</c>.
/// </summary>
public static class RouteHandlerBuilderNoStoreExtensions
{
    /// <summary>
    /// Attaches a <see cref="NoStoreEndpointFilter"/> so the response is never
    /// cached by browsers, proxies, or CDN intermediaries.
    /// </summary>
    /// <param name="builder">The route handler to harden.</param>
    /// <returns>The same route handler for chaining.</returns>
    public static RouteHandlerBuilder WithNoStoreResponse(this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddEndpointFilter<NoStoreEndpointFilter>();
    }

    /// <summary>
    /// Attaches a <see cref="NoStoreEndpointFilter"/> to every endpoint in the group.
    /// </summary>
    /// <param name="builder">The route group to harden.</param>
    /// <returns>The same route group for chaining.</returns>
    public static RouteGroupBuilder WithNoStoreResponse(this RouteGroupBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddEndpointFilter<NoStoreEndpointFilter>();
    }
}
