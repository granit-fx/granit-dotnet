using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.Validation.AspNetCore;

/// <summary>
/// Extension methods for creating Granit route groups with automatic validation.
/// </summary>
public static class GranitEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Creates a <see cref="RouteGroupBuilder"/> with the <see cref="FluentValidationAutoEndpointFilter"/>
    /// pre-applied to all endpoints in the group.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the recommended way to create route groups in Granit <c>.Endpoints</c> modules.
    /// Every endpoint in the group automatically validates its arguments against registered
    /// <c>IValidator&lt;T&gt;</c> implementations, removing the need for explicit
    /// <c>.ValidateBody&lt;T&gt;()</c> calls.
    /// </para>
    /// <para>
    /// To opt out of validation for a specific endpoint, use
    /// <see cref="SkipAutoValidationAttribute"/>:
    /// <code>
    /// group.MapPost("/special", Handler)
    ///     .WithMetadata(new SkipAutoValidationAttribute());
    /// </code>
    /// </para>
    /// </remarks>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="prefix">The route prefix for the group.</param>
    /// <returns>A <see cref="RouteGroupBuilder"/> with automatic validation enabled.</returns>
    public static RouteGroupBuilder MapGranitGroup(
        this IEndpointRouteBuilder endpoints, string prefix) =>
        endpoints.MapGroup(prefix)
            .AddEndpointFilter<FluentValidationAutoEndpointFilter>();
}
