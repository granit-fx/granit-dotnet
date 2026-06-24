using Granit.Validation.AspNetCore;
using Granit.Validation.Endpoints.Endpoints;
using Granit.Validation.Endpoints.Options;
using Microsoft.AspNetCore.Builder;
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
    /// The endpoints declare an <strong>explicit authorization stance</strong>: mapped
    /// <c>AllowAnonymous</c> by default — suitable for public forms (registration, contact).
    /// To require authorization instead, set
    /// <see cref="ValidationEndpointsOptions.AuthorizationPolicy"/> via <paramref name="configure"/>
    /// (chaining <c>.RequireAuthorization()</c> on the returned group is ignored — the default
    /// <c>AllowAnonymous</c> stance wins).
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

        // Explicit authorization stance (never implicit): public by default for anonymous
        // forms, or a required policy when the host configures one via options.
        if (string.IsNullOrEmpty(options.AuthorizationPolicy))
        {
            group.AllowAnonymous();
        }
        else
        {
            group.RequireAuthorization(options.AuthorizationPolicy);
        }

        group.MapValidationEndpoints();

        return group;
    }
}
