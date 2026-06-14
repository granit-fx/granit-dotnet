using Granit.AI.Prompts.Endpoints.Endpoints;
using Granit.AI.Prompts.Endpoints.Options;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.AI.Prompts.Endpoints.Extensions;

/// <summary>Registers the prompt-catalogue endpoints.</summary>
public static class AIPromptsEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the prompt-catalogue endpoints: browse, picker, get, create, update, customise, delete.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional options customisation.</param>
    /// <returns>The prompts route group, for chaining.</returns>
    public static RouteGroupBuilder MapGranitPrompts(
        this IEndpointRouteBuilder endpoints,
        Action<AIPromptsEndpointsOptions>? configure = null)
    {
        AIPromptsEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName);

        group.MapPromptCatalogueEndpoints();
        return group;
    }
}
