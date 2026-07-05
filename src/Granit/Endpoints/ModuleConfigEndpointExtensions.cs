using System.Diagnostics.CodeAnalysis;
using Granit.Modularity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Endpoints;

/// <summary>
/// Extension methods for mapping standardized <c>GET /{module}/config</c> endpoints.
/// </summary>
public static class ModuleConfigEndpointExtensions
{
    /// <summary>
    /// Maps a <c>GET /{routePrefix}/config</c> endpoint that returns the module configuration
    /// from the registered <see cref="IModuleConfigProvider{TResponse}"/>.
    /// </summary>
    /// <typeparam name="TProvider">
    /// The <see cref="IModuleConfigProvider{TResponse}"/> implementation (resolved from DI).
    /// </typeparam>
    /// <typeparam name="TResponse">The response DTO type.</typeparam>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="routePrefix">Module route prefix (e.g., <c>"webhooks"</c>, <c>"cookies"</c>).</param>
    /// <param name="endpointName">
    /// Unique endpoint name for link generation (e.g., <c>"GetWebhooksConfig"</c>).
    /// </param>
    /// <param name="tag">OpenAPI tag (e.g., <c>"Webhooks"</c>).</param>
    /// <param name="configureEndpoint">
    /// Optional delegate to customize the <see cref="RouteHandlerBuilder"/>
    /// (e.g., <c>.AllowAnonymous()</c>, cache headers).
    /// </param>
    /// <returns>The endpoint route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapGranitModuleConfig<TProvider, TResponse>(
        this IEndpointRouteBuilder endpoints,
        string routePrefix,
        string endpointName,
        string tag,
        Action<RouteHandlerBuilder>? configureEndpoint = null)
        where TProvider : class, IModuleConfigProvider<TResponse>
        where TResponse : class
    {
        RouteHandlerBuilder builder = endpoints
            .MapGet($"{routePrefix}/config", ([FromServices] TProvider provider) => HandleGetConfig(provider))
            .WithName(endpointName)
            .WithTags(tag)
            .WithSummary($"Returns the current {tag} module configuration.")
            .WithDescription($"Returns the runtime configuration of the {tag} module — the same shape used by SPAs to render module-specific UI without hardcoding values. The payload is module-defined and stable; clients should not assume new fields are absent.")
            .Produces<TResponse>();

        configureEndpoint?.Invoke(builder);

        return endpoints;
    }

    /// <summary>
    /// Maps a <c>GET /{routePrefix}/config</c> endpoint that returns the module configuration
    /// from the registered <see cref="IAsyncModuleConfigProvider{TResponse}"/>.
    /// </summary>
    /// <typeparam name="TProvider">
    /// The <see cref="IAsyncModuleConfigProvider{TResponse}"/> implementation (resolved from DI).
    /// </typeparam>
    /// <typeparam name="TResponse">The response DTO type.</typeparam>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="routePrefix">Module route prefix (e.g., <c>"account"</c>).</param>
    /// <param name="endpointName">
    /// Unique endpoint name for link generation (e.g., <c>"GetAccountConfig"</c>).
    /// </param>
    /// <param name="tag">OpenAPI tag (e.g., <c>"Account"</c>).</param>
    /// <param name="configureEndpoint">
    /// Optional delegate to customize the <see cref="RouteHandlerBuilder"/>
    /// (e.g., <c>.AllowAnonymous()</c>, cache headers).
    /// </param>
    /// <returns>The endpoint route builder for chaining.</returns>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'",
        Justification = "The 'Async' suffix disambiguates this IAsyncModuleConfigProvider overload from the "
            + "synchronous MapGranitModuleConfig above; the two differ only by generic constraint (not part of "
            + "the signature), so they cannot share a name.")]
    public static IEndpointRouteBuilder MapGranitModuleConfigAsync<TProvider, TResponse>(
        this IEndpointRouteBuilder endpoints,
        string routePrefix,
        string endpointName,
        string tag,
        Action<RouteHandlerBuilder>? configureEndpoint = null)
        where TProvider : class, IAsyncModuleConfigProvider<TResponse>
        where TResponse : class
    {
        RouteHandlerBuilder builder = endpoints
            .MapGet(
                $"{routePrefix}/config",
                ([FromServices] TProvider provider, CancellationToken cancellationToken) =>
                    HandleGetConfigAsync(provider, cancellationToken))
            .WithName(endpointName)
            .WithTags(tag)
            .WithSummary($"Returns the current {tag} module configuration.")
            .WithDescription($"Returns the runtime configuration of the {tag} module — the same shape used by SPAs to render module-specific UI without hardcoding values. The payload is module-defined and stable; clients should not assume new fields are absent.")
            .Produces<TResponse>();

        configureEndpoint?.Invoke(builder);

        return endpoints;
    }

    private static Ok<TResponse> HandleGetConfig<TResponse>([FromServices] IModuleConfigProvider<TResponse> provider)
        where TResponse : class =>
        TypedResults.Ok(provider.GetConfig());

    private static async Task<Ok<TResponse>> HandleGetConfigAsync<TResponse>(
        [FromServices] IAsyncModuleConfigProvider<TResponse> provider,
        CancellationToken cancellationToken)
        where TResponse : class =>
        TypedResults.Ok(await provider.GetConfigAsync(cancellationToken).ConfigureAwait(false));
}
