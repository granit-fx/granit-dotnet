using Granit.AI.Chat.Endpoints.Endpoints;
using Granit.AI.Chat.Endpoints.Options;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.AI.Chat.Endpoints.Extensions;

/// <summary>Registers the chat conversation endpoints.</summary>
public static class AIChatEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the owner-scoped conversation management endpoints (list, get, create, rename, delete).
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional options customisation.</param>
    /// <returns>The conversations route group, for chaining.</returns>
    public static RouteGroupBuilder MapGranitConversations(
        this IEndpointRouteBuilder endpoints,
        Action<AIChatEndpointsOptions>? configure = null)
    {
        AIChatEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName);

        group.MapConversationEndpoints();
        group.MapChatSendEndpoint();
        group.MapChatWorkspaceEndpoint();
        return group;
    }
}
