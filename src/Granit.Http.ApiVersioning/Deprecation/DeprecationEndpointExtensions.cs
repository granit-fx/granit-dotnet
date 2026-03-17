using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Granit.Http.ApiVersioning.Deprecation;

/// <summary>
/// Extension methods for marking endpoints as deprecated.
/// </summary>
public static class DeprecationEndpointExtensions
{
    /// <summary>
    /// Marks the endpoint as deprecated, adding <c>Deprecation</c> and <c>Sunset</c>
    /// response headers per RFC 8594.
    /// </summary>
    /// <param name="builder">The route handler builder.</param>
    /// <param name="sunsetDate">
    /// Optional sunset date in ISO 8601 format (e.g. <c>"2025-11-01"</c>).
    /// </param>
    /// <param name="link">
    /// Optional URL to migration documentation.
    /// </param>
    /// <returns>The route handler builder for chaining.</returns>
    public static RouteHandlerBuilder Deprecated(
        this RouteHandlerBuilder builder,
        string? sunsetDate = null,
        string? link = null)
    {
        builder.WithMetadata(new DeprecatedAttribute { SunsetDate = sunsetDate, Link = link });
        builder.AddEndpointFilter<DeprecationEndpointFilter>();
        return builder;
    }
}
