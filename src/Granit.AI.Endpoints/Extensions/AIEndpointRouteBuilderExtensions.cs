using Granit.AI.Endpoints.Endpoints;
using Granit.AI.Endpoints.Options;
using Granit.AI.Endpoints.Permissions;
using Granit.QueryEngine.AspNetCore.Extensions;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.AI.Endpoints.Extensions;

/// <summary>
/// Extension methods for registering AI endpoints.
/// </summary>
public static class AIEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps AI administration and inference endpoints onto the given route builder.
    /// </summary>
    /// <remarks>
    /// <para>Call this from your application route registration:</para>
    /// <code>
    /// app.MapGranitAI();
    ///
    /// // With custom options:
    /// app.MapGranitAI(opts =>
    /// {
    ///     opts.RoutePrefix = "api/ai";
    /// });
    /// </code>
    /// </remarks>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="AIEndpointsOptions"/>.</param>
    /// <returns>The <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    public static RouteGroupBuilder MapGranitAI(
        this IEndpointRouteBuilder endpoints,
        Action<AIEndpointsOptions>? configure = null)
    {
        AIEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints.MapGranitGroup(options.RoutePrefix);

        // Admin endpoints — workspace CRUD
        RouteGroupBuilder adminGroup = group.MapGroup("")
            .WithTags(options.WorkspacesTagName)
            .RequireAuthorization(AIPermissions.Workspaces.Manage);
        adminGroup.MapWorkspaceEndpoints();

        // Admin endpoints — usage tracking via Granit.QueryEngine.
        RouteGroupBuilder usageGroup = group.MapGroup("usage")
            .WithTags(options.UsageTagName)
            .RequireAuthorization(AIPermissions.Usage.Read);
        usageGroup.MapGranitQuery<AIUsageRecord>();

        // User endpoints — chat completion proxy
        RouteGroupBuilder chatGroup = group.MapGroup("")
            .WithTags(options.InferenceTagName)
            .RequireAuthorization(AIPermissions.Chat.Execute);
        chatGroup.MapChatEndpoints();

        // User endpoints — embedding generation proxy
        RouteGroupBuilder embeddingGroup = group.MapGroup("")
            .WithTags(options.InferenceTagName)
            .RequireAuthorization(AIPermissions.Embeddings.Execute);
        embeddingGroup.MapEmbeddingEndpoints();

        return group;
    }
}
