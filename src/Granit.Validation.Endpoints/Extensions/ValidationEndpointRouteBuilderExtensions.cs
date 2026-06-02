using Granit.Validation.AspNetCore;
using Granit.Validation.Endpoints.Endpoints;
using Granit.Validation.Endpoints.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.Validation.Endpoints.Extensions;

/// <summary>
/// Extension methods for mapping server-side field validation endpoints.
/// </summary>
public static class ValidationEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps server-side field validation endpoints under <c>/{prefix}/validation</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The returned <see cref="RouteGroupBuilder"/> has <strong>no authentication</strong>
    /// by default — suitable for public forms (registration, contact).
    /// Chain <c>.RequireAuthorization()</c> for authenticated applications.
    /// </para>
    /// <para>
    /// <strong>Rate limiting is strongly recommended</strong> for public endpoints.
    /// Chain <c>.RequireGranitRateLimiting("validation")</c> to protect against abuse.
    /// </para>
    /// </remarks>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="ValidationEndpointsOptions"/>.</param>
    /// <returns>The route group builder for further chaining (auth, rate limiting, bulkhead).</returns>
    public static RouteGroupBuilder MapGranitValidation(
        this IEndpointRouteBuilder endpoints,
        Action<ValidationEndpointsOptions>? configure = null)
    {
        ValidationEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName);

        group.MapValidationEndpoints();

        return group;
    }
}
