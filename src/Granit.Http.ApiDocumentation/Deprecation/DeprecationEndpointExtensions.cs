using Microsoft.AspNetCore.Builder;

namespace Granit.Http.ApiDocumentation.Deprecation;

/// <summary>
/// Extension methods for marking endpoints as deprecated.
/// </summary>
public static class DeprecationEndpointExtensions
{
    /// <summary>
    /// Marks the endpoint as deprecated. The deprecation headers middleware
    /// (auto-registered by <c>AddGranitApiDocumentation</c>) then emits
    /// <c>Deprecation</c>, <c>Sunset</c>, and <c>Link</c> response headers
    /// per RFC 8594 — this method only attaches metadata.
    /// </summary>
    /// <param name="builder">The route handler builder.</param>
    /// <param name="sunsetDate">
    /// Optional date after which the endpoint will be removed.
    /// </param>
    /// <param name="link">
    /// Optional URL to migration documentation.
    /// </param>
    /// <returns>The route handler builder for chaining.</returns>
    public static RouteHandlerBuilder Deprecated(
        this RouteHandlerBuilder builder,
        DateOnly? sunsetDate = null,
        string? link = null)
    {
        builder.WithMetadata(new DeprecatedAttribute { SunsetDate = sunsetDate, Link = link });
        return builder;
    }
}
